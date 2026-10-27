using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ORO_POS.Core.DTOs;
using ORO_POS.Infrastructure.Services;

namespace ORO_POS.API.Controllers;
[ApiController, AllowAnonymous, Route("api/public/outlets/{key}")]
public class PublicOrderingController(PublicOrderingService ordering) : ControllerBase
{
    [HttpGet("menu"), EnableRateLimiting("public-read")]
    public async Task<IActionResult> Menu(string key) => Ok(new ApiResponse<PublicMenuDto> { Success = true, Data = await ordering.GetMenuAsync(key) });
    [HttpPost("orders"), EnableRateLimiting("public-order")]
    public async Task<IActionResult> Place(string key, GuestOrderRequest request) => Ok(new ApiResponse<GuestOrderConfirmation> { Success = true, Data = await ordering.PlaceAsync(key, request) });
    [HttpPost("orders/{number}/status"), EnableRateLimiting("public-read")]
    public async Task<IActionResult> Track(string key, string number, GuestTrackingRequest request) => Ok(new ApiResponse<GuestOrderConfirmation> { Success = true, Data = await ordering.TrackAsync(key, number, request.Proof) });
}
