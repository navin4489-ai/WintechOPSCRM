using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;
    private readonly IAuthService _auth;

    public InventoryController(IInventoryService inventory, IAuthService auth) { _inventory = inventory; _auth = auth; }

    [HttpGet("stock")]
    public async Task<IActionResult> GetStock([FromQuery] int? outletId)
        => Ok(new ApiResponse<List<ProductStockDto>> { Success = true, Data = await _inventory.GetStockLevelsAsync(outletId, GetUserId()) });

    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements([FromQuery] int? outletId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var items = await _inventory.GetInventoryMovementsAsync(outletId, new PagedRequest { Page = page, PageSize = pageSize }, GetUserId());
        return Ok(new ApiResponse<List<InventoryLedgerDto>> { Success = true, Data = items });
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock([FromQuery] int? outletId)
        => Ok(new ApiResponse<List<ProductStockDto>> { Success = true, Data = await _inventory.GetLowStockItemsAsync(outletId, GetUserId()) });

    [HttpPost("movement")]
    public async Task<IActionResult> RecordMovement([FromBody] RecordInventoryRequest request)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ManageInventory);
        if (!can) return Forbid();
        var recorded = await _inventory.RecordInventoryMovementAsync(request, GetUserId());
        return Ok(new ApiResponse<InventoryLedgerDto> { Success = true, Data = recorded });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecipesController : ControllerBase
{
    private readonly IInventoryService _inventory;
    private readonly IAuthService _auth;

    public RecipesController(IInventoryService inventory, IAuthService auth) { _inventory = inventory; _auth = auth; }

    [HttpGet]
    public async Task<IActionResult> GetRecipes([FromQuery] int? productId)
        => Ok(new ApiResponse<List<RecipeDto>> { Success = true, Data = await _inventory.GetRecipesAsync(productId, GetUserId()) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecipeRequest request)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ManageInventory);
        if (!can) return Forbid();
        var created = await _inventory.CreateRecipeAsync(request, GetUserId());
        return CreatedAtAction(nameof(GetRecipes), new { productId = request.ProductId }, new ApiResponse<RecipeDto> { Success = true, Data = created });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}
