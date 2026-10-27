using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class ReportsViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private ReportDto _r = new();
    public ReportsViewModel(ApiClient api) { _api = api; RefreshCommand = Cmd(LoadAsync); _period = Periods[1]; }

    public Option[] Periods { get; } = { new("7", "7 derniers jours"), new("30", "30 derniers jours"), new("90", "90 derniers jours"), new("365", "12 derniers mois") };
    private Option _period;
    public Option Period { get => _period; set { if (value != null && Set(ref _period, value)) _ = LoadAsync(); } }

    public string Summary => $"{_r.TotalBreakdowns} pannes · {_r.TotalInterventions} interventions";
    public string Availability => $"{_r.AvailabilityPercent:0.#} %";
    public string Mttr => _r.MttrHours.HasValue ? $"{_r.MttrHours:0.#} h" : "—";
    public string Hint => $"{_r.ResolvedBreakdowns} pannes résolues sur {_r.TotalBreakdowns} · {_r.CompletedInterventions} interventions terminées · {_r.LaborHours:0.##} h de main-d'œuvre · coût total {_r.TotalCost:0.##}";
    public List<CountBySeverity> BySeverity => _r.BreakdownsBySeverity;
    public List<CountByStatus> ByStatus => _r.InterventionsByStatus;
    public Command RefreshCommand { get; }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var days = int.Parse(_period.Value);
            var from = DateTime.UtcNow.AddDays(-days).ToString("o");
            _r = await _api.GetAsync<ReportDto>($"api/dashboard/reports?fromUtc={Uri.EscapeDataString(from)}&toUtc={Uri.EscapeDataString(DateTime.UtcNow.ToString("o"))}");
        }, "Rapports");
        OnAll();
    }
}
