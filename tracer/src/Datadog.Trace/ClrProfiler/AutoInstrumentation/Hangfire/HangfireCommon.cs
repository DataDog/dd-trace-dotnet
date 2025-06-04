// <copyright file="HangfireCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

internal static class HangfireCommon
{
    private const string Component = "Hangfire.Core";
    private const string HangfireServiceName = "Hangfire";
    private const string HangfireType = "Hangfire";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HangfireCommon));

    internal const string IntegrationName = nameof(Configuration.IntegrationId.Hangfire);
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.Hangfire;

    public static Scope CreateScope(Tracer tracer, string operationName, HangfireTags tags, ISpanContext parentContext = null, bool finishOnClose = true)
    {
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || !tracer.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
        {
            // integration disabled, don't create a scope, skip this trace
            return null;
        }

        Scope scope = null;

        try
        {
            var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, HangfireServiceName);
            scope = tracer.StartActiveInternal(operationName, parent: parentContext, serviceName: serviceName, tags: tags);
            var span = scope.Span;
            span.Type = HangfireType;
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error creating or populating scope.");
        }

        return scope;
    }

    internal static IEnumerable<string> ExtractSpanProperties(Dictionary<string, string> carrier, string key)
    {
        if (carrier.TryGetValue(key, out var value))
        {
            return new string[] { value };
        }

        return Enumerable.Empty<string>();
    }

    internal static void SetStatusAndRecordException(Scope scope, Exception exception)
    {
        scope.Span.SetException(exception);
    }

    internal static void InjectSpanProperties(IDictionary<string, string> jobParams, string key, string value)
    {
        jobParams[key] = value;
    }

    internal static void PopulatePerformSpanTags(Scope scope, IPerformingContextProxy performingContext, IPerformContextProxy performContext)
    {
        if (performContext is null || performingContext is null)
        {
            Log.Debug("Issue with populating the onPerform Span due to the performContext: {PerformContext} or CreatingContext: {PerformingContext} being null", performContext, performingContext);
            return;
        }

        scope.Span.ResourceName = HangfireConstants.ResourceNamePrefix + performContext.Job;
        scope.Span.SetTag(HangfireConstants.JobIdTag, performContext.JobId);
        scope.Span.SetTag(HangfireConstants.JobCreatedAtTag, performContext.BackgroundJob.CreatedAt.ToString("O"));
    }
}
