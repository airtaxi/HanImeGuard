using HanImeGuard.Models;

namespace HanImeGuard.Services;

public sealed class PhotoshopAutomationAdapter() : AdobeAutomationAdapterBase("Photoshop.Application", "HanImeGuard Photoshop STA")
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
    documentKey = readValue(function () { return document.id; }, documentKey);
    documentKey = readValue(function () { return document.name; }, documentKey);

    var lines = [encodeValue(documentKey)];

    function layerRecordKey(layer, layerPath) {
        var identifier = readValue(function () { return layer.id; }, "");
        if (!identifier) identifier = layerPath;
        return "layer:" + identifier;
    }

    function layerText(layer) {
        try {
            if (layer.typename === "ArtLayer" && layer.kind === LayerKind.TEXT && layer.textItem) return layer.textItem.contents;
        } catch (ignored) {}

        return "";
    }

    function isTextLayer(layer) {
        try {
            return layer.typename === "ArtLayer" && layer.kind === LayerKind.TEXT && layer.textItem;
        } catch (ignored) {}

        return false;
    }

    function visitLayers(layers, parentPath) {
        for (var layerIndex = 0; layerIndex < layers.length; layerIndex++) {
            try {
                var layer = layers[layerIndex];
                var layerPath = parentPath + "/" + layerIndex;
                var name = readValue(function () { return layer.name; }, "");
                var text = layerText(layer);
                var textLayer = isTextLayer(layer);
                lines.push(encodeValue(layerRecordKey(layer, layerPath)) + "\t" + hashText(name) + "\t" + hashText(text) + "\t" + encodeValue(name) + "\t" + encodeValue(text) + "\t" + (textLayer ? "1" : "0"));

                if (layer.typename === "LayerSet") visitLayers(layer.layers, layerPath);
            } catch (ignored) {}
        }
    }

    visitLayers(document.layers, "");
    return lines.join("\n");
}());
""";

    public override AdobeApplicationKind ApplicationKind => AdobeApplicationKind.Photoshop;

    protected override string GetSnapshotScript() => SnapshotScript;

    protected override object? ExecuteJavaScript(dynamic application, string script) => application.DoJavaScript(script, null, 2);
}
