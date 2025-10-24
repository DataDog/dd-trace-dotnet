// <copyright file="QuartzDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz;
using Datadog.Trace.Logging;

#nullable enable

namespace Datadog.Trace.DiagnosticListeners;

/// <summary>
/// Instruments Quartz.NET job scheduler.
/// <para/>
/// This observer listens to Quartz diagnostic events to trace job execution,
/// scheduling, and other Quartz-related operations.
/// </summary>
internal sealed class QuartzDiagnosticObserver : DiagnosticObserver
{
    private const string DiagnosticListenerName = "Quartz";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<QuartzDiagnosticObserver>();

    protected override string ListenerName => DiagnosticListenerName;

    protected override void OnNext(string eventName, object arg)
    {
        QuartzCommon.HandleDiagnosticEvent(eventName, arg);
    }
}
