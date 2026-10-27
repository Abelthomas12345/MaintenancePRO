using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class UserRow
{
    public UserDto Dto { get; init; } = new();
    public string RoleLabel => Labels.Role(Dto.Role);
    public string StatusLabel => Labels.AccountStatus(Dto.Status);
    public Color StatusColor => Dto.Status == "Active" ? Color.FromArgb("#23C6A5") : Dto.Status == "Pending" ? Color.FromArgb("#FF8A00") : Color.FromArgb("#FF626D");
    public bool ShowActivate { get; init; }
    public bool ShowSuspend { get; init; }
    public bool CanEdit { get; init; }
}

/// <summary>Gestion des comptes et techniciens : réservée à l'administrateur.</summary>
public sealed class TechniciansViewModel : BaseViewModel
{
    private readonly ApiClient _api; private readonly SessionService _session;
    private List<UserDto> _users = new();
    private static readonly (string Value, string Label)[] RoleChoices = { ("Admin", "Administrateur"), ("Supervisor", "Superviseur"), ("Technician", "Technicien"), ("User", "Utilisateur") };

    public TechniciansViewModel(ApiClient api, SessionService session)
    {
        _api = api; _session = session;
        ActivateCommand = Cmd<UserRow>(r => SetStatusAsync(r, "Active"));
        SuspendCommand = Cmd<UserRow>(r => SetStatusAsync(r, "Suspended"));
        RoleCommand = Cmd<UserRow>(ChangeRoleAsync);
        PasswordCommand = Cmd<UserRow>(ResetPasswordAsync);
        ToggleCreateCommand = new Command(() => IsCreating = !IsCreating);
        CreateCommand = Cmd(CreateAsync);
        RefreshCommand = Cmd(LoadAsync);
        _newRole = NewRoles[2];
    }

    public Option[] NewRoles { get; } = RoleChoices.Select(r => new Option(r.Value, r.Label)).ToArray();
    private Option _newRole;
    public Option NewRole { get => _newRole; set { if (value != null) Set(ref _newRole, value); } }
    private bool _isCreating;
    public bool IsCreating { get => _isCreating; set => Set(ref _isCreating, value); }
    public string NewName { get; set; } = ""; public string NewEmail { get; set; } = ""; public string NewPassword { get; set; } = "";

    public bool IsAdmin => _session.IsAdmin;
    public bool NotAdmin => !_session.IsAdmin;
    public IEnumerable<UserRow> Users => _users.Select(u => new UserRow { Dto = u, ShowActivate = u.Status != "Active" && u.Id != _session.UserId, ShowSuspend = u.Status == "Active" && u.Id != _session.UserId, CanEdit = u.Id != _session.UserId });
    public string Counts => $"{_users.Count} comptes · {_users.Count(u => u.Role == "Technician")} techniciens · {_users.Count(u => u.Status == "Pending")} en attente";

    public Command<UserRow> ActivateCommand { get; } public Command<UserRow> SuspendCommand { get; } public Command<UserRow> RoleCommand { get; } public Command<UserRow> PasswordCommand { get; }
    public Command ToggleCreateCommand { get; } public Command CreateCommand { get; } public Command RefreshCommand { get; }

    public async Task LoadAsync()
    {
        OnAll();
        if (!_session.IsAdmin) return;
        await RunAsync(async () => { _users = (await _api.GetAsync<PagedResult<UserDto>>("api/admin/users?pageSize=100")).Items; }, "Comptes");
        OnAll();
    }

    private async Task SetStatusAsync(UserRow r, string status)
    {
        if (!await Dialogs.Confirm(status == "Active" ? "Activer" : "Suspendre", $"{(status == "Active" ? "Activer" : "Suspendre")} le compte de {r.Dto.DisplayName} ?")) return;
        if (await RunAsync(async () => await _api.PatchAsync<UserDto>($"api/admin/users/{r.Dto.Id}/status", new { status }), "Action impossible")) await LoadAsync();
    }

    private async Task ChangeRoleAsync(UserRow r)
    {
        var label = await Dialogs.Choose($"Rôle de {r.Dto.DisplayName}", RoleChoices.Select(x => x.Label).ToArray());
        var choice = RoleChoices.FirstOrDefault(x => x.Label == label);
        if (choice.Value == null || choice.Value == r.Dto.Role) return;
        if (await RunAsync(async () => await _api.PutAsync<UserDto>($"api/admin/users/{r.Dto.Id}/role", new { role = choice.Value }), "Changement impossible")) await LoadAsync();
    }

    private async Task ResetPasswordAsync(UserRow r)
    {
        var pwd = await Dialogs.Prompt("Nouveau mot de passe", $"Mot de passe temporaire pour {r.Dto.DisplayName} (12 caractères min., majuscule, minuscule, chiffre, symbole).");
        if (string.IsNullOrWhiteSpace(pwd)) return;
        if (await RunAsync(async () => await _api.PostAsync<object>($"api/admin/users/{r.Dto.Id}/reset-password", new { newPassword = pwd }), "Réinitialisation impossible"))
            await Dialogs.Alert("Mot de passe modifié", "Communique le mot de passe temporaire à l'utilisateur ; ses sessions ont été révoquées.");
    }

    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewEmail) || !NewEmail.Contains('@') || string.IsNullOrWhiteSpace(NewPassword)) { await Dialogs.Alert("Champs requis", "Nom, e-mail et mot de passe temporaire sont obligatoires."); return; }
        var ok = await RunAsync(async () => await _api.PostAsync<UserDto>("api/admin/users", new { email = NewEmail.Trim(), displayName = NewName.Trim(), password = NewPassword, role = NewRole.Value }), "Création impossible");
        if (!ok) return;
        NewName = NewEmail = NewPassword = ""; IsCreating = false; OnAll();
        await Dialogs.Alert("Compte créé", "Le compte est actif immédiatement.");
        await LoadAsync();
    }
}
