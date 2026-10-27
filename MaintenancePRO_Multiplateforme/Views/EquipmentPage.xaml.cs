using MaintenancePRO.ViewModels;
namespace MaintenancePRO.Views;
public partial class EquipmentPage : ContentPage
{
    private readonly EquipmentViewModel _vm;
    public EquipmentPage(EquipmentViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadAsync(); }
}
