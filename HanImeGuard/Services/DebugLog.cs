using System.Diagnostics;

namespace HanImeGuard.Services;

public static class DebugLog
{
    [Conditional("DEBUG")]
    public static void WriteLine(string message) => Debug.WriteLine($"[HanImeGuard] {DateTimeOffset.Now:HH:mm:ss.fff} {message}");
}
