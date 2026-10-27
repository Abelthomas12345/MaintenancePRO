using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class DashboardViewModel : BaseViewModel
{
    private readonly ApiClient _api; private readonly SessionService _session; private readonly NavState _nav;
    public DashboardViewModel(ApiClient api, SessionService session, NavState nav)
    {
        _api = api; _session = session; _nav = nav;
        RefreshCommand = Cmd(LoadAsync);
        NewBreakdownCommand = Cmd(() => Shell.Current.GoToAsync(Routes.Breakdown));
        OpenEquipmentCommand = Cmd(() => Shell.Current.GoToAsync(Routes.Equipments));
        OpenHistoryCommand = Cmd(() => Shell.Current.GoToAsync(Routes.History));
        OpenApprovalsCommand = Cmd(() => Shell.Current.GoToAsync(Routes.Diagnosis));
        OpenInterventionCommand = Cmd<InterventionDto>(i => { _nav.InterventionId = i.Id; return Shell.Current.GoToAsync(Routes.InterventionDetail); });
    }

    private DashboardDto _d = new();
    public string Greeting => $"Bonjour {_session.DisplayName} 👋";
    public string RoleText => _session.RoleLabel;
    public string Initials => string.Concat((_session.DisplayName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
    public string EquipmentLabel => _session.IsSimpleUser ? "MES ÉQUIPEMENTS" : "ÉQUIPEMENTS";
    public string EquipmentHint => _session.IsSimpleUser ? "Concernés par mes déclarations" : "Parc suivi";
    public string Scope => _session.IsSimpleUser ? "MES DÉCLARATIONS" : _session.IsTechnician ? "MES TRAVAUX" : "SUIVI DE MAINTENANCE";
    public int EquipmentCount => _d.EquipmentCount;
    public int BrokenEquipment => _d.BrokenEquipment;
    public int OpenBreakdowns => _d.OpenBreakdowns;
    public int OpenInterventions => _d.OpenInterventions;
    public int PendingApprovals => _d.PendingApprovals;
    public int PendingAccounts => _d.PendingAccounts;
    public bool ShowApprovals => _session.IsManager;
    public bool ShowInterventions => !_session.IsSimpleUser;
    public bool ShowPendingAccounts => _session.IsAdmin;
    public List<BreakdownDto> RecentBreakdowns => _d.RecentBreakdowns;
    public List<InterventionDto> RecentInterventions => _d.RecentInterventions;
    public bool NoBreakdowns => _d.RecentBreakdowns.Count == 0;

    public Command RefreshCommand { get; }
    public Command NewBreakdownCommand { get; }
    public Command OpenEquipmentCommand { get; }
    public Command OpenHistoryCommand { get; }
    public Command OpenApprovalsCommand { get; }
    public Command<InterventionDto> OpenInterventionCommand { get; }

    public async Task LoadAsync()
    {
        OnAll();
        await RunAsync(async () => { _d = await _api.GetAsync<DashboardDto>("api/dashboard"); }, "Tableau de bord");
        OnAll();
    }
}
