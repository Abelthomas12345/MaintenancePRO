using MaintenancePRO.Services;
namespace MaintenancePRO.ViewModels;
public sealed class LoginViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    public LoginViewModel(ApiClient api) => _api = api;
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }
    public Command SignInCommand => new(async () =>
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        { await Shell.Current.DisplayAlert("Champs requis", "Saisis ton adresse e-mail et ton mot de passe.", "OK"); return; }
        try
        {
            IsBusy = true;
            await _api.LoginAsync(Email.Trim(), Password);
            await Shell.Current.GoToAsync("//home/dashboard");
        }
        catch (ApiException ex) { await Shell.Current.DisplayAlert("Connexion impossible", ex.Message, "OK"); }
        catch (HttpRequestException) { await Shell.Current.DisplayAlert("API inaccessible", "Vérifie que le backend est lancé et que l'adresse API dans Services/ApiBaseUrl.cs est correcte.", "OK"); }
        catch (TaskCanceledException) { await Shell.Current.DisplayAlert("Délai dépassé", "Le backend ne répond pas. Vérifie le réseau et l'adresse de l'API.", "OK"); }
        catch (Exception) { await Shell.Current.DisplayAlert("Erreur", "Une erreur inattendue est survenue pendant la connexion.", "OK"); }
        finally { IsBusy = false; }
    });
    public Command ForgotCommand => new(async () => await Shell.Current.DisplayAlert("Réinitialisation", "La réinitialisation sécurisée du mot de passe doit être configurée côté serveur.", "OK"));
}
