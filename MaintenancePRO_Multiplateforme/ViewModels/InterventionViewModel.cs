using System.Globalization;
using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

/// <summary>Intervention (08) : création après approbation du diagnostic, démarrage, pause, clôture avec compte rendu.</summary>
public sealed class InterventionViewModel : BaseViewModel
{
    private readonly ApiClient _api; private readonly SessionService _session; private readonly NavState _nav;
    private BreakdownDetailDto? _detail; private bool _loading;

    public InterventionViewModel(ApiClient api, SessionService session, NavState nav)
    {
        _api = api; _session = session; _nav = nav;
        CreateCommand = Cmd(CreateAsync);
        SelectCommand = Cmd<InterventionDto>(i => { SelectedIntervention = i; return Task.CompletedTask; });
        StartCommand = Cmd(() => ChangeStatusAsync("InProgress"));
        PauseCommand = Cmd(() => ChangeStatusAsync("Paused"));
        ResumeCommand = Cmd(() => ChangeStatusAsync("InProgress"));
        CompleteCommand = Cmd(() => ChangeStatusAsync("Completed"));
        CancelCommand = Cmd(() => ChangeStatusAsync("Cancelled"));
        SaveDetailsCommand = Cmd(SaveDetailsAsync);
        AssignCommand = Cmd(AssignAsync);
        OpenDetailCommand = Cmd(() => { _nav.InterventionId = SelectedIntervention!.Id; return Shell.Current.GoToAsync(Routes.InterventionDetail); });
    }

    private List<BreakdownDto> _breakdowns = new();
    public List<BreakdownDto> Breakdowns { get => _breakdowns; private set => Set(ref _breakdowns, value); }
    private BreakdownDto? _selectedBreakdown;
    public BreakdownDto? SelectedBreakdown { get => _selectedBreakdown; set { if (Set(ref _selectedBreakdown, value) && !_loading) _ = LoadDetailAsync(null); } }
    private InterventionDto? _selectedIntervention;
    public InterventionDto? SelectedIntervention
    {
        get => _selectedIntervention;
        set { if (Set(ref _selectedIntervention, value)) { Resolution = value?.Resolution ?? ""; Minutes = value?.LaborHours is decimal h ? (h * 60).ToString("0", CultureInfo.InvariantCulture) : ""; CostText = value?.Cost?.ToString(CultureInfo.InvariantCulture) ?? ""; OnAll(); } }
    }
    private List<TechnicianDto> _technicians = new();
    public List<TechnicianDto> Technicians { get => _technicians; private set => Set(ref _technicians, value); }
    private TechnicianDto? _tech; private TechnicianDto? _newTech;
    public TechnicianDto? SelectedTechnician { get => _tech; set => Set(ref _tech, value); }
    public TechnicianDto? NewTechnician { get => _newTech; set => Set(ref _newTech, value); }

    private string _title = "", _work = "", _resolution = "", _minutes = "", _cost = "";
    public string NewTitle { get => _title; set => Set(ref _title, value); }
    public string NewWork { get => _work; set => Set(ref _work, value); }
    public string Resolution { get => _resolution; set => Set(ref _resolution, value); }
    public string Minutes { get => _minutes; set => Set(ref _minutes, value); }
    public string CostText { get => _cost; set => Set(ref _cost, value); }

    public IEnumerable<InterventionDto> Interventions => _detail?.Interventions ?? new List<InterventionDto>();
    public bool HasBreakdown => _selectedBreakdown != null;
    public bool NoBreakdowns => _breakdowns.Count == 0;
    public bool IsManager => _session.IsManager;
    public bool CanCreate => _selectedBreakdown != null && (_session.IsManager || _selectedBreakdown.AssignedTechnicianId == _session.UserId);
    public bool HasIntervention => _selectedIntervention != null;
    public bool IsEditable => _selectedIntervention != null && !_selectedIntervention.IsTerminal;
    public bool CanStart => _selectedIntervention?.Status == "Assigned";
    public bool CanPause => _selectedIntervention?.Status == "InProgress";
    public bool CanResume => _selectedIntervention?.Status == "Paused";
    public bool CanComplete => _selectedIntervention?.Status == "InProgress";
    public bool CanCancel => _session.IsManager && _selectedIntervention?.Status is "Planned" or "Assigned" or "Paused";
    public bool ShowManagerFields => _session.IsManager && _selectedIntervention != null;
    public bool ShowAssign => _session.IsManager && IsEditable;
    public string InterventionInfo => _selectedIntervention == null ? "" : $"{_selectedIntervention.StatusLabel} · {_selectedIntervention.TechnicianText}\nDurée mesurée : {_selectedIntervention.DurationText}\n\n{_selectedIntervention.WorkDescription}";
    public string BreakdownInfo => _selectedBreakdown == null ? "" : $"{_selectedBreakdown.Equipment.Display} · {_selectedBreakdown.SeverityLabel}\n{_selectedBreakdown.TechnicianText}";

