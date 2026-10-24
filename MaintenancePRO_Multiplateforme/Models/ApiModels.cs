namespace MaintenancePRO.Models;

public sealed record Option(string Value, string Label);

public sealed class AuthResult
{
    public string AccessToken { get; set; } = ""; public DateTime ExpiresUtc { get; set; }
    public string RefreshToken { get; set; } = ""; public DateTime RefreshExpiresUtc { get; set; }
    public string UserId { get; set; } = ""; public string Email { get; set; } = ""; public string DisplayName { get; set; } = "";
    public string[] Roles { get; set; } = Array.Empty<string>();
}
public sealed class MeDto { public string Id { get; set; } = ""; public string Email { get; set; } = ""; public string DisplayName { get; set; } = ""; public string Status { get; set; } = ""; public string[] Roles { get; set; } = Array.Empty<string>(); }
public sealed class PagedResult<T> { public int Page { get; set; } public int PageSize { get; set; } public int Total { get; set; } public List<T> Items { get; set; } = new(); }
public sealed class TechnicianDto { public string Id { get; set; } = ""; public string DisplayName { get; set; } = ""; public string Email { get; set; } = ""; }
public sealed class UserDto
{
    public string Id { get; set; } = ""; public string Email { get; set; } = ""; public string DisplayName { get; set; } = ""; public string Status { get; set; } = ""; public DateTime CreatedUtc { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string Role => Roles.FirstOrDefault() ?? "";
}

public sealed class EquipmentRef
{
    public int Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = "";
    public string Display => $"{Code} — {Name}";
}
public sealed class EquipmentDto
{
    public int Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string Location { get; set; } = ""; public string Category { get; set; } = "";
    public string? Manufacturer { get; set; } public string? Model { get; set; } public string? SerialNumber { get; set; } public DateTime? CommissionedUtc { get; set; } public string? Notes { get; set; }
    public string Status { get; set; } = ""; public DateTime CreatedUtc { get; set; } public DateTime UpdatedUtc { get; set; }
    public string Display => $"{Code} — {Name}";
    public string StatusLabel => Labels.EquipmentStatus(Status);
    public Color StatusColor => Labels.EquipmentColor(Status);
}
public sealed class BreakdownDto
{
    public int Id { get; set; } public EquipmentRef Equipment { get; set; } = new(); public string Title { get; set; } = ""; public string Description { get; set; } = "";
    public string Severity { get; set; } = ""; public string Status { get; set; } = ""; public string ReportedBy { get; set; } = "";
    public string? AssignedTechnicianId { get; set; } public string? AssignedTechnician { get; set; } public DateTime ReportedUtc { get; set; } public DateTime? ResolvedUtc { get; set; }
    public string StatusLabel => Labels.BreakdownStatus(Status);
    public string SeverityLabel => Labels.Severity(Severity);
    public Color StatusColor => Labels.BreakdownColor(Status);
    public Color SeverityColor => Labels.SeverityColor(Severity);
    public bool IsOpen => Status is not ("Resolved" or "Closed" or "Rejected");
    public string Display => $"#{Id} · {Equipment.Code} · {Title}";
    public string Meta => $"{Equipment.Name} · {SeverityLabel} · {Labels.Date(ReportedUtc)} · par {ReportedBy}";
    public string TechnicianText => string.IsNullOrEmpty(AssignedTechnician) ? "Aucun technicien attribué" : $"Technicien : {AssignedTechnician}";
}
public sealed class DiagnosisDto
{
    public int Id { get; set; } public int BreakdownId { get; set; } public string Findings { get; set; } = ""; public string? RootCause { get; set; } public string? RecommendedAction { get; set; }
    public string Status { get; set; } = ""; public string CreatedBy { get; set; } = ""; public DateTime CreatedUtc { get; set; } public DateTime? SubmittedUtc { get; set; } public DateTime? DecidedUtc { get; set; }
    public string StatusLabel => Labels.DiagnosisStatus(Status);
    public string Meta => $"{CreatedBy} · {Labels.Date(CreatedUtc)}";
}
public sealed class InterventionDto
{
    public int Id { get; set; } public int BreakdownId { get; set; } public EquipmentRef Equipment { get; set; } = new(); public string BreakdownTitle { get; set; } = "";
    public string Title { get; set; } = ""; public string WorkDescription { get; set; } = ""; public string? Resolution { get; set; } public string Status { get; set; } = "";
    public string? AssignedTechnicianId { get; set; } public string? AssignedTechnician { get; set; } public DateTime CreatedUtc { get; set; } public DateTime? StartedUtc { get; set; } public DateTime? CompletedUtc { get; set; }
    public decimal? LaborHours { get; set; } public decimal? Cost { get; set; } public int? DurationMinutes { get; set; }
    public string StatusLabel => Labels.InterventionStatus(Status);
    public Color StatusColor => Labels.InterventionColor(Status);
    public bool IsTerminal => Status is "Completed" or "Cancelled" or "Rejected";
    public string TechnicianText => string.IsNullOrEmpty(AssignedTechnician) ? "Non attribuée" : AssignedTechnician!;
    public string Display => $"#{Id} · {Title} ({StatusLabel})";
    public string Meta => $"{Equipment.Name} · {TechnicianText} · {Labels.Date(CreatedUtc)}";
    public string DurationText => DurationMinutes.HasValue ? $"{DurationMinutes} min" : "—";
}
public sealed class PhotoDto { public int Id { get; set; } public string FileName { get; set; } = ""; public string? Caption { get; set; } public long SizeBytes { get; set; } public DateTime CreatedUtc { get; set; } }
public sealed class BreakdownDetailDto { public BreakdownDto Breakdown { get; set; } = new(); public List<DiagnosisDto> Diagnoses { get; set; } = new(); public List<InterventionDto> Interventions { get; set; } = new(); public List<PhotoDto> Photos { get; set; } = new(); }
public sealed class EquipmentHistoryDto { public List<BreakdownDto> Breakdowns { get; set; } = new(); public List<InterventionDto> Interventions { get; set; } = new(); }

public sealed class DashboardDto
{
    public string DisplayName { get; set; } = ""; public string Role { get; set; } = "";
    public int EquipmentCount { get; set; } public int BrokenEquipment { get; set; } public int OpenBreakdowns { get; set; } public int CriticalBreakdowns { get; set; }
    public int OpenInterventions { get; set; } public int CompletedLast30Days { get; set; } public int PendingApprovals { get; set; } public int PendingAccounts { get; set; }
    public List<BreakdownDto> RecentBreakdowns { get; set; } = new(); public List<InterventionDto> RecentInterventions { get; set; } = new();
}
public sealed class CountBySeverity { public string Severity { get; set; } = ""; public int Count { get; set; } public string Text => $"{Labels.Severity(Severity)} : {Count}"; }
public sealed class CountByStatus { public string Status { get; set; } = ""; public int Count { get; set; } public string Text => $"{Labels.InterventionStatus(Status)} : {Count}"; }
public sealed class ReportDto
{
    public int TotalBreakdowns { get; set; } public int ResolvedBreakdowns { get; set; } public int TotalInterventions { get; set; } public int CompletedInterventions { get; set; }
    public decimal LaborHours { get; set; } public decimal TotalCost { get; set; } public double AvailabilityPercent { get; set; } public double? MttrHours { get; set; }
    public List<CountBySeverity> BreakdownsBySeverity { get; set; } = new(); public List<CountByStatus> InterventionsByStatus { get; set; } = new();
}
