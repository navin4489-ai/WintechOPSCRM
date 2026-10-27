using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.Entities;
using ORO_POS.Infrastructure.Data;

namespace ORO_POS.Infrastructure.Services;
public class ProductImageService(AppDbContext db)
{
    public const int MaximumBytes = 1024 * 1024;
    public static string Validate(byte[] bytes)
    {
        if (bytes.Length < 24 || bytes.Length > MaximumBytes) throw new InvalidOperationException("Upload a PNG, JPEG or WebP image up to 1 MB.");
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217) return "image/jpeg";
        if (System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return "image/webp";
        throw new InvalidOperationException("Unsupported image content. SVG, GIF and document uploads are not accepted.");
    }
    private async Task<Product> RequireAccess(int id, int userId)
    {
        var product = await db.Products.FindAsync(id) ?? throw new KeyNotFoundException("Product not found.");
        var actor = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId && u.IsActive)
            ?? throw new UnauthorizedAccessException("User not found.");
        if (actor.Role?.Name != "Owner" && (actor.OutletId != product.OutletId || (actor.Role?.Name != "StoreManager" && !(JsonSerializer.Deserialize<string[]>(actor.Role?.Permissions ?? "[]") ?? []).Contains("ManageSettings"))))
            throw new UnauthorizedAccessException("Only authorized outlet administrators can upload item images.");
        return product;
    }
    public async Task<string> SaveAsync(int id, byte[] bytes, int userId)
    {
        var product = await RequireAccess(id, userId);
        var type = Validate(bytes);
        var image = await db.ProductImages.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.ProductId == id);
        if (image == null) { image = new ProductImage { ProductId = id }; db.ProductImages.Add(image); }
        image.IsDeleted = false; image.ContentType = type; image.Data = bytes; image.Hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); image.UpdatedAt = DateTime.UtcNow;
        product.ImageUrl = $"/api/products/{id}/image?v={image.Hash[..16]}";
        product.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { ActorId = userId, Action = "UploadProductImage", Entity = "Product", EntityId = id, Reason = $"Validated {type}, {bytes.Length} bytes. Image stored in PostgreSQL." });
        await db.SaveChangesAsync();
        return product.ImageUrl;
    }
    public async Task RemoveAsync(int id, int userId)
    {
        var product = await RequireAccess(id, userId);
        var image = await db.ProductImages.FirstOrDefaultAsync(i => i.ProductId == id);
        if (image != null) db.ProductImages.Remove(image);
        product.ImageUrl = null;
        db.AuditLogs.Add(new AuditLog { ActorId = userId, Action = "RemoveProductImage", Entity = "Product", EntityId = id, Reason = "Outlet administrator removed the item image." });
        await db.SaveChangesAsync();
    }
}
