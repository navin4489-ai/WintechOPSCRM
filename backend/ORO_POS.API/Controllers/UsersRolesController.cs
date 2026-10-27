using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IMenuService _menu;

    public UsersController(IAuthService auth, IMenuService menu) { _auth = auth; _menu = menu; }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var can = await _auth.ValidatePermissionAsync(GetUserId(), Permission.ManageUsers);
        if (!can) return Forbid();
        return Ok(new ApiResponse<object> { Success = true, Data = new { message = "Users endpoint - use Outlets for outlet-scoped user management" } });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = GetUserId();
        var canManage = await _auth.ValidatePermissionAsync(userId, Permission.ManageSettings);
        return Ok(new ApiResponse<object> { Success = true, Data = new { userId, canManageSettings = canManage } });
    }

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IAuthService _auth;

    public RolesController(IAuthService auth) => _auth = auth;

    [HttpGet("permissions")]
    public IActionResult GetPermissions()
    {
        var values = Enum.GetValues<Permission>().Select(p => new { Value = p.ToString(), Name = p.ToString() }).ToList();
        return Ok(new ApiResponse<List<object>> { Success = true, Data = values.Cast<object>().ToList() });
    }
}
