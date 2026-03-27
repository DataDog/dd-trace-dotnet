// <copyright file="CallTargetAotHandlerRegistration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Stores the generated adapter metadata used to materialize a typed CallTarget delegate in AOT mode.
/// </summary>
internal readonly struct CallTargetAotHandlerRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotHandlerRegistration"/> struct.
    /// </summary>
    /// <param name="method">The generated adapter method, when one exists.</param>
    /// <param name="hasHandler">Whether the integration defines a handler for this registration.</param>
    /// <param name="preserveContext">Whether async continuations must preserve the captured synchronization context.</param>
    /// <param name="isAsyncCallback">Whether the generated async callback itself returns a <see cref="System.Threading.Tasks.Task"/>.</param>
    public CallTargetAotHandlerRegistration(MethodInfo? method, bool hasHandler = true, bool preserveContext = false, bool isAsyncCallback = false)
    {
        Method = method;
        HasHandler = hasHandler;
        PreserveContext = preserveContext;
        IsAsyncCallback = isAsyncCallback;
    }

    /// <summary>
    /// Gets the generated adapter method.
    /// </summary>
    public MethodInfo? Method { get; }

    /// <summary>
    /// Gets a value indicating whether the integration defines a callback for this binding.
    /// </summary>
    public bool HasHandler { get; }

    /// <summary>
    /// Gets a value indicating whether async callbacks should preserve the ambient synchronization context.
    /// </summary>
    public bool PreserveContext { get; }

    /// <summary>
    /// Gets a value indicating whether the generated callback returns a <see cref="System.Threading.Tasks.Task"/>.
    /// </summary>
    public bool IsAsyncCallback { get; }
}
