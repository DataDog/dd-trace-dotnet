// <copyright file="QuartzCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz
{
    internal static class QuartzCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(QuartzCommon));

        internal static string CreateResourceName(string operationName, string jobName)
        {
            return operationName switch
            {
                _ when operationName.Contains("Execute") => "execute " + jobName,
                _ when operationName.Contains("Veto") => "veto " + jobName,
                _ => operationName
            };
        }

        internal static ActivityKind GetActivityKind(IActivity5 activity)
        {
            return activity.OperationName switch
            {
                string name when name.Contains("Execute") => ActivityKind.Internal,
                string name when name.Contains("Veto") => ActivityKind.Internal,
                _ => activity.Kind
            };
        }

        internal static void SetActivityKind(IActivity5 activity)
        {
            ActivityListener.SetActivityKind(activity, GetActivityKind(activity));
        }

        internal static void EnhanceActivityMetadata(IActivity5 activity)
        {
            activity.AddTag("operation.name", activity.DisplayName);

            string? jobName = null;
            foreach (var tag in activity.Tags)
            {
                if (tag.Key == "job.name")
                {
                    jobName = tag.Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(jobName))
            {
                Log.Debug("Unable to update Quartz Span's resource name: job.name tag was not found.");
                return;
            }

            activity.DisplayName = CreateResourceName(activity.DisplayName, jobName!);
        }

        internal static void EnhanceActivityMetadata(IActivity activity)
        {
            if (activity is IActivity5 activity5)
            {
                EnhanceActivityMetadata(activity5);
                return;
            }

            if (activity.OperationName is null)
            {
                return;
            }

            activity.AddTag("operation.name", activity.OperationName);

            string? jobName = null;
            foreach (var tag in activity.Tags)
            {
                if (tag.Key == "job.name")
                {
                    jobName = tag.Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(jobName))
            {
                Log.Debug("Unable to update Quartz Span's resource name: job.name tag was not found.");
                return;
            }

            activity.AddTag("resource.name", CreateResourceName(activity.OperationName, jobName!));
        }

        internal static void AddException(object exceptionArg, IActivity activity)
        {
            if (exceptionArg is not Exception exception)
            {
                Log.Debug("Arg: {Arg}, was not of type exception. Unable to populate error tags", exceptionArg);
                return;
            }

            activity.AddTag(Tags.ErrorMsg, exception.Message);
            activity.AddTag(Tags.ErrorType, exception.GetType().ToString());
            activity.AddTag(Tags.ErrorStack, exception.ToString());
            activity.AddTag("otel.status_code", "STATUS_CODE_ERROR");
        }

        /// <summary>
        /// Handles Quartz diagnostic events.
        /// This method is shared between the DiagnosticObserver (modern .NET) and reflection-based observer (.NET Framework).
        /// </summary>
        internal static void HandleDiagnosticEvent(string eventName, object arg)
        {
            switch (eventName)
            {
                case "Quartz.Job.Execute.Start":
                case "Quartz.Job.Veto.Start":
                    var activity = ActivityListener.GetCurrentActivity();
                    if (activity is IActivity5 activity5)
                    {
                        SetActivityKind(activity5);
                    }
                    else
                    {
                        Log.Debug("The loaded System.Diagnostics.Activity type does not have a Kind property. Unable to populate the Kind property.");
                    }

                    if (activity?.Instance is not null)
                    {
                        EnhanceActivityMetadata(activity);
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
                        AddException(arg, closingActivity);
                    }

                    break;
            }
        }
    }
}
