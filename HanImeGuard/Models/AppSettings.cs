namespace HanImeGuard.Models;

public sealed class AppSettings
{
    public bool SwitchOnFocusReturn { get; set; } = true;

    public bool SwitchOnMouseMove { get; set; }

    public bool SwitchOnTextEdit { get; set; } = true;

    public bool SwitchOnLayerNameChange { get; set; } = true;

    public bool PreventDuplicateSwitches { get; set; }

    public int DuplicateSwitchDelayMilliseconds { get; set; } = 500;

    public bool StartWithWindows { get; set; } = true;
}
