using System.Security.Claims;
using MaintenancePRO.Api.Models;

namespace MaintenancePRO.Api.Security;

public static class UserExtensions
{
    /// <summary>Identifiant de l'utilisateur (claim « sub » du JWT).</summary>
    public static string? Id(this ClaimsPrincipal u) => u.FindFirstValue("sub") ?? u.FindFirstValue(ClaimTypes.NameIdentifier);
    public static bool IsManager(this ClaimsPrincipal u) => u.IsInRole(AppRoles.Admin) || u.IsInRole(AppRoles.Supervisor);
}
