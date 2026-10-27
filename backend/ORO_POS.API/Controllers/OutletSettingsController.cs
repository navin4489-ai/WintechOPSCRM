using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.API.Security;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Data;
using ORO_POS.Infrastructure.Services;

namespace ORO_POS.API.Controllers;
[ApiController,Authorize,Route("api/outlet-settings")]
public class OutletSettingsController(AppDbContext db,IOutletService outlets,OutletOrderingSettingsService ordering):ControllerBase
{
    private int UserId=>int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<int> AssignedOutlet()
    {
        var actor=await TenantAccess.ActorAsync(db,UserId);
        var id=actor.OutletId??throw new UnauthorizedAccessException("Outlet assignment required.");
        await TenantAccess.RequireOutletAdminAsync(db,UserId,id);return id;
    }
    [HttpGet] public async Task<IActionResult> Get()=>Ok(new ApiResponse<OutletDto>{Success=true,Data=await outlets.GetOutletByIdAsync(await AssignedOutlet(),UserId)});
    [HttpPut] public async Task<IActionResult> Update(OutletDto request)
    {
        var id=await AssignedOutlet();if(request.Id!=0&&request.Id!=id)return Forbid();
        return Ok(new ApiResponse<OutletDto>{Success=true,Data=await outlets.UpdateOutletAsync(id,request,UserId)});
    }
    [HttpGet("ordering")] public async Task<IActionResult> Settings()=>Ok(new ApiResponse<object>{Success=true,Data=await ordering.GetAsync(await AssignedOutlet(),UserId)});
    [HttpPost("ordering")] public async Task<IActionResult> Configure(OrderingSettingsRequest request)=>Ok(new ApiResponse<object>{Success=true,Data=await ordering.ConfigureAsync(await AssignedOutlet(),request,UserId)});
}
