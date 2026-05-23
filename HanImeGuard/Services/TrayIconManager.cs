using DevWinUI;
using Microsoft.UI.Xaml.Controls;

namespace HanImeGuard.Services;

public sealed class TrayIconManager(SettingsService settingsService, StartupTaskService startupTaskService) : IDisposable
{
    private const uint TrayIconIdentifier = 1;
    private SystemTrayIcon? _trayIcon;
    private bool _disposed;

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_trayIcon is not null) return;

        _trayIcon = new SystemTrayIcon(TrayIconIdentifier, AssetPathResolver.IconFilePath, "한영지킴이");
        _trayIcon.LeftClick += OnTrayIconClicked;
        _trayIcon.RightClick += OnTrayIconClicked;
        _trayIcon.IsVisible = true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_trayIcon is not null)
        {
            _trayIcon.LeftClick -= OnTrayIconClicked;
            _trayIcon.RightClick -= OnTrayIconClicked;
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _disposed = true;
    }

    private void OnTrayIconClicked(SystemTrayIcon _, SystemTrayIconEventArgs args) => args.Flyout = CreateMenuFlyout();

    private MenuFlyout CreateMenuFlyout()
    {
        var flyout = new MenuFlyout();

        flyout.Items.Add(CreateToggleItem("포토샵/일러스트레이터로 돌아오면 영문 상태로 되돌리기", settingsService.Current.SwitchOnFocusReturn, settingsService.SetSwitchOnFocusReturn));
        flyout.Items.Add(CreateToggleItem("마우스를 움직이면 영문 상태로 되돌리기", settingsService.Current.SwitchOnMouseMove, settingsService.SetSwitchOnMouseMove));
        flyout.Items.Add(CreateToggleItem("텍스트 편집 후 영문 상태로 되돌리기", settingsService.Current.SwitchOnTextEdit, settingsService.SetSwitchOnTextEdit));
        flyout.Items.Add(CreateToggleItem("레이어 이름 변경 후 영문 상태로 되돌리기", settingsService.Current.SwitchOnLayerNameChange, settingsService.SetSwitchOnLayerNameChange));
        flyout.Items.Add(CreateToggleItem("짧은 시간 안에 중복 전환 막기", settingsService.Current.PreventDuplicateSwitches, settingsService.SetPreventDuplicateSwitches));
        flyout.Items.Add(CreateStartupTaskToggleItem());
        flyout.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem { Text = "종료" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        flyout.Items.Add(exitItem);

        return flyout;
    }

    private static ToggleMenuFlyoutItem CreateToggleItem(string text, bool isChecked, Action<bool> applyValue)
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = text,
            IsChecked = isChecked
        };
        item.Click += (_, _) => applyValue(item.IsChecked);
        return item;
    }

    private ToggleMenuFlyoutItem CreateStartupTaskToggleItem()
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = "시스템 시작 시 자동 실행",
            IsChecked = settingsService.Current.StartWithWindows
        };

        item.Click += async (_, _) =>
        {
            var wasApplied = await startupTaskService.SetStartWithWindowsEnabledAsync(item.IsChecked);
            if (!wasApplied) item.IsChecked = settingsService.Current.StartWithWindows;
        };

        return item;
    }
}
