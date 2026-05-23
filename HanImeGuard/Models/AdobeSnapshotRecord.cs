namespace HanImeGuard.Models;

public sealed record AdobeSnapshotRecord(string RecordKey, string LayerName, string NameHash, string TextContent, string TextHash, bool IsTextRecord);
