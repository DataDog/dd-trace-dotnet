// <copyright file="RuntimeAsyncHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

// net6.0 is the only Datadog.Trace asset a .NET 10+ process can load, and Runtime-async is a .NET 10+ feature
#if NET6_0_OR_GREATER // NET 10+ really

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

/// <summary>
/// Helpers shared by the .NET 11 runtime-async end-method handlers.
/// </summary>
internal static class RuntimeAsyncHelper
{
    /// <summary>
    /// Whether a callback is itself async. Such a callback cannot be used on a runtime-async
    /// target, because the epilog is a finally block and the spec forbids suspending there.
    /// </summary>
    internal static bool IsTaskReturning(Type returnType)
        => returnType == typeof(Task) || (returnType.IsGenericType && typeof(Task).IsAssignableFrom(returnType));

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
}
#endif
