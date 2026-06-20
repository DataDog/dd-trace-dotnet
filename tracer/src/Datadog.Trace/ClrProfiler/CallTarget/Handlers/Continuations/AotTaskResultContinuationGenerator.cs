// <copyright file="AotTaskResultContinuationGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

/// <summary>
/// Executes typed <see cref="System.Threading.Tasks.Task{TResult}"/> async-end continuations in AOT mode by binding
/// directly to the generated continuation wrapper registered in <see cref="CallTargetAotEngine"/>.
/// </summary>
internal sealed class AotTaskResultContinuationGenerator<TIntegration, TTarget, TReturn> : ContinuationGenerator<TTarget, TReturn>
{
    private static readonly Executor Resolver;

    static AotTaskResultContinuationGenerator()
    {
        var registration = CallTargetAotEngine.GetAsyncTaskResultContinuationRegistration(typeof(TIntegration), typeof(TTarget), typeof(TReturn));
        if (!registration.HasHandler || registration.Method is null)
        {
            Resolver = static (TTarget? _, TReturn? returnValue, Exception? __, CallTargetState ignoredState) => returnValue;
            return;
        }

        Resolver = CallTargetAotEngine.CreateAsyncTaskResultContinuationDelegate<Executor>(typeof(TIntegration), typeof(TTarget), typeof(TReturn));
    }

    private delegate TReturn? Executor(TTarget? instance, TReturn? returnValue, Exception? exception, CallTargetState state);

    /// <inheritdoc />
    public override TReturn? SetContinuation(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        return Resolver(instance, returnValue, exception, state);
    }
}
