namespace MaintenancePRO.Services;

/// <summary>Adresse de l'API, modifiable dans Paramètres (utile pour un téléphone réel : IP du PC).</summary>
public sealed class ApiSettings
{
    private const string Key = "maintenancepro_api_url";
#if ANDROID
    // Émulateur Android : 10.0.2.2 = PC hôte. Téléphone réel : saisir http://IP_DU_PC:5080 dans Paramètres.
    public const string Default = "http://10.0.2.2:5080/";
#else
    public const string Default = "http://localhost:5080/";
#endif

    public string BaseUrl => Normalize(Preferences.Default.Get(Key, Default));

    public static string Normalize(string url)
    {
        url = (url ?? "").Trim();
        return url.EndsWith('/') ? url : url + "/";
    }

    public static bool TryValidate(string input, out string normalized, out string error)
    {
        normalized = ""; error = "";
        if (!Uri.TryCreate((input ?? "").Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        { error = "Adresse invalide. Exemple : https://api.monentreprise.com ou http://192.168.1.20:5080"; return false; }
#if !DEBUG
        if (uri.Scheme != "https") { error = "En version Release, seul HTTPS est autorisé."; return false; }
#endif
        normalized = Normalize(uri.ToString());
        return true;
    }

    public void Save(string normalizedUrl) => Preferences.Default.Set(Key, normalizedUrl);
    public void Reset() => Preferences.Default.Remove(Key);
}
