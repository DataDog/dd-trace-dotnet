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
        internal const string ComponentName = "quartz";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(QuartzCommon));

        internal static string CreateResourceName(string operationName, string jobName)
        {
            return operationName switch
            {
                _ when operationName.Contains("Execute") => "execute " + jobName,
                _ when operationName.Contains("Veto") => "veto " + jobName,
                "Quartz.JobStore.ScheduleJob" => "schedule " + jobName,
                _ => operationName
            };
        }

        internal static ActivityKind GetActivityKind<T>(T activity)
            where T : IActivity5
        {
            return activity.OperationName switch
            {
                string name when name.Contains("Execute") => ActivityKind.Internal,
                string name when name.Contains("Veto") => ActivityKind.Internal,
                _ => activity.Kind
            };
        }

        internal static void SetActivityKind<T>(T activity)
            where T : IActivity5
        {
            ActivityListener.SetActivityKind(activity, GetActivityKind(activity));
        }

        internal static void EnhanceActivityMetadata<T>(T activity)
            where T : IActivity5
        {
            activity.AddTag("operation.name", activity.DisplayName);
            activity.AddTag(Tags.InstrumentationName, ComponentName);

            string? jobName = null;
            foreach (var tag in activity.TagObjects)
            {
                if (tag.Key is "job.name" or "quartz.job.name")
                {
                    jobName = tag.Value as string;
                    break;
                }
            }

            if (string.IsNullOrEmpty(jobName))
            {
                Log.Debug("Unable to update Quartz Span's resource name: job.name or quartz.job.name tag was not found.");
                return;
            }

            activity.DisplayName = CreateResourceName(activity.DisplayName, jobName!);
        }

        internal static void EnhanceLegacyActivityMetadata<T>(T activity)
            where T : IActivity
        {
            if (activity.OperationName is null)
            {
                return;
            }

            activity.AddTag("operation.name", activity.OperationName);
            activity.AddTag(Tags.InstrumentationName, ComponentName);

            string? jobName = null;
            foreach (var tag in activity.Tags)
            {
                if (tag.Key is "job.name" or "quartz.job.name")
                {
                    jobName = tag.Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(jobName))
            {
                Log.Debug("Unable to update Quartz Span's resource name: job.name or quartz.job.name tag was not found.");
                return;
            }

            activity.AddTag("resource.name", CreateResourceName(activity.OperationName, jobName!));
        }

        internal static void AddException<T>(object exceptionArg, T activity)
            where T : IActivity
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
    }
}
