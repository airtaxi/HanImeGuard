using HanImeGuard.Models;

namespace HanImeGuard.Services;

public sealed class IllustratorAutomationAdapter() : AdobeAutomationAdapterBase("Illustrator.Application", "HanImeGuard Illustrator STA")
{
    private const string SnapshotScript = """
(function () {
    function hashText(value) {
        var text = String(value || "");
        var hash = 2166136261;
        for (var index = 0; index < text.length; index++) {
            hash ^= text.charCodeAt(index);
            hash += (hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24);
        }
        return (hash >>> 0).toString(16);
    }

    function encodeValue(value) {
        return encodeURIComponent(String(value || ""));
    }

    function readValue(callback, fallback) {
        try {
            var value = callback();
            if (value !== undefined && value !== null && String(value).length > 0) return value;
        } catch (ignored) {}

        return fallback;
    }

    if (!app.documents || app.documents.length === 0) return "NO_DOCUMENT";

    var document = app.activeDocument;
    var documentKey = readValue(function () { return document.fullName.fsName; }, "");
    documentKey = readValue(function () { return document.name; }, documentKey);

    var lines = [encodeValue(documentKey)];

    function objectRecordIdentifier(target, fallback) {
        var identifier = readValue(function () { return target.uuid; }, "");
        identifier = readValue(function () { return target.id; }, identifier);
        if (!identifier) identifier = fallback;
        return identifier;
    }

    function visitLayers(layers, parentPath) {
        for (var layerIndex = 0; layerIndex < layers.length; layerIndex++) {
            try {
                var layer = layers[layerIndex];
                var layerPath = parentPath + "/" + layerIndex;
                var recordKey = "layer:" + objectRecordIdentifier(layer, layerPath);
                var name = readValue(function () { return layer.name; }, "");
                lines.push(encodeValue(recordKey) + "\t" + hashText(name) + "\t" + hashText("") + "\t" + encodeValue(name) + "\t\t0");
                visitLayers(layer.layers, layerPath);
            } catch (ignored) {}
        }
    }

    function visitTextFrames(textFrames) {
        for (var textFrameIndex = 0; textFrameIndex < textFrames.length; textFrameIndex++) {
            try {
                var textFrame = textFrames[textFrameIndex];
                var recordKey = "text:" + objectRecordIdentifier(textFrame, String(textFrameIndex));
                var name = readValue(function () { return textFrame.name; }, "");
                var text = readValue(function () { return textFrame.contents; }, "");
                lines.push(encodeValue(recordKey) + "\t" + hashText(name) + "\t" + hashText(text) + "\t" + encodeValue(name) + "\t" + encodeValue(text) + "\t1");
            } catch (ignored) {}
        }
    }

    visitLayers(document.layers, "");
    visitTextFrames(document.textFrames);
    return lines.join("\n");
}());
""";

    public override AdobeApplicationKind ApplicationKind => AdobeApplicationKind.Illustrator;

    protected override string GetSnapshotScript() => SnapshotScript;

    protected override object? ExecuteJavaScript(dynamic application, string script) => application.DoJavaScript(script);
}
