using MaintenancePRO.ViewModels;
namespace MaintenancePRO.Views;
public partial class DiagnosisPage : ContentPage
{
    private readonly DiagnosisViewModel _vm;
    public DiagnosisPage(DiagnosisViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadAsync(); }
}
