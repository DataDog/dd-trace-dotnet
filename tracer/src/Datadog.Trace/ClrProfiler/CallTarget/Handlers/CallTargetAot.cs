// <copyright file="CallTargetAot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

/// <summary>
/// Holds the integration callback a NativeAOT CallTarget registry provides for one integration and one handler
/// delegate shape. The handler static constructors consult it before falling back to <see cref="IntegrationMapper"/>,
/// which needs dynamic code.
/// </summary>
/// <remarks>
/// The registry fills the holder from the generic context of the instrumented method, so generic targets get their
/// own closed holders without constructing types at runtime. It must happen before the first call reaches the
/// handler: the registry inserts the registration at the entry of every instrumented method.
/// </remarks>
/// <typeparam name="TIntegration">Integration type</typeparam>
/// <typeparam name="TDelegate">Handler delegate type</typeparam>
internal static class CallTargetAot<TIntegration, TDelegate>
    where TDelegate : Delegate
{
    private static TDelegate? _callback;
    private static bool _preserveContext;
    private static string? _failure;
    private static volatile bool _registered;

    /// <summary>
    /// Registers the callback. A null callback records that the integration has no method for this shape.
    /// </summary>
    internal static void Register(TDelegate? callback, bool preserveContext = false)
    {
        _callback = callback;
        _preserveContext = preserveContext;
        _failure = null;
        _registered = true;
    }

    /// <summary>
    /// Registers that the integration method can't be bound to this shape. The handler fails as it does when
    /// <see cref="IntegrationMapper"/> throws, which disables the integration for the target.
    /// </summary>
    internal static void RegisterFailure(string message)
    {
        _callback = null;
        _preserveContext = false;
        _failure = message;
        _registered = true;
    }

    internal static bool TryGet(out TDelegate? callback, out bool preserveContext)
    {
        if (!_registered)
        {
            callback = null;
            preserveContext = false;
            return false;
        }

        if (_failure is { } failure)
        {
            throw new ArgumentException(failure);
        }

        callback = _callback;
        preserveContext = _preserveContext;
        return true;
    }
}
