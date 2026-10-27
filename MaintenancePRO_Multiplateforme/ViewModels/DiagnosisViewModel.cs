using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class DiagnosisRow
{
    public DiagnosisDto Dto { get; init; } = new();
    public bool ShowDecision { get; init; }
}

/// <summary>Diagnostic (07) : le technicien attribué diagnostique et soumet ; le superviseur attribue, approuve ou rejette.</summary>
public sealed class DiagnosisViewModel : BaseViewModel
{
    private readonly ApiClient _api; private readonly SessionService _session; private readonly NavState _nav;
    private BreakdownDetailDto? _detail; private int? _draftId; private bool _loading;

    public DiagnosisViewModel(ApiClient api, SessionService session, NavState nav)
    {
        _api = api; _session = session; _nav = nav;
        SaveDraftCommand = Cmd(() => SaveAsync(false));
        SubmitCommand = Cmd(() => SaveAsync(true));
        AssignCommand = Cmd(AssignAsync);
        ApproveCommand = Cmd<DiagnosisRow>(r => DecideAsync(r, "Approved"));
        RejectCommand = Cmd<DiagnosisRow>(r => DecideAsync(r, "Rejected"));
        RejectBreakdownCommand = Cmd(RejectBreakdownAsync);
        GoInterventionCommand = Cmd(() => { _nav.BreakdownId = SelectedBreakdown!.Id; return Shell.Current.GoToAsync(Routes.Intervention); });
    }

    private List<BreakdownDto> _breakdowns = new();
    public List<BreakdownDto> Breakdowns { get => _breakdowns; private set => Set(ref _breakdowns, value); }
    private BreakdownDto? _selected;
    public BreakdownDto? SelectedBreakdown { get => _selected; set { if (Set(ref _selected, value) && !_loading) _ = LoadDetailAsync(); } }
    private List<TechnicianDto> _technicians = new();
    public List<TechnicianDto> Technicians { get => _technicians; private set => Set(ref _technicians, value); }
    private TechnicianDto? _selectedTech;
    public TechnicianDto? SelectedTechnician { get => _selectedTech; set => Set(ref _selectedTech, value); }

    private string _findings = "", _rootCause = "", _action = "";
    public string Findings { get => _findings; set => Set(ref _findings, value); }
    public string RootCause { get => _rootCause; set => Set(ref _rootCause, value); }
    public string RecommendedAction { get => _action; set => Set(ref _action, value); }

    public IEnumerable<DiagnosisRow> Diagnoses => (_detail?.Diagnoses ?? new()).Select(d => new DiagnosisRow { Dto = d, ShowDecision = _session.IsManager && d.Status == "Submitted" });
    public bool HasSelection => _selected != null;
    public bool NoBreakdowns => _breakdowns.Count == 0;
    public bool IsManager => _session.IsManager;
    public bool CanAssign => _session.IsManager && _selected != null && _selected.IsOpen;
    public bool CanWrite => _selected != null && _selected.Status is "Reported" or "Diagnosing" && (_session.IsManager || _selected.AssignedTechnicianId == _session.UserId);
    public bool WaitingApproval => _selected?.Status == "AwaitingApproval";
    public bool CanGoIntervention => _selected?.Status == "InMaintenance" && _session.CanSeeIntervention;
    public bool CanRejectBreakdown => _session.IsManager && _selected != null && _selected.Status is "Reported" or "Diagnosing" or "AwaitingApproval";
    public string Summary => _selected == null ? "" : $"{_selected.Equipment.Display}\n{_selected.StatusLabel} · {_selected.SeverityLabel}\n{_selected.TechnicianText}\n\n{_selected.Description}";

    public Command SaveDraftCommand { get; } public Command SubmitCommand { get; } public Command AssignCommand { get; }
    public Command<DiagnosisRow> ApproveCommand { get; } public Command<DiagnosisRow> RejectCommand { get; }
    public Command RejectBreakdownCommand { get; } public Command GoInterventionCommand { get; }

