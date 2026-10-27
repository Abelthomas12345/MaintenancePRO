using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace MaintenancePRO.Api.Models;

public sealed class ApplicationUser : IdentityUser
{
    [StringLength(100)] public string DisplayName { get; set; } = "";
    public AccountStatus Status { get; set; } = AccountStatus.Pending;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public static class AppRoles
{
    public const string Admin = "Admin", Supervisor = "Supervisor", Technician = "Technician", User = "User";
    public static readonly string[] All = { Admin, Supervisor, Technician, User };
}

public enum AccountStatus { Pending, Active, Suspended, Disabled }
// Available = « En service », ToInspect = « À vérifier », Broken = « En panne », InMaintenance = « En maintenance »
public enum EquipmentStatus { Available, ToInspect, Broken, InMaintenance, Retired }
public enum BreakdownSeverity { Low, Medium, High, Critical }
public enum BreakdownStatus { Reported, Diagnosing, AwaitingApproval, InMaintenance, Resolved, Rejected, Closed }
public enum DiagnosisStatus { Draft, Submitted, Approved, Rejected }
public enum InterventionStatus { Planned, Assigned, InProgress, Paused, Completed, Cancelled, Rejected }

public sealed class Equipment
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string Code { get; set; } = "";
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [Required, StringLength(160)] public string Location { get; set; } = "";
    [Required, StringLength(80)] public string Category { get; set; } = "Machine";
    [StringLength(100)] public string? Manufacturer { get; set; }
    [StringLength(100)] public string? Model { get; set; }
    [StringLength(120)] public string? SerialNumber { get; set; }
    public DateTime? CommissionedUtc { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Available;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Breakdown
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [Required, StringLength(3000)] public string Description { get; set; } = "";
    public BreakdownSeverity Severity { get; set; } = BreakdownSeverity.Medium;
    public BreakdownStatus Status { get; set; } = BreakdownStatus.Reported;
    [Required] public string ReportedByUserId { get; set; } = "";
    public ApplicationUser? ReportedBy { get; set; }
    public string? AssignedTechnicianId { get; set; }
    public ApplicationUser? AssignedTechnician { get; set; }
    public DateTime ReportedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedUtc { get; set; }
    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();
    public ICollection<Intervention> Interventions { get; set; } = new List<Intervention>();
    public ICollection<PhotoRecord> Photos { get; set; } = new List<PhotoRecord>();
}

public sealed class Diagnosis
{
    public int Id { get; set; }
    public int BreakdownId { get; set; }
    public Breakdown? Breakdown { get; set; }
    [Required, StringLength(3000)] public string Findings { get; set; } = "";
    [StringLength(3000)] public string? RootCause { get; set; }
    [StringLength(3000)] public string? RecommendedAction { get; set; }
    public DiagnosisStatus Status { get; set; } = DiagnosisStatus.Draft;
    [Required] public string CreatedByUserId { get; set; } = "";
    public ApplicationUser? CreatedBy { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedUtc { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public string? DecidedByUserId { get; set; }
}

public sealed class Intervention
{
    public int Id { get; set; }
    public int BreakdownId { get; set; }
    public Breakdown? Breakdown { get; set; }
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [Required, StringLength(3000)] public string WorkDescription { get; set; } = "";
    [StringLength(3000)] public string? Resolution { get; set; }
    public InterventionStatus Status { get; set; } = InterventionStatus.Planned;
    public string CreatedByUserId { get; set; } = "";
    public string? AssignedTechnicianId { get; set; }
    public ApplicationUser? AssignedTechnician { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public decimal? LaborHours { get; set; }
    public decimal? Cost { get; set; }
}

public sealed class PhotoRecord
{
    public int Id { get; set; }
    public int? BreakdownId { get; set; }
    public Breakdown? Breakdown { get; set; }
    public int? InterventionId { get; set; }
    public Intervention? Intervention { get; set; }
    [Required, StringLength(260)] public string FileName { get; set; } = "";
    [Required, StringLength(500)] public string RelativePath { get; set; } = "";
    [StringLength(200)] public string? Caption { get; set; }
    [Required, StringLength(100)] public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string UploadedByUserId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RefreshToken
{
    public long Id { get; set; }
    [Required] public string UserId { get; set; } = "";
    [Required, StringLength(128)] public string TokenHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    [StringLength(128)] public string? ReplacedByHash { get; set; }
    [StringLength(64)] public string? CreatedByIp { get; set; }
}

public sealed class AuditEvent
{
    public long Id { get; set; }
    public string? ActorUserId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = "";
    [StringLength(100)] public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    [StringLength(64)] public string? IpAddress { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
