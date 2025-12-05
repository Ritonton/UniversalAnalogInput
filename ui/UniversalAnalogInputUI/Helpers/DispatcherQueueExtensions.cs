using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace UniversalAnalogInputUI.Helpers;

/// <summary>Adds awaitable enqueue helpers for DispatcherQueue.</summary>
public static class DispatcherQueueExtensions
{
    /// <summary>Enqueues an action on the UI thread and awaits completion.</summary>
    public static Task EnqueueAsync(this DispatcherQueue dispatcher, Action callback, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        var tcs = new TaskCompletionSource<bool>();

        bool enqueued = dispatcher.TryEnqueue(priority, () =>
        {
            try
            {
                callback();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue action on dispatcher"));
        }

        return tcs.Task;
    }

    /// <summary>Enqueues a function on the UI thread and awaits its result.</summary>
    public static Task<T> EnqueueAsync<T>(this DispatcherQueue dispatcher, Func<T> callback, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        var tcs = new TaskCompletionSource<T>();

        bool enqueued = dispatcher.TryEnqueue(priority, () =>
        {
            try
            {
                var result = callback();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue func on dispatcher"));
        }

        return tcs.Task;
    }

    /// <summary>Enqueues an async function on the UI thread and awaits completion.</summary>
    public static Task EnqueueAsync(this DispatcherQueue dispatcher, Func<Task> asyncCallback, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        var tcs = new TaskCompletionSource<bool>();

        bool enqueued = dispatcher.TryEnqueue(priority, async () =>
        {
            try
            {
                await asyncCallback();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue async action on dispatcher"));
        }

        return tcs.Task;
    }

    /// <summary>Enqueues an async function on the UI thread and awaits its result.</summary>
    public static Task<T> EnqueueAsync<T>(this DispatcherQueue dispatcher, Func<Task<T>> asyncCallback, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        var tcs = new TaskCompletionSource<T>();

        bool enqueued = dispatcher.TryEnqueue(priority, async () =>
        {
            try
            {
                var result = await asyncCallback();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue async func on dispatcher"));
        }

        return tcs.Task;
    }
}
