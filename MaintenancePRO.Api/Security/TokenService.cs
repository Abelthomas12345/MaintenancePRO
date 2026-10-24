using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MaintenancePRO.Api.Contracts;
using MaintenancePRO.Api.Data;
using MaintenancePRO.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MaintenancePRO.Api.Security;

public sealed class TokenService(IConfiguration c, UserManager<ApplicationUser> users, AppDbContext db)
{
    public async Task<AuthResponse> IssueAsync(ApplicationUser u, string? ip)
    {
        var (access, expires) = await CreateAccessAsync(u);
        var (refresh, refreshExpires) = await CreateRefreshAsync(u, ip);
        var roles = (await users.GetRolesAsync(u)).ToArray();
        await db.RefreshTokens.Where(x => x.ExpiresUtc < DateTime.UtcNow.AddDays(-30)).ExecuteDeleteAsync();
        return new AuthResponse(access, expires, refresh, refreshExpires, u.Id, u.Email ?? "", u.DisplayName, roles);
    }

    private async Task<(string Token, DateTime Expires)> CreateAccessAsync(ApplicationUser u)
    {
        var mins = c.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 15;
        var claims = new List<Claim>
        {
            new("sub", u.Id),
            new("email", u.Email ?? ""),
            new("name", string.IsNullOrWhiteSpace(u.DisplayName) ? (u.Email ?? "") : u.DisplayName),
            new("jti", Guid.NewGuid().ToString("N")),
            new("sstamp", u.SecurityStamp ?? "")   // vérifié à chaque requête : permet de révoquer immédiatement
        };
        foreach (var role in await users.GetRolesAsync(u)) claims.Add(new Claim("role", role));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(c["Jwt:Key"]!));
        var expires = DateTime.UtcNow.AddMinutes(mins);
        var token = new JwtSecurityToken(c["Jwt:Issuer"], c["Jwt:Audience"], claims, DateTime.UtcNow, expires, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private async Task<(string Raw, DateTime Expires)> CreateRefreshAsync(ApplicationUser u, string? ip)
    {
        var days = c.GetValue<int?>("Jwt:RefreshTokenDays") ?? 14;
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var expires = DateTime.UtcNow.AddDays(days);
        db.RefreshTokens.Add(new RefreshToken { UserId = u.Id, TokenHash = Hash(raw), ExpiresUtc = expires, CreatedByIp = ip });
        await db.SaveChangesAsync();
        return (raw, expires);
    }

    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    /// <summary>Rotation : l'ancien refresh token est révoqué, un nouveau est émis. Réutilisation d'un jeton révoqué = vol probable → tout est révoqué.</summary>
    public async Task<AuthResponse?> RotateAsync(string raw, string? ip)
    {
        var hash = Hash(raw);
        var t = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash);
        if (t == null) return null;
        if (t.RevokedUtc != null) { await RevokeAllAsync(t.UserId); return null; }
        if (t.ExpiresUtc <= DateTime.UtcNow) return null;
        var u = await users.FindByIdAsync(t.UserId);
        if (u == null || u.Status != AccountStatus.Active) return null;

        var (newRaw, newExp) = await CreateRefreshAsync(u, ip);
        t.RevokedUtc = DateTime.UtcNow;
        t.ReplacedByHash = Hash(newRaw);
        await db.SaveChangesAsync();
        var (access, exp) = await CreateAccessAsync(u);
        var roles = (await users.GetRolesAsync(u)).ToArray();
        return new AuthResponse(access, exp, newRaw, newExp, u.Id, u.Email ?? "", u.DisplayName, roles);
    }

    public async Task RevokeAsync(string raw)
    {
        var hash = Hash(raw);
        await db.RefreshTokens.Where(x => x.TokenHash == hash && x.RevokedUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedUtc, DateTime.UtcNow));
    }

    public Task RevokeAllAsync(string userId) =>
        db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedUtc, DateTime.UtcNow));
}
