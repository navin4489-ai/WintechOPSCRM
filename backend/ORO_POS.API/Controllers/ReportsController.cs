using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _report;
    private readonly IAuthService _auth;

    public ReportsController(IReportService report, IAuthService auth) { _report = report; _auth = auth; }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSalesReport(
        [FromQuery] int? outletId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? groupBy = "outlet")
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ViewReports);
        if (!can) return Forbid();
        var data = await _report.GetSalesReportAsync(new SalesReportRequest { OutletId = outletId, FromDate = fromDate, ToDate = toDate, GroupBy = groupBy }, GetUserId());
        return Ok(new ApiResponse<List<SalesReportDto>> { Success = true, Data = data });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] int? outletId)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ViewReports);
        if (!can) return Forbid();
        var data = await _report.GetDashboardSummaryAsync(outletId, GetUserId());
        return Ok(new ApiResponse<DashboardSummaryDto> { Success = true, Data = data });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int? outletId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ViewReports);
        if (!can) return Forbid();
        var data = await _report.GetAuditLogsAsync(outletId, new PagedRequest { Page = page, PageSize = pageSize }, GetUserId());
        return Ok(new ApiResponse<List<AuditLogDto>> { Success = true, Data = data });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string reportType = "sales", [FromQuery] int? outletId = null, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ExportReports);
        if (!can) return Forbid();
        var bytes = await _report.ExportReportAsync(reportType, new SalesReportRequest { OutletId = outletId, FromDate = fromDate, ToDate = toDate }, GetUserId());
        return File(bytes, "text/csv", $"{reportType}_report.csv");
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}
