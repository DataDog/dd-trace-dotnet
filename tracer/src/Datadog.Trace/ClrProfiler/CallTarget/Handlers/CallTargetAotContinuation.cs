// <copyright file="CallTargetAotContinuation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

/// <summary>
/// Holds the factory of the continuation generator a NativeAOT CallTarget registry provides for a Task&lt;T&gt; or
/// ValueTask&lt;T&gt; return type, which <see cref="EndMethodHandler{TIntegration, TTarget, TReturn}"/> otherwise creates
/// through <see cref="Type.MakeGenericType"/>.
/// </summary>
/// <remarks>
/// A factory rather than an instance: the generator is created, and its static constructor binds
/// <c>OnAsyncMethodEnd</c>, from the end handler's static constructor, as in the dynamic path.
/// </remarks>
/// <typeparam name="TIntegration">Integration type</typeparam>
/// <typeparam name="TTarget">Target type</typeparam>
/// <typeparam name="TReturn">Return type</typeparam>
internal static class CallTargetAotContinuation<TIntegration, TTarget, TReturn>
{
    private static Func<ContinuationGenerator<TTarget, TReturn>>? _factory;
    private static volatile bool _registered;

    internal static void Register(Func<ContinuationGenerator<TTarget, TReturn>>? factory)
    {
        _factory = factory;
        _registered = true;
    }

    internal static bool TryGet(out Func<ContinuationGenerator<TTarget, TReturn>>? factory)
    {
        factory = _registered ? _factory : null;
        return _registered;
    }
}
