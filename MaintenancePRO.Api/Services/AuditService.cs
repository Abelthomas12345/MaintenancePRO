using MaintenancePRO.Api.Data;
using MaintenancePRO.Api.Models;
using MaintenancePRO.Api.Security;

namespace MaintenancePRO.Api.Services;

/// <summary>Journal d'audit. À appeler APRÈS SaveChanges pour que l'identifiant de l'entité soit connu.</summary>
public sealed class AuditService(AppDbContext db, IHttpContextAccessor http)
{
    public async Task LogAsync(string action, string entityType, object? entityId, string? actorId = null)
    {
        var ctx = http.HttpContext;
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = actorId ?? ctx?.User.Id(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();
    }
}
