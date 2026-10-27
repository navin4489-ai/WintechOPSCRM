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
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
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

    [HttpPost("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, [FromServices] ORO_POS.Infrastructure.Data.AppDbContext db)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await db.Users.FindAsync(userId);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new ApiResponse<object> { Success = false, Errors = new[] { "Current password is incorrect." } });
        if (request.NewPassword.Length < 12 || !request.NewPassword.Any(char.IsUpper) || !request.NewPassword.Any(char.IsLower) || !request.NewPassword.Any(char.IsDigit) || request.NewPassword == request.CurrentPassword)
            return BadRequest(new ApiResponse<object> { Success = false, Errors = new[] { "Use a different password with at least 12 characters, upper/lowercase letters and a number." } });
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        db.AuditLogs.Add(new ORO_POS.Core.Entities.AuditLog { ActorId = userId, Action = "ChangePassword", Entity = "User", EntityId = userId, Reason = "Self-service password rotation. All existing sessions revoked." });
        await db.SaveChangesAsync();
        return Ok(new ApiResponse<object> { Success = true, Data = new { signInAgain = true } });
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
