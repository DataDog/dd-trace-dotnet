// <copyright file="AsyncUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Util;

/*
 * This code based on:
 * https://www.ryadel.com/en/asyncutil-c-helper-class-async-method-sync-result-wait/
 *
 * WARNING: this class should be only used as a last resort for awaiting a Task in
 * a sync context.
 */

/// <summary>
/// Helper class to run async methods within a sync process.
/// </summary>
internal static class AsyncUtil
{
    private static readonly TaskFactory _taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

    /// <summary>
    /// Executes an async Task method which has a void return value synchronously
    /// USAGE: AsyncUtil.RunSync(() => AsyncMethod());
    /// </summary>
    /// <param name="task">Task method to execute</param>
    public static void RunSync(Func<Task> task)
        => _taskFactory
          .StartNew(task)
          .Unwrap()
          .GetAwaiter()
          .GetResult();

    /// <summary>
    /// Executes an async Task method which has a void return value synchronously
    /// USAGE: AsyncUtil.RunSync(() => AsyncMethod(), cancellationToken);
    /// </summary>
    /// <param name="task">Task method to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static void RunSync(Func<Task> task, CancellationToken cancellationToken)
        => _taskFactory
          .StartNew(task, cancellationToken)
          .Unwrap()
          .GetAwaiter()
          .GetResult();

    /// <summary>
    /// Executes an async Task[T] method which has a T return type synchronously
    /// USAGE: T result = AsyncUtil.RunSync(() => AsyncMethod[T]());
    /// </summary>
    /// <typeparam name="TResult">Return Type</typeparam>
    /// <param name="task">Task[T] method to execute</param>
    /// <returns>TResult result</returns>
    public static TResult RunSync<TResult>(Func<Task<TResult>> task)
        => _taskFactory
          .StartNew(task)
          .Unwrap()
          .GetAwaiter()
          .GetResult();

    /// <summary>
    /// Executes an async Task[T] method which has a T return type synchronously
    /// USAGE: T result = AsyncUtil.RunSync(() => AsyncMethod[T](), cancellationToken);
    /// </summary>
    /// <typeparam name="TResult">Return Type</typeparam>
    /// <param name="task">Task[T] method to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TResult result</returns>
    public static TResult RunSync<TResult>(Func<Task<TResult>> task, CancellationToken cancellationToken)
        => _taskFactory
          .StartNew(task, cancellationToken)
          .Unwrap()
          .GetAwaiter()
          .GetResult();
}
