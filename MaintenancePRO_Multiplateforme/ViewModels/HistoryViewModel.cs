using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class BreakdownRow
{
    public BreakdownDto Dto { get; init; } = new();
    public bool ShowDiagnose { get; init; }
    public bool ShowIntervene { get; init; }
    public bool ShowClose { get; init; }
}

/// <summary>Historique (09) : déclarations et interventions, filtrables ; ouvre le détail d'intervention (10).</summary>
public sealed class HistoryViewModel : BaseViewModel
{
    private readonly ApiClient _api; private readonly SessionService _session; private readonly NavState _nav;
    private int? _equipmentId;
    private List<BreakdownDto> _breakdowns = new(); private List<InterventionDto> _interventions = new();

    public HistoryViewModel(ApiClient api, SessionService session, NavState nav)
    {
        _api = api; _session = session; _nav = nav;
        DiagnoseCommand = Cmd<BreakdownRow>(r => { _nav.BreakdownId = r.Dto.Id; return Shell.Current.GoToAsync(Routes.Diagnosis); });
        InterveneCommand = Cmd<BreakdownRow>(r => { _nav.BreakdownId = r.Dto.Id; return Shell.Current.GoToAsync(Routes.Intervention); });
        CloseCommand = Cmd<BreakdownRow>(CloseAsync);
        OpenInterventionCommand = Cmd<InterventionDto>(i => { _nav.InterventionId = i.Id; return Shell.Current.GoToAsync(Routes.InterventionDetail); });
        ClearEquipmentCommand = Cmd(async () => { _equipmentId = null; EquipmentFilterText = ""; await LoadAsync(); });
    }

    public string[] Filters { get; } = { "Tous", "Ouverts", "Clôturés" };
    private string _filter = "Tous";
    public string Filter { get => _filter; set { if (Set(ref _filter, value)) Refresh(); } }
    private string _equipmentFilterText = "";
    public string EquipmentFilterText { get => _equipmentFilterText; set { if (Set(ref _equipmentFilterText, value)) OnPropertyChanged(nameof(HasEquipmentFilter)); } }
    public bool HasEquipmentFilter => !string.IsNullOrEmpty(_equipmentFilterText);

    public IEnumerable<BreakdownRow> Breakdowns => _breakdowns.Where(b => Filter == "Tous" || (Filter == "Ouverts") == b.IsOpen)
        .Select(b => new BreakdownRow
        {
            Dto = b,
            ShowDiagnose = _session.CanSeeDiagnosis && b.Status is "Reported" or "Diagnosing" or "AwaitingApproval",
            ShowIntervene = _session.CanSeeIntervention && b.Status == "InMaintenance",
            ShowClose = _session.IsManager && b.Status == "Resolved"
        });
    public IEnumerable<InterventionDto> Interventions => _interventions.Where(i => Filter == "Tous" || (Filter == "Ouverts") == !i.IsTerminal);
    public bool NoBreakdowns => !Breakdowns.Any();
    public bool NoInterventions => !Interventions.Any();

    public Command<BreakdownRow> DiagnoseCommand { get; } public Command<BreakdownRow> InterveneCommand { get; } public Command<BreakdownRow> CloseCommand { get; }
    public Command<InterventionDto> OpenInterventionCommand { get; } public Command ClearEquipmentCommand { get; }

    private void Refresh() { OnPropertyChanged(nameof(Breakdowns)); OnPropertyChanged(nameof(Interventions)); OnPropertyChanged(nameof(NoBreakdowns)); OnPropertyChanged(nameof(NoInterventions)); }

    public async Task LoadAsync()
    {
        var eqId = _nav.TakeEquipmentId();
        if (eqId.HasValue) _equipmentId = eqId;
        await RunAsync(async () =>
        {
            if (_equipmentId.HasValue)
            {
                var h = await _api.GetAsync<EquipmentHistoryDto>($"api/equipment/{_equipmentId}/history");
                _breakdowns = h.Breakdowns; _interventions = h.Interventions;
                var eq = await _api.GetAsync<EquipmentDto>($"api/equipment/{_equipmentId}");
                EquipmentFilterText = $"Machine : {eq.Display}";
            }
            else
            {
                _breakdowns = (await _api.GetAsync<PagedResult<BreakdownDto>>("api/breakdowns?pageSize=100")).Items;
                _interventions = (await _api.GetAsync<PagedResult<InterventionDto>>("api/interventions?pageSize=100")).Items;
            }
        }, "Historique");
        Refresh();
    }

    private async Task CloseAsync(BreakdownRow r)
    {
        if (!await Dialogs.Confirm("Clôturer la panne", $"Clôturer définitivement « {r.Dto.Title} » ?")) return;
        var ok = await RunAsync(async () => await _api.PatchAsync<BreakdownDto>($"api/breakdowns/{r.Dto.Id}/status", new { status = "Closed" }), "Clôture impossible");
        if (ok) await LoadAsync();
    }
}
