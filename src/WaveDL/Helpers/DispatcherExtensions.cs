using Microsoft.UI.Dispatching;

namespace WaveDL.Helpers;

public static class DispatcherExtensions
{
    /// <summary>Runs <paramref name="action"/> on the dispatcher thread, awaiting completion.</summary>
    public static Task EnqueueAsync(this DispatcherQueue dispatcher, Action action)
    {
        if (dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        var queued = dispatcher.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!queued)
        {
            tcs.SetException(new InvalidOperationException("Impossible de planifier l'action sur le thread UI."));
        }

        return tcs.Task;
    }

    public static void Post(this DispatcherQueue dispatcher, Action action)
    {
        if (dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            dispatcher.TryEnqueue(() => action());
        }
    }
}
