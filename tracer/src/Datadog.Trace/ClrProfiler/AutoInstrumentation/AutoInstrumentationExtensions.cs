// <copyright file="AutoInstrumentationExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation
{
    internal static class AutoInstrumentationExtensions
    {
        private const double ServerlessMaxWaitingFlushTime = 3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DisposeWithException(this Scope scope, Exception exception)
        {
            if (scope != null)
            {
                try
                {
                    if (exception != null)
                    {
                        scope.Span?.SetException(exception);
                    }
                }
                finally
                {
                    scope.Dispose();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ServerlessDispose(this Scope scope)
        {
            if (scope != null)
            {
                scope.Dispose();
                scope.Span.Context.TraceContext.CloseServerlessSpan();
                try
                {
                    // here we need a sync flush, since the lambda environment can be destroy after each invocation
                    // 3 seconds is enough to send payload to the extension (via localhost)
                    Tracer.Instance.TracerManager.AgentWriter.FlushTracesAsync().Wait(TimeSpan.FromSeconds(ServerlessMaxWaitingFlushTime));
                }
                catch (Exception ex)
                {
                    Serverless.Error("Could not flush to the extension", ex);
                }
            }
        }
    }
}
