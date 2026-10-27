using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

/// <summary>Déclaration de panne (06) : l'équipement se choisit dans la liste des équipements enregistrés.</summary>
public sealed class BreakdownViewModel : BaseViewModel
{
    private readonly ApiClient _api; private readonly NavState _nav;
    public BreakdownViewModel(ApiClient api, NavState nav)
    {
        _api = api; _nav = nav;
        SaveCommand = Cmd(SaveAsync);
        _severity = SeverityOptions[1];
    }

    public Option[] SeverityOptions { get; } = { new("Low", "Faible"), new("Medium", "Moyenne"), new("High", "Élevée"), new("Critical", "Critique") };
    private List<EquipmentDto> _equipments = new();
    public List<EquipmentDto> Equipments { get => _equipments; private set => Set(ref _equipments, value); }
    private EquipmentDto? _selectedEquipment; private Option _severity;
    public EquipmentDto? SelectedEquipment { get => _selectedEquipment; set => Set(ref _selectedEquipment, value); }
    public Option SelectedSeverity { get => _severity; set { if (value != null) Set(ref _severity, value); } }
    private string _title = "", _description = "";
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Description { get => _description; set => Set(ref _description, value); }
    public Command SaveCommand { get; }

    public async Task LoadAsync()
    {
        var preselect = _nav.TakeEquipmentId() ?? _selectedEquipment?.Id;
        await RunAsync(async () =>
        {
            var r = await _api.GetAsync<PagedResult<EquipmentDto>>("api/equipment?pageSize=100");
            Equipments = r.Items;
        }, "Équipements");
        SelectedEquipment = Equipments.FirstOrDefault(e => e.Id == preselect);
    }

    private async Task SaveAsync()
    {
        if (SelectedEquipment == null) { await Dialogs.Alert("Équipement requis", "Choisis l'équipement concerné dans la liste."); return; }
        if (string.IsNullOrWhiteSpace(Title) || Title.Trim().Length > 160) { await Dialogs.Alert("Titre requis", "Donne un titre court à la panne (160 caractères max)."); return; }
        if (string.IsNullOrWhiteSpace(Description) || Description.Length > 3000) { await Dialogs.Alert("Description requise", "Décris les symptômes (1 à 3000 caractères)."); return; }
        var ok = await RunAsync(async () => await _api.PostAsync<BreakdownDto>("api/breakdowns", new { equipmentId = SelectedEquipment.Id, title = Title.Trim(), description = Description.Trim(), severity = SelectedSeverity.Value }), "Déclaration impossible");
        if (!ok) return;
        Title = ""; Description = ""; SelectedEquipment = null;
        await Dialogs.Alert("Déclaration envoyée", "Ta panne a été enregistrée. Elle apparaît en tête de l'historique.");
        await Shell.Current.GoToAsync(Routes.History);   // la déclaration s'affiche directement dans l'historique
    }
}
