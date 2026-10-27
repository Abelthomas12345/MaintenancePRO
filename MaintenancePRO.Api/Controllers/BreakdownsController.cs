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

[ApiController, Route("api/breakdowns"), Authorize]
public sealed class BreakdownsController(AppDbContext db, UserManager<ApplicationUser> users, AuditService audit) : ControllerBase
{
    private IQueryable<Breakdown> Visible(IQueryable<Breakdown> q)
    {
        var uid = User.Id();
        if (User.IsManager()) return q;
        if (User.IsInRole(AppRoles.Technician)) return q.Where(x => x.AssignedTechnicianId == uid || x.ReportedByUserId == uid);
        return q.Where(x => x.ReportedByUserId == uid);
    }

    private static IQueryable<Breakdown> WithRefs(IQueryable<Breakdown> q) => q.Include(x => x.Equipment).Include(x => x.ReportedBy).Include(x => x.AssignedTechnician);

    private bool CanRead(Breakdown b)
    {
        var uid = User.Id();
        return User.IsManager() || b.ReportedByUserId == uid || (User.IsInRole(AppRoles.Technician) && b.AssignedTechnicianId == uid);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BreakdownDto>>> List([FromQuery] BreakdownStatus? status, [FromQuery] BreakdownSeverity? severity, [FromQuery] int? equipmentId, [FromQuery] bool? openOnly, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = Visible(WithRefs(db.Breakdowns.AsNoTracking()));
        if (status.HasValue) q = q.Where(x => x.Status == status);
        if (severity.HasValue) q = q.Where(x => x.Severity == severity);
        if (equipmentId.HasValue) q = q.Where(x => x.EquipmentId == equipmentId);
        if (openOnly == true) q = q.Where(x => x.Status != BreakdownStatus.Resolved && x.Status != BreakdownStatus.Closed && x.Status != BreakdownStatus.Rejected);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.ReportedUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<BreakdownDto>(page, pageSize, total, items.Select(Mapper.Breakdown).ToList()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BreakdownDetailDto>> Get(int id)
    {
        var b = await WithRefs(db.Breakdowns.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return NotFound();
        if (!CanRead(b)) return Forbid();
        var manager = User.IsManager();
        var diagnoses = new List<DiagnosisDto>();
        if (!User.IsInRole(AppRoles.User))
            diagnoses = (await db.Diagnoses.AsNoTracking().Include(d => d.CreatedBy).Where(d => d.BreakdownId == id).OrderByDescending(d => d.CreatedUtc).ToListAsync()).Select(Mapper.Diagnosis).ToList();
        var ivs = await db.Interventions.AsNoTracking().Include(i => i.Equipment).Include(i => i.Breakdown).Include(i => i.AssignedTechnician).Where(i => i.BreakdownId == id).OrderByDescending(i => i.CreatedUtc).ToListAsync();
        if (User.IsInRole(AppRoles.Technician)) ivs = ivs.Where(i => i.AssignedTechnicianId == User.Id()).ToList();
        var photos = await db.Photos.AsNoTracking().Where(p => p.BreakdownId == id).OrderByDescending(p => p.CreatedUtc).ToListAsync();
        return Ok(new BreakdownDetailDto(Mapper.Breakdown(b), diagnoses, ivs.Select(i => Mapper.Intervention(i, manager)).ToList(), photos.Select(Mapper.Photo).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateBreakdownRequest r)
    {
        if (!Enum.IsDefined(r.Severity)) return BadRequest(new { message = "Sévérité invalide." });
        var eq = await db.Equipments.FirstOrDefaultAsync(x => x.Id == r.EquipmentId && x.Status != EquipmentStatus.Retired);
        if (eq == null) return BadRequest(new { message = "Équipement inconnu ou archivé." });
        var uid = User.Id()!;
        var title = r.Title.Trim();

        // Limites anti-abus
        var since = DateTime.UtcNow.AddHours(-24);
        if (await db.Breakdowns.CountAsync(x => x.ReportedByUserId == uid && x.ReportedUtc >= since) >= 20)
            return StatusCode(429, new { message = "Limite de 20 déclarations par 24 heures atteinte." });
        if (User.IsInRole(AppRoles.User) && await db.Breakdowns.CountAsync(x => x.ReportedByUserId == uid && x.Status != BreakdownStatus.Resolved && x.Status != BreakdownStatus.Closed && x.Status != BreakdownStatus.Rejected) >= 10)
            return Conflict(new { message = "Tu as déjà 10 déclarations ouvertes. Attends leur traitement avant d'en créer d'autres." });
        if (await db.Breakdowns.AnyAsync(x => x.EquipmentId == eq.Id && x.ReportedByUserId == uid && x.Title == title && x.Status != BreakdownStatus.Resolved && x.Status != BreakdownStatus.Closed && x.Status != BreakdownStatus.Rejected))
            return Conflict(new { message = "Une déclaration identique est déjà ouverte pour cet équipement." });

        var b = new Breakdown { EquipmentId = eq.Id, Title = title, Description = r.Description.Trim(), Severity = r.Severity, ReportedByUserId = uid };
        db.Breakdowns.Add(b);
        // Un simple utilisateur ne peut que marquer l'équipement « À vérifier » ; « En panne » est confirmé par le personnel de maintenance.
        var newStatus = User.IsInRole(AppRoles.User) ? EquipmentStatus.ToInspect : EquipmentStatus.Broken;
        if (eq.Status == EquipmentStatus.Available || (eq.Status == EquipmentStatus.ToInspect && newStatus == EquipmentStatus.Broken)) { eq.Status = newStatus; eq.UpdatedUtc = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        await audit.LogAsync("BreakdownReported", "Breakdown", b.Id);
        var created = await WithRefs(db.Breakdowns.AsNoTracking()).FirstAsync(x => x.Id == b.Id);
        return CreatedAtAction(nameof(Get), new { id = b.Id }, Mapper.Breakdown(created));
    }

    /// <summary>Attribution d'une panne à un technicien (Admin/Superviseur).</summary>
    [HttpPatch("{id:int}/assign"), Authorize(Policy = "ManageEquipment")]
    public async Task<IActionResult> Assign(int id, AssignBreakdownRequest r)
    {
        var b = await db.Breakdowns.FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return NotFound();
        if (!Mapper.IsOpen(b.Status)) return Conflict(new { message = "Cette panne est clôturée." });
        var tech = await users.FindByIdAsync(r.TechnicianId);
        if (tech == null || tech.Status != AccountStatus.Active || !await users.IsInRoleAsync(tech, AppRoles.Technician)) return BadRequest(new { message = "Technicien actif invalide." });
        b.AssignedTechnicianId = tech.Id;
        await db.SaveChangesAsync();
        await audit.LogAsync("BreakdownAssigned", "Breakdown", id);
        return Ok(Mapper.Breakdown(await WithRefs(db.Breakdowns.AsNoTracking()).FirstAsync(x => x.Id == id)));
    }

    /// <summary>Changement de statut manuel réservé aux managers. L'avancement normal passe par les diagnostics et interventions.</summary>
    [HttpPatch("{id:int}/status"), Authorize(Policy = "ManageEquipment")]
    public async Task<IActionResult> SetStatus(int id, SetBreakdownStatusRequest r)
    {
        var b = await db.Breakdowns.FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return NotFound();
        if (!Allowed(b.Status, r.Status)) return Conflict(new { message = "Transition de panne non autorisée." });
        b.Status = r.Status;
        await db.SaveChangesAsync();
        await Workflow.RefreshEquipmentAsync(db, b.EquipmentId);
        await audit.LogAsync("BreakdownStatusChanged", "Breakdown", id);
        return Ok(Mapper.Breakdown(await WithRefs(db.Breakdowns.AsNoTracking()).FirstAsync(x => x.Id == id)));
    }

    private static bool Allowed(BreakdownStatus a, BreakdownStatus b) => a switch
    {
        BreakdownStatus.Reported => b == BreakdownStatus.Rejected,
        BreakdownStatus.Diagnosing => b == BreakdownStatus.Rejected,
        BreakdownStatus.AwaitingApproval => b == BreakdownStatus.Rejected,
        BreakdownStatus.InMaintenance => b == BreakdownStatus.Diagnosing,
        BreakdownStatus.Resolved => b == BreakdownStatus.Closed,
        _ => false
    };
}
