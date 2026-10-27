using MaintenancePRO.Api.Contracts;
using MaintenancePRO.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MaintenancePRO.Api.Controllers;

[ApiController, Route("api/users"), Authorize(Policy = "ManageEquipment")]
public sealed class UsersController(UserManager<ApplicationUser> users) : ControllerBase
{
    /// <summary>Techniciens actifs (pour l'attribution des pannes/interventions par Admin et Superviseur).</summary>
    [HttpGet("technicians")]
    public async Task<ActionResult<List<TechnicianDto>>> Technicians()
    {
        var list = await users.GetUsersInRoleAsync(AppRoles.Technician);
        return Ok(list.Where(u => u.Status == AccountStatus.Active).OrderBy(u => u.DisplayName).Select(u => new TechnicianDto(u.Id, u.DisplayName, u.Email ?? "")).ToList());
    }
}
