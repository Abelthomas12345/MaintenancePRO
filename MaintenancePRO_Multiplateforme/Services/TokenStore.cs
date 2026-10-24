namespace MaintenancePRO.Services;

/// <summary>Stockage des jetons (SecureStorage) avec cache mémoire et repli si SecureStorage est indisponible.</summary>
public sealed class TokenStore
{
    private const string AccessKey = "maintenancepro_access_token", RefreshKey = "maintenancepro_refresh_token";
    private string? _access, _refresh;
    private bool _loaded;

    private async Task LoadAsync()
    {
        if (_loaded) return;
        try { _access = await SecureStorage.Default.GetAsync(AccessKey); _refresh = await SecureStorage.Default.GetAsync(RefreshKey); } catch { }
        _loaded = true;
    }

    public async Task<string?> GetAccessAsync() { await LoadAsync(); return _access; }
    public async Task<string?> GetRefreshAsync() { await LoadAsync(); return _refresh; }

    public async Task SaveAsync(string access, string refresh)
    {
        _access = access; _refresh = refresh; _loaded = true;
        try { await SecureStorage.Default.SetAsync(AccessKey, access); await SecureStorage.Default.SetAsync(RefreshKey, refresh); } catch { }
    }

    public void Clear()
    {
        _access = null; _refresh = null; _loaded = true;
        try { SecureStorage.Default.Remove(AccessKey); SecureStorage.Default.Remove(RefreshKey); } catch { }
    }
}
