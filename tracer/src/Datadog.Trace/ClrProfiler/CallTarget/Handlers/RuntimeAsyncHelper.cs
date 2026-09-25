// <copyright file="RuntimeAsyncHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

// net6.0 is the only Datadog.Trace asset a .NET 10+ process can load, and Runtime-async is a .NET 10+ feature
#if NET6_0_OR_GREATER // NET 10+ really

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

/// <summary>
/// Helpers shared by the .NET 11 runtime-async end-method handlers.
/// </summary>
internal static class RuntimeAsyncHelper
{
    /// <summary>
    /// Builds an already-completed value of a declared non-generic Task or ValueTask return type.
    /// </summary>
    /// <remarks>
    /// A runtime-async body never materialises the task it declares, but an integration's
    /// OnMethodEnd is written against the declared signature and expects one. By the time the
    /// epilog runs the operation genuinely has completed, so a completed value is accurate.
    /// Both <see cref="Task.CompletedTask"/> and <c>default(ValueTask)</c> are allocation-free.
    /// </remarks>
    /// <typeparam name="TDeclaredReturn">Task or ValueTask</typeparam>
    /// <returns>A completed instance, or default if the type is not recognised</returns>
    internal static TDeclaredReturn? CreateCompleted<TDeclaredReturn>()
    {
        if (typeof(TDeclaredReturn) == typeof(Task))
        {
            var completed = Task.CompletedTask;
            return Unsafe.As<Task, TDeclaredReturn>(ref completed);
        }

        // default(ValueTask) is already a successfully completed ValueTask.
        return default;
    }

    /// <summary>
    /// Wraps an unwrapped result back into the declared Task&lt;T&gt; or ValueTask&lt;T&gt;.
    /// </summary>
    /// <typeparam name="TResult">The unwrapped result type</typeparam>
    /// <typeparam name="TDeclaredReturn">Task&lt;TResult&gt; or ValueTask&lt;TResult&gt;</typeparam>
    /// <param name="result">The value the body returned</param>
    /// <returns>A completed instance carrying <paramref name="result"/></returns>
    internal static TDeclaredReturn? CreateCompletedFromResult<TResult, TDeclaredReturn>(TResult? result)
    {
        if (typeof(TDeclaredReturn) == typeof(Task<TResult>))
        {
            var completed = Task.FromResult(result!);
            return Unsafe.As<Task<TResult>, TDeclaredReturn>(ref completed);
        }

        if (typeof(TDeclaredReturn) == typeof(ValueTask<TResult>))
        {
            var completed = new ValueTask<TResult>(result!);
            return Unsafe.As<ValueTask<TResult>, TDeclaredReturn>(ref completed);
        }

        return default;
    }

    /// <summary>
    /// The inverse of <see cref="CreateCompletedFromResult{TResult, TDeclaredReturn}"/>: recovers the
    /// unwrapped result from a declared Task&lt;T&gt;/ValueTask&lt;T&gt; that has <em>already</em>
    /// completed successfully.
    /// </summary>
    /// <remarks>
    /// This is what lets an <c>OnMethodEnd</c> substitution be honoured on a runtime-async target.
    /// A runtime-async body returns the unwrapped value and the runtime builds the task from it, so
    /// the only way to act on a replacement task is to take its result back out - and the only way
    /// to do that without suspending (forbidden in the epilog's finally) or blocking is if it is
    /// already complete. A still-running, faulted or cancelled replacement returns false; the caller
    /// reports that rather than silently dropping it.
    /// </remarks>
    /// <typeparam name="TResult">The unwrapped result type</typeparam>
    /// <typeparam name="TDeclaredReturn">Task&lt;TResult&gt; or ValueTask&lt;TResult&gt;</typeparam>
    /// <param name="declared">The value OnMethodEnd returned</param>
    /// <param name="result">The unwrapped result, when this returns true</param>
    /// <returns>Whether the result could be recovered without waiting</returns>
    internal static bool TryGetCompletedResult<TResult, TDeclaredReturn>(TDeclaredReturn? declared, out TResult? result)
    {
        if (typeof(TDeclaredReturn) == typeof(Task<TResult>))
        {
            if (declared is not null)
            {
                var task = Unsafe.As<TDeclaredReturn, Task<TResult>>(ref declared);
                if (task.IsCompletedSuccessfully)
                {
                    result = task.Result;
                    return true;
                }
            }
        }
        else if (typeof(TDeclaredReturn) == typeof(ValueTask<TResult>))
        {
            var valueTask = Unsafe.As<TDeclaredReturn, ValueTask<TResult>>(ref declared!);
            if (valueTask.IsCompletedSuccessfully)
            {
                result = valueTask.Result;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Whether OnMethodEnd handed back the same value it was given, meaning it did not attempt a
    /// substitution.
    /// </summary>
    /// <remarks>
    /// The default comparer does the right thing for both shapes: <see cref="Task"/> does not
    /// override Equals, so it compares by reference against the <see cref="Task.CompletedTask"/>
    /// singleton, and <c>ValueTask</c> is <c>IEquatable</c>.
    /// </remarks>
    /// <typeparam name="TDeclaredReturn">Task or ValueTask</typeparam>
    /// <param name="returned">What OnMethodEnd returned</param>
    /// <param name="original">What it was handed</param>
    /// <returns>True when nothing was substituted</returns>
    internal static bool IsUnchanged<TDeclaredReturn>(TDeclaredReturn? returned, TDeclaredReturn? original)
        => EqualityComparer<TDeclaredReturn?>.Default.Equals(returned, original);
}
#endif
