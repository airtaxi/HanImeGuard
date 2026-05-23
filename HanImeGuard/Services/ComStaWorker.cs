using System.Collections.Concurrent;

namespace HanImeGuard.Services;

public sealed class ComStaWorker : IDisposable
{
    private readonly BlockingCollection<Action> _workItems = [];
    private readonly Thread _workerThread;
    private bool _disposed;

    public ComStaWorker(string threadName)
    {
        _workerThread = new Thread(Run)
        {
            IsBackground = true,
            Name = threadName
        };
        _workerThread.SetApartmentState(ApartmentState.STA);
        _workerThread.Start();
    }

    public Task<TValue> InvokeAsync<TValue>(Func<TValue> callback)
    {
        if (_disposed) return Task.FromException<TValue>(new ObjectDisposedException(nameof(ComStaWorker)));

        var taskCompletionSource = new TaskCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _workItems.Add(CompleteTask);
        }
        catch (InvalidOperationException exception) { taskCompletionSource.SetException(exception); }

        return taskCompletionSource.Task;

        void CompleteTask()
        {
            try { taskCompletionSource.SetResult(callback()); }
            catch (Exception exception) { taskCompletionSource.SetException(exception); }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _workItems.CompleteAdding();
        if (!_workerThread.Join(TimeSpan.FromSeconds(1))) _workerThread.Interrupt();
        _workItems.Dispose();
    }

    private void Run()
    {
        try
        {
            foreach (var workItem in _workItems.GetConsumingEnumerable()) workItem();
        }
        catch (ThreadInterruptedException) { }
    }
}
