// <copyright file="StackTraceLeakIntegrationCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.StackTraceLeak;

#nullable enable
internal static class StackTraceLeakIntegrationCommon
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StackTraceLeakIntegrationCommon));

    internal static CallTargetState OnExceptionLeak(IntegrationId integrationId, Exception exception)
    {
        if (!Tracer.Instance.Settings.IsIntegrationEnabled(integrationId))
        {
            return CallTargetState.GetDefault();
        }

        try
        {
            if (exception is not null)
            {
                return new CallTargetState(IastModule.OnStackTraceLeak(exception, integrationId).SingleSpan);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error in {nameof(OnExceptionLeak)}.");
        }

        return CallTargetState.GetDefault();
    }
}
