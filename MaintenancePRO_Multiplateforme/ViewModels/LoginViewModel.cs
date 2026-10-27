using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class LoginViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    public LoginViewModel(ApiClient api)
    {
        _api = api;
        SignInCommand = Cmd(SignInAsync);
        RegisterCommand = Cmd(() => Shell.Current.GoToAsync(Routes.Register));
        ForgotCommand = new Command(() => { ForgotSent = false; ForgotEmail = Email; IsForgotOpen = true; });
        CloseForgotCommand = new Command(() => IsForgotOpen = false);
        SendLinkCommand = Cmd(SendLinkAsync);
    }

    private string _email = "", _password = "", _forgotEmail = "";
    public string Email { get => _email; set => Set(ref _email, value); }
    public string Password { get => _password; set => Set(ref _password, value); }
    public string ForgotEmail { get => _forgotEmail; set => Set(ref _forgotEmail, value); }

    private bool _isForgotOpen, _forgotSent;
    public bool IsForgotOpen { get => _isForgotOpen; set => Set(ref _isForgotOpen, value); }
    public bool ForgotSent { get => _forgotSent; set { if (Set(ref _forgotSent, value)) OnPropertyChanged(nameof(IsForgotForm)); } }
    public bool IsForgotForm => !_forgotSent;

    public Command SignInCommand { get; }
    public Command RegisterCommand { get; }
    public Command ForgotCommand { get; }
    public Command CloseForgotCommand { get; }
    public Command SendLinkCommand { get; }

    /// <summary>Au lancement : si un refresh token valide existe, on ouvre directement l'accueil.</summary>
    public async Task TryRestoreAsync()
    {
        if (IsBusy || _api == null) return;
        IsBusy = true;
        try { if (await _api.TryRestoreSessionAsync()) await Shell.Current.GoToAsync(Routes.Home); }
        finally { IsBusy = false; }
    }

    private async Task SignInAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password)) { await Dialogs.Alert("Champs requis", "Saisis ton adresse e-mail et ton mot de passe."); return; }
        var ok = await RunAsync(async () => await _api.LoginAsync(Email.Trim(), Password), "Connexion impossible");
        if (!ok) return;
        Password = "";
        await Shell.Current.GoToAsync(Routes.Home);
    }

    private async Task SendLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(ForgotEmail) || !ForgotEmail.Contains('@')) { await Dialogs.Alert("E-mail invalide", "Saisis une adresse e-mail valide."); return; }
        var ok = await RunAsync(async () => await _api.ForgotPasswordAsync(ForgotEmail.Trim()));
        if (ok) ForgotSent = true;   // 01b : confirmation (réponse volontairement identique que le compte existe ou non)
    }
}
