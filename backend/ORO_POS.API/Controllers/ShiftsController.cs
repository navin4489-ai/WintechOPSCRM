using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shift;
    private readonly IAuthService _auth;

    public ShiftsController(IShiftService shift, IAuthService auth) { _shift = shift; _auth = auth; }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenShiftRequest request)
    {
        var opened = await _shift.OpenShiftAsync(request, GetUserId());
        return Ok(new ApiResponse<ShiftDto> { Success = true, Data = opened });
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseShiftRequest request)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.CloseShift);
        if (!can) return Forbid();
        var closed = await _shift.CloseShiftAsync(request, GetUserId());
        return Ok(new ApiResponse<ShiftDto> { Success = true, Data = closed });
    }

    [HttpGet("open")]
    public async Task<IActionResult> GetOpen([FromQuery] int outletId)
    {
        var shift = await _shift.GetOpenShiftAsync(GetUserId(), outletId);
        return Ok(new ApiResponse<ShiftDto?> { Success = true, Data = shift });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? outletId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var shifts = await _shift.GetShiftsAsync(outletId, new PagedRequest { Page = page, PageSize = pageSize }, GetUserId());
        return Ok(new ApiResponse<List<ShiftDto>> { Success = true, Data = shifts });
    }

    [HttpGet("{shiftId}/z-report")]
    public async Task<IActionResult> ZReport(int shiftId)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ViewReports);
        if (!can) return Forbid();
        var report = await _shift.GenerateZReportAsync(shiftId, GetUserId());
        return Ok(new ApiResponse<ZReportDto> { Success = true, Data = report });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}
