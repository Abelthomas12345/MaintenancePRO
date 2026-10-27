using System.ComponentModel;
using System.Runtime.CompilerServices;
using MaintenancePRO.Services;

namespace MaintenancePRO.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    protected void OnAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { if (Set(ref _isBusy, value)) OnPropertyChanged(nameof(IsNotBusy)); } }
    public bool IsNotBusy => !_isBusy;

    /// <summary>Exécute une action avec indicateur d'activité et gestion homogène des erreurs réseau/API. Retourne true si tout s'est bien passé.</summary>
    protected async Task<bool> RunAsync(Func<Task> action, string title = "Erreur")
    {
        if (IsBusy) return false;
        try { IsBusy = true; await action(); return true; }
        catch (ApiException ex) { if (ex.StatusCode != 401) await Dialogs.Alert(title, ex.Message); }
        catch (HttpRequestException) { await Dialogs.Alert("API inaccessible", "Vérifie que le backend est lancé et que l'adresse de l'API (Paramètres) est correcte."); }
        catch (TaskCanceledException) { await Dialogs.Alert("Délai dépassé", "Le backend ne répond pas. Vérifie le réseau et l'adresse de l'API."); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); await Dialogs.Alert(title, "Une erreur inattendue est survenue."); }
        finally { IsBusy = false; }
        return false;
    }

    protected static Command Cmd(Func<Task> run) => new(async () => await run());
    protected static Command<T> Cmd<T>(Func<T, Task> run) => new(async p => { if (p != null) await run(p); });
}
