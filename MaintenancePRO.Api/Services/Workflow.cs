using MaintenancePRO.Api.Data;
using MaintenancePRO.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePRO.Api.Services;

public static class Workflow
{
    /// <summary>Recalcule l'état d'un équipement d'après ses pannes ouvertes. À appeler après SaveChanges des pannes.</summary>
    public static async Task RefreshEquipmentAsync(AppDbContext db, int equipmentId)
    {
        var eq = await db.Equipments.FindAsync(equipmentId);
        if (eq == null || eq.Status == EquipmentStatus.Retired) return;
        var open = await db.Breakdowns.Where(b => b.EquipmentId == equipmentId && b.Status != BreakdownStatus.Resolved && b.Status != BreakdownStatus.Closed && b.Status != BreakdownStatus.Rejected)
            .Select(b => b.Status).ToListAsync();
        EquipmentStatus target;
        if (open.Count == 0) target = EquipmentStatus.Available;
        else if (open.Contains(BreakdownStatus.InMaintenance)) target = EquipmentStatus.InMaintenance;
        else target = eq.Status == EquipmentStatus.InMaintenance || eq.Status == EquipmentStatus.Available ? EquipmentStatus.Broken : eq.Status;
        if (eq.Status != target) { eq.Status = target; eq.UpdatedUtc = DateTime.UtcNow; await db.SaveChangesAsync(); }
    }
}
