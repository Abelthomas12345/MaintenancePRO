using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MaintenancePRO.Models;

namespace MaintenancePRO.Services;

public sealed class ApiException(string message, int statusCode) : Exception(message) { public int StatusCode { get; } = statusCode; }

/// <summary>Client HTTP : jeton Bearer, renouvellement automatique sur 401 (refresh token), déconnexion propre si la session est perdue.</summary>
public sealed class ApiClient
{
    private readonly ApiSettings _settings; private readonly TokenStore _tokens; private readonly SessionService _session;
    private HttpClient? _http; private string _baseUrl = "";
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _expiring;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ApiClient(ApiSettings settings, TokenStore tokens, SessionService session) { _settings = settings; _tokens = tokens; _session = session; }

    private HttpClient Http
    {
        get
        {
            var url = _settings.BaseUrl;
            if (_http == null || url != _baseUrl) { _http?.Dispose(); _http = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(25) }; _baseUrl = url; }
            return _http;
        }
    }

    // ---------- Authentification ----------
    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var r = await SendAsync<AuthResult>(HttpMethod.Post, "api/auth/login", new { email, password }, auth: false);
        await _tokens.SaveAsync(r.AccessToken, r.RefreshToken);
        _session.SignIn(r.UserId, r.DisplayName, r.Email, r.Roles);
        return r;
    }

    public Task<MessageDto> RegisterAsync(string email, string password, string displayName) => SendAsync<MessageDto>(HttpMethod.Post, "api/auth/register", new { email, password, displayName }, auth: false);
    public Task<MessageDto> ForgotPasswordAsync(string email) => SendAsync<MessageDto>(HttpMethod.Post, "api/auth/forgot-password", new { email }, auth: false);

    /// <summary>Reprend une session existante au démarrage (refresh token conservé dans SecureStorage).</summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        if (string.IsNullOrEmpty(await _tokens.GetRefreshAsync())) return false;
        try
        {
            var me = await GetAsync<MeDto>("api/auth/me");
            _session.SignIn(me.Id, me.DisplayName, me.Email, me.Roles);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Déconnexion réelle : refresh token révoqué côté API, jetons effacés, session vidée.</summary>
    public async Task LogoutAsync()
    {
        var refresh = await _tokens.GetRefreshAsync();
        try { if (!string.IsNullOrEmpty(refresh)) await SendAsync<object>(HttpMethod.Post, "api/auth/logout", new { refreshToken = refresh }, auth: false, expectBody: false); } catch { /* hors-ligne : on efface quand même localement */ }
        _tokens.Clear();
        _session.Clear();
    }

    public async Task<bool> PingAsync()
    {
        try { using var r = await Http.GetAsync("api/health"); return r.IsSuccessStatusCode; } catch { return false; }
    }

    // ---------- Appels génériques ----------
    public Task<T> GetAsync<T>(string route) => SendAsync<T>(HttpMethod.Get, route);
    public Task<T> PostAsync<T>(string route, object body) => SendAsync<T>(HttpMethod.Post, route, body);
    public Task<T> PutAsync<T>(string route, object body) => SendAsync<T>(HttpMethod.Put, route, body);
    public Task<T> PatchAsync<T>(string route, object body) => SendAsync<T>(HttpMethod.Patch, route, body);
    public Task DeleteAsync(string route) => SendAsync<object>(HttpMethod.Delete, route, expectBody: false);

    public async Task<byte[]> GetBytesAsync(string route)
    {
        using var resp = await SendRawAsync(() => new HttpRequestMessage(HttpMethod.Get, route), true);
        await EnsureSuccess(resp);
        return await resp.Content.ReadAsByteArrayAsync();
    }

    public async Task<T> UploadAsync<T>(string route, string fileName, byte[] data, string? caption)
    {
        using var resp = await SendRawAsync(() =>
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(data);
            form.Add(file, "file", fileName);
            if (!string.IsNullOrWhiteSpace(caption)) form.Add(new StringContent(caption), "caption");
            return new HttpRequestMessage(HttpMethod.Post, route) { Content = form };
        }, true);
        await EnsureSuccess(resp);
        return (await resp.Content.ReadFromJsonSafeAsync<T>())!;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string route, object? body = null, bool auth = true, bool expectBody = true)
    {
        using var resp = await SendRawAsync(() =>
        {
            var req = new HttpRequestMessage(method, route);
            if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
            return req;
        }, auth);
        await EnsureSuccess(resp);
        if (!expectBody || resp.StatusCode == HttpStatusCode.NoContent) return default!;
        return (await resp.Content.ReadFromJsonSafeAsync<T>()) ?? throw new ApiException("Réponse API invalide.", 500);
    }

    private async Task<HttpResponseMessage> SendRawAsync(Func<HttpRequestMessage> make, bool auth)
    {
        var usedToken = auth ? await _tokens.GetAccessAsync() : null;
        var resp = await SendOnce(make, usedToken);
        if (resp.StatusCode != HttpStatusCode.Unauthorized || !auth) return resp;

        // 401 : jeton expiré/révoqué -> on tente UN renouvellement puis on rejoue la requête.
        resp.Dispose();
        if (await TryRefreshAsync(usedToken))
        {
            resp = await SendOnce(make, await _tokens.GetAccessAsync());
            if (resp.StatusCode != HttpStatusCode.Unauthorized) return resp;
            resp.Dispose();
        }
        await ExpireSessionAsync();
        throw new ApiException("Session expirée. Reconnecte-toi.", 401);
    }

    private async Task<HttpResponseMessage> SendOnce(Func<HttpRequestMessage> make, string? token)
    {
        using var req = make();
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await Http.SendAsync(req);
    }

    private async Task<bool> TryRefreshAsync(string? tokenUsed)
    {
        await _refreshLock.WaitAsync();
        try
        {
            // Un autre appel a peut-être déjà renouvelé le jeton pendant qu'on attendait.
            var current = await _tokens.GetAccessAsync();
            if (!string.IsNullOrEmpty(current) && current != tokenUsed) return true;
            var refresh = await _tokens.GetRefreshAsync();
            if (string.IsNullOrEmpty(refresh)) return false;
            using var resp = await Http.PostAsync("api/auth/refresh", new StringContent(JsonSerializer.Serialize(new { refreshToken = refresh }, Json), Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return false;
            var r = await resp.Content.ReadFromJsonSafeAsync<AuthResult>();
            if (r == null) return false;
            await _tokens.SaveAsync(r.AccessToken, r.RefreshToken);
            _session.SignIn(r.UserId, r.DisplayName, r.Email, r.Roles); // le rôle peut avoir changé
            return true;
        }
        catch { return false; }
        finally { _refreshLock.Release(); }
    }

    private async Task ExpireSessionAsync()
    {
        if (_expiring) return;
        _expiring = true;
        try
        {
            _tokens.Clear();
            var wasLoggedIn = _session.IsLoggedIn;
            _session.Clear();
            if (wasLoggedIn)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.GoToAsync(Routes.Login);
                    await Shell.Current.DisplayAlert("Session expirée", "Ta session n'est plus valide. Reconnecte-toi.", "OK");
                });
            }
        }
        finally { _expiring = false; }
    }

    private static async Task EnsureSuccess(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync();
        throw new ApiException(ParseError(body, (int)resp.StatusCode), (int)resp.StatusCode);
    }

    private static readonly Dictionary<string, string> CodeText = new()
    {
        ["PasswordTooShort"] = "mot de passe trop court (12 caractères minimum)",
        ["PasswordRequiresDigit"] = "le mot de passe doit contenir un chiffre",
        ["PasswordRequiresLower"] = "le mot de passe doit contenir une minuscule",
        ["PasswordRequiresUpper"] = "le mot de passe doit contenir une majuscule",
        ["PasswordRequiresNonAlphanumeric"] = "le mot de passe doit contenir un symbole (ex. ! @ # ?)",
        ["InvalidEmail"] = "adresse e-mail invalide",
        ["DuplicateEmail"] = "adresse e-mail déjà utilisée",
        ["DuplicateUserName"] = "adresse e-mail déjà utilisée",
    };

    internal static string ParseError(string body, int status)
    {
        var fallback = status switch { 403 => "Action non autorisée pour ton rôle.", 404 => "Élément introuvable.", 429 => "Trop de requêtes. Patiente une minute.", >= 500 => "Erreur du serveur. Réessaie plus tard.", _ => $"Erreur API ({status})." };
        if (string.IsNullOrWhiteSpace(body)) return fallback;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return fallback;
            var parts = new List<string>();
            if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) parts.Add(m.GetString()!);
            if (root.TryGetProperty("errors", out var e))
            {
                if (e.ValueKind == JsonValueKind.Array)
                    foreach (var x in e.EnumerateArray()) { var c = x.GetString() ?? ""; parts.Add(CodeText.TryGetValue(c, out var t) ? t : c); }
                else if (e.ValueKind == JsonValueKind.Object)
                    foreach (var p in e.EnumerateObject()) foreach (var x in p.Value.EnumerateArray()) parts.Add(x.GetString() ?? "");
            }
            if (parts.Count == 0 && root.TryGetProperty("title", out var t2) && t2.ValueKind == JsonValueKind.String) parts.Add(t2.GetString()!);
            return parts.Count == 0 ? fallback : string.Join(" — ", parts.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
        }
        catch { return fallback; }
    }
}

public sealed class MessageDto { public string Message { get; set; } = ""; }

internal static class HttpContentExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task<T?> ReadFromJsonSafeAsync<T>(this HttpContent c) => await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<T>(c, Json);
}
