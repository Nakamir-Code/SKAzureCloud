#if WINDOWS_UWP
// <copyright file="DispatcherTaskExtensions.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Common;

using System;
using System.Threading.Tasks;
using Windows.UI.Core;

public static class DispatcherTaskExtensions
{
    public static async Task<T> RunTaskAsync<T>(this CoreDispatcher dispatcher,
        Func<Task<T>> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
    {
        TaskCompletionSource<T> taskCompletionSource = new();
        await dispatcher.RunAsync(priority, async () =>
        {
            try
            {
                taskCompletionSource.SetResult(await func());
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }
        });
        return await taskCompletionSource.Task;
    }

    // There is no TaskCompletionSource<void> so we use a bool that we throw away.
    public static async Task RunTaskAsync(this CoreDispatcher dispatcher,
        Func<Task> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal) =>
            await RunTaskAsync(dispatcher, async () => { await func(); return false; }, priority);
}
#endif
