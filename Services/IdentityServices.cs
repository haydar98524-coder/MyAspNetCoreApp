using System.Security.Claims;
using KirkukInstitute.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace KirkukInstitute.Services;

public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("FullName", user.FullName));
        identity.AddClaim(new Claim("PreferredLanguage", user.PreferredLanguage));
        if (user.TeacherId.HasValue) identity.AddClaim(new Claim("TeacherId", user.TeacherId.Value.ToString()));
        if (user.ParentId.HasValue) identity.AddClaim(new Claim("ParentId", user.ParentId.Value.ToString()));
        if (user.StudentId.HasValue) identity.AddClaim(new Claim("StudentId", user.StudentId.Value.ToString()));
        return identity;
    }
}

public static class UserExtensions
{
    public static string CurrentUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue("FullName") ?? user.Identity?.Name ?? "";

    public static int? TeacherId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("TeacherId"), out var id) ? id : null;

    public static int? ParentId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("ParentId"), out var id) ? id : null;

    public static int? StudentId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("StudentId"), out var id) ? id : null;

    public static bool IsStaff(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Administrator) || user.IsInRole(Roles.Manager) ||
        user.IsInRole(Roles.Accountant) || user.IsInRole(Roles.Hr) || user.IsInRole(Roles.Registrar);
}
