using MaintenancePRO.Services;
using MaintenancePRO.Views;

namespace MaintenancePRO;

public partial class AppShell : Shell
{
    private readonly SessionService _session; private readonly ApiClient _api;

    public AppShell(SessionService session, ApiClient api)
    {
        InitializeComponent();
        _session = session; _api = api;
        BindingContext = session;   // le menu se filtre selon le rôle (FlyoutItemIsVisible)

        Routing.RegisterRoute(Routes.Register, typeof(RegisterPage));
        Routing.RegisterRoute(Routes.EquipmentDetail, typeof(EquipmentDetailPage));
        Routing.RegisterRoute(Routes.InterventionDetail, typeof(InterventionDetailPage));

        Navigating += OnNavigating;
        session.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => FlyoutBehavior = _session.IsLoggedIn ? FlyoutBehavior.Flyout : FlyoutBehavior.Disabled);
    }

    /// <summary>Garde de navigation : sans session, impossible d'ouvrir une autre page que Connexion/Inscription ; les pages réservées exigent le bon rôle.</summary>
    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        var target = e.Target?.Location.OriginalString ?? "";
        if (string.IsNullOrEmpty(target)) return;
        var isPublic = target.Contains("login") || target.Contains("register") || target.StartsWith("..");

        if (!_session.IsLoggedIn)
        {
            if (isPublic || !e.CanCancel) return;
            e.Cancel();
            MainThread.BeginInvokeOnMainThread(async () => await GoToAsync(Routes.Login));
            return;
        }
        var forbidden = (target.Contains("technicians") && !_session.CanSeeTechnicians) || (target.Contains("reports") && !_session.CanSeeReports)
                     || (target.Contains("diagnosis") && !_session.CanSeeDiagnosis) || (target.Contains("intervention-form") && !_session.CanSeeIntervention);
        if (forbidden && e.CanCancel)
        {
            e.Cancel();
            MainThread.BeginInvokeOnMainThread(async () => await DisplayAlert("Accès refusé", "Cet écran n'est pas disponible pour ton rôle.", "OK"));
        }
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        FlyoutIsPresented = false;
        if (!await DisplayAlert("Déconnexion", "Te déconnecter de MaintenancePRO ?", "Se déconnecter", "Annuler")) return;
        await _api.LogoutAsync();
        await GoToAsync(Routes.Login);
    }
}
