using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class EquipmentViewModel : BaseViewModel
{
    private readonly ApiClient _api; private readonly SessionService _session; private readonly NavState _nav;
    private List<EquipmentDto> _all = new();
    public EquipmentViewModel(ApiClient api, SessionService session, NavState nav)
    {
        _api = api; _session = session; _nav = nav;
        SelectCommand = Cmd<EquipmentDto>(e => { _nav.EquipmentId = e.Id; return Shell.Current.GoToAsync(Routes.EquipmentDetail); });
        AddCommand = Cmd(() => { _nav.CreateEquipment = true; return Shell.Current.GoToAsync(Routes.EquipmentDetail); });
        RefreshCommand = Cmd(LoadAsync);
    }

    private string _search = "";
    public string Search { get => _search; set { if (Set(ref _search, value)) { OnPropertyChanged(nameof(Items)); OnPropertyChanged(nameof(IsEmpty)); } } }
    public IEnumerable<EquipmentDto> Items => string.IsNullOrWhiteSpace(Search) ? _all : _all.Where(x => $"{x.Name} {x.Code} {x.Location} {x.Category}".Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase));
    public bool IsEmpty => !Items.Any();
    public bool CanAdd => _session.CanManageEquipment;

    public Command<EquipmentDto> SelectCommand { get; }
    public Command AddCommand { get; }
    public Command RefreshCommand { get; }

    public async Task LoadAsync()
    {
        OnPropertyChanged(nameof(CanAdd));
        await RunAsync(async () =>
        {
            var all = new List<EquipmentDto>();
            for (var page = 1; page <= 10; page++)
            {
                var r = await _api.GetAsync<PagedResult<EquipmentDto>>($"api/equipment?pageSize=100&page={page}&includeRetired={_session.CanManageEquipment.ToString().ToLowerInvariant()}");
                all.AddRange(r.Items);
                if (all.Count >= r.Total || r.Items.Count == 0) break;
            }
            _all = all;
        }, "Équipements");
        OnPropertyChanged(nameof(Items)); OnPropertyChanged(nameof(IsEmpty));
    }
}
