using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ORO_POS.API.Security;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Services;

namespace ORO_POS.API.Controllers;
[ApiController, Authorize(Roles="Owner"), Route("api/super-admin/outlets")]
public class SuperAdminController(IOutletService outlets, OutletOrderingSettingsService ordering) : ControllerBase
{
    private int UserId=>int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public async Task<IActionResult> List()=>Ok(new ApiResponse<List<OutletDto>>{Success=true,Data=await outlets.GetOutletsAsync(UserId)});
    [HttpPost] public async Task<IActionResult> Create(CreateOutletWithAdminRequest request,[FromServices] OutletProvisioningService provisioning)
        =>Ok(new ApiResponse<OutletOnboardingResult>{Success=true,Data=await provisioning.CreateAsync(request,UserId)});
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id,OutletDto request)=>Ok(new ApiResponse<OutletDto>{Success=true,Data=await outlets.UpdateOutletAsync(id,request,UserId)});
    [HttpGet("{id:int}/ordering")] public async Task<IActionResult> Settings(int id)=>Ok(new ApiResponse<object>{Success=true,Data=await ordering.GetAsync(id,UserId)});
    [HttpPost("{id:int}/ordering")] public async Task<IActionResult> Configure(int id,OrderingSettingsRequest request)=>Ok(new ApiResponse<object>{Success=true,Data=await ordering.ConfigureAsync(id,request,UserId)});
}
