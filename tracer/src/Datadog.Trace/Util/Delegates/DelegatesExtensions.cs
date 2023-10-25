// <copyright file="DelegatesExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Util.Delegates;

internal static class DelegatesExtensions
{
    /// <summary>
    /// Instruments the Delegate by adding OnDelegateBegin, OnDelegateEnd, OnDelegateAsyncEnd, OnException callbacks
    /// </summary>
    /// <param name="target">Instance of the delegate to be instrumented</param>
    /// <param name="callbacks">Callbacks object/struct instance</param>
    /// <typeparam name="TDelegate">Type of the delegate to be instrumented</typeparam>
    /// <typeparam name="TCallbacks">Type of the callbacks object/struct</typeparam>
    /// <returns>A new instrumented Delegate of type TDelegate</returns>
    public static TDelegate Instrument<TDelegate, TCallbacks>(this TDelegate target, TCallbacks callbacks)
        where TDelegate : Delegate
        where TCallbacks : struct, ICallbacks
        => DelegateInstrumentation.Wrap<TDelegate, TCallbacks>(target, callbacks);

    /// <summary>
    /// Instruments the Delegate inside a `ValueWithType` ducktype struct by adding OnDelegateBegin, OnDelegateEnd, OnDelegateAsyncEnd, OnException callbacks
    /// </summary>
    /// <param name="target">Instance of the delegate to be instrumented inside of a `ValueWithType` ducktype struct</param>
    /// <param name="callbacks">Callbacks object/struct instance</param>
    /// <typeparam name="TDelegate">Type of the delegate to be instrumented</typeparam>
    /// <typeparam name="TCallbacks">Type of the callbacks object/struct</typeparam>
    /// <returns>A new instrumented Delegate of type TDelegate inside of a `ValueWithType` ducktype struct</returns>
    public static ValueWithType<TDelegate> Instrument<TDelegate, TCallbacks>(this ValueWithType<TDelegate> target, TCallbacks callbacks)
        where TDelegate : Delegate
        where TCallbacks : struct, ICallbacks
        => ValueWithType<TDelegate>.Create((TDelegate)DelegateInstrumentation.Wrap(target.Value, target.Type, callbacks), target.Type);
}
