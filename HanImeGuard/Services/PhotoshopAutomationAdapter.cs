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

    function typeIdentifier(value) {
        return stringIDToTypeID(value);
    }

    function charIdentifier(value) {
        return charIDToTypeID(value);
    }

    function descriptorHasValue(descriptor, key) {
        try {
            return descriptor && descriptor.hasKey(key);
        } catch (ignored) {}

        return false;
    }

    function descriptorStringValue(descriptor, key) {
        try {
            if (descriptorHasValue(descriptor, key)) return descriptor.getString(key);
        } catch (ignored) {}

        return "";
    }

    function descriptorIntegerValue(descriptor, key) {
        try {
            if (descriptorHasValue(descriptor, key)) return descriptor.getInteger(key);
        } catch (ignored) {}

        return "";
    }

    function activeLayerDescriptor() {
        var reference = new ActionReference();
        reference.putEnumerated(charIdentifier("Lyr "), charIdentifier("Ordn"), charIdentifier("Trgt"));
        return executeActionGet(reference);
    }

    function descriptorLayerText(descriptor, allowActiveLayerFallback) {
        try {
            var textKeyIdentifier = typeIdentifier("textKey");
            if (descriptorHasValue(descriptor, textKeyIdentifier)) {
                var textDescriptor = descriptor.getObjectValue(textKeyIdentifier);
                var textIdentifier = charIdentifier("Txt ");
                if (descriptorHasValue(textDescriptor, textIdentifier)) return textDescriptor.getString(textIdentifier);
            }
        } catch (ignored) {}

        if (!allowActiveLayerFallback) return "";

        return readValue(function () {
            var layer = app.activeDocument.activeLayer;
            if (layer.typename === "ArtLayer" && layer.kind === LayerKind.TEXT && layer.textItem) return layer.textItem.contents;
            return "";
        }, "");
    }

    function descriptorIsTextLayer(descriptor, allowActiveLayerFallback) {
        try {
            if (descriptorHasValue(descriptor, typeIdentifier("textKey"))) return true;
        } catch (ignored) {}

        if (!allowActiveLayerFallback) return false;

        return readValue(function () {
            var layer = app.activeDocument.activeLayer;
            return layer.typename === "ArtLayer" && layer.kind === LayerKind.TEXT && layer.textItem;
        }, false) === true;
    }

    function layerDescriptorByIdentifier(layerIdentifier) {
        try {
            if (!layerIdentifier) return null;

            var reference = new ActionReference();
            reference.putIdentifier(charIdentifier("Lyr "), Number(layerIdentifier));
            return executeActionGet(reference);
        } catch (ignored) {}

        return null;
    }

    function appendLayerRecord(layerDescriptor, fallbackLayerIdentifier, allowActiveLayerFallback) {
        if (!layerDescriptor) return "";

        var layerIdentifier = descriptorIntegerValue(layerDescriptor, typeIdentifier("layerID"));
        if (!layerIdentifier) layerIdentifier = fallbackLayerIdentifier;
        if (!layerIdentifier && allowActiveLayerFallback) layerIdentifier = readValue(function () { return app.activeDocument.activeLayer.id; }, "");
        if (!layerIdentifier) return "";

        var layerName = descriptorStringValue(layerDescriptor, charIdentifier("Nm  "));
        if (!layerName && allowActiveLayerFallback) layerName = readValue(function () { return app.activeDocument.activeLayer.name; }, layerName);

        var layerText = descriptorLayerText(layerDescriptor, allowActiveLayerFallback);
        var textLayer = descriptorIsTextLayer(layerDescriptor, allowActiveLayerFallback);
        lines.push(encodeValue("layer:" + layerIdentifier) + "\t" + hashText(layerName) + "\t" + hashText(layerText) + "\t" + encodeValue(layerName) + "\t" + encodeValue(layerText) + "\t" + (textLayer ? "1" : "0"));
        return String(layerIdentifier);
    }

    var layerDescriptor = activeLayerDescriptor();
    var currentLayerIdentifier = appendLayerRecord(layerDescriptor, "", true);
    var previousDocumentKey = readValue(function () { return $.global.HanImeGuardPreviousDocumentKey; }, "");
    var previousLayerIdentifier = readValue(function () { return $.global.HanImeGuardPreviousLayerIdentifier; }, "");

    if (previousDocumentKey === documentKey && previousLayerIdentifier && previousLayerIdentifier !== currentLayerIdentifier) {
        appendLayerRecord(layerDescriptorByIdentifier(previousLayerIdentifier), previousLayerIdentifier, false);
    }

    $.global.HanImeGuardPreviousDocumentKey = documentKey;
    $.global.HanImeGuardPreviousLayerIdentifier = currentLayerIdentifier;
    return lines.join("\n");
}());
""";

    public override AdobeApplicationKind ApplicationKind => AdobeApplicationKind.Photoshop;

    protected override string GetSnapshotScript() => SnapshotScript;

    protected override object? ExecuteJavaScript(dynamic application, string script) => application.DoJavaScript(script, null, 2);
}
