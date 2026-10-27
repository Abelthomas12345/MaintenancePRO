using System.ComponentModel;
using MaintenancePRO.Models;

namespace MaintenancePRO.Services;

/// <summary>État de la session : utilisateur connecté et droits d'affichage (le contrôle réel reste côté API).</summary>
public sealed class SessionService : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    public bool IsLoggedIn { get; private set; }
    public string UserId { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string[] Roles { get; private set; } = Array.Empty<string>();

    public string Role => Roles.FirstOrDefault() ?? "";
    public string RoleLabel => Labels.Role(Role);
    public bool IsAdmin => Role == "Admin";
    public bool IsSupervisor => Role == "Supervisor";
    public bool IsManager => IsAdmin || IsSupervisor;
    public bool IsTechnician => Role == "Technician";
    public bool IsSimpleUser => Role == "User";
    public bool CanSeeDiagnosis => IsManager || IsTechnician;
    public bool CanSeeIntervention => IsManager || IsTechnician;
    public bool CanSeeReports => IsManager;
    public bool CanSeeTechnicians => IsAdmin;
    public bool CanManageEquipment => IsManager;

    public void SignIn(string userId, string displayName, string email, string[] roles)
    {
        UserId = userId; DisplayName = displayName; Email = email; Roles = roles; IsLoggedIn = true;
        Refresh();
    }

    public void Clear()
    {
        UserId = ""; DisplayName = ""; Email = ""; Roles = Array.Empty<string>(); IsLoggedIn = false;
        Refresh();
    }
}

/// <summary>Données passées d'un écran à l'autre (équipement/panne/intervention sélectionnés). Chaque valeur est consommée une seule fois.</summary>
public sealed class NavState
{
    public int? EquipmentId { get; set; }
    public int? BreakdownId { get; set; }
    public int? InterventionId { get; set; }
    public bool CreateEquipment { get; set; }

    public int? TakeEquipmentId() { var v = EquipmentId; EquipmentId = null; return v; }
    public int? TakeBreakdownId() { var v = BreakdownId; BreakdownId = null; return v; }
    public int? TakeInterventionId() { var v = InterventionId; InterventionId = null; return v; }
    public bool TakeCreateEquipment() { var v = CreateEquipment; CreateEquipment = false; return v; }
}

public static class Routes
{
    public const string Login = "//login", Home = "//home/dashboard", Equipments = "//equipments/equipment-list", Breakdown = "//breakdown/breakdown-form",
        Diagnosis = "//diagnosis/diagnosis-form", Intervention = "//intervention/intervention-form", History = "//history/machine-history",
        Reports = "//reports/reports-home", Technicians = "//technicians/technicians-home", Settings = "//settings/settings-home",
        Register = "register", EquipmentDetail = "equipment-detail", InterventionDetail = "intervention-detail";
}

public static class Dialogs
{
    public static Task Alert(string title, string message) => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlert(title, message, "OK"));
    public static Task<bool> Confirm(string title, string message, string accept = "Confirmer") => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlert(title, message, accept, "Annuler"));
    public static Task<string?> Choose(string title, params string[] options) => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayActionSheet(title, "Annuler", null, options));
    public static Task<string?> Prompt(string title, string message, string placeholder = "") => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayPromptAsync(title, message, "OK", "Annuler", placeholder));
}