    public Command CreateCommand { get; } public Command<InterventionDto> SelectCommand { get; }
    public Command StartCommand { get; } public Command PauseCommand { get; } public Command ResumeCommand { get; } public Command CompleteCommand { get; } public Command CancelCommand { get; }
    public Command SaveDetailsCommand { get; } public Command AssignCommand { get; } public Command OpenDetailCommand { get; }

    public async Task LoadAsync()
    {
        var wanted = _nav.TakeBreakdownId() ?? _selectedBreakdown?.Id;
        _loading = true;
        await RunAsync(async () =>
        {
            var r = await _api.GetAsync<PagedResult<BreakdownDto>>("api/breakdowns?status=InMaintenance&pageSize=100");
            Breakdowns = r.Items;
            if (_session.IsManager) Technicians = await _api.GetAsync<List<TechnicianDto>>("api/users/technicians");
        }, "Interventions");
        _selectedBreakdown = Breakdowns.FirstOrDefault(b => b.Id == wanted);
        _loading = false;
        OnPropertyChanged(nameof(SelectedBreakdown)); OnPropertyChanged(nameof(NoBreakdowns));
        await LoadDetailAsync(_selectedIntervention?.Id);
    }

    private async Task LoadDetailAsync(int? keepInterventionId)
    {
        _detail = null;
        if (_selectedBreakdown != null)
            await RunAsync(async () => { _detail = await _api.GetAsync<BreakdownDetailDto>($"api/breakdowns/{_selectedBreakdown.Id}"); }, "Panne");
        var open = Interventions.FirstOrDefault(i => i.Id == keepInterventionId) ?? Interventions.FirstOrDefault(i => !i.IsTerminal);
        SelectedIntervention = open;
        OnAll();
    }

    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle) || string.IsNullOrWhiteSpace(NewWork)) { await Dialogs.Alert("Champs requis", "Titre et travaux prévus sont obligatoires."); return; }
        InterventionDto? created = null;
        var ok = await RunAsync(async () => created = await _api.PostAsync<InterventionDto>("api/interventions", new { breakdownId = _selectedBreakdown!.Id, title = NewTitle.Trim(), workDescription = NewWork.Trim(), assignedTechnicianId = _session.IsManager ? NewTechnician?.Id : null }), "Création impossible");
        if (!ok) return;
        NewTitle = ""; NewWork = ""; NewTechnician = null;
        await LoadDetailAsync(created?.Id);
    }

    private async Task ChangeStatusAsync(string status)
    {
        if (status == "Completed" && string.IsNullOrWhiteSpace(Resolution)) { await Dialogs.Alert("Compte rendu requis", "Renseigne le résultat de l'intervention avant de la clôturer."); return; }
        if (status is "Completed" or "Cancelled" && !await Dialogs.Confirm(status == "Completed" ? "Clôturer" : "Annuler", status == "Completed" ? "Vérifie le résultat avant de clôturer : l'intervention ne sera plus modifiable." : "Annuler cette intervention ?")) return;
        var id = _selectedIntervention!.Id;
        var ok = await RunAsync(async () => await _api.PatchAsync<InterventionDto>($"api/interventions/{id}", new { status, resolution = string.IsNullOrWhiteSpace(Resolution) ? null : Resolution.Trim() }), "Action impossible");
        if (!ok) return;
        if (status == "Completed") await Dialogs.Alert("Intervention clôturée", "Le compte rendu est enregistré ; l'équipement repasse « En service » si plus rien n'est ouvert.");
        await LoadAsync2(id);
    }

    private async Task SaveDetailsAsync()
    {
        var id = _selectedIntervention!.Id;
        decimal? hours = null, cost = null;
        if (_session.IsManager)
        {
            if (!string.IsNullOrWhiteSpace(Minutes)) { if (!decimal.TryParse(Minutes.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var m) || m < 0) { await Dialogs.Alert("Durée invalide", "Saisis la durée en minutes (nombre positif)."); return; } hours = Math.Round(m / 60, 2); }
            if (!string.IsNullOrWhiteSpace(CostText)) { if (!decimal.TryParse(CostText.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var c) || c < 0) { await Dialogs.Alert("Coût invalide", "Saisis un coût positif."); return; } cost = c; }
        }
        object body = _session.IsManager ? new { resolution = IsEditable ? Resolution.Trim() : null, laborHours = hours, cost } : new { resolution = Resolution.Trim() };
        var ok = await RunAsync(async () => await _api.PatchAsync<InterventionDto>($"api/interventions/{id}", body), "Enregistrement impossible");
        if (ok) { await Dialogs.Alert("Enregistré", "Modifications enregistrées."); await LoadAsync2(id); }
    }

    private async Task AssignAsync()
    {
        if (SelectedTechnician == null) { await Dialogs.Alert("Technicien", "Choisis un technicien."); return; }
        var id = _selectedIntervention!.Id;
        var ok = await RunAsync(async () => await _api.PatchAsync<InterventionDto>($"api/interventions/{id}", new { assignedTechnicianId = SelectedTechnician.Id }), "Attribution impossible");
        if (ok) await LoadAsync2(id);
    }

    private async Task LoadAsync2(int interventionId) { await LoadDetailAsync(interventionId); }
}
