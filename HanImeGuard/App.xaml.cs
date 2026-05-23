using HanImeGuard.Views;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Diagnostics;

namespace HanImeGuard;

public partial class App : Application
{
    private TrayHostWindow? _trayHostWindow;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var currentInstance = AppInstance.FindOrRegisterForKey("HanImeGuard");
        if (!currentInstance.IsCurrent)
        {
            await currentInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Process.GetCurrentProcess().Kill();
            return;
        }

        _trayHostWindow = new TrayHostWindow();
        _trayHostWindow.Activate();
        await _trayHostWindow.InitializeAsync();
    }
}
