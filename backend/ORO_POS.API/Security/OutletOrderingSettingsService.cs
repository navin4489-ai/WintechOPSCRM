using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Infrastructure.Data;
using ORO_POS.Infrastructure.Services;
using QRCoder;

namespace ORO_POS.API.Security;
public class OutletOrderingSettingsService(AppDbContext db, IConfiguration configuration)
{
    public async Task<object> GetAsync(int id, int actorId)
    {
        await TenantAccess.RequireOutletAdminAsync(db, actorId, id);
        return Describe(await db.DeviceConfigs.FirstOrDefaultAsync(d=>d.OutletId==id&&d.StationType=="PublicOrdering"));
    }
    public async Task<object> ConfigureAsync(int id, OrderingSettingsRequest request, int actorId)
    {
        await TenantAccess.RequireOutletAdminAsync(db, actorId, id);
        if (!await db.Outlets.AnyAsync(o=>o.Id==id)) throw new KeyNotFoundException("Outlet not found.");
        await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable):null;
        var device=await db.DeviceConfigs.FirstOrDefaultAsync(d=>d.OutletId==id&&d.StationType=="PublicOrdering");
        if(device==null){device=new DeviceConfig{OutletId=id,StationType="PublicOrdering",DeviceName=NewName()};db.DeviceConfigs.Add(device);}
        if(request.RotateKey)device.DeviceName=NewName();
        device.IsActive=request.Enabled;
        db.AuditLogs.Add(new AuditLog{ActorId=actorId,Entity="Outlet",EntityId=id,Action="ConfigurePublicOrdering",Reason=$"Cash-at-outlet ordering enabled={request.Enabled}; link rotated={request.RotateKey}."});
        await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();return Describe(device);
    }
    private static string NewName()=>"PublicOrdering-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private object Describe(DeviceConfig? device)
    {
        if(device==null)return new{enabled=false,publicUrl=(string?)null,qrPngBase64=(string?)null,cashOnly=true};
        var url=(configuration["PublicBaseUrl"]??"https://the-bathany.onrender.com").TrimEnd('/')+"/order/"+device.DeviceName["PublicOrdering-".Length..];
        return new{enabled=device.IsActive,publicUrl=url,qrPngBase64=Convert.ToBase64String(PngByteQRCodeHelper.GetQRCode(url,QRCodeGenerator.ECCLevel.M,8)),cashOnly=true};
    }
}
