namespace MaintenancePRO.Services;
public static class ApiBaseUrl
{
    // API en développement : dotnet run --urls http://0.0.0.0:5080
#if ANDROID
    // Émulateur Android. Pour téléphone physique, remplacer 10.0.2.2 par l'IP locale du PC.
    public const string Value = "http://10.0.2.2:5080/";
#else
    public const string Value = "http://localhost:5080/";
#endif
}
