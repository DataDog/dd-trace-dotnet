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
                    var currentActivity = ActivityListener.GetCurrentActivity();
                    if (currentActivity is IActivity5 activity5)
                    {
                        QuartzCommon.EnhanceActivityMetadata(activity5);
                        QuartzCommon.SetActivityKind(activity5);
                    }
                    else
                    {
                        Log.Debug("The activity was not Activity5 (Less than .NET 5.0). Unable enhance the span metadata.");
                    }

                    break;
                case "Quartz.Job.Execute.Stop":
                case "Quartz.Job.Veto.Stop":
                    break;
                case "Quartz.Job.Execute.Exception":
                case "Quartz.Job.Veto.Exception":
                    // setting an exception manually
                    var closingActivity = ActivityListener.GetCurrentActivity();
                    if (closingActivity?.Instance is not null)
                    {
                        QuartzCommon.AddException(arg, closingActivity);
                    }

                    break;
            }
        }
    }
}

#endif
