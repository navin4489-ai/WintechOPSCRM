using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KitchenTicketsController : ControllerBase
{
    private readonly IKitchenService _kitchen;

    public KitchenTicketsController(IKitchenService kitchen) => _kitchen = kitchen;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? outletId, [FromQuery] int? stationId)
        => Ok(new ApiResponse<List<KitchenTicketDto>> { Success = true, Data = await _kitchen.GetKitchenTicketsAsync(outletId, stationId, GetUserId()) });

    [HttpGet("{ticketId}")]
    public async Task<IActionResult> GetById(int ticketId)
    {
        var ticket = await _kitchen.GetKitchenTicketByIdAsync(ticketId, GetUserId());
        return ticket == null ? NotFound() : Ok(new ApiResponse<KitchenTicketDto> { Success = true, Data = ticket });
    }

    [HttpGet("station/{stationId}")]
    public async Task<IActionResult> GetByStation(int stationId)
        => Ok(new ApiResponse<List<KitchenTicketDto>> { Success = true, Data = await _kitchen.GetTicketsByStationAsync(stationId, GetUserId()) });

    [HttpPut("{ticketId}/status")]
    public async Task<IActionResult> UpdateStatus(int ticketId, [FromQuery] string status)
    {
        var updated = await _kitchen.UpdateTicketStatusAsync(new UpdateTicketStatusRequest { TicketId = ticketId, Status = status }, GetUserId());
        return Ok(new ApiResponse<KitchenTicketDto> { Success = true, Data = updated });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}
