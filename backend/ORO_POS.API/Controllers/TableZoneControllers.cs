using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TablesController : ControllerBase
{
    private readonly ITableService _table;
    private readonly IAuthService _auth;

    public TablesController(ITableService table, IAuthService auth) { _table = table; _auth = auth; }

    [HttpGet("outlet/{outletId}")]
    public async Task<IActionResult> GetByOutlet(int outletId) => Ok(new ApiResponse<List<TableDto>> { Success = true, Data = await _table.GetTablesAsync(outletId, GetUserId()) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTableRequest request)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ManageSettings);
        if (!can) return Forbid();
        var created = await _table.CreateTableAsync(request, GetUserId());
        return CreatedAtAction(nameof(GetByOutlet), new { outletId = request.OutletId }, new ApiResponse<TableDto> { Success = true, Data = created });
    }

    [HttpPut("{tableId}/status")]
    public async Task<IActionResult> UpdateStatus(int tableId, [FromQuery] string status)
    {
        var updated = await _table.UpdateTableStatusAsync(tableId, status, GetUserId());
        return Ok(new ApiResponse<TableDto> { Success = true, Data = updated });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ZonesController : ControllerBase
{
    private readonly ITableService _table;
    private readonly IAuthService _auth;

    public ZonesController(ITableService table, IAuthService auth) { _table = table; _auth = auth; }

    [HttpGet("outlet/{outletId}")]
    public async Task<IActionResult> GetByOutlet(int outletId) => Ok(new ApiResponse<List<ZoneDto>> { Success = true, Data = await _table.GetZonesAsync(outletId, GetUserId()) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateZoneRequest request)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ManageSettings);
        if (!can) return Forbid();
        var created = await _table.CreateZoneAsync(request, GetUserId());
        return CreatedAtAction(nameof(GetByOutlet), new { outletId = request.OutletId }, new ApiResponse<ZoneDto> { Success = true, Data = created });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}
