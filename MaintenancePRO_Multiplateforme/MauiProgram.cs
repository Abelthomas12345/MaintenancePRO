using MaintenancePRO.Services;
using MaintenancePRO.ViewModels;
using MaintenancePRO.Views;

namespace MaintenancePRO;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Services partagés (une seule instance pour toute l'application)
        builder.Services.AddSingleton<ApiSettings>();
        builder.Services.AddSingleton<TokenStore>();
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<NavState>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<AppShell>();

        // Pages + ViewModels
        builder.Services.AddTransient<LoginPage>(); builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterPage>(); builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<DashboardPage>(); builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<EquipmentPage>(); builder.Services.AddTransient<EquipmentViewModel>();
        builder.Services.AddTransient<EquipmentDetailPage>(); builder.Services.AddTransient<EquipmentDetailViewModel>();
        builder.Services.AddTransient<BreakdownPage>(); builder.Services.AddTransient<BreakdownViewModel>();
        builder.Services.AddTransient<DiagnosisPage>(); builder.Services.AddTransient<DiagnosisViewModel>();
        builder.Services.AddTransient<InterventionPage>(); builder.Services.AddTransient<InterventionViewModel>();
        builder.Services.AddTransient<HistoryPage>(); builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<InterventionDetailPage>(); builder.Services.AddTransient<InterventionDetailViewModel>();
        builder.Services.AddTransient<ReportsPage>(); builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<TechniciansPage>(); builder.Services.AddTransient<TechniciansViewModel>();
        builder.Services.AddTransient<SettingsPage>(); builder.Services.AddTransient<SettingsViewModel>();

        return builder.Build();
    }
}
