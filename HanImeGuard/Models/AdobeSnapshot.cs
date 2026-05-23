namespace HanImeGuard.Models;

public sealed record AdobeSnapshot(string DocumentKey, Dictionary<string, AdobeSnapshotRecord> Records);
