using MaintenancePRO.Api.Contracts;
using MaintenancePRO.Api.Models;
using MaintenancePRO.Api.Security;
using MaintenancePRO.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MaintenancePRO.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, TokenService tokens, AuditService audit, ILogger<AuthController> log, IHostEnvironment env) : ControllerBase
{
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private const string GenericRegister = "Si l'adresse est valide, le compte a été créé. Un administrateur doit l'activer avant la première connexion.";

    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest r)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        var u = new ApplicationUser { UserName = email, Email = email, DisplayName = r.DisplayName.Trim(), Status = AccountStatus.Pending };

        // Politique de mot de passe vérifiée AVANT de regarder si l'e-mail existe (pas d'énumération de comptes).
        var pwErrors = new List<string>();
        foreach (var v in users.PasswordValidators)
        {
            var vr = await v.ValidateAsync(users, u, r.Password);
            if (!vr.Succeeded) pwErrors.AddRange(vr.Errors.Select(e => e.Code));
        }
        if (pwErrors.Count > 0) return BadRequest(new { message = "Mot de passe non conforme.", errors = pwErrors });

        var result = await users.CreateAsync(u, r.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                await audit.LogAsync("RegisterDuplicate", "User", null, null);
                return StatusCode(201, new { message = GenericRegister });
            }
            return BadRequest(new { message = "Création impossible.", errors = result.Errors.Select(e => e.Code) });
        }
        await users.AddToRoleAsync(u, AppRoles.User);
        await audit.LogAsync("AccountRegistered", "User", u.Id, u.Id);
        return StatusCode(201, new { message = GenericRegister });
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest r)
    {
        var email = r.Email.Trim();
        var u = await users.FindByEmailAsync(email);
        if (u is null)
        {
            await audit.LogAsync("LoginFailed", "User", email.ToLowerInvariant());
            return Unauthorized(new { message = "Identifiants invalides." });
        }
        var result = await signIn.CheckPasswordSignInAsync(u, r.Password, true);
        if (result.IsLockedOut)
        {
            await audit.LogAsync("LoginLockedOut", "User", u.Id, u.Id);
            return Unauthorized(new { message = "Compte temporairement verrouillé après trop d'échecs. Réessaie dans 15 minutes." });
        }
        if (!result.Succeeded)
        {
            await audit.LogAsync("LoginFailed", "User", u.Id, u.Id);
            return Unauthorized(new { message = "Identifiants invalides." });
        }
        // Mot de passe correct mais compte non actif : message explicite (403), sans fuite d'information pour un tiers.
        if (u.Status != AccountStatus.Active)
        {
            await audit.LogAsync("LoginBlockedInactive", "User", u.Id, u.Id);
            var msg = u.Status == AccountStatus.Pending ? "Ton compte est en attente d'activation par un administrateur." : "Ton compte est suspendu ou désactivé. Contacte un administrateur.";
            return StatusCode(403, new { message = msg, code = "AccountNotActive" });
        }
        var response = await tokens.IssueAsync(u, Ip);
        await audit.LogAsync("LoginSucceeded", "User", u.Id, u.Id);
        return Ok(response);
    }

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest r)
    {
        var res = await tokens.RotateAsync(r.RefreshToken, Ip);
        if (res == null) return Unauthorized(new { message = "Session expirée. Reconnecte-toi." });
        return Ok(res);
    }

    /// <summary>Déconnexion réelle : le refresh token est révoqué côté serveur.</summary>
    [HttpPost("logout"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Logout(LogoutRequest r)
    {
        if (!string.IsNullOrWhiteSpace(r.RefreshToken)) await tokens.RevokeAsync(r.RefreshToken);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var id = User.Id();
        var u = id == null ? null : await users.FindByIdAsync(id);
        if (u == null) return NotFound();
        return Ok(new MeResponse(u.Id, u.Email ?? "", u.DisplayName, u.Status, (await users.GetRolesAsync(u)).ToArray()));
    }

    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest r)
    {
        var u = await users.FindByEmailAsync(r.Email.Trim());
        if (u != null && u.Status == AccountStatus.Active)
        {
            var token = await users.GeneratePasswordResetTokenAsync(u);
            // Aucun service e-mail n'est branché : en développement le jeton est écrit dans la console de l'API.
            // En production, envoyer le lien par e-mail (SMTP/SendGrid...) ou laisser l'admin réinitialiser (POST /api/admin/users/{id}/reset-password).
            if (env.IsDevelopment()) log.LogWarning("[DEV] Jeton de réinitialisation pour {Email} : {Token}", u.Email, token);
            await audit.LogAsync("PasswordResetRequested", "User", u.Id, u.Id);
        }
        return Accepted(new { message = "Si un compte existe pour cette adresse, un lien de réinitialisation a été envoyé." });
    }

    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Reset(ResetPasswordRequest r)
    {
        var u = await users.FindByEmailAsync(r.Email.Trim());
        if (u == null || u.Status != AccountStatus.Active) return BadRequest(new { message = "Jeton invalide ou expiré." });
        var res = await users.ResetPasswordAsync(u, r.Token, r.NewPassword);
        if (!res.Succeeded) return BadRequest(new { message = "Jeton invalide ou mot de passe non conforme.", errors = res.Errors.Select(e => e.Code) });
        await users.UpdateSecurityStampAsync(u);
        await tokens.RevokeAllAsync(u.Id);
        await audit.LogAsync("PasswordReset", "User", u.Id, u.Id);
        return Ok(new { message = "Mot de passe modifié." });
    }
}
