using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly IMenuService _menu;
    private readonly IAuthService _auth;

    public CategoriesController(IMenuService menu, IAuthService auth) { _menu = menu; _auth = auth; }

    [HttpGet("outlet/{outletId}")]
    public async Task<IActionResult> GetByOutlet(int outletId) => Ok(new ApiResponse<List<CategoryDto>> { Success = true, Data = await _menu.GetCategoriesAsync(outletId, GetUserId()) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ManageSettings);
        if (!can) return Forbid();
        var created = await _menu.CreateCategoryAsync(request, GetUserId());
        return CreatedAtAction(nameof(GetByOutlet), new { outletId = request.OutletId }, new ApiResponse<CategoryDto> { Success = true, Data = created });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IMenuService _menu;
    private readonly IAuthService _auth;

    public ProductsController(IMenuService menu, IAuthService auth) { _menu = menu; _auth = auth; }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int outletId, [FromQuery] int? categoryId, [FromQuery] bool availableOnly = true)
        => Ok(new ApiResponse<List<ProductDto>> { Success = true, Data = await _menu.GetProductsAsync(outletId, categoryId, availableOnly, GetUserId()) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ManageSettings);
        if (!can) return Forbid();
        var created = await _menu.CreateProductAsync(request, GetUserId());
        return CreatedAtAction(nameof(Get), new { outletId = request.OutletId }, new ApiResponse<ProductDto> { Success = true, Data = created });
    }

    [HttpPost("{productId:int}/image"), RequestSizeLimit(1100000)]
    public async Task<IActionResult> UploadImage(int productId, IFormFile file, [FromServices] ORO_POS.Infrastructure.Services.ProductImageService images)
    {
        if (file.Length == 0 || file.Length > ORO_POS.Infrastructure.Services.ProductImageService.MaximumBytes)
            return BadRequest(new ApiResponse<object> { Success = false, Errors = new[] { "Choose an image up to 1 MB." } });
        using var buffer = new MemoryStream(); await file.CopyToAsync(buffer);
        var url = await images.SaveAsync(productId, buffer.ToArray(), GetUserId());
        return Ok(new ApiResponse<object> { Success = true, Data = new { imageUrl = url } });
    }
    [HttpPut("{productId:int}")]
    public async Task<IActionResult> Update(int productId, CreateProductRequest request)
        => Ok(new ApiResponse<ProductDto> { Success = true, Data = await _menu.UpdateProductAsync(productId, request, GetUserId()) });
    [HttpDelete("{productId:int}/image")]
    public async Task<IActionResult> RemoveImage(int productId, [FromServices] ORO_POS.Infrastructure.Services.ProductImageService images)
    { await images.RemoveAsync(productId, GetUserId()); return Ok(new ApiResponse<object> { Success = true }); }
    [HttpGet("{productId:int}/image"), AllowAnonymous]
    public async Task<IActionResult> Image(int productId, [FromServices] ORO_POS.Infrastructure.Data.AppDbContext db)
    {
        var image = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.ProductImages.Where(i => i.ProductId == productId && !i.Product.IsDeleted));
        if (image == null) return NotFound();
        var etag = $"\"{image.Hash}\"";
        Response.Headers.ETag = etag; Response.Headers.CacheControl = "public,max-age=86400"; Response.Headers.XContentTypeOptions = "nosniff";
        if (Request.Headers.IfNoneMatch.ToString() == etag) return StatusCode(304);
        return File(image.Data, image.ContentType);
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModifierGroupsController : ControllerBase
{
    private readonly IMenuService _menu;
    private readonly IAuthService _auth;

    public ModifierGroupsController(IMenuService menu, IAuthService auth) { _menu = menu; _auth = auth; }

    [HttpGet("outlet/{outletId}")]
    public async Task<IActionResult> GetByOutlet(int outletId) => Ok(new ApiResponse<List<ModifierGroupDto>> { Success = true, Data = await _menu.GetModifierGroupsAsync(outletId, GetUserId()) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateModifierGroupRequest request)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ManageSettings);
        if (!can) return Forbid();
        var created = await _menu.CreateModifierGroupAsync(request, GetUserId());
        return CreatedAtAction(nameof(GetByOutlet), new { outletId = request.OutletId }, new ApiResponse<ModifierGroupDto> { Success = true, Data = created });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}
