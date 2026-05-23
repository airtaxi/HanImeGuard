using HanImeGuard.Models;
using Microsoft.UI.Dispatching;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace HanImeGuard.Services;

public sealed class ForegroundApplicationWatcher : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SettingsService _settingsService;
    private readonly InputModeService _inputModeService;
    private readonly WINEVENTPROC _winEventProcedure;
    private UnhookWinEventSafeHandle? _hookHandle;
    private AdobeForegroundTarget _currentTarget = new(null, 0);
    private bool _disposed;

    public ForegroundApplicationWatcher(DispatcherQueue dispatcherQueue, SettingsService settingsService, InputModeService inputModeService)
    {
        _dispatcherQueue = dispatcherQueue;
        _settingsService = settingsService;
        _inputModeService = inputModeService;
        _winEventProcedure = OnWinEvent;
    }

    public event Action<AdobeForegroundTarget>? ForegroundTargetChanged;

    public AdobeForegroundTarget CurrentTarget => _currentTarget;

    public void Start()
    {
        if (_hookHandle is { IsInvalid: false }) return;

        _hookHandle = PInvoke.SetWinEventHook(EventSystemForeground, EventSystemForeground, default, _winEventProcedure, 0, 0, WinEventOutOfContext | WinEventSkipOwnProcess);
        RefreshForegroundTarget();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _hookHandle?.Dispose();
        _hookHandle = null;

        _disposed = true;
    }

    private void OnWinEvent(HWINEVENTHOOK _, uint __, HWND ___, int ____, int _____, uint ______, uint _______) => _dispatcherQueue.TryEnqueue(RefreshForegroundTarget);

    private unsafe void RefreshForegroundTarget()
    {
        var foregroundWindowHandle = PInvoke.GetForegroundWindow();
        var foregroundWindowNativeHandle = (nint)foregroundWindowHandle.Value;
        var targetApplicationKind = GetTargetApplicationKind(foregroundWindowHandle);
        var previousApplicationKind = _currentTarget.ApplicationKind;

        _currentTarget = new AdobeForegroundTarget(targetApplicationKind, foregroundWindowNativeHandle);
        DebugLog.WriteLine($"Foreground changed. handle=0x{foregroundWindowNativeHandle:X}, target={targetApplicationKind?.ToString() ?? "none"}, previous={previousApplicationKind?.ToString() ?? "none"}");

        if (targetApplicationKind.HasValue && previousApplicationKind != targetApplicationKind && _settingsService.Current.SwitchOnFocusReturn)
        {
            DebugLog.WriteLine("Focus-return IME trigger fired.");
            _inputModeService.EnsureAlphanumericMode(foregroundWindowNativeHandle);
        }

        ForegroundTargetChanged?.Invoke(_currentTarget);
    }

    private static unsafe AdobeApplicationKind? GetTargetApplicationKind(HWND windowHandle)
    {
        if (windowHandle.IsNull) return null;

        try
        {
            var processIdentifier = 0u;
            _ = PInvoke.GetWindowThreadProcessId(windowHandle, &processIdentifier);
            if (processIdentifier == 0) return null;

            using var process = Process.GetProcessById((int)processIdentifier);
            var processName = process.ProcessName;

            if (string.Equals(processName, "Photoshop", StringComparison.OrdinalIgnoreCase) || string.Equals(processName, "Photoshop.exe", StringComparison.OrdinalIgnoreCase)) return AdobeApplicationKind.Photoshop;
            if (string.Equals(processName, "Illustrator", StringComparison.OrdinalIgnoreCase) || string.Equals(processName, "Illustrator.exe", StringComparison.OrdinalIgnoreCase)) return AdobeApplicationKind.Illustrator;
        }
        catch (Exception) { }

        return null;
    }
}
