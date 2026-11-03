// <copyright file="JobFilterCollectionCtorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// System.Void Hangfire.Common.JobFilterCollection::.ctor() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Hangfire.Core",
    TypeName = "Hangfire.Common.JobFilterCollection",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [],
    MinimumVersion = "1.7.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Hangfire))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class JobFilterCollectionCtorIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(JobFilterCollectionCtorIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        where TTarget : IJobFilterCollectionProxy
    {
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        where TTarget : IJobFilterCollectionProxy
    {
        if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(HangfireCommon.IntegrationId))
        {
            // integration disabled, skip injection of datadog job filter
            return CallTargetReturn.GetDefault();
        }

        // Hangfire reads GlobalJobFilters.Filters, but jobs can also take filters from an application
        // collection, so every collection gets the filters; they are idempotent when combined.
        HangfireCommon.CreateDatadogFilter(out var serverFilter, out var clientFilter);
        if (serverFilter is not null && clientFilter is not null)
        {
            instance.Add(serverFilter);
            instance.Add(clientFilter);
        }
        else
        {
            Log.Error("Unable to create Datadog Hangfire server filter");
        }

        return CallTargetReturn.GetDefault();
    }
}
