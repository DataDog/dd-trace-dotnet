// <copyright file="QuartzDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz;
using Datadog.Trace.Logging;
#nullable enable

// Currently to our DiagnosticObserver class isn't available for .NET Framework.
// Our QuartzDiagnosticObserver only works for .NET Framework due to this limitation
// We are purposely avoiding adding a dependency to System.Diagnostics.DiagnosticSource
#if !NETFRAMEWORK
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
        private const string DiagnosticListenerName = "Quartz";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<QuartzDiagnosticObserver>();

        protected override string ListenerName => DiagnosticListenerName;

        protected override void OnNext(string eventName, object arg)
        {
            switch (eventName)
            {
                case "Quartz.Job.Execute.Start":
                case "Quartz.Job.Veto.Start":
                    Log.Debug("{EventName}",  eventName);

                    // get current acitvity
                    // ducktype it
                    // gets an Iactivity (atleast), IActivity5 has the display name
                    // set the displayname to execute span

                    // move this to its own function in quartz commoon
                    var currentActivity = ActivityListener.GetCurrentActivity();
                    if (currentActivity is IActivity5 activity5)
                    {
                        var displayName = QuartzCommon.CreateResourceName(activity5.DisplayName, activity5.Tags.FirstOrDefault(kv => kv.Key == "job.name").Value ?? string.Empty);
                        currentActivity.AddTag("operation.name", activity5.DisplayName);
                        activity5.DisplayName = displayName;
                        QuartzCommon.SetSpanKind(activity5);
                    }

                    break;
                case "Quartz.Job.Execute.Stop":
                case "Quartz.Job.Veto.Stop":
                    Log.Debug("{EventName}",  eventName);
                    break;
                case "Quartz.Job.Execute.Exception":
                case "Quartz.Job.Veto.Exception":
                    Log.Debug("{EventName}",  eventName);
                    // setting an exception manually
                    var closingActivity = ActivityListener.GetCurrentActivity();
                    if (closingActivity != null)
                    {
                        AddException(arg, closingActivity);
                    }

                    break;
                default:
                    Log.Debug("default");
                    break;
            }
        }

        private static void AddException(object exceptionArg, IActivity activity)
        {
            if (exceptionArg is not Exception exception)
            {
                return;
            }

            activity.AddTag(Tags.ErrorMsg, exception.Message);
            activity.AddTag(Tags.ErrorType, exception.GetType().ToString());
            activity.AddTag(Tags.ErrorStack, exception.ToString());
            activity.AddTag("otel.status_code", "STATUS_CODE_ERROR");
        }
    }
}

#endif
