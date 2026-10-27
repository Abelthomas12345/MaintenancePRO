using MaintenancePRO.Models;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public sealed class PhotoItem { public PhotoDto Dto { get; init; } = new(); public ImageSource? Source { get; init; } public string Caption => string.IsNullOrWhiteSpace(Dto.Caption) ? Dto.FileName : Dto.Caption!; }

/// <summary>Détail intervention (10) : compte rendu, photos, export PDF.</summary>
public sealed class InterventionDetailViewModel : BaseViewModel
{
    private readonly ApiClient _api; private readonly SessionService _session; private readonly NavState _nav;
    private InterventionDto? _i;

    public InterventionDetailViewModel(ApiClient api, SessionService session, NavState nav)
    {
        _api = api; _session = session; _nav = nav;
        AddPhotoCommand = Cmd(AddPhotoAsync);
        ExportCommand = Cmd(ExportAsync);
    }

    public InterventionDto? Intervention { get => _i; private set { _i = value; OnAll(); } }
    private List<PhotoItem> _photos = new();
    public List<PhotoItem> Photos { get => _photos; private set { _photos = value; OnPropertyChanged(); OnPropertyChanged(nameof(NoPhotos)); } }
    public bool NoPhotos => _photos.Count == 0;
    public bool HasData => _i != null;
    public bool CanAddPhoto => _i != null && (_session.IsManager || _i.AssignedTechnicianId == _session.UserId);
    public bool ShowCost => _session.IsManager && _i != null;
    public string Header => _i == null ? "" : _i.Status == "Completed" ? "INTERVENTION CLÔTURÉE" : $"INTERVENTION · {_i.StatusLabel.ToUpperInvariant()}";
    public string Resolution => string.IsNullOrWhiteSpace(_i?.Resolution) ? "Aucun compte rendu pour l'instant." : _i!.Resolution!;
    public string Dates => _i == null ? "" : $"Créée : {Labels.Date(_i.CreatedUtc)}\nDémarrée : {Labels.Date(_i.StartedUtc)}\nTerminée : {Labels.Date(_i.CompletedUtc)}\nDurée : {_i.DurationText}";
    public string CostText => _i == null ? "" : $"Heures : {_i.LaborHours?.ToString() ?? "—"} · Coût : {_i.Cost?.ToString() ?? "—"}";

    public Command AddPhotoCommand { get; } public Command ExportCommand { get; }

    public async Task LoadAsync()
    {
        var id = _nav.TakeInterventionId() ?? _i?.Id;
        if (id == null) return;
        await RunAsync(async () =>
        {
            Intervention = await _api.GetAsync<InterventionDto>($"api/interventions/{id}");
            await LoadPhotosAsync(id.Value);
        }, "Intervention");
    }

    private async Task LoadPhotosAsync(int id)
    {
        var list = await _api.GetAsync<List<PhotoDto>>($"api/photos/intervention/{id}");
        var items = new List<PhotoItem>();
        foreach (var p in list)
        {
            try { var bytes = await _api.GetBytesAsync($"api/photos/{p.Id}"); items.Add(new PhotoItem { Dto = p, Source = ImageSource.FromStream(() => new MemoryStream(bytes)) }); }
            catch (ApiException) { items.Add(new PhotoItem { Dto = p }); }
        }
        Photos = items;
    }

    private async Task AddPhotoAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choisir une photo", FileTypes = FilePickerFileType.Images });
        if (file == null) return;
        byte[] data;
        await using (var s = await file.OpenReadAsync()) { using var ms = new MemoryStream(); await s.CopyToAsync(ms); data = ms.ToArray(); }
        if (data.Length > 5 * 1024 * 1024) { await Dialogs.Alert("Photo trop lourde", "La photo dépasse 5 Mo."); return; }
        var ok = await RunAsync(async () => { await _api.UploadAsync<PhotoDto>($"api/photos/intervention/{_i!.Id}", file.FileName, data, null); await LoadPhotosAsync(_i.Id); }, "Envoi impossible");
        if (ok) await Dialogs.Alert("Photo ajoutée", "La photo a été jointe à l'intervention.");
    }

    private async Task ExportAsync()
    {
        await RunAsync(async () =>
        {
            var bytes = await _api.GetBytesAsync($"api/interventions/{_i!.Id}/report");
            var path = Path.Combine(FileSystem.CacheDirectory, $"intervention-{_i.Id}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            await Share.Default.RequestAsync(new ShareFileRequest { Title = "Compte rendu d'intervention", File = new ShareFile(path, "application/pdf") });
        }, "Export impossible");
    }
}
