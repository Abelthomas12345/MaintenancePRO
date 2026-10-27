using MaintenancePRO.ViewModels;
namespace MaintenancePRO.Views;
public partial class RegisterPage : ContentPage { public RegisterPage(RegisterViewModel vm) { InitializeComponent(); BindingContext = vm; } }
