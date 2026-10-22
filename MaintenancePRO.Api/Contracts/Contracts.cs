using System.ComponentModel.DataAnnotations; using MaintenancePRO.Api.Models;
namespace MaintenancePRO.Api.Contracts;
public sealed record RegisterRequest([Required,EmailAddress,StringLength(254)]string Email,[Required,MinLength(12),StringLength(128)]string Password,[Required,StringLength(100)]string DisplayName);
public sealed record LoginRequest([Required,EmailAddress,StringLength(254)]string Email,[Required,StringLength(128)]string Password);
public sealed record AuthResponse(string AccessToken,DateTime ExpiresUtc,string Email,string DisplayName,string[] Roles);
public sealed record CreateInterventionRequest([Required,StringLength(160)]string Title,[Required,StringLength(2000)]string Description,[Required,StringLength(40)]string EquipmentCode);
public sealed record UpdateInterventionRequest(InterventionStatus Status,string? Resolution,string? AssignedTechnicianId);
public sealed record SetAccountStatusRequest(AccountStatus Status); public sealed record SetRoleRequest(string Role); public sealed record SetEquipmentStatusRequest(EquipmentStatus Status);
