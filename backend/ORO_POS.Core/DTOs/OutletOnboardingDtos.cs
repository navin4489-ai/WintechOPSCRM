using System.ComponentModel.DataAnnotations;
namespace ORO_POS.Core.DTOs;

public class CreateOutletWithAdminRequest
{
    [Required] public OutletDto Outlet { get; set; } = new();
    [Required, RegularExpression("^[a-zA-Z0-9._-]{3,64}$")] public string AdminUsername { get; set; } = "";
    [StringLength(254)] public string? AdminEmail { get; set; }
    [Required, StringLength(72, MinimumLength = 12)] public string AdminPassword { get; set; } = "";
}
public class OutletOnboardingResult
{
    public OutletDto Outlet { get; set; } = new();
    public UserDto Administrator { get; set; } = new();
}
