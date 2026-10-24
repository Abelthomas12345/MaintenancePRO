using MaintenancePRO.ViewModels;
namespace MaintenancePRO.Views;
public partial class BreakdownPage : ContentPage
{
    private readonly BreakdownViewModel _vm;
    public BreakdownPage(BreakdownViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadAsync(); }
}
