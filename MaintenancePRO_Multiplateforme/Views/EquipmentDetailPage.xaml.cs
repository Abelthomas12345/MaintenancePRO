using MaintenancePRO.ViewModels;
namespace MaintenancePRO.Views;
public partial class EquipmentDetailPage : ContentPage
{
    private readonly EquipmentDetailViewModel _vm;
    public EquipmentDetailPage(EquipmentDetailViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadAsync(); }
}
