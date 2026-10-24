using MaintenancePRO.Api.Contracts;
using MaintenancePRO.Api.Data;
using MaintenancePRO.Api.Models;
using MaintenancePRO.Api.Security;
using MaintenancePRO.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePRO.Api.Controllers;

[ApiController, Route("api/diagnoses"), Authorize(Policy = "ManageMaintenance")]
public sealed class DiagnosesController(AppDbContext db, AuditService audit) : ControllerBase
{
    private bool CanAccess(Breakdown b) => User.IsManager() || (User.IsInRole(AppRoles.Technician) && b.AssignedTechnicianId == User.Id());

    private IQueryable<Diagnosis> Visible(IQueryable<Diagnosis> q)
    {
        if (User.IsManager()) return q;
        var uid = User.Id();
        return q.Where(x => x.Breakdown!.AssignedTechnicianId == uid);
    }

    [HttpGet]
    public async Task<ActionResult<List<DiagnosisDto>>> List([FromQuery] int? breakdownId, [FromQuery] DiagnosisStatus? status)
    {
        var q = Visible(db.Diagnoses.AsNoTracking().Include(d => d.CreatedBy).AsQueryable());
        if (breakdownId.HasValue) q = q.Where(x => x.BreakdownId == breakdownId);
        if (status.HasValue) q = q.Where(x => x.Status == status);
        return Ok((await q.OrderByDescending(x => x.CreatedUtc).Take(200).ToListAsync()).Select(Mapper.Diagnosis).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DiagnosisDto>> Get(int id)
    {
        var x = await Visible(db.Diagnoses.AsNoTracking().Include(d => d.CreatedBy).AsQueryable()).FirstOrDefaultAsync(d => d.Id == id);
        if (x == null) return NotFound();
        return Ok(Mapper.Diagnosis(x));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromQuery] int breakdownId, CreateDiagnosisRequest r)
    {
        var b = await db.Breakdowns.FirstOrDefaultAsync(x => x.Id == breakdownId);
        if (b == null) return NotFound(new { message = "Panne introuvable." });
        if (!CanAccess(b)) return Forbid();
        if (b.Status is not (BreakdownStatus.Reported or BreakdownStatus.Diagnosing)) return Conflict(new { message = "Cette panne n'est pas en phase de diagnostic." });
        if (await db.Diagnoses.AnyAsync(d => d.BreakdownId == breakdownId && d.Status == DiagnosisStatus.Submitted)) return Conflict(new { message = "Un diagnostic est déjà en attente d'approbation." });
        var x = new Diagnosis { BreakdownId = breakdownId, Findings = r.Findings.Trim(), RootCause = r.RootCause?.Trim(), RecommendedAction = r.RecommendedAction?.Trim(), CreatedByUserId = User.Id()! };
        db.Diagnoses.Add(x);
        b.Status = BreakdownStatus.Diagnosing;
        await db.SaveChangesAsync();
        await audit.LogAsync("DiagnosisCreated", "Diagnosis", x.Id);
        return CreatedAtAction(nameof(Get), new { id = x.Id }, Mapper.Diagnosis(await db.Diagnoses.Include(d => d.CreatedBy).FirstAsync(d => d.Id == x.Id)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateDraft(int id, CreateDiagnosisRequest r)
    {
        var x = await db.Diagnoses.Include(d => d.Breakdown).Include(d => d.CreatedBy).FirstOrDefaultAsync(d => d.Id == id);
        if (x == null) return NotFound();
        if (!CanAccess(x.Breakdown!)) return Forbid();
        if (x.Status != DiagnosisStatus.Draft) return Conflict(new { message = "Seul un brouillon peut être modifié." });
        if (x.CreatedByUserId != User.Id() && !User.IsManager()) return Forbid();
        x.Findings = r.Findings.Trim(); x.RootCause = r.RootCause?.Trim(); x.RecommendedAction = r.RecommendedAction?.Trim();
        await db.SaveChangesAsync();
        return Ok(Mapper.Diagnosis(x));
    }

    /// <summary>Le technicien soumet son diagnostic : la panne passe en attente d'approbation du superviseur.</summary>
    [HttpPatch("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        var x = await db.Diagnoses.Include(d => d.Breakdown).Include(d => d.CreatedBy).FirstOrDefaultAsync(d => d.Id == id);
        if (x == null) return NotFound();
        if (!CanAccess(x.Breakdown!)) return Forbid();
        if (x.Status != DiagnosisStatus.Draft || x.Breakdown!.Status != BreakdownStatus.Diagnosing) return Conflict(new { message = "Ce diagnostic ne peut pas être soumis." });
        x.Status = DiagnosisStatus.Submitted; x.SubmittedUtc = DateTime.UtcNow;
        x.Breakdown.Status = BreakdownStatus.AwaitingApproval;
        await db.SaveChangesAsync();
        await audit.LogAsync("DiagnosisSubmitted", "Diagnosis", id);
        return Ok(Mapper.Diagnosis(x));
    }

    [HttpPatch("{id:int}/decision"), Authorize(Policy = "ManageEquipment")]
    public async Task<IActionResult> Decision(int id, DiagnosisDecisionRequest r)
    {
        if (r.Status is not (DiagnosisStatus.Approved or DiagnosisStatus.Rejected)) return BadRequest(new { message = "Décision attendue : Approved ou Rejected." });
        var x = await db.Diagnoses.Include(d => d.Breakdown).Include(d => d.CreatedBy).FirstOrDefaultAsync(d => d.Id == id);
        if (x == null) return NotFound();
        if (x.Status != DiagnosisStatus.Submitted || x.Breakdown!.Status != BreakdownStatus.AwaitingApproval) return Conflict(new { message = "Ce diagnostic n'est pas en attente de décision." });
        x.Status = r.Status; x.DecidedUtc = DateTime.UtcNow; x.DecidedByUserId = User.Id();
        // Approuvé → maintenance autorisée ; Rejeté → le technicien doit refaire son diagnostic.
        x.Breakdown.Status = r.Status == DiagnosisStatus.Approved ? BreakdownStatus.InMaintenance : BreakdownStatus.Diagnosing;
        await db.SaveChangesAsync();
        await Workflow.RefreshEquipmentAsync(db, x.Breakdown.EquipmentId);
        await audit.LogAsync("DiagnosisDecision", "Diagnosis", id);
        return Ok(Mapper.Diagnosis(x));
    }
}
