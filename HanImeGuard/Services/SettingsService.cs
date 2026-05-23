using HanImeGuard.Models;
using Windows.Storage;

namespace HanImeGuard.Services;

public sealed class SettingsService
{
    private const string SwitchOnFocusReturnKey = nameof(AppSettings.SwitchOnFocusReturn);
    private const string SwitchOnMouseMoveKey = nameof(AppSettings.SwitchOnMouseMove);
    private const string SwitchOnTextEditKey = nameof(AppSettings.SwitchOnTextEdit);
    private const string SwitchOnLayerNameChangeKey = nameof(AppSettings.SwitchOnLayerNameChange);
    private const string PreventDuplicateSwitchesKey = nameof(AppSettings.PreventDuplicateSwitches);
    private const string StartWithWindowsKey = nameof(AppSettings.StartWithWindows);
    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public SettingsService()
    {
        Current = Load();
    }

    public event EventHandler? SettingsChanged;

    public AppSettings Current { get; }

    public void SetSwitchOnFocusReturn(bool value) => UpdateSetting(SwitchOnFocusReturnKey, value, (settings, settingValue) => settings.SwitchOnFocusReturn = settingValue);

    public void SetSwitchOnMouseMove(bool value) => UpdateSetting(SwitchOnMouseMoveKey, value, (settings, settingValue) => settings.SwitchOnMouseMove = settingValue);

    public void SetSwitchOnTextEdit(bool value) => UpdateSetting(SwitchOnTextEditKey, value, (settings, settingValue) => settings.SwitchOnTextEdit = settingValue);

    public void SetSwitchOnLayerNameChange(bool value) => UpdateSetting(SwitchOnLayerNameChangeKey, value, (settings, settingValue) => settings.SwitchOnLayerNameChange = settingValue);

    public void SetPreventDuplicateSwitches(bool value) => UpdateSetting(PreventDuplicateSwitchesKey, value, (settings, settingValue) => settings.PreventDuplicateSwitches = settingValue);

    public void SetStartWithWindows(bool value) => UpdateSetting(StartWithWindowsKey, value, (settings, settingValue) => settings.StartWithWindows = settingValue);

    private AppSettings Load()
    {
        var settings = new AppSettings
        {
            SwitchOnFocusReturn = ReadBoolean(SwitchOnFocusReturnKey, true),
            SwitchOnMouseMove = ReadBoolean(SwitchOnMouseMoveKey, false),
            SwitchOnTextEdit = ReadBoolean(SwitchOnTextEditKey, true),
            SwitchOnLayerNameChange = ReadBoolean(SwitchOnLayerNameChangeKey, true),
            PreventDuplicateSwitches = ReadBoolean(PreventDuplicateSwitchesKey, false),
            StartWithWindows = ReadBoolean(StartWithWindowsKey, true),
        };

        return settings;
    }

    private bool ReadBoolean(string key, bool defaultValue)
    {
        if (_localSettings.Values.TryGetValue(key, out var value) && value is bool booleanValue) return booleanValue;
        _localSettings.Values[key] = defaultValue;
        return defaultValue;
    }

    private void UpdateSetting<TValue>(string key, TValue value, Action<AppSettings, TValue> updateSetting)
    {
        updateSetting(Current, value);
        _localSettings.Values[key] = value;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
