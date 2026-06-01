using System.Collections.Concurrent;

namespace TwincatMcpServer.TwinCat;

internal sealed class StaComDispatcher : IDisposable
{
    private readonly BlockingCollection<WorkItem> _queue = new();
    private readonly Thread _thread;

    public StaComDispatcher()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "TwinCAT XAE COM STA"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<T> work)
    {
        TaskCompletionSource<T> typedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(new WorkItem(
            () => typedSource.SetResult(work()),
            ex => typedSource.SetException(ex)));
        return typedSource.Task;
    }

    public Task InvokeAsync(Action work)
    {
        TaskCompletionSource typedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(new WorkItem(
            () =>
            {
                work();
                typedSource.SetResult();
            },
            ex => typedSource.SetException(ex)));
        return typedSource.Task;
    }

    private void Run()
    {
        using IDisposable filter = ComMessageFilter.Register();

        foreach (WorkItem item in _queue.GetConsumingEnumerable())
        {
            try
            {
                item.Execute();
            }
            catch (Exception ex)
            {
                item.Fail(ex);
            }
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
        _queue.Dispose();
    }

    private sealed record WorkItem(Action Execute, Action<Exception> Fail);
}
