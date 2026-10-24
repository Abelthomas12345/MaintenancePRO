namespace MaintenancePRO.Models;

/// <summary>Traductions françaises des valeurs techniques de l'API et couleurs associées.</summary>
public static class Labels
{
    public static string EquipmentStatus(string s) => s switch { "Available" => "En service", "ToInspect" => "À vérifier", "Broken" => "En panne", "InMaintenance" => "En maintenance", "Retired" => "Retiré", _ => s };
    public static string BreakdownStatus(string s) => s switch { "Reported" => "Signalée", "Diagnosing" => "En diagnostic", "AwaitingApproval" => "À approuver", "InMaintenance" => "En maintenance", "Resolved" => "Résolue", "Rejected" => "Rejetée", "Closed" => "Clôturée", _ => s };
    public static string Severity(string s) => s switch { "Low" => "Faible", "Medium" => "Moyenne", "High" => "Élevée", "Critical" => "Critique", _ => s };
    public static string InterventionStatus(string s) => s switch { "Planned" => "Planifiée", "Assigned" => "Attribuée", "InProgress" => "En cours", "Paused" => "En pause", "Completed" => "Terminée", "Cancelled" => "Annulée", "Rejected" => "Rejetée", _ => s };
    public static string DiagnosisStatus(string s) => s switch { "Draft" => "Brouillon", "Submitted" => "Soumis", "Approved" => "Approuvé", "Rejected" => "Rejeté", _ => s };
    public static string Role(string s) => s switch { "Admin" => "Administrateur", "Supervisor" => "Superviseur", "Technician" => "Technicien", "User" => "Utilisateur", _ => s };
    public static string AccountStatus(string s) => s switch { "Pending" => "En attente", "Active" => "Actif", "Suspended" => "Suspendu", "Disabled" => "Désactivé", _ => s };

    private static readonly Color Teal = Color.FromArgb("#23C6A5"), Orange = Color.FromArgb("#FF8A00"), Red = Color.FromArgb("#FF626D"), Grey = Color.FromArgb("#9AAABD");
    public static Color EquipmentColor(string s) => s switch { "Available" => Teal, "ToInspect" => Orange, "Broken" => Red, "InMaintenance" => Orange, _ => Grey };
    public static Color BreakdownColor(string s) => s switch { "Resolved" or "Closed" => Teal, "Rejected" => Grey, "Reported" => Red, _ => Orange };
    public static Color SeverityColor(string s) => s switch { "Low" => Teal, "Medium" => Orange, _ => Red };
    public static Color InterventionColor(string s) => s switch { "Completed" => Teal, "Cancelled" or "Rejected" => Grey, _ => Orange };

    public static string Date(DateTime? d) => d.HasValue ? d.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "—";
}