    public async Task LoadAsync()
    {
        var wanted = _nav.TakeBreakdownId() ?? _selected?.Id;
        _loading = true;
        await RunAsync(async () =>
        {
            var r = await _api.GetAsync<PagedResult<BreakdownDto>>("api/breakdowns?openOnly=true&pageSize=100");
            Breakdowns = r.Items.Where(b => b.Status is "Reported" or "Diagnosing" or "AwaitingApproval" or "InMaintenance").ToList();
            if (_session.IsManager) Technicians = await _api.GetAsync<List<TechnicianDto>>("api/users/technicians");
        }, "Diagnostics");
        _selected = Breakdowns.FirstOrDefault(b => b.Id == wanted);
        _loading = false;
        OnPropertyChanged(nameof(SelectedBreakdown)); OnPropertyChanged(nameof(NoBreakdowns));
        await LoadDetailAsync();
    }

    private async Task LoadDetailAsync()
    {
        _detail = null; _draftId = null; Findings = RootCause = RecommendedAction = "";
        if (_selected != null)
        {
            await RunAsync(async () =>
            {
                _detail = await _api.GetAsync<BreakdownDetailDto>($"api/breakdowns/{_selected.Id}");
                var draft = _detail.Diagnoses.FirstOrDefault(d => d.Status == "Draft");
                if (draft != null) { _draftId = draft.Id; Findings = draft.Findings; RootCause = draft.RootCause ?? ""; RecommendedAction = draft.RecommendedAction ?? ""; }
            }, "Panne");
        }
        OnAll();
    }

    private async Task SaveAsync(bool submit)
    {
        if (string.IsNullOrWhiteSpace(Findings) || Findings.Length > 3000) { await Dialogs.Alert("Constats requis", "Décris tes constats (1 à 3000 caractères)."); return; }
        var body = new { findings = Findings.Trim(), rootCause = string.IsNullOrWhiteSpace(RootCause) ? null : RootCause.Trim(), recommendedAction = string.IsNullOrWhiteSpace(RecommendedAction) ? null : RecommendedAction.Trim() };
        var ok = await RunAsync(async () =>
        {
            if (_draftId == null) _draftId = (await _api.PostAsync<DiagnosisDto>($"api/diagnoses?breakdownId={_selected!.Id}", body)).Id;
            else await _api.PutAsync<DiagnosisDto>($"api/diagnoses/{_draftId}", body);
            if (submit) await _api.PatchAsync<DiagnosisDto>($"api/diagnoses/{_draftId}/submit", new { });
        }, "Diagnostic");
        if (!ok) return;
        await Dialogs.Alert("Diagnostic", submit ? "Diagnostic soumis : en attente d'approbation du superviseur." : "Brouillon enregistré.");
        await ReloadKeepingSelectionAsync();
    }

    private async Task AssignAsync()
    {
        if (SelectedTechnician == null) { await Dialogs.Alert("Technicien", "Choisis un technicien dans la liste."); return; }
        var ok = await RunAsync(async () => await _api.PatchAsync<BreakdownDto>($"api/breakdowns/{_selected!.Id}/assign", new { technicianId = SelectedTechnician.Id }), "Attribution impossible");
        if (ok) await ReloadKeepingSelectionAsync();
    }

    private async Task DecideAsync(DiagnosisRow r, string decision)
    {
        if (!await Dialogs.Confirm("Décision", decision == "Approved" ? "Approuver ce diagnostic et autoriser la maintenance ?" : "Rejeter ce diagnostic ? Le technicien devra le refaire.")) return;
        var ok = await RunAsync(async () => await _api.PatchAsync<DiagnosisDto>($"api/diagnoses/{r.Dto.Id}/decision", new { status = decision }), "Décision impossible");
        if (ok) await ReloadKeepingSelectionAsync();
    }

    private async Task RejectBreakdownAsync()
    {
        if (!await Dialogs.Confirm("Rejeter la déclaration", "Rejeter cette panne (non justifiée) ?", "Rejeter")) return;
        var ok = await RunAsync(async () => await _api.PatchAsync<BreakdownDto>($"api/breakdowns/{_selected!.Id}/status", new { status = "Rejected" }), "Rejet impossible");
        if (ok) { _nav.BreakdownId = null; _selected = null; await LoadAsync(); }
    }

    private async Task ReloadKeepingSelectionAsync() { _nav.BreakdownId = _selected?.Id; await LoadAsync(); }
}
