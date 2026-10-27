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

[ApiController, Route("api/interventions"), Authorize]
public sealed class InterventionsController(AppDbContext db, UserManager<ApplicationUser> users, AuditService audit) : ControllerBase
{
    private static IQueryable<Intervention> WithRefs(IQueryable<Intervention> q) => q.Include(x => x.Equipment).Include(x => x.Breakdown).Include(x => x.AssignedTechnician);

    private IQueryable<Intervention> Visible(IQueryable<Intervention> q)
    {
        var uid = User.Id();
        if (User.IsManager()) return q;
        if (User.IsInRole(AppRoles.Technician)) return q.Where(x => x.AssignedTechnicianId == uid);
        return q.Where(x => x.Breakdown!.ReportedByUserId == uid);
    }

    private bool CanRead(Intervention x)
    {
        var uid = User.Id();
        return User.IsManager() || x.AssignedTechnicianId == uid || x.Breakdown?.ReportedByUserId == uid;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InterventionDto>>> List([FromQuery] InterventionStatus? status, [FromQuery] int? breakdownId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = Visible(WithRefs(db.Interventions.AsNoTracking()));
        if (status.HasValue) q = q.Where(x => x.Status == status);
        if (breakdownId.HasValue) q = q.Where(x => x.BreakdownId == breakdownId);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.CreatedUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var m = User.IsManager();
        return Ok(new PagedResult<InterventionDto>(page, pageSize, total, items.Select(x => Mapper.Intervention(x, m)).ToList()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InterventionDto>> Get(int id)
    {
        var x = await WithRefs(db.Interventions.AsNoTracking()).FirstOrDefaultAsync(i => i.Id == id);
        if (x == null) return NotFound();
        if (!CanRead(x)) return Forbid();
        return Ok(Mapper.Intervention(x, User.IsManager()));
    }

    [HttpPost, Authorize(Policy = "ManageMaintenance")]
    public async Task<IActionResult> Create(CreateInterventionRequest r)
    {
        var b = await db.Breakdowns.FirstOrDefaultAsync(x => x.Id == r.BreakdownId);
        if (b == null) return BadRequest(new { message = "Panne inconnue." });
        var manager = User.IsManager();
        var uid = User.Id()!;
        if (!manager && b.AssignedTechnicianId != uid) return Forbid();
        if (b.Status != BreakdownStatus.InMaintenance) return Conflict(new { message = "L'intervention n'est possible qu'après approbation du diagnostic (panne « En maintenance »)." });

        string? techId = b.AssignedTechnicianId;
        if (manager && r.AssignedTechnicianId != null)
        {
            var tech = await users.FindByIdAsync(r.AssignedTechnicianId);
            if (tech == null || tech.Status != AccountStatus.Active || !await users.IsInRoleAsync(tech, AppRoles.Technician)) return BadRequest(new { message = "Technicien actif invalide." });
            techId = tech.Id;
        }
        if (!manager) techId = uid;
        var x = new Intervention { BreakdownId = b.Id, EquipmentId = b.EquipmentId, Title = r.Title.Trim(), WorkDescription = r.WorkDescription.Trim(), CreatedByUserId = uid, AssignedTechnicianId = techId, Status = techId == null ? InterventionStatus.Planned : InterventionStatus.Assigned };
        db.Interventions.Add(x);
        await db.SaveChangesAsync();
        await audit.LogAsync("InterventionCreated", "Intervention", x.Id);
        var created = await WithRefs(db.Interventions.AsNoTracking()).FirstAsync(i => i.Id == x.Id);
        return CreatedAtAction(nameof(Get), new { id = x.Id }, Mapper.Intervention(created, manager));
    }

    [HttpPatch("{id:int}"), Authorize(Policy = "ManageMaintenance")]
    public async Task<IActionResult> Update(int id, UpdateInterventionRequest r)
    {
        var x = await WithRefs(db.Interventions).FirstOrDefaultAsync(i => i.Id == id);
        if (x == null) return NotFound();
        var manager = User.IsManager();
        var uid = User.Id();
        if (!manager && x.AssignedTechnicianId != uid) return Forbid();
        // Un technicien ne modifie ni l'attribution, ni les heures, ni le coût, ni le descriptif.
        if (!manager && (r.AssignedTechnicianId != null || r.LaborHours != null || r.Cost != null || r.Title != null || r.WorkDescription != null)) return Forbid();

        var terminal = x.Status is InterventionStatus.Completed or InterventionStatus.Cancelled or InterventionStatus.Rejected;
        if (terminal && (r.Status != null || r.AssignedTechnicianId != null || r.Resolution != null || r.Title != null || r.WorkDescription != null))
            return Conflict(new { message = "Cette intervention est terminée : seuls les heures et le coût peuvent encore être ajustés par un manager." });

        if (r.LaborHours is < 0 or > 100000 || r.Cost is < 0 or > 1000000000m) return BadRequest(new { message = "Heures et coût invalides." });

        if (r.AssignedTechnicianId != null)
        {
            var tech = await users.FindByIdAsync(r.AssignedTechnicianId);
            if (tech == null || tech.Status != AccountStatus.Active || !await users.IsInRoleAsync(tech, AppRoles.Technician)) return BadRequest(new { message = "Technicien actif invalide." });
            x.AssignedTechnicianId = tech.Id;
            if (x.Status == InterventionStatus.Planned) x.Status = InterventionStatus.Assigned;
        }
        if (r.Title != null) x.Title = r.Title.Trim();
        if (r.WorkDescription != null) x.WorkDescription = r.WorkDescription.Trim();
        if (r.Resolution != null) x.Resolution = r.Resolution.Trim();
        if (r.LaborHours.HasValue) x.LaborHours = r.LaborHours;
        if (r.Cost.HasValue) x.Cost = r.Cost;

        var breakdownResolved = false;
        if (r.Status.HasValue && r.Status.Value != x.Status)
        {
            var to = r.Status.Value;
            if (!Enum.IsDefined(to) || !CanTransition(x.Status, to, manager)) return Conflict(new { message = "Transition d'intervention non autorisée." });
            if (to == InterventionStatus.Assigned && x.AssignedTechnicianId == null) return BadRequest(new { message = "Attribue d'abord un technicien." });
            x.Status = to;
            if (to == InterventionStatus.InProgress) x.StartedUtc ??= DateTime.UtcNow;
            if (to == InterventionStatus.Completed)
            {
                if (string.IsNullOrWhiteSpace(x.Resolution)) return BadRequest(new { message = "Une résolution est obligatoire pour terminer l'intervention." });
                x.CompletedUtc = DateTime.UtcNow;
                // La panne n'est résolue que s'il ne reste AUCUNE autre intervention ouverte.
                var othersOpen = await db.Interventions.AnyAsync(i => i.BreakdownId == x.BreakdownId && i.Id != x.Id && i.Status != InterventionStatus.Completed && i.Status != InterventionStatus.Cancelled && i.Status != InterventionStatus.Rejected);
                if (!othersOpen && x.Breakdown!.Status == BreakdownStatus.InMaintenance) { x.Breakdown.Status = BreakdownStatus.Resolved; x.Breakdown.ResolvedUtc = DateTime.UtcNow; breakdownResolved = true; }
            }
        }
        await db.SaveChangesAsync();
        if (breakdownResolved) await Workflow.RefreshEquipmentAsync(db, x.EquipmentId);
        await audit.LogAsync("InterventionUpdated", "Intervention", id);
        return Ok(Mapper.Intervention(x, manager));
    }

    /// <summary>Compte rendu d'intervention au format PDF.</summary>
    [HttpGet("{id:int}/report")]
    public async Task<IActionResult> Report(int id)
    {
        var x = await WithRefs(db.Interventions.AsNoTracking()).FirstOrDefaultAsync(i => i.Id == id);
        if (x == null) return NotFound();
        if (!CanRead(x)) return Forbid();
        var manager = User.IsManager();
        string D(DateTime? d) => d?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";
        var dto = Mapper.Intervention(x, manager);
        var lines = new List<string>
        {
            $"Intervention n°{x.Id} : {x.Title}",
            $"Équipement : {x.Equipment!.Code} - {x.Equipment.Name}",
            $"Panne liée : {x.Breakdown?.Title}",
            $"Statut : {x.Status}",
            $"Technicien : {x.AssignedTechnician?.DisplayName ?? "non attribué"}",
            $"Créée le : {D(x.CreatedUtc)}   Démarrée : {D(x.StartedUtc)}   Terminée : {D(x.CompletedUtc)}",
            $"Durée : {(dto.DurationMinutes.HasValue ? dto.DurationMinutes + " min" : "—")}"
        };
        if (manager) lines.Add($"Heures facturées : {x.LaborHours?.ToString() ?? "—"}   Coût : {x.Cost?.ToString() ?? "—"}");
        lines.Add(""); lines.Add("Travaux prévus :"); lines.Add(x.WorkDescription);
        lines.Add(""); lines.Add("Compte rendu / résolution :"); lines.Add(x.Resolution ?? "—");
        lines.Add(""); lines.Add($"Document généré le {DateTime.Now:dd/MM/yyyy HH:mm}");
        return File(SimplePdf.Create("MaintenancePRO - Compte rendu d'intervention", lines), "application/pdf", $"intervention-{x.Id}.pdf");
    }

    private static bool CanTransition(InterventionStatus a, InterventionStatus b, bool manager) => a switch
    {
        InterventionStatus.Planned => b == InterventionStatus.Assigned || (manager && b == InterventionStatus.Cancelled),
        InterventionStatus.Assigned => b == InterventionStatus.InProgress || (manager && b == InterventionStatus.Cancelled),
        InterventionStatus.InProgress => b is InterventionStatus.Paused or InterventionStatus.Completed,
        InterventionStatus.Paused => b == InterventionStatus.InProgress || (manager && b == InterventionStatus.Cancelled),
        _ => false
    };
}
