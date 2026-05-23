using HanImeGuard.Models;
using System.Runtime.InteropServices;
using Windows.Win32;

namespace HanImeGuard.Services;

public abstract class AdobeAutomationAdapterBase(string programIdentifier, string workerName) : IAdobeAutomationAdapter
{
    private readonly ComStaWorker _worker = new(workerName);
    private object? _application;
    private bool _disposed;

    public abstract AdobeApplicationKind ApplicationKind { get; }

    public async Task<AdobeSnapshot?> CaptureSnapshotAsync()
    {
        try
        {
            var scriptResult = await ExecuteJavaScriptAsync(GetSnapshotScript());
            return AdobeSnapshotParser.Parse(scriptResult);
        }
        catch (Exception exception)
        {
            DebugLog.WriteLine($"Adobe snapshot capture exception. application={ApplicationKind}, exception={exception.GetType().Name}: {exception.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try { _worker.InvokeAsync(ReleaseApplication).Wait(TimeSpan.FromSeconds(1)); }
        catch (Exception) { }
        _worker.Dispose();
        _disposed = true;
    }

    protected abstract string GetSnapshotScript();

    protected abstract object? ExecuteJavaScript(dynamic application, string script);

    private string ExecuteJavaScript(string script)
    {
        try
        {
            var application = GetApplication();
            if (application is null) return string.Empty;

            var result = ExecuteJavaScript(application, script);
            return result?.ToString() ?? string.Empty;
        }
        catch (Exception)
        {
            ReleaseApplication();
            throw;
        }
    }

    private Task<string> ExecuteJavaScriptAsync(string script) => _worker.InvokeAsync(() => ExecuteJavaScript(script));

    private dynamic? GetApplication()
    {
        if (_application is not null) return _application;

        _application = GetActiveComObject();
        return _application;
    }

    private unsafe object? GetActiveComObject()
    {
        var classIdentifier = Guid.Empty;
        fixed (char* programIdentifierPointer = programIdentifier)
        {
            var classIdentifierResult = PInvoke.CLSIDFromProgID(programIdentifierPointer, &classIdentifier);
            if (classIdentifierResult.Failed) return null;
        }

        var activeObjectResult = PInvoke.GetActiveObject(&classIdentifier, null, out var activeObject);
        if (activeObjectResult.Failed) return null;

        return activeObject;
    }

    private bool ReleaseApplication()
    {
        if (_application is not null && Marshal.IsComObject(_application)) Marshal.ReleaseComObject(_application);
        _application = null;
        return true;
    }
}
