using Microsoft.AspNetCore.Identity;

namespace OrderProcessing.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public bool IsActive { get; set; } = true;
}
