using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class RegisterViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    public RegisterViewModel(ApiClient api)
    {
        _api = api;
        RegisterCommand = Cmd(RegisterAsync);
        BackCommand = Cmd(() => Shell.Current.GoToAsync(".."));
    }

    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Confirm { get; set; } = "";
    public Command RegisterCommand { get; }
    public Command BackCommand { get; }

    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Email) || !Email.Contains('@')) { await Dialogs.Alert("Champs requis", "Renseigne ton nom et une adresse e-mail valide."); return; }
        if (Password != Confirm) { await Dialogs.Alert("Mot de passe", "Les deux mots de passe ne correspondent pas."); return; }
        string? message = null;
        var ok = await RunAsync(async () => message = (await _api.RegisterAsync(Email.Trim(), Password, DisplayName.Trim())).Message, "Inscription impossible");
        if (!ok) return;
        await Dialogs.Alert("Compte créé", message ?? "Ton compte doit être activé par un administrateur avant ta première connexion.");
        await Shell.Current.GoToAsync("..");
    }
}
