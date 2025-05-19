// <copyright file="HangfireCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

internal static class HangfireCommon
{
    private const string Component = "Hangfire.Core";
    private const string Method = "OnPerformed";
    private const string HangfireServiceName = "Hangfire";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HangfireCommon));

    internal const string IntegrationName = nameof(Configuration.IntegrationId.Hangfire);
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.Hangfire;

    public static Scope CreateScope(Tracer tracer, string operationName, out HangfireTags tags, ISpanContext parentContext = null)
    {
        tags = null;
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || !tracer.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
        {
            // integration disabled, don't create a scope, skip this trace
            return null;
        }

        Scope scope = null;

        try
        {
            var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, HangfireServiceName);
            scope = tracer.StartActiveInternal(operationName, parent: parentContext, tags: tags, serviceName: serviceName);
            var span = scope.Span;
            // I refuse to add sample analytics rate, it's been deprecated no?
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            Log.Debug("A span was generated from {OperationName}", operationName);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error creating or populating scope.");
        }

        return scope;
    }

    // internal static IEnumerable<string> ExtractSpanProperties(Dictionary<string, string> telemetryData, string key)
    // {
    //     return telemetryData.TryGetValue(key, out var value) ? [value] : [];
    // }

    internal static IEnumerable<string> ExtractSpanProperties(Dictionary<string, string> carrier, string key)
    {
        if (carrier.TryGetValue(key, out var value))
        {
            return new string[] { value };
        }

        return Enumerable.Empty<string>();
    }

    internal static void SetStatusAndRecordException(ISpan span, Exception exception)
    {
        return;
    }

    internal static void InjectSpanProperties(IDictionary<string, string> jobParams, string key, string value)
    {
        jobParams[key] = value;
    }
}
