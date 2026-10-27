using MaintenancePRO.Api.Contracts;
using MaintenancePRO.Api.Data;
using MaintenancePRO.Api.Models;
using MaintenancePRO.Api.Security;
using MaintenancePRO.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePRO.Api.Controllers;

[ApiController, Route("api/photos"), Authorize]
public sealed class PhotosController(AppDbContext db, IWebHostEnvironment env, IConfiguration config, AuditService audit) : ControllerBase
{
    private const int MaxPhotosPerTarget = 20;
    private string PhotoDir => config["Storage:PhotoDirectory"] ?? "uploads/photos";

    // ---- droits ----
    private bool CanReadBreakdown(Breakdown b) { var uid = User.Id(); return User.IsManager() || b.ReportedByUserId == uid || b.AssignedTechnicianId == uid; }
    private bool CanReadIntervention(Intervention i, Breakdown? b) { var uid = User.Id(); return User.IsManager() || i.AssignedTechnicianId == uid || b?.ReportedByUserId == uid; }
    private bool CanWriteIntervention(Intervention i) => User.IsManager() || i.AssignedTechnicianId == User.Id();

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await db.Photos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        if (!await CanReadPhoto(p)) return Forbid();
        var full = Path.GetFullPath(Path.Combine(env.ContentRootPath, p.RelativePath));
        var root = Path.GetFullPath(Path.Combine(env.ContentRootPath, PhotoDir)) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return BadRequest();
        if (!System.IO.File.Exists(full)) return NotFound();
        return PhysicalFile(full, p.ContentType);
    }

    [HttpGet("breakdown/{breakdownId:int}")]
    public async Task<ActionResult<List<PhotoDto>>> ListBreakdown(int breakdownId)
    {
        var b = await db.Breakdowns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == breakdownId);
        if (b == null) return NotFound();
        if (!CanReadBreakdown(b)) return Forbid();
        return Ok((await db.Photos.AsNoTracking().Where(x => x.BreakdownId == breakdownId).OrderByDescending(x => x.CreatedUtc).ToListAsync()).Select(Mapper.Photo).ToList());
    }

    [HttpGet("intervention/{interventionId:int}")]
    public async Task<ActionResult<List<PhotoDto>>> ListIntervention(int interventionId)
    {
        var i = await db.Interventions.AsNoTracking().Include(x => x.Breakdown).FirstOrDefaultAsync(x => x.Id == interventionId);
        if (i == null) return NotFound();
        if (!CanReadIntervention(i, i.Breakdown)) return Forbid();
        return Ok((await db.Photos.AsNoTracking().Where(x => x.InterventionId == interventionId).OrderByDescending(x => x.CreatedUtc).ToListAsync()).Select(Mapper.Photo).ToList());
    }

    [HttpPost("breakdown/{breakdownId:int}"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadBreakdown(int breakdownId, IFormFile file, [FromForm] string? caption)
    {
        var b = await db.Breakdowns.FirstOrDefaultAsync(x => x.Id == breakdownId);
        if (b == null) return NotFound();
        if (!CanReadBreakdown(b)) return Forbid();
        if (await db.Photos.CountAsync(p => p.BreakdownId == breakdownId) >= MaxPhotosPerTarget) return Conflict(new { message = $"Maximum {MaxPhotosPerTarget} photos par panne." });
        return await Save(file, caption, breakdownId, null);
    }

    [HttpPost("intervention/{interventionId:int}"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadIntervention(int interventionId, IFormFile file, [FromForm] string? caption)
    {
        var i = await db.Interventions.FirstOrDefaultAsync(x => x.Id == interventionId);
        if (i == null) return NotFound();
        if (!CanWriteIntervention(i)) return Forbid();
        if (await db.Photos.CountAsync(p => p.InterventionId == interventionId) >= MaxPhotosPerTarget) return Conflict(new { message = $"Maximum {MaxPhotosPerTarget} photos par intervention." });
        return await Save(file, caption, null, interventionId);
    }

    private async Task<bool> CanReadPhoto(PhotoRecord p)
    {
        if (User.IsManager()) return true;
        if (p.BreakdownId.HasValue)
        {
            var b = await db.Breakdowns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == p.BreakdownId);
            if (b != null) return CanReadBreakdown(b);
        }
        if (p.InterventionId.HasValue)
        {
            var i = await db.Interventions.AsNoTracking().Include(x => x.Breakdown).FirstOrDefaultAsync(x => x.Id == p.InterventionId);
            if (i != null) return CanReadIntervention(i, i.Breakdown);
        }
        return p.UploadedByUserId == User.Id();
    }

    /// <summary>Type réel de l'image déterminé par les premiers octets (signature), jamais par l'extension ou le Content-Type envoyés par le client.</summary>
    private static (string Mime, string Ext)? Detect(byte[] h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return ("image/png", ".png");
        if (h.Length >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ("image/webp", ".webp");
        return null;
    }

    private async Task<IActionResult> Save(IFormFile? file, string? caption, int? breakdownId, int? interventionId)
    {
        var max = config.GetValue<long?>("Storage:MaxPhotoBytes") ?? 5242880;
        if (file == null || file.Length <= 0 || file.Length > max) return BadRequest(new { message = $"Photo obligatoire et limitée à {max / 1024 / 1024} Mo." });

        var header = new byte[12];
        await using (var s = file.OpenReadStream()) await s.ReadAtLeastAsync(header.AsMemory(), 12, false);
        var kind = Detect(header);
        if (kind == null) return BadRequest(new { message = "Fichier refusé : ce n'est pas une image JPG, PNG ou WEBP valide." });

        var dir = Path.Combine(env.ContentRootPath, PhotoDir);
        Directory.CreateDirectory(dir);
        var full = Path.Combine(dir, Guid.NewGuid().ToString("N") + kind.Value.Ext);
        await using (var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous)) await file.CopyToAsync(stream);

        var name = new string(Path.GetFileName(file.FileName).Where(c => !char.IsControl(c)).ToArray());
        if (name.Length > 200) name = name[^200..];
        var p = new PhotoRecord
        {
            BreakdownId = breakdownId, InterventionId = interventionId, FileName = string.IsNullOrWhiteSpace(name) ? "photo" + kind.Value.Ext : name,
            RelativePath = Path.GetRelativePath(env.ContentRootPath, full), Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim()[..Math.Min(200, caption.Trim().Length)],
            ContentType = kind.Value.Mime, SizeBytes = file.Length, UploadedByUserId = User.Id()!
        };
        db.Photos.Add(p);
        try { await db.SaveChangesAsync(); }
        catch { try { System.IO.File.Delete(full); } catch { /* fichier orphelin supprimé au mieux */ } throw; }
        await audit.LogAsync("PhotoUploaded", "Photo", p.Id);
        return CreatedAtAction(nameof(Get), new { id = p.Id }, Mapper.Photo(p));
    }
}
