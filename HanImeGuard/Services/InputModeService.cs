using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;

namespace HanImeGuard.Services;

public sealed class InputModeService(SettingsService settingsService)
{
    private DateTimeOffset _lastSuccessfulSwitchTime = DateTimeOffset.MinValue;

    public bool EnsureAlphanumericMode(nint targetWindowHandle)
    {
        DebugLog.WriteLine($"IME ensure requested. target=0x{targetWindowHandle:X}");
        if (targetWindowHandle == 0)
        {
            DebugLog.WriteLine("IME ensure skipped because target handle is zero.");
            return false;
        }

        if (ShouldSkipDuplicateSwitch())
        {
            DebugLog.WriteLine("IME ensure skipped by duplicate-switch guard.");
            return false;
        }

        var foregroundWindowHandle = new HWND(targetWindowHandle);
        if (TryRequestAlphanumericModeWithDefaultImeWindow(foregroundWindowHandle, "foreground")) return true;

        DebugLog.WriteLine("IME ensure finished without switching.");
        return false;
    }

    private bool ShouldSkipDuplicateSwitch()
    {
        if (!settingsService.Current.PreventDuplicateSwitches) return false;

        var elapsedTime = DateTimeOffset.UtcNow - _lastSuccessfulSwitchTime;
        return elapsedTime < TimeSpan.FromMilliseconds(settingsService.Current.DuplicateSwitchDelayMilliseconds);
    }

    private bool TryRequestAlphanumericModeWithDefaultImeWindow(HWND windowHandle, string source)
    {
        var nativeWindowHandle = GetNativeWindowHandle(windowHandle);
        var defaultImeWindowHandle = PInvoke.ImmGetDefaultIMEWnd(windowHandle);
        if (defaultImeWindowHandle.IsNull)
        {
            DebugLog.WriteLine($"ImmGetDefaultIMEWnd returned null. source={source}, handle=0x{nativeWindowHandle:X}");
            return false;
        }

        var nativeDefaultImeWindowHandle = GetNativeWindowHandle(defaultImeWindowHandle);
        var alphanumericConversionMode = IME_CONVERSION_MODE.IME_CMODE_ALPHANUMERIC;
        _ = PInvoke.SendMessage(defaultImeWindowHandle, PInvoke.WM_IME_CONTROL, new WPARAM(PInvoke.IMC_SETCONVERSIONMODE), new LPARAM((nint)(uint)alphanumericConversionMode));
        _lastSuccessfulSwitchTime = DateTimeOffset.UtcNow;
        DebugLog.WriteLine($"Default IME alphanumeric request sent. source={source}, handle=0x{nativeWindowHandle:X}, imeWindow=0x{nativeDefaultImeWindowHandle:X}, requestedConversion=0x{(uint)alphanumericConversionMode:X}");
        return true;
    }

    private static unsafe nint GetNativeWindowHandle(HWND windowHandle) => (nint)windowHandle.Value;
}
