using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Data;
using QRCoder;

namespace ORO_POS.API.Controllers;
[ApiController, Authorize(Roles = "Owner"), Route("api/super-admin/outlets")]
public class SuperAdminController(AppDbContext db, IOutletService outlets, IConfiguration configuration) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public async Task<IActionResult> List() => Ok(new ApiResponse<List<OutletDto>> { Success = true, Data = await outlets.GetOutletsAsync(UserId) });
    [HttpPost] public async Task<IActionResult> Create(OutletDto request) => Ok(new ApiResponse<OutletDto> { Success = true, Data = await outlets.CreateOutletAsync(request, UserId) });
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, OutletDto request) => Ok(new ApiResponse<OutletDto> { Success = true, Data = await outlets.UpdateOutletAsync(id, request, UserId) });
    [HttpGet("{id:int}/ordering")]
    public async Task<IActionResult> Settings(int id) { await RequireOutlet(id); return Ok(new ApiResponse<object> { Success = true, Data = Result(await Device(id)) }); }
    [HttpPost("{id:int}/ordering")]
    public async Task<IActionResult> Configure(int id, OrderingSettingsRequest request)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        await RequireOutlet(id);
        var device = await Device(id);
        if (device == null) { device = new DeviceConfig { OutletId = id, StationType = "PublicOrdering", DeviceName = NewName() }; db.DeviceConfigs.Add(device); }
        if (request.RotateKey) device.DeviceName = NewName();
        device.IsActive = request.Enabled;
        db.AuditLogs.Add(new AuditLog { ActorId = UserId, Entity = "Outlet", EntityId = id, Action = "ConfigurePublicOrdering", Reason = $"Cash-at-outlet QR ordering enabled={request.Enabled}; link rotated={request.RotateKey}." });
        await db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return Ok(new ApiResponse<object> { Success = true, Data = Result(device) });
    }
    private static string NewName() => "PublicOrdering-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private Task<DeviceConfig?> Device(int id) => db.DeviceConfigs.FirstOrDefaultAsync(d => d.OutletId == id && d.StationType == "PublicOrdering");
    private async Task RequireOutlet(int id) { if (!await db.Outlets.AnyAsync(o => o.Id == id)) throw new KeyNotFoundException("Outlet not found."); }
    private object Result(DeviceConfig? device)
    {
        if (device == null) return new { enabled = false, publicUrl = (string?)null, qrPngBase64 = (string?)null, cashOnly = true };
        var url = (configuration["PublicBaseUrl"] ?? "https://the-bathany.onrender.com").TrimEnd('/') + "/order/" + device.DeviceName["PublicOrdering-".Length..];
        var bytes = PngByteQRCodeHelper.GetQRCode(url, QRCodeGenerator.ECCLevel.M, 8);
        return new { enabled = device.IsActive, publicUrl = url, qrPngBase64 = Convert.ToBase64String(bytes), cashOnly = true };
    }
}
