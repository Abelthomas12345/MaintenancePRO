using Android.App;
using Android.Runtime;

namespace MaintenancePRO;

// Le trafic HTTP (non chiffré) n'est autorisé qu'en Debug pour parler à l'API locale ; en Release, HTTPS obligatoire.
#if DEBUG
[Application(UsesCleartextTraffic = true, AllowBackup = false)]
#else
[Application(UsesCleartextTraffic = false, AllowBackup = false)]
#endif
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
