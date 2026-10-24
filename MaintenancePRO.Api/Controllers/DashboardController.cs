using MaintenancePRO.Api.Contracts;
using MaintenancePRO.Api.Data;
using MaintenancePRO.Api.Models;
using MaintenancePRO.Api.Security;
using MaintenancePRO.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePRO.Api.Controllers;

[ApiController, Route("api/dashboard"), Authorize]
public sealed class DashboardController(AppDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    /// <summary>Tableau de bord filtré par rôle : un utilisateur simple ne voit que SES déclarations et les équipements concernés.</summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var uid = User.Id()!;
        var manager = User.IsManager();
        var isTech = User.IsInRole(AppRoles.Technician);
        var eq = db.Equipments.AsNoTracking().Where(x => x.Status != EquipmentStatus.Retired);
        var br = db.Breakdowns.AsNoTracking().AsQueryable();
        var iv = db.Interventions.AsNoTracking().AsQueryable();
        if (!manager)
        {
            if (isTech) { br = br.Where(x => x.AssignedTechnicianId == uid || x.ReportedByUserId == uid); iv = iv.Where(x => x.AssignedTechnicianId == uid); }
            else { br = br.Where(x => x.ReportedByUserId == uid); iv = iv.Where(x => x.Breakdown!.ReportedByUserId == uid); eq = eq.Where(e => br.Any(b => b.EquipmentId == e.Id)); }
        }
        var month = DateTime.UtcNow.AddDays(-30);
        var recentB = await br.Include(x => x.Equipment).Include(x => x.ReportedBy).Include(x => x.AssignedTechnician).OrderByDescending(x => x.ReportedUtc).Take(5).ToListAsync();
        var recentI = await iv.Include(x => x.Equipment).Include(x => x.Breakdown).Include(x => x.AssignedTechnician).OrderByDescending(x => x.CreatedUtc).Take(5).ToListAsync();
        var dto = new DashboardDto(
            DateTime.UtcNow,
            User.FindFirst("name")?.Value ?? "",
            User.FindFirst("role")?.Value ?? "",
            await eq.CountAsync(),
            await eq.CountAsync(x => x.Status == EquipmentStatus.Broken),
            await br.CountAsync(x => x.Status != BreakdownStatus.Resolved && x.Status != BreakdownStatus.Closed && x.Status != BreakdownStatus.Rejected),
            await br.CountAsync(x => x.Severity == BreakdownSeverity.Critical && x.Status != BreakdownStatus.Resolved && x.Status != BreakdownStatus.Closed && x.Status != BreakdownStatus.Rejected),
            await iv.CountAsync(x => x.Status != InterventionStatus.Completed && x.Status != InterventionStatus.Cancelled && x.Status != InterventionStatus.Rejected),
            await iv.CountAsync(x => x.Status == InterventionStatus.Completed && x.CompletedUtc >= month),
            await br.CountAsync(x => x.Status == BreakdownStatus.AwaitingApproval),
            User.IsInRole(AppRoles.Admin) ? await users.Users.CountAsync(x => x.Status == AccountStatus.Pending) : 0,
            recentB.Select(Mapper.Breakdown).ToList(),
            recentI.Select(x => Mapper.Intervention(x, manager)).ToList());
        return Ok(dto);
    }

    [HttpGet("reports"), Authorize(Policy = "ManageEquipment")]
    public async Task<ActionResult<ReportDto>> Reports([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        var from = fromUtc?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-30);
        var to = toUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        if (from > to || to - from > TimeSpan.FromDays(366)) return BadRequest(new { message = "Période invalide (maximum 366 jours)." });

        var breaks = db.Breakdowns.AsNoTracking().Where(x => x.ReportedUtc >= from && x.ReportedUtc <= to);
        var ivs = db.Interventions.AsNoTracking().Where(x => x.CreatedUtc >= from && x.CreatedUtc <= to);

        // SQLite ne sait pas faire SUM sur decimal : on rapatrie les valeurs (période bornée) et on additionne en C#.
        var amounts = await ivs.Select(x => new { x.LaborHours, x.Cost }).ToListAsync();
        var resolved = await breaks.Where(x => x.ResolvedUtc != null).Select(x => new { x.ReportedUtc, x.ResolvedUtc }).ToListAsync();
        double? mttr = resolved.Count == 0 ? null : Math.Round(resolved.Average(x => (x.ResolvedUtc!.Value - x.ReportedUtc).TotalHours), 1);

        var active = await db.Equipments.CountAsync(x => x.Status != EquipmentStatus.Retired);
        var available = await db.Equipments.CountAsync(x => x.Status == EquipmentStatus.Available);

        var sev = (await breaks.GroupBy(x => x.Severity).Select(g => new { g.Key, C = g.Count() }).ToListAsync()).Select(g => new CountBySeverity(g.Key, g.C)).ToList();
        var st = (await ivs.GroupBy(x => x.Status).Select(g => new { g.Key, C = g.Count() }).ToListAsync()).Select(g => new CountByStatus(g.Key, g.C)).ToList();

        return Ok(new ReportDto(from, to, await breaks.CountAsync(), await breaks.CountAsync(x => x.Status == BreakdownStatus.Resolved || x.Status == BreakdownStatus.Closed),
            await ivs.CountAsync(), await ivs.CountAsync(x => x.Status == InterventionStatus.Completed),
            amounts.Sum(x => x.LaborHours ?? 0), amounts.Sum(x => x.Cost ?? 0),
            active == 0 ? 100 : Math.Round(100.0 * available / active, 1), mttr, sev, st));
    }
}
