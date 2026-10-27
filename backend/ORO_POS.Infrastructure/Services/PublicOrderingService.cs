using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Data;

namespace ORO_POS.Infrastructure.Services;

public class PublicOrderingService(AppDbContext db, IOrderService orders, IMenuService menu, IConfiguration configuration)
{
    private async Task<DeviceConfig> FindOutletAsync(string key)
    {
        if (key.Length != 64 || !key.All(Uri.IsHexDigit)) throw new KeyNotFoundException("Ordering link is invalid or has been disabled.");
        return await db.DeviceConfigs.Include(d => d.Outlet).FirstOrDefaultAsync(d => d.DeviceName == "PublicOrdering-" + key && d.StationType == "PublicOrdering" && d.IsActive && d.Outlet != null && !d.Outlet.IsDeleted)
            ?? throw new KeyNotFoundException("Ordering link is invalid or has been disabled.");
    }
    public async Task<PublicMenuDto> GetMenuAsync(string key)
    {
        var device = await FindOutletAsync(key);
        var outlet = device.Outlet!;
        return new PublicMenuDto {
            OutletName = outlet.Name, Address = outlet.Address, Phone = outlet.Phone, Currency = "INR",
            AcceptingOrders = outlet.IsOperating && await db.Shifts.AnyAsync(s => s.OutletId == outlet.Id && s.Status == "Open" && s.User.IsActive),
            Products = await menu.GetProductsAsync(outlet.Id, null, true, 0)
        };
    }
    public async Task<GuestOrderConfirmation> PlaceAsync(string key, GuestOrderRequest request)
    {
        var device = await FindOutletAsync(key);
        var outlet = device.Outlet!;
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.CustomerName) || request.CustomerName.Length > 80
            || request.Items.Count == 0 || request.Items.Count > 30 || request.Items.Any(i => i.Quantity < 1 || i.Quantity > 20 || (i.Notes?.Length ?? 0) > 500))
            throw new InvalidOperationException("Enter a name and a valid cart of up to 30 items (1–20 units each).");
        var phone = new string(request.CustomerPhone.Where(c => c >= '0' && c <= '9').ToArray());
        if (phone.Length < 7 || phone.Length > 15 || request.CustomerPhone.Length > 24 || request.CustomerPhone.Count(c=>c=='+')>1
            || (request.CustomerPhone.Contains('+') && !request.CustomerPhone.TrimStart().StartsWith('+')) || request.CustomerPhone.Any(c => !(c >= '0' && c <= '9') && !"+ ()-".Contains(c)))
            throw new InvalidOperationException("Enter a valid contact number containing 7–15 digits, with an optional country code.");
        if (request.CustomerPhone.TrimStart().StartsWith('+')) phone = "+" + phone;
        // The unguessable request ID is also the retry key. Never capture payment
        // or accept client prices, discounts, outlet IDs, table IDs or staff IDs.
        var existing = await db.Orders.FirstOrDefaultAsync(o => o.OrderNumber == $"WEB-{request.RequestId:N}");
        if (existing != null)
        {
            if (existing.OutletId != outlet.Id) throw new InvalidOperationException("Start a new order for this outlet.");
            return Confirmation(existing);
        }
        if (!outlet.IsOperating) throw new InvalidOperationException("This outlet is closed for ordering.");
        var shift = await db.Shifts.Include(s => s.User).Where(s => s.OutletId == outlet.Id && s.Status == "Open" && s.User.IsActive)
            .OrderBy(s => s.OpenTime).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("This outlet is not accepting orders yet. Please contact the outlet.");
        var created = await orders.CreateOrderAsync(new CreateOrderRequest {
            ClientRequestId = request.RequestId, OutletId = outlet.Id, ServiceType = "Takeaway",
            Notes = $"[PUBLIC QR · CASH AT OUTLET] Customer: {request.CustomerName.Trim()}\nContact: {phone}\n{request.Notes?.Trim()}", Items = request.Items
        }, shift.UserId);
        db.AuditLogs.Add(new AuditLog { ActorId = null, Entity = "Order", EntityId = created.Id, Action = "GuestOrderSubmitted", Reason = "Public outlet QR order. Unpaid; collect cash at the outlet." });
        await db.SaveChangesAsync();
        var order = await db.Orders.FindAsync(created.Id);
        return Confirmation(order!);
    }
    public async Task<GuestOrderConfirmation> TrackAsync(string key, string number, string proof)
    {
        var device = await FindOutletAsync(key);
        var expected = Proof(number, device.OutletId!.Value);
        if (proof.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(proof), Encoding.UTF8.GetBytes(expected)))
            throw new KeyNotFoundException("Order confirmation not found.");
        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderNumber == number && o.OutletId == device.OutletId)
            ?? throw new KeyNotFoundException("Order confirmation not found.");
        return Confirmation(order);
    }
    private string Proof(string number, int outlet) => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new InvalidOperationException("Signing key missing.")),
        Encoding.UTF8.GetBytes($"guest-order:{outlet}:{number}"))).ToLowerInvariant();
    private GuestOrderConfirmation Confirmation(Order o) => new() { OrderNumber = o.OrderNumber, Status = o.Status, Subtotal = o.Subtotal,
        TaxAmount = o.TaxAmount, TotalAmount = o.TotalAmount, CreatedAt = o.CreatedAt, TrackingProof = Proof(o.OrderNumber, o.OutletId),
        PaymentStatus = o.Status == "Closed" ? "Paid at outlet" : o.Status == "Voided" ? "Cancelled by outlet" : "Pay cash at the outlet" };
}
