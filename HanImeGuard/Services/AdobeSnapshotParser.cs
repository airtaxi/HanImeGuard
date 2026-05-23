using HanImeGuard.Models;

namespace HanImeGuard.Services;

public static class AdobeSnapshotParser
{
    public static AdobeSnapshot? Parse(string scriptResult)
    {
        if (string.IsNullOrWhiteSpace(scriptResult)) return null;

        var normalizedResult = scriptResult.Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.Equals(normalizedResult.Trim(), "NO_DOCUMENT", StringComparison.OrdinalIgnoreCase)) return null;

        var lines = normalizedResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;

        var documentKey = Uri.UnescapeDataString(lines[0].Trim());
        var records = new Dictionary<string, AdobeSnapshotRecord>(StringComparer.Ordinal);

        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            var parts = lines[lineIndex].Split('\t');
            if (parts.Length < 3) continue;

            var recordKey = Uri.UnescapeDataString(parts[0]);
            var layerName = parts.Length > 3 ? Uri.UnescapeDataString(parts[3]) : string.Empty;
            var textContent = parts.Length > 4 ? Uri.UnescapeDataString(parts[4]) : string.Empty;
            var isTextRecord = parts.Length > 5 && string.Equals(parts[5], "1", StringComparison.Ordinal);
            var record = new AdobeSnapshotRecord(recordKey, layerName, parts[1], textContent, parts[2], isTextRecord);
            records[recordKey] = record;
        }

        return new AdobeSnapshot(documentKey, records);
    }
}
