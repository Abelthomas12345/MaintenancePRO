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

[ApiController, Route("api/admin"), Authorize(Policy = "AdminOnly")]
public sealed class AdminController(UserManager<ApplicationUser> users, AppDbContext db, TokenService tokens, AuditService audit) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<UserDto>>> UsersList([FromQuery] AccountStatus? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = users.Users.AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.Email!.Contains(s) || x.DisplayName.Contains(s)); }
        q = q.OrderBy(x => x.Email);
        var total = await q.CountAsync();
        var list = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = new List<UserDto>();
        foreach (var u in list) items.Add(await ToDto(u));
        return Ok(new PagedResult<UserDto>(page, pageSize, total, items));
    }

    /// <summary>Création d'un compte (ex. technicien) directement actif, par l'administrateur.</summary>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest r)
    {
        if (!AppRoles.All.Contains(r.Role)) return BadRequest(new { message = "Rôle invalide." });
        var email = r.Email.Trim().ToLowerInvariant();
        var u = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = r.DisplayName.Trim(), Status = AccountStatus.Active };
        var res = await users.CreateAsync(u, r.Password);
        if (!res.Succeeded) return BadRequest(new { message = "Création impossible.", errors = res.Errors.Select(e => e.Code) });
        await users.AddToRoleAsync(u, r.Role);
        await audit.LogAsync("UserCreatedByAdmin", "User", u.Id);
        return StatusCode(201, await ToDto(u));
    }

    [HttpPatch("users/{id}/status")]
    public async Task<IActionResult> SetStatus(string id, SetAccountStatusRequest r)
    {
        if (!Enum.IsDefined(r.Status)) return BadRequest(new { message = "Statut invalide." });
        var u = await users.FindByIdAsync(id);
        if (u == null) return NotFound();
        if (u.Id == User.Id()) return BadRequest(new { message = "Impossible de modifier son propre statut." });
        u.Status = r.Status;
        var res = await users.UpdateAsync(u);
        if (!res.Succeeded) return BadRequest(new { errors = res.Errors.Select(e => e.Code) });
        await users.UpdateSecurityStampAsync(u);              // invalide immédiatement les jetons d'accès existants
        if (r.Status != AccountStatus.Active) await tokens.RevokeAllAsync(u.Id);
        await audit.LogAsync("AccountStatusChanged", "User", id);
        return Ok(await ToDto(u));
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> SetRole(string id, SetRoleRequest r)
    {
        if (!AppRoles.All.Contains(r.Role)) return BadRequest(new { message = "Rôle invalide." });
        var u = await users.FindByIdAsync(id);
        if (u == null) return NotFound();
        if (u.Id == User.Id()) return BadRequest(new { message = "Impossible de modifier son propre rôle." });
        var current = await users.GetRolesAsync(u);
        var rem = await users.RemoveFromRolesAsync(u, current);
        if (!rem.Succeeded) return BadRequest(new { errors = rem.Errors.Select(e => e.Code) });
        var add = await users.AddToRoleAsync(u, r.Role);
        if (!add.Succeeded) return BadRequest(new { errors = add.Errors.Select(e => e.Code) });
        await users.UpdateSecurityStampAsync(u);              // l'ancien jeton (ancien rôle) est refusé, le refresh donnera le nouveau rôle
        await audit.LogAsync("UserRoleChanged", "User", id);
        return Ok(await ToDto(u));
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, AdminResetPasswordRequest r)
    {
        var u = await users.FindByIdAsync(id);
        if (u == null) return NotFound();
        var token = await users.GeneratePasswordResetTokenAsync(u);
        var res = await users.ResetPasswordAsync(u, token, r.NewPassword);
        if (!res.Succeeded) return BadRequest(new { message = "Mot de passe non conforme.", errors = res.Errors.Select(e => e.Code) });
        await users.UpdateSecurityStampAsync(u);
        await tokens.RevokeAllAsync(u.Id);
        await audit.LogAsync("PasswordResetByAdmin", "User", id);
        return NoContent();
    }

    [HttpGet("audit")]
    public async Task<IActionResult> AuditList([FromQuery] int take = 100) =>
        Ok(await db.AuditEvents.AsNoTracking().OrderByDescending(x => x.CreatedUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync());

    private async Task<UserDto> ToDto(ApplicationUser u) => new(u.Id, u.Email ?? "", u.DisplayName, u.Status, u.CreatedUtc, (await users.GetRolesAsync(u)).ToArray());
}
