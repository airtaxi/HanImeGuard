using HanImeGuard.Models;
using Microsoft.UI.Dispatching;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace HanImeGuard.Services;

public sealed class AdobeInputSnapshotService : IDisposable
{
    private const int SnapshotTriggerDelayMilliseconds = 50;
    private readonly SettingsService _settingsService;
    private readonly InputModeService _inputModeService;
    private readonly ForegroundApplicationWatcher _foregroundApplicationWatcher;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly HOOKPROC _mouseHookProcedure;
    private readonly HOOKPROC _keyboardHookProcedure;
    private readonly Dictionary<AdobeApplicationKind, IAdobeAutomationAdapter> _automationAdapters;
    private UnhookWindowsHookExSafeHandle? _mouseHookHandle;
    private UnhookWindowsHookExSafeHandle? _keyboardHookHandle;
    private AdobeForegroundTarget _activeTarget = new(null, 0);
    private AdobeSnapshot? _previousSnapshot;
    private CursorPoint? _previousCursorPoint;
    private bool _isCapturingSnapshot;
    private bool _disposed;

    public AdobeInputSnapshotService(DispatcherQueue dispatcherQueue, SettingsService settingsService, InputModeService inputModeService, ForegroundApplicationWatcher foregroundApplicationWatcher, PhotoshopAutomationAdapter photoshopAutomationAdapter, IllustratorAutomationAdapter illustratorAutomationAdapter)
    {
        _dispatcherQueue = dispatcherQueue;
        _settingsService = settingsService;
        _inputModeService = inputModeService;
        _foregroundApplicationWatcher = foregroundApplicationWatcher;
        _mouseHookProcedure = OnMouseHook;
        _keyboardHookProcedure = OnKeyboardHook;
        _automationAdapters = new Dictionary<AdobeApplicationKind, IAdobeAutomationAdapter>
        {
            [AdobeApplicationKind.Photoshop] = photoshopAutomationAdapter,
            [AdobeApplicationKind.Illustrator] = illustratorAutomationAdapter
        };

        _settingsService.SettingsChanged += OnSettingsServiceSettingsChanged;
        _foregroundApplicationWatcher.ForegroundTargetChanged += OnForegroundApplicationWatcherForegroundTargetChanged;
    }

    public void Start()
    {
        _activeTarget = _foregroundApplicationWatcher.CurrentTarget;
        InstallInputHooks();
        QueueBaselineSnapshotIfNeeded(InputSnapshotTriggerKind.Start);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _mouseHookHandle?.Dispose();
        _mouseHookHandle = null;
        _keyboardHookHandle?.Dispose();
        _keyboardHookHandle = null;
        _settingsService.SettingsChanged -= OnSettingsServiceSettingsChanged;
        _foregroundApplicationWatcher.ForegroundTargetChanged -= OnForegroundApplicationWatcherForegroundTargetChanged;

        foreach (var automationAdapter in _automationAdapters.Values) automationAdapter.Dispose();

        _disposed = true;
    }

    private void InstallInputHooks()
    {
        _mouseHookHandle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, _mouseHookProcedure, default, 0);
        if (_mouseHookHandle.IsInvalid) DebugLog.WriteLine("Mouse hook installation failed.");

