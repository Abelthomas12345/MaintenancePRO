using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class SettingsViewModel : BaseViewModel
{
    private readonly ApiClient _api; private readonly SessionService _session; private readonly ApiSettings _settings;
    public SettingsViewModel(ApiClient api, SessionService session, ApiSettings settings)
    {
        _api = api; _session = session; _settings = settings;
        SaveUrlCommand = Cmd(SaveUrlAsync);
        TestCommand = Cmd(TestAsync);
        ResetUrlCommand = Cmd(async () => { _settings.Reset(); ApiUrl = _settings.BaseUrl; await Dialogs.Alert("API", "Adresse par défaut rétablie."); });
        SecurityCommand = Cmd(() => Dialogs.Alert("Sécurité", "Ne stocke jamais de mot de passe dans le code ou Git. Les rôles et permissions sont contrôlés par l'API ; cacher un bouton n'est pas une protection."));
        LogoutCommand = Cmd(LogoutAsync);
        _apiUrl = _settings.BaseUrl;
    }

    public string Version => "MaintenancePRO 3.0 — connecté à l'API";
    public string UserName => _session.DisplayName; public string Email => _session.Email; public string RoleLabel => _session.RoleLabel;
    private string _apiUrl; private string _status = "Connexion non testée";
    public string ApiUrl { get => _apiUrl; set => Set(ref _apiUrl, value); }
    public string Status { get => _status; set => Set(ref _status, value); }

    public Command SaveUrlCommand { get; } public Command TestCommand { get; } public Command ResetUrlCommand { get; } public Command SecurityCommand { get; } public Command LogoutCommand { get; }

    public void OnAppearing() { _apiUrl = _settings.BaseUrl; OnAll(); }

    private async Task SaveUrlAsync()
    {
        if (!ApiSettings.TryValidate(ApiUrl, out var url, out var error)) { await Dialogs.Alert("Adresse invalide", error); return; }
        _settings.Save(url); ApiUrl = url;
        await Dialogs.Alert("API", "Adresse enregistrée.");
    }

    private async Task TestAsync()
    {
        IsBusy = true;
        try { Status = await _api.PingAsync() ? "✔ API joignable" : "✖ API injoignable (vérifie l'adresse et que le backend tourne)"; }
        finally { IsBusy = false; }
    }

    private async Task LogoutAsync()
    {
        if (!await Dialogs.Confirm("Déconnexion", "Te déconnecter de MaintenancePRO ?", "Se déconnecter")) return;
        await _api.LogoutAsync();
        await Shell.Current.GoToAsync(Routes.Login);
    }
}
