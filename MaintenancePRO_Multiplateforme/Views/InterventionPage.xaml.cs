using MaintenancePRO.ViewModels;
namespace MaintenancePRO.Views;
public partial class InterventionPage : ContentPage
{
    private readonly InterventionViewModel _vm;
    public InterventionPage(InterventionViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadAsync(); }
}
