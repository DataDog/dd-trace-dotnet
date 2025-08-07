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
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast;
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

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<QuartzDiagnosticObserver>();
        private readonly Tracer _tracer;
        private readonly Security _security;
        private readonly Iast.Iast _iast;
        private readonly SpanCodeOrigin _spanCodeOrigin;

        public QuartzDiagnosticObserver()
            : this(null, null, null, null)
        {
        }

        public QuartzDiagnosticObserver(Tracer tracer, Security security, Iast.Iast iast, SpanCodeOrigin spanCodeOrigin)
        {
            _tracer = tracer;
            _security = security;
            _iast = iast;
            _spanCodeOrigin = spanCodeOrigin;
        }

        protected override string ListenerName => DiagnosticListenerName;

        private Tracer CurrentTracer => _tracer ?? Tracer.Instance;

        private Security CurrentSecurity => _security ?? Security.Instance;

        private Iast.Iast CurrentIast => _iast ?? Iast.Iast.Instance;

        private SpanCodeOrigin CurrentCodeOrigin => _spanCodeOrigin ?? DebuggerManager.Instance.CodeOrigin;

        protected override void OnNext(string eventName, object arg)
        {
            try
            {
                switch (eventName)
                {
                    case "Quartz.Job.Execute.Start":
                    case "Quartz.Job.Veto.Start":
                        // OnStartSpan(arg);
                        break;
                    case "Quartz.Job.Execute.Stop":
                    case "Quartz.Job.Veto.Stop":
                       // OnStopSpan(arg);
                        break;
                    case "Quartz.Job.Execute.Exception":
                    case "Quartz.Job.Veto.Exception":
                        // OnException(arg);
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
