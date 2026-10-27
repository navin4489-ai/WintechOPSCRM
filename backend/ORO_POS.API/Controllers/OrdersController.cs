using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _order;
    private readonly IAuthService _auth;

    public OrdersController(IOrderService order, IAuthService auth) { _order = order; _auth = auth; }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var created = await _order.CreateOrderAsync(request, GetUserId());
        return CreatedAtAction(nameof(GetById), new { orderId = created.Id }, new ApiResponse<OrderDto> { Success = true, Data = created });
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetById(int orderId)
    {
        var order = await _order.GetOrderByIdAsync(orderId, GetUserId());
        return order == null ? NotFound() : Ok(new ApiResponse<OrderDto> { Success = true, Data = order });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? outletId = null, [FromQuery] string? search = null)
    {
        var result = await _order.GetOrdersAsync(new PagedRequest { Page = page, PageSize = pageSize, Search = search }, outletId, GetUserId());
        return Ok(new ApiResponse<PagedResult<OrderDto>> { Success = true, Data = result, TotalCount = result.TotalCount });
    }

    [HttpPut("{orderId}/status")]
    public async Task<IActionResult> UpdateStatus(int orderId, [FromQuery] string status, [FromQuery] string? reason = null)
    {
        var updated = await _order.UpdateOrderStatusAsync(orderId, status, GetUserId(), reason);
        return Ok(new ApiResponse<OrderDto> { Success = true, Data = updated });
    }

    [HttpPost("{orderId}/void")]
    public async Task<IActionResult> Void(int orderId, [FromBody] VoidOrderRequest request)
    {
        var voided = await _order.VoidOrderAsync(orderId, request, GetUserId());
        return Ok(new ApiResponse<OrderDto> { Success = true, Data = voided });
    }

    [HttpPost("{orderId}/discount")]
    public async Task<IActionResult> ApplyDiscount(int orderId, [FromBody] ApplyDiscountRequest request)
    {
        var updated = await _order.ApplyDiscountAsync(orderId, request, GetUserId());
        return Ok(new ApiResponse<OrderDto> { Success = true, Data = updated });
    }

    [HttpPost("{orderId}/items")]
    public async Task<IActionResult> AddItem(int orderId, [FromBody] OrderItemRequest request)
    {
        var updated = await _order.AddOrderItemAsync(orderId, request, GetUserId());
        return Ok(new ApiResponse<OrderDto> { Success = true, Data = updated });
    }

    [HttpDelete("{orderId}/items/{orderItemId}")]
    public async Task<IActionResult> RemoveItem(int orderId, int orderItemId)
    {
        var updated = await _order.RemoveOrderItemAsync(orderId, orderItemId, GetUserId());
        return Ok(new ApiResponse<OrderDto> { Success = true, Data = updated });
    }

    [HttpPost("{orderId}/payment")]
    public async Task<IActionResult> ProcessPayment(int orderId, [FromBody] ProcessPaymentRequest request)
    {
        var updated = await _order.ProcessPaymentAsync(orderId, request, GetUserId());
        return Ok(new ApiResponse<OrderDto> { Success = true, Data = updated });
    }

    [HttpPost("{orderId}/transfer-table")]
    public async Task<IActionResult> TransferTable(int orderId, [FromQuery] int newTableId)
    {
        var updated = await _order.TransferTableAsync(orderId, newTableId, GetUserId());
        return Ok(new ApiResponse<OrderDto> { Success = true, Data = updated });
    }

    [HttpPost("{sourceOrderId}/merge/{targetOrderId}")]
    public async Task<IActionResult> Merge(int sourceOrderId, int targetOrderId)
    {
        var updated = await _order.MergeOrdersAsync(sourceOrderId, targetOrderId, GetUserId());
        return Ok(new ApiResponse<OrderDto> { Success = true, Data = updated });
    }

    [HttpPost("{orderId}/split")]
    public async Task<IActionResult> Split(int orderId, [FromBody] List<int> orderItemIds, [FromQuery] int newTableId)
    {
        var updated = await _order.SplitOrderAsync(orderId, orderItemIds, newTableId, GetUserId());
        return Ok(new ApiResponse<OrderDto> { Success = true, Data = updated });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}