        _keyboardHookHandle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _keyboardHookProcedure, default, 0);
        if (_keyboardHookHandle.IsInvalid) DebugLog.WriteLine("Keyboard hook installation failed.");
    }

    private void OnSettingsServiceSettingsChanged(object? _, EventArgs __) => QueueBaselineSnapshotIfNeeded(InputSnapshotTriggerKind.SettingsChanged);

    private void OnForegroundApplicationWatcherForegroundTargetChanged(AdobeForegroundTarget foregroundTarget)
    {
        var didTargetChange = foregroundTarget.ApplicationKind != _activeTarget.ApplicationKind || foregroundTarget.WindowHandle != _activeTarget.WindowHandle;
        _activeTarget = foregroundTarget;

        if (!foregroundTarget.ApplicationKind.HasValue || didTargetChange)
        {
            _previousCursorPoint = null;
            _previousSnapshot = null;
        }

        if (didTargetChange) QueueBaselineSnapshotIfNeeded(InputSnapshotTriggerKind.ForegroundChanged);
    }

    private unsafe LRESULT OnMouseHook(int code, WPARAM messageParameter, LPARAM hookParameter)
    {
        if (code >= 0)
        {
            if (IsMouseMoveMessage(messageParameter))
            {
                var mouseHook = (MSLLHOOKSTRUCT*)hookParameter.Value;
                if (mouseHook is not null) QueueMouseMovement(new CursorPoint(mouseHook->pt.X, mouseHook->pt.Y));
            }
            else if (IsMouseSnapshotMessage(messageParameter))
            {
                QueueInputSnapshot(InputSnapshotTriggerKind.MouseClick);
            }
        }

        return PInvoke.CallNextHookEx(default, code, messageParameter, hookParameter);
    }

    private unsafe LRESULT OnKeyboardHook(int code, WPARAM messageParameter, LPARAM hookParameter)
    {
        if (code >= 0 && IsKeyboardSnapshotMessage(messageParameter))
        {
            var keyboardHook = (KBDLLHOOKSTRUCT*)hookParameter.Value;
            if (keyboardHook is not null && IsSnapshotVirtualKey((VIRTUAL_KEY)keyboardHook->vkCode)) QueueInputSnapshot(InputSnapshotTriggerKind.Keyboard);
        }

        return PInvoke.CallNextHookEx(default, code, messageParameter, hookParameter);
    }

    private void QueueMouseMovement(CursorPoint cursorPoint)
    {
        if (_disposed) return;
        _dispatcherQueue.TryEnqueue(() => HandleMouseMovement(cursorPoint));
    }

    private void QueueInputSnapshot(InputSnapshotTriggerKind triggerKind)
    {
        if (_disposed) return;
        _dispatcherQueue.TryEnqueue(() => HandleInputSnapshotTrigger(triggerKind));
    }

    private void HandleMouseMovement(CursorPoint cursorPoint)
    {
        if (!_activeTarget.ApplicationKind.HasValue) return;
        if (!_settingsService.Current.SwitchOnMouseMove)
        {
            _previousCursorPoint = null;
            return;
        }

        if (_previousCursorPoint is not null && _previousCursorPoint != cursorPoint) _inputModeService.EnsureAlphanumericMode(_activeTarget.WindowHandle);
        _previousCursorPoint = cursorPoint;
    }

    private void HandleInputSnapshotTrigger(InputSnapshotTriggerKind triggerKind)
    {
        if (!_activeTarget.ApplicationKind.HasValue) return;

        var activeTarget = _activeTarget;
        DebugLog.WriteLine($"Input snapshot trigger. trigger={triggerKind}, target={activeTarget.ApplicationKind}, handle=0x{activeTarget.WindowHandle:X}");

        if (!ShouldCaptureDocumentSnapshots()) return;

        _ = CaptureSnapshotAsync(activeTarget, triggerKind);
    }

    private void QueueBaselineSnapshotIfNeeded(InputSnapshotTriggerKind triggerKind)
    {
        if (!_activeTarget.ApplicationKind.HasValue) return;
        if (!ShouldCaptureDocumentSnapshots()) return;
        if (_previousSnapshot is not null) return;

        _ = CaptureSnapshotAsync(_activeTarget, triggerKind);
    }

    private bool ShouldCaptureDocumentSnapshots() => _settingsService.Current.SwitchOnTextEdit || _settingsService.Current.SwitchOnLayerNameChange;

    private async Task CaptureSnapshotAsync(AdobeForegroundTarget targetAtTriggerStart, InputSnapshotTriggerKind triggerKind)
    {
        if (!targetAtTriggerStart.ApplicationKind.HasValue) return;
        if (!_automationAdapters.TryGetValue(targetAtTriggerStart.ApplicationKind.Value, out var automationAdapter)) return;

        if (_isCapturingSnapshot)
        {
            DebugLog.WriteLine($"Snapshot skipped because previous capture is still running. trigger={triggerKind}");
            return;
        }

        _isCapturingSnapshot = true;
        try { await CaptureSnapshotCoreAsync(targetAtTriggerStart, triggerKind, automationAdapter); }
        finally { _isCapturingSnapshot = false; }
    }

    private async Task CaptureSnapshotCoreAsync(AdobeForegroundTarget targetAtTriggerStart, InputSnapshotTriggerKind triggerKind, IAdobeAutomationAdapter automationAdapter)
    {
        AdobeSnapshot? currentSnapshot;
        try
        {
            DebugLog.WriteLine($"Snapshot capture started. trigger={triggerKind}, target={targetAtTriggerStart.ApplicationKind}, handle=0x{targetAtTriggerStart.WindowHandle:X}");
            await Task.Delay(SnapshotTriggerDelayMilliseconds);
            if (targetAtTriggerStart != _activeTarget) return;
            currentSnapshot = await automationAdapter.CaptureSnapshotAsync();
        }
        catch (Exception)
        {
            DebugLog.WriteLine($"Snapshot capture failed and previous baseline was preserved. trigger={triggerKind}");
            return;
        }

        if (targetAtTriggerStart != _activeTarget) return;

        if (currentSnapshot is null)
        {
            DebugLog.WriteLine($"Snapshot capture returned no data and previous baseline was preserved. trigger={triggerKind}");
            return;
        }

        LogSnapshotRecords(triggerKind, currentSnapshot);

        if (_previousSnapshot is null || !string.Equals(_previousSnapshot.DocumentKey, currentSnapshot.DocumentKey, StringComparison.Ordinal))
        {
            _previousSnapshot = currentSnapshot;
            DebugLog.WriteLine($"Snapshot stored as baseline. trigger={triggerKind}, document={currentSnapshot.DocumentKey}, records={currentSnapshot.Records.Count}");
            return;
        }

        var detectedChange = DetectChange(_previousSnapshot, currentSnapshot);
        _previousSnapshot = currentSnapshot;

        if (detectedChange.HasTextChange && _settingsService.Current.SwitchOnTextEdit) _inputModeService.EnsureAlphanumericMode(targetAtTriggerStart.WindowHandle);
        else if (detectedChange.HasLayerNameChange && _settingsService.Current.SwitchOnLayerNameChange) _inputModeService.EnsureAlphanumericMode(targetAtTriggerStart.WindowHandle);
    }

    private static void LogSnapshotRecords(InputSnapshotTriggerKind triggerKind, AdobeSnapshot snapshot)
    {
        DebugLog.WriteLine($"Snapshot records. trigger={triggerKind}, document={EscapeLogValue(snapshot.DocumentKey)}, records={snapshot.Records.Count}");

        foreach (var record in snapshot.Records.Values)
        {
            DebugLog.WriteLine($"Snapshot record. trigger={triggerKind}, record={EscapeLogValue(record.RecordKey)}, textLayer={record.IsTextRecord}, layerName=\"{EscapeLogValue(record.LayerName)}\", nameHash={record.NameHash}, text=\"{EscapeLogValue(record.TextContent)}\", textHash={record.TextHash}");
        }
    }

    private static string EscapeLogValue(string value) => value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

    private static bool IsMouseMoveMessage(WPARAM messageParameter) => (uint)messageParameter.Value == PInvoke.WM_MOUSEMOVE;

    private static bool IsMouseSnapshotMessage(WPARAM messageParameter)
    {
        var windowMessage = (uint)messageParameter.Value;
        return windowMessage is PInvoke.WM_LBUTTONDOWN or PInvoke.WM_RBUTTONDOWN or PInvoke.WM_MBUTTONDOWN;
    }

    private static bool IsKeyboardSnapshotMessage(WPARAM messageParameter)
    {
        var windowMessage = (uint)messageParameter.Value;
        return windowMessage is PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP;
    }

    private static bool IsSnapshotVirtualKey(VIRTUAL_KEY virtualKey) => virtualKey is VIRTUAL_KEY.VK_SHIFT or VIRTUAL_KEY.VK_LSHIFT or VIRTUAL_KEY.VK_RSHIFT or VIRTUAL_KEY.VK_CONTROL or VIRTUAL_KEY.VK_LCONTROL or VIRTUAL_KEY.VK_RCONTROL or VIRTUAL_KEY.VK_MENU or VIRTUAL_KEY.VK_LMENU or VIRTUAL_KEY.VK_RMENU or VIRTUAL_KEY.VK_RETURN;

    private static (bool HasLayerNameChange, bool HasTextChange) DetectChange(AdobeSnapshot previousSnapshot, AdobeSnapshot currentSnapshot)
    {
        var hasLayerNameChange = false;
        var hasTextChange = false;

        foreach (var currentRecord in currentSnapshot.Records.Values)
        {
            if (!previousSnapshot.Records.TryGetValue(currentRecord.RecordKey, out var previousRecord)) continue;

            if (!string.Equals(previousRecord.NameHash, currentRecord.NameHash, StringComparison.Ordinal)) hasLayerNameChange = true;
            if (!string.Equals(previousRecord.TextHash, currentRecord.TextHash, StringComparison.Ordinal)) hasTextChange = true;
            if (hasLayerNameChange && hasTextChange) break;
        }

        return (hasLayerNameChange, hasTextChange);
    }

    private enum InputSnapshotTriggerKind
    {
        Start,
        SettingsChanged,
        ForegroundChanged,
        MouseClick,
        Keyboard
    }
}
