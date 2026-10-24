using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

/// <summary>Fiche équipement (05) : lecture, modification et ajout sont trois variantes du même écran.</summary>
public sealed class EquipmentDetailViewModel : BaseViewModel
{
    public enum Mode { View, Edit, Create }
    private readonly ApiClient _api; private readonly SessionService _session; private readonly NavState _nav;
    private EquipmentDto? _equipment;
    private Mode _mode = Mode.View;

    public EquipmentDetailViewModel(ApiClient api, SessionService session, NavState nav)
    {
        _api = api; _session = session; _nav = nav;
        EditCommand = new Command(() => { FillForm(_equipment!); SetMode(Mode.Edit); });
        CancelCommand = Cmd(async () => { if (_mode == Mode.Create) await Shell.Current.GoToAsync(".."); else SetMode(Mode.View); });
        SaveCommand = Cmd(SaveAsync);
        DeclareCommand = Cmd(() => { _nav.EquipmentId = _equipment!.Id; return Shell.Current.GoToAsync(Routes.Breakdown); });
        HistoryCommand = Cmd(() => { _nav.EquipmentId = _equipment!.Id; return Shell.Current.GoToAsync(Routes.History); });
        DeleteCommand = Cmd(DeleteAsync);
    }

    public Option[] StatusOptions { get; } = { new("Available", "En service"), new("ToInspect", "À vérifier"), new("Broken", "En panne"), new("InMaintenance", "En maintenance"), new("Retired", "Retiré (archivé)") };
    private Option? _selectedStatus;
    public Option? SelectedStatus { get => _selectedStatus; set => Set(ref _selectedStatus, value); }

    public string Code { get; set; } = ""; public string EqName { get; set; } = ""; public string Location { get; set; } = ""; public string Category { get; set; } = "";
    public string Manufacturer { get; set; } = ""; public string Model { get; set; } = ""; public string SerialNumber { get; set; } = ""; public string Notes { get; set; } = "";

    public EquipmentDto? Equipment => _equipment;
    public bool IsView => _mode == Mode.View; public bool IsForm => _mode != Mode.View; public bool IsCreate => _mode == Mode.Create; public bool IsEdit => _mode == Mode.Edit;
    public bool CanEdit => IsView && _session.CanManageEquipment && _equipment != null;
    public bool CanDelete => IsView && _session.IsAdmin && _equipment != null;
    public bool CanDeclare => IsView && _equipment != null && _equipment.Status != "Retired";
    public string PageTitle => _mode switch { Mode.Create => "Nouvel équipement", Mode.Edit => "Modifier l'équipement", _ => "Fiche équipement" };
    public string PageSubtitle => _mode switch { Mode.Create => "Renseigne les informations de la machine", Mode.Edit => "Mets à jour les informations", _ => "Informations machine et actions rapides" };
    public string ExtraInfo => _equipment == null ? "" : $"{_equipment.Manufacturer ?? "—"} · {_equipment.Model ?? "—"} · N° série {_equipment.SerialNumber ?? "—"}";

    public Command EditCommand { get; } public Command CancelCommand { get; } public Command SaveCommand { get; }
    public Command DeclareCommand { get; } public Command HistoryCommand { get; } public Command DeleteCommand { get; }

    private void SetMode(Mode m) { _mode = m; OnAll(); }

    private void FillForm(EquipmentDto e)
    {
        Code = e.Code; EqName = e.Name; Location = e.Location; Category = e.Category; Manufacturer = e.Manufacturer ?? ""; Model = e.Model ?? ""; SerialNumber = e.SerialNumber ?? ""; Notes = e.Notes ?? "";
        _selectedStatus = StatusOptions.FirstOrDefault(o => o.Value == e.Status);
    }

    public async Task LoadAsync()
    {
        if (_nav.TakeCreateEquipment())
        {
            _equipment = null; Code = EqName = Location = Category = Manufacturer = Model = SerialNumber = Notes = "";
            _selectedStatus = StatusOptions[0]; SetMode(Mode.Create); return;
        }
        var id = _nav.TakeEquipmentId();
        if (id == null)
        {
            if (_mode != Mode.View || _equipment == null) { OnAll(); return; }   // édition en cours ou rien à afficher
            id = _equipment.Id;                                                   // retour sur la fiche : on rafraîchit
        }
        else _mode = Mode.View;
        await RunAsync(async () => { _equipment = await _api.GetAsync<EquipmentDto>($"api/equipment/{id}"); }, "Fiche équipement");
        OnAll();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EqName) || string.IsNullOrWhiteSpace(Location) || string.IsNullOrWhiteSpace(Category) || (IsCreate && string.IsNullOrWhiteSpace(Code)))
        { await Dialogs.Alert("Champs requis", "Code, nom, emplacement et catégorie sont obligatoires."); return; }
        string? N(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        if (IsCreate)
        {
            var ok = await RunAsync(async () =>
            {
                _equipment = await _api.PostAsync<EquipmentDto>("api/equipment", new { code = Code.Trim(), name = EqName.Trim(), location = Location.Trim(), category = Category.Trim(), manufacturer = N(Manufacturer), model = N(Model), serialNumber = N(SerialNumber), notes = N(Notes) });
            }, "Ajout impossible");
            if (!ok) return;
            await Dialogs.Alert("Équipement ajouté", $"{_equipment!.Code} a été ajouté au parc.");
            _mode = Mode.View; OnAll();
            await Shell.Current.GoToAsync("..");   // la liste et l'accueil se rechargent à l'affichage
        }
        else
        {
            var ok = await RunAsync(async () =>
            {
                _equipment = await _api.PutAsync<EquipmentDto>($"api/equipment/{_equipment!.Id}", new { name = EqName.Trim(), location = Location.Trim(), category = Category.Trim(), manufacturer = N(Manufacturer), model = N(Model), serialNumber = N(SerialNumber), notes = N(Notes), status = SelectedStatus?.Value ?? _equipment.Status });
            }, "Modification impossible");
            if (ok) SetMode(Mode.View);
        }
    }

    private async Task DeleteAsync()
    {
        if (!await Dialogs.Confirm("Supprimer", $"Supprimer définitivement {_equipment!.Name} ? (impossible s'il a un historique)", "Supprimer")) return;
        var ok = await RunAsync(async () => await _api.DeleteAsync($"api/equipment/{_equipment!.Id}"), "Suppression impossible");
        if (ok) { _equipment = null; await Shell.Current.GoToAsync(".."); }
    }
}
