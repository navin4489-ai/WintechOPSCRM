using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace ORO_POS.Infrastructure.Services;
public class OutletProvisioningService(AppDbContext db, IOutletService outlets, IUserService users)
{
    public async Task<OutletOnboardingResult> CreateAsync(CreateOutletWithAdminRequest request, int actorId)
    {
        await TenantAccess.RequireOwnerAsync(db, actorId);
        var username = request.AdminUsername.Trim();
        var email = string.IsNullOrWhiteSpace(request.AdminEmail) ? username + "@outlet.invalid" : request.AdminEmail.Trim();
        if (!Regex.IsMatch(username, "^[a-zA-Z0-9._-]{3,64}$") || !System.Net.Mail.MailAddress.TryCreate(email, out _))
            throw new InvalidOperationException("Enter a unique admin username (3–64 letters/numbers/._-) and a valid email address.");
        var password = request.AdminPassword;
        if (password.Length < 12 || Encoding.UTF8.GetByteCount(password) > 72 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            throw new InvalidOperationException("Admin password needs 12+ characters, upper/lowercase and a number (maximum 72 UTF-8 bytes).");
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        // Check before creating the outlet; the transaction also rolls back any
        // later account failure. Plaintext passwords never enter audit records.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username || u.Email == email))
            throw new InvalidOperationException("Admin username or email is already in use. Choose another.");
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == "StoreManager")
            ?? throw new InvalidOperationException("Outlet administrator role is not configured.");
        var outlet = await outlets.CreateOutletAsync(request.Outlet, actorId);
        var admin = await users.CreateUserAsync(new CreateUserRequest {
            Username = username, Email = email, Password = password, RoleId = role.Id, OutletId = outlet.Id
        }, actorId);
        if (tx != null) await tx.CommitAsync();
        return new OutletOnboardingResult { Outlet = outlet, Administrator = admin };
    }
}
