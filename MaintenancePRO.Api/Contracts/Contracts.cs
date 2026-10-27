using System.ComponentModel.DataAnnotations;
using MaintenancePRO.Api.Models;

namespace MaintenancePRO.Api.Contracts;

// ---------- Requêtes ----------
public sealed record RegisterRequest([Required, EmailAddress, StringLength(254)] string Email, [Required, StringLength(128)] string Password, [Required, StringLength(100)] string DisplayName);
public sealed record LoginRequest([Required, EmailAddress, StringLength(254)] string Email, [Required, StringLength(128)] string Password);
public sealed record RefreshRequest([Required, StringLength(512)] string RefreshToken);
public sealed record LogoutRequest([StringLength(512)] string? RefreshToken);
public sealed record ForgotPasswordRequest([Required, EmailAddress, StringLength(254)] string Email);
public sealed record ResetPasswordRequest([Required, EmailAddress, StringLength(254)] string Email, [Required] string Token, [Required, StringLength(128)] string NewPassword);

public sealed record CreateEquipmentRequest([Required, StringLength(40)] string Code, [Required, StringLength(160)] string Name, [Required, StringLength(160)] string Location, [Required, StringLength(80)] string Category,
    [StringLength(100)] string? Manufacturer, [StringLength(100)] string? Model, [StringLength(120)] string? SerialNumber, DateTime? CommissionedUtc, [StringLength(1000)] string? Notes);
public sealed record UpdateEquipmentRequest([Required, StringLength(160)] string Name, [Required, StringLength(160)] string Location, [Required, StringLength(80)] string Category,
    [StringLength(100)] string? Manufacturer, [StringLength(100)] string? Model, [StringLength(120)] string? SerialNumber, DateTime? CommissionedUtc, [StringLength(1000)] string? Notes, EquipmentStatus Status);

public sealed record CreateBreakdownRequest([Range(1, int.MaxValue)] int EquipmentId, [Required, StringLength(160)] string Title, [Required, StringLength(3000)] string Description, BreakdownSeverity Severity);
public sealed record SetBreakdownStatusRequest(BreakdownStatus Status);
public sealed record AssignBreakdownRequest([Required] string TechnicianId);

public sealed record CreateDiagnosisRequest([Required, StringLength(3000)] string Findings, [StringLength(3000)] string? RootCause, [StringLength(3000)] string? RecommendedAction);
public sealed record DiagnosisDecisionRequest(DiagnosisStatus Status);

public sealed record CreateInterventionRequest([Range(1, int.MaxValue)] int BreakdownId, [Required, StringLength(160)] string Title, [Required, StringLength(3000)] string WorkDescription, string? AssignedTechnicianId);
public sealed record UpdateInterventionRequest(InterventionStatus? Status, [StringLength(160)] string? Title, [StringLength(3000)] string? WorkDescription, [StringLength(3000)] string? Resolution, string? AssignedTechnicianId, decimal? LaborHours, decimal? Cost);

public sealed record SetAccountStatusRequest(AccountStatus Status);
public sealed record SetRoleRequest([Required] string Role);
public sealed record CreateUserRequest([Required, EmailAddress, StringLength(254)] string Email, [Required, StringLength(100)] string DisplayName, [Required, StringLength(128)] string Password, [Required] string Role);
public sealed record AdminResetPasswordRequest([Required, StringLength(128)] string NewPassword);

// ---------- Réponses (DTO : jamais d'entités EF dans le JSON) ----------
public sealed record AuthResponse(string AccessToken, DateTime ExpiresUtc, string RefreshToken, DateTime RefreshExpiresUtc, string UserId, string Email, string DisplayName, string[] Roles);
public sealed record MeResponse(string Id, string Email, string DisplayName, AccountStatus Status, string[] Roles);
public sealed record UserDto(string Id, string Email, string DisplayName, AccountStatus Status, DateTime CreatedUtc, string[] Roles);
public sealed record TechnicianDto(string Id, string DisplayName, string Email);

public sealed record EquipmentRef(int Id, string Code, string Name);
public sealed record EquipmentDto(int Id, string Code, string Name, string Location, string Category, string? Manufacturer, string? Model, string? SerialNumber, DateTime? CommissionedUtc, string? Notes, EquipmentStatus Status, DateTime CreatedUtc, DateTime UpdatedUtc);
public sealed record BreakdownDto(int Id, EquipmentRef Equipment, string Title, string Description, BreakdownSeverity Severity, BreakdownStatus Status, string ReportedBy, string? AssignedTechnicianId, string? AssignedTechnician, DateTime ReportedUtc, DateTime? ResolvedUtc);
public sealed record DiagnosisDto(int Id, int BreakdownId, string Findings, string? RootCause, string? RecommendedAction, DiagnosisStatus Status, string CreatedBy, DateTime CreatedUtc, DateTime? SubmittedUtc, DateTime? DecidedUtc);
public sealed record InterventionDto(int Id, int BreakdownId, EquipmentRef Equipment, string BreakdownTitle, string Title, string WorkDescription, string? Resolution, InterventionStatus Status, string? AssignedTechnicianId, string? AssignedTechnician, DateTime CreatedUtc, DateTime? StartedUtc, DateTime? CompletedUtc, decimal? LaborHours, decimal? Cost, int? DurationMinutes);
public sealed record PhotoDto(int Id, int? BreakdownId, int? InterventionId, string FileName, string? Caption, string ContentType, long SizeBytes, DateTime CreatedUtc);
public sealed record BreakdownDetailDto(BreakdownDto Breakdown, List<DiagnosisDto> Diagnoses, List<InterventionDto> Interventions, List<PhotoDto> Photos);
public sealed record EquipmentHistoryDto(List<BreakdownDto> Breakdowns, List<InterventionDto> Interventions);
public sealed record PagedResult<T>(int Page, int PageSize, int Total, List<T> Items);

public sealed record DashboardDto(DateTime GeneratedUtc, string DisplayName, string Role, int EquipmentCount, int BrokenEquipment, int OpenBreakdowns, int CriticalBreakdowns,
    int OpenInterventions, int CompletedLast30Days, int PendingApprovals, int PendingAccounts, List<BreakdownDto> RecentBreakdowns, List<InterventionDto> RecentInterventions);
public sealed record CountBySeverity(BreakdownSeverity Severity, int Count);
public sealed record CountByStatus(InterventionStatus Status, int Count);
public sealed record ReportDto(DateTime FromUtc, DateTime ToUtc, int TotalBreakdowns, int ResolvedBreakdowns, int TotalInterventions, int CompletedInterventions, decimal LaborHours, decimal TotalCost,
    double AvailabilityPercent, double? MttrHours, List<CountBySeverity> BreakdownsBySeverity, List<CountByStatus> InterventionsByStatus);
