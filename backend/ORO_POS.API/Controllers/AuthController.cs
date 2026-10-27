using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _auth.LoginAsync(request);
            return Ok(new ApiResponse<LoginResponse> { Success = true, Data = response, Message = "Login successful" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object> { Success = false, Errors = new[] { ex.Message } });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        var can = await _auth.ValidatePermissionAsync(userId, Permission.ManageSettings);
        return Ok(new ApiResponse<object> { Success = true, Data = new { userId, canManageSettings = can } });
    }

    [HttpPost("validate-permission")]
    [Authorize]
    public async Task<IActionResult> ValidatePermission([FromQuery] Permission permission)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        var result = await _auth.ValidatePermissionAsync(userId, permission);
        return Ok(new ApiResponse<bool> { Success = true, Data = result });
    }
}
