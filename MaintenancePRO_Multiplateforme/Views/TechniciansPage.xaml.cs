using MaintenancePRO.ViewModels;
namespace MaintenancePRO.Views;
public partial class TechniciansPage : ContentPage
{
    private readonly TechniciansViewModel _vm;
    public TechniciansPage(TechniciansViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadAsync(); }
}
