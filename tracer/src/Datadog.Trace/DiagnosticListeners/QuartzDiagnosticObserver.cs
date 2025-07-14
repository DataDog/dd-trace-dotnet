// <copyright file="QuartzDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Instruments Quartz.NET job scheduler.
    /// <para/>
    /// This observer listens to Quartz diagnostic events to trace job execution,
    /// scheduling, and other Quartz-related operations.
    /// </summary>
    internal sealed class QuartzDiagnosticObserver : DiagnosticObserver
    {
        public const IntegrationId IntegrationId = Configuration.IntegrationId.Quartz;

        private const string DiagnosticListenerName = "Quartz";
        private const string JobExecutionOperationName = "quartz.job.execution";
        private const string JobSchedulingOperationName = "quartz.job.scheduling";
        private const string TriggerFiredOperationName = "quartz.trigger.fired";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<QuartzDiagnosticObserver>();
        private readonly Tracer _tracer;
        private readonly LiveDebugger _liveDebugger;
        private readonly SpanCodeOriginManager _spanOriginManager;

        public QuartzDiagnosticObserver()
            : this(null, null, null)
        {
        }

        public QuartzDiagnosticObserver(Tracer tracer, LiveDebugger liveDebugger, SpanCodeOriginManager spanOriginManager)
        {
            _tracer = tracer;
            _liveDebugger = liveDebugger;
            _spanOriginManager = spanOriginManager;
        }

        protected override string ListenerName => DiagnosticListenerName;

        private Tracer CurrentTracer => _tracer ?? Tracer.Instance;

        private LiveDebugger CurrentLiveDebugger => _liveDebugger ?? LiveDebugger.Instance;

        private SpanCodeOriginManager CurrentCodeOriginManager => _spanOriginManager ?? SpanCodeOriginManager.Instance;

        protected override void OnNext(string eventName, object arg)
        {
            try
            {
                switch (eventName)
                {
                    case "Quartz.Job.Execute.Start":
                    case "Quartz.Job.Veto.Start":
                        OnStartSpan(arg);
                        break;
                    case "Quartz.Job.Execute.Stop":
                    case "Quartz.Job.Veto.Stop":
                        OnStopSpan(arg);
                        break;
                    case "Quartz.Job.Execute.Exception":
                    case "Quartz.Job.Veto.Exception":
                        OnException(arg);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing Quartz diagnostic event: {EventName}", eventName);
            }
        }

        private void OnStartSpan(object arg)
        {
            Log.Information("Quartz JobExecution.Start event received");
            QuartzCommon.CreateScope(CurrentTracer, arg, QuartzCommon.OnJobExecuteOperation);
        }

        private void OnStopSpan(object arg)
        {
            Log.Information("Quartz JobExecution.Stop event received");
            CurrentTracer.ActiveScope.Close();
        }

        private void OnException(object arg)
        {
            Log.Information("Quartz JobExecution.Exception event received");
            if (arg is Exception exception)
            {
                CurrentTracer.ActiveScope.Span.SetException(exception);
            }
        }
    }
}
#endif
