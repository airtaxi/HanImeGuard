using HanImeGuard.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace HanImeGuard.Views;

public sealed class TrayHostWindow : Window
{
    private ForegroundApplicationWatcher? _foregroundApplicationWatcher;
    private AdobeInputSnapshotService? _adobeInputSnapshotService;
    private TrayIconManager? _trayIconManager;
    private bool _initialized;

    public TrayHostWindow()
    {
        Title = "한영지킴이";
        Content = new Grid();
        AppWindow.Title = "한영지킴이";
        AppWindow.SetIcon(AssetPathResolver.IconFilePath);
        AppWindow.IsShownInSwitchers = false;
        Closed += OnTrayHostWindowClosed;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        HideWindow();

        var settingsService = new SettingsService();
        var startupTaskService = new StartupTaskService(settingsService);
        var inputModeService = new InputModeService(settingsService);
        _foregroundApplicationWatcher = new ForegroundApplicationWatcher(DispatcherQueue, settingsService, inputModeService);

        var photoshopAutomationAdapter = new PhotoshopAutomationAdapter();
        var illustratorAutomationAdapter = new IllustratorAutomationAdapter();

        _adobeInputSnapshotService = new AdobeInputSnapshotService(DispatcherQueue, settingsService, inputModeService, _foregroundApplicationWatcher, photoshopAutomationAdapter, illustratorAutomationAdapter);
        _trayIconManager = new TrayIconManager(settingsService, startupTaskService);
        _trayIconManager.ExitRequested += OnTrayIconManagerExitRequested;
        _trayIconManager.Initialize();

        _foregroundApplicationWatcher.Start();
        _adobeInputSnapshotService.Start();
        await startupTaskService.SynchronizeStoredPreferenceAsync();

        _initialized = true;
    }

    private void OnTrayHostWindowClosed(object? _, WindowEventArgs __) => DisposeServices();

    private void OnTrayIconManagerExitRequested(object? _, EventArgs __)
    {
        Close();
        Application.Current.Exit();
    }

    private void HideWindow()
    {
        AppWindow.IsShownInSwitchers = false;
        var windowHandle = new HWND(WindowNative.GetWindowHandle(this));
        PInvoke.ShowWindow(windowHandle, SHOW_WINDOW_CMD.SW_HIDE);
    }

    private void DisposeServices()
    {
        if (_trayIconManager is not null)
        {
            _trayIconManager.ExitRequested -= OnTrayIconManagerExitRequested;
            _trayIconManager.Dispose();
            _trayIconManager = null;
        }

        _adobeInputSnapshotService?.Dispose();
        _adobeInputSnapshotService = null;
        _foregroundApplicationWatcher?.Dispose();
        _foregroundApplicationWatcher = null;
    }
}
