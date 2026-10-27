using MaintenancePRO.Api.Contracts;
using MaintenancePRO.Api.Data;
using MaintenancePRO.Api.Models;
using MaintenancePRO.Api.Security;
using MaintenancePRO.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePRO.Api.Controllers;

[ApiController, Route("api/equipment"), Authorize]
public sealed class EquipmentController(AppDbContext db, AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<EquipmentDto>>> List([FromQuery] string? search, [FromQuery] EquipmentStatus? status, [FromQuery] bool includeRetired = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = db.Equipments.AsNoTracking().AsQueryable();
        if (!includeRetired) q = q.Where(x => x.Status != EquipmentStatus.Retired);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.Code.Contains(s) || x.Name.Contains(s) || x.Location.Contains(s) || x.Category.Contains(s)); }
        if (status.HasValue) q = q.Where(x => x.Status == status);
        var total = await q.CountAsync();
        var items = await q.OrderBy(x => x.Code).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<EquipmentDto>(page, pageSize, total, items.Select(Mapper.Equipment).ToList()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EquipmentDto>> Get(int id)
    {
        var x = await db.Equipments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (x == null) return NotFound();
        return Ok(Mapper.Equipment(x));
    }

    /// <summary>Historique : le contenu dépend du rôle (un utilisateur simple ne voit que ses propres déclarations, sans coûts).</summary>
    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<EquipmentHistoryDto>> History(int id)
    {
        if (!await db.Equipments.AnyAsync(x => x.Id == id)) return NotFound();
        var uid = User.Id();
        var manager = User.IsManager();
        var bq = db.Breakdowns.AsNoTracking().Include(x => x.Equipment).Include(x => x.ReportedBy).Include(x => x.AssignedTechnician).Where(x => x.EquipmentId == id);
        var iq = db.Interventions.AsNoTracking().Include(x => x.Equipment).Include(x => x.Breakdown).Include(x => x.AssignedTechnician).Where(x => x.EquipmentId == id);
        if (!manager)
        {
            if (User.IsInRole(AppRoles.Technician)) { bq = bq.Where(x => x.AssignedTechnicianId == uid || x.ReportedByUserId == uid); iq = iq.Where(x => x.AssignedTechnicianId == uid); }
            else { bq = bq.Where(x => x.ReportedByUserId == uid); iq = iq.Where(x => x.Breakdown!.ReportedByUserId == uid); }
        }
        var b = await bq.OrderByDescending(x => x.ReportedUtc).Take(200).ToListAsync();
        var i = await iq.OrderByDescending(x => x.CreatedUtc).Take(200).ToListAsync();
        return Ok(new EquipmentHistoryDto(b.Select(Mapper.Breakdown).ToList(), i.Select(x => Mapper.Intervention(x, manager)).ToList()));
    }

    [HttpPost, Authorize(Policy = "ManageEquipment")]
    public async Task<IActionResult> Create(CreateEquipmentRequest r)
    {
        var code = r.Code.Trim().ToUpperInvariant();
        if (await db.Equipments.AnyAsync(x => x.Code == code)) return Conflict(new { message = "Ce code équipement existe déjà." });
        var x = new Equipment { Code = code, Name = r.Name.Trim(), Location = r.Location.Trim(), Category = r.Category.Trim(), Manufacturer = Clean(r.Manufacturer), Model = Clean(r.Model), SerialNumber = Clean(r.SerialNumber), CommissionedUtc = r.CommissionedUtc, Notes = Clean(r.Notes) };
        db.Equipments.Add(x);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Ce code équipement existe déjà." }); }
        await audit.LogAsync("EquipmentCreated", "Equipment", x.Id);
        return CreatedAtAction(nameof(Get), new { id = x.Id }, Mapper.Equipment(x));
    }

    [HttpPut("{id:int}"), Authorize(Policy = "ManageEquipment")]
    public async Task<IActionResult> Update(int id, UpdateEquipmentRequest r)
    {
        if (!Enum.IsDefined(r.Status)) return BadRequest(new { message = "Statut invalide." });
        var x = await db.Equipments.FindAsync(id);
        if (x == null) return NotFound();
        if (r.Status == EquipmentStatus.Retired && await db.Breakdowns.AnyAsync(b => b.EquipmentId == id && b.Status != BreakdownStatus.Resolved && b.Status != BreakdownStatus.Closed && b.Status != BreakdownStatus.Rejected))
            return Conflict(new { message = "Impossible d'archiver : des pannes sont encore ouvertes sur cet équipement." });
        x.Name = r.Name.Trim(); x.Location = r.Location.Trim(); x.Category = r.Category.Trim();
        x.Manufacturer = Clean(r.Manufacturer); x.Model = Clean(r.Model); x.SerialNumber = Clean(r.SerialNumber); x.CommissionedUtc = r.CommissionedUtc; x.Notes = Clean(r.Notes);
        x.Status = r.Status; x.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("EquipmentUpdated", "Equipment", id);
        return Ok(Mapper.Equipment(x));
    }

    [HttpDelete("{id:int}"), Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        var x = await db.Equipments.FindAsync(id);
        if (x == null) return NotFound();
        if (await db.Breakdowns.AnyAsync(b => b.EquipmentId == id)) return Conflict(new { message = "Équipement référencé dans l'historique : archive-le (statut Retiré) plutôt que de le supprimer." });
        db.Equipments.Remove(x);
        await db.SaveChangesAsync();
        await audit.LogAsync("EquipmentDeleted", "Equipment", id);
        return NoContent();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
