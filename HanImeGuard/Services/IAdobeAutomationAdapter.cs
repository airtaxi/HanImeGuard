using HanImeGuard.Models;

namespace HanImeGuard.Services;

public interface IAdobeAutomationAdapter : IDisposable
{
    AdobeApplicationKind ApplicationKind { get; }

    Task<AdobeSnapshot?> CaptureSnapshotAsync();
}
