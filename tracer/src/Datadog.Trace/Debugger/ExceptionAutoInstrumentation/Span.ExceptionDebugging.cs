// <copyright file="Span.ExceptionDebugging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

namespace Datadog.Trace
{
    /// <summary>
    /// Exception Debugging V1 support 5xx errors. Thus, we follow the lifecycle of the root span.
    /// The duration and async context in which the root span is active (between RootSpanStarted -> SpanFinished) is where we allow the capturing of snapshots,
    /// meaning - the methods that execute between this time period and in the same async context - are the ones collected.
    /// The marking of the root span as error is where the Exception Debugging kickoff for analyzing call stack and reporting snapshots for already tracked exceptions.
    /// </summary>
    internal partial class Span
    {
        private ShadowStackTree _shadowStack;

        internal void RootSpanStarted()
        {
            ExceptionDebugging.TryBeginRequest(out _shadowStack);
        }

        private void SpanIsMarkedWithAnError(Exception exception)
        {
            if (IsRootSpan)
            {
                ExceptionDebugging.Report(this, exception);
            }
        }

        private void SpanFinished()
        {
            if (IsRootSpan)
            {
                ExceptionDebugging.EndRequest(_shadowStack);
            }
        }
    }
}
