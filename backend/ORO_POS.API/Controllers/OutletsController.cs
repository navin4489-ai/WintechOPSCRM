using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OutletsController : ControllerBase
{
    private readonly IOutletService _outlet;
    private readonly IAuthService _auth;

    public OutletsController(IOutletService outlet, IAuthService auth) { _outlet = outlet; _auth = auth; }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(new ApiResponse<List<OutletDto>> { Success = true, Data = await _outlet.GetOutletsAsync(GetUserId()) });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await _outlet.GetOutletByIdAsync(id, GetUserId());
        return dto == null ? NotFound() : Ok(new ApiResponse<OutletDto> { Success = true, Data = dto });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OutletDto dto)
    {
        await Task.CompletedTask;
        return BadRequest(new ApiResponse<object> { Success = false, Errors = new[] { "Use Super Admin outlet onboarding, including the outlet-admin username, email and password." } });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] OutletDto dto)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ManageSettings);
        if (!can && !User.IsInRole("StoreManager")) return Forbid();
        var updated = await _outlet.UpdateOutletAsync(id, dto, GetUserId());
        return Ok(new ApiResponse<OutletDto> { Success = true, Data = updated });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}
