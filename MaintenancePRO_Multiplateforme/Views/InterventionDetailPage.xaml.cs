using MaintenancePRO.ViewModels;
namespace MaintenancePRO.Views;
public partial class InterventionDetailPage : ContentPage
{
    private readonly InterventionDetailViewModel _vm;
    public InterventionDetailPage(InterventionDetailViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadAsync(); }
}
