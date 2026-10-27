using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Enums;
using ORO_POS.Core.Interfaces;

namespace ORO_POS.API.Controllers;

/// <summary>
/// Staff account management (BRD HR-001): list, create, edit and deactivate
/// users, assigning each a role and outlet scope. Every action requires the
/// ManageUsers permission and is audit-logged server-side.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;
    private readonly IAuthService _auth;

    public UsersController(IUserService users, IAuthService auth)
    {
        _users = users;
        _auth = auth;
    }

    /// <summary>List staff accounts visible to the caller (outlet-scoped for non-owners).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(new ApiResponse<List<UserDto>> { Success = true, Data = await _users.GetUsersAsync(GetUserId()) });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _users.GetUserByIdAsync(id, GetUserId());
        if (user == null) return NotFound(new ApiResponse<object> { Success = false, Errors = new[] { $"User {id} not found." } });
        return Ok(new ApiResponse<UserDto> { Success = true, Data = user });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = GetUserId();
        var canManage = await _auth.ValidatePermissionAsync(userId, Permission.ManageSettings);
        return Ok(new ApiResponse<object> { Success = true, Data = new { userId, canManageSettings = canManage } });
    }

    /// <summary>Create a staff account with role and outlet assignment.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        => Ok(new ApiResponse<UserDto> { Success = true, Data = await _users.CreateUserAsync(request, GetUserId()) });

    /// <summary>Update an account's details, role, outlet scope, status or password.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
        => Ok(new ApiResponse<UserDto> { Success = true, Data = await _users.UpdateUserAsync(id, request, GetUserId()) });

    /// <summary>Deactivate and soft-delete a staff account.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(new ApiResponse<UserDto> { Success = true, Data = await _users.DeleteUserAsync(id, GetUserId()) });

    private int GetUserId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    /// <summary>List assignable roles with their permission sets (for the staff form).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromServices] IUserService users)
        => Ok(new ApiResponse<List<RoleDto>> { Success = true, Data = await users.GetRolesAsync() });

    [HttpGet("permissions")]
    public IActionResult GetPermissions()
    {
        var values = Enum.GetValues<Permission>().Select(p => new { Value = p.ToString(), Name = p.ToString() }).ToList();
        return Ok(new ApiResponse<List<object>> { Success = true, Data = values.Cast<object>().ToList() });
    }
}
