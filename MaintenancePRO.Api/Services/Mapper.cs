using MaintenancePRO.Api.Contracts;
using MaintenancePRO.Api.Models;

namespace MaintenancePRO.Api.Services;

/// <summary>Conversion entités → DTO (évite les cycles JSON et masque les champs sensibles).</summary>
public static class Mapper
{
    public static EquipmentRef Ref(Equipment e) => new(e.Id, e.Code, e.Name);

    public static EquipmentDto Equipment(Equipment e) => new(e.Id, e.Code, e.Name, e.Location, e.Category, e.Manufacturer, e.Model, e.SerialNumber, e.CommissionedUtc, e.Notes, e.Status, e.CreatedUtc, e.UpdatedUtc);

    public static BreakdownDto Breakdown(Breakdown b) => new(b.Id, Ref(b.Equipment!), b.Title, b.Description, b.Severity, b.Status,
        b.ReportedBy?.DisplayName ?? "—", b.AssignedTechnicianId, b.AssignedTechnician?.DisplayName, b.ReportedUtc, b.ResolvedUtc);

    public static DiagnosisDto Diagnosis(Diagnosis d) => new(d.Id, d.BreakdownId, d.Findings, d.RootCause, d.RecommendedAction, d.Status,
        d.CreatedBy?.DisplayName ?? "—", d.CreatedUtc, d.SubmittedUtc, d.DecidedUtc);

    /// <param name="manager">Seuls Admin/Superviseur voient le coût.</param>
    public static InterventionDto Intervention(Intervention i, bool manager)
    {
        int? minutes = i.StartedUtc.HasValue && i.CompletedUtc.HasValue ? (int)Math.Round((i.CompletedUtc.Value - i.StartedUtc.Value).TotalMinutes) : null;
        return new(i.Id, i.BreakdownId, Ref(i.Equipment!), i.Breakdown?.Title ?? "", i.Title, i.WorkDescription, i.Resolution, i.Status,
            i.AssignedTechnicianId, i.AssignedTechnician?.DisplayName, i.CreatedUtc, i.StartedUtc, i.CompletedUtc, i.LaborHours, manager ? i.Cost : null, minutes);
    }

    public static PhotoDto Photo(PhotoRecord p) => new(p.Id, p.BreakdownId, p.InterventionId, p.FileName, p.Caption, p.ContentType, p.SizeBytes, p.CreatedUtc);

    public static bool IsOpen(BreakdownStatus s) => s is not (BreakdownStatus.Resolved or BreakdownStatus.Closed or BreakdownStatus.Rejected);
}
