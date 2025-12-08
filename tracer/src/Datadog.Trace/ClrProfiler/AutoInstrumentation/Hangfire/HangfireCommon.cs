// <copyright file="HangfireCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

internal static class HangfireCommon
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HangfireCommon));

    internal const string IntegrationName = nameof(IntegrationId.Hangfire);
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.Hangfire;
    internal const string IntegrationType = "hangfire";

    public static Scope? CreateScope(Tracer tracer, HangfireTags tags, IPerformingContextProxy performingContext, ISpanContext? parentContext = null, bool finishOnClose = true)
    {
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
        {
            // integration disabled, don't create a scope, skip this trace
            return null;
        }

        Scope? scope = null;

        try
        {
            scope = tracer.StartActiveInternal(HangfireConstants.OnPerformOperation, parent: parentContext, tags: tags);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            scope.Span.Type = IntegrationType;
            scope.Span.ResourceName = HangfireConstants.ResourceNamePrefix + performingContext.Job;
            tags.JobId = performingContext.JobId;
            tags.CreatedAt = performingContext.BackgroundJob.CreatedAt.ToString("O");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to create Hangfire span.");
        }

        return scope;
    }

    internal static void SetStatusAndRecordException(Scope? scope, Exception exception)
    {
        scope?.Span.SetException(exception);
    }

    internal static void InjectSpanProperties(IDictionary<string, string> jobParams, string key, string value)
    {
        jobParams[key] = value;
    }

    internal static void CreateDatadogFilter(out object? serverFilter, out object? clientFilter)
    {
        serverFilter = null;
        clientFilter = null;

        Assembly? hangfireAssembly = AppDomain.CurrentDomain
                                              .GetAssemblies()
                                              .FirstOrDefault(asm => asm.GetName().Name == "Hangfire.Core");
        if (hangfireAssembly == null)
        {
            Log.Debug("Error getting required Hangfire assembly. Hangfire integration is not enabled: abort injecting datadog filter");
            return;
        }

        Type? serverFilterType = hangfireAssembly.GetType("Hangfire.Server.IServerFilter");
        Type? clientFilterType = hangfireAssembly.GetType("Hangfire.Client.IClientFilter");

        if (serverFilterType is null || clientFilterType is null)
        {
            Log.Debug("Error getting required Hangfire Server/Client filter assembly. Hangfire integration is not enabled: abort injecting datadog filter");
            return;
        }

        serverFilter = DuckType.CreateReverse(serverFilterType, new DatadogHangfireServerFilter());
        clientFilter = DuckType.CreateReverse(clientFilterType, new DatadogHangfireClientFilter());
    }
}
