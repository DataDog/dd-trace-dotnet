// <copyright file="ActivityListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal static class ActivityListenerHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListenerHandler));
        private static readonly string[] IgnoreSourcesNames =
        {
            string.Empty,
            "System.Net.Http.Desktop",
        };

        public static void OnActivityStarted<T>(T activity)
            where T : IActivity
        {
            var tagsBuilder = new StringBuilder();
            foreach (var activityTag in activity.Tags)
            {
                tagsBuilder.Append($"{activityTag.Key}={activityTag.Value} |");
            }

            Log.Information($"OnActivityStarted: [OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}, Tags={tagsBuilder}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
        }

        public static void OnActivityStopped<T>(T activity)
            where T : IActivity
        {
            var tagsBuilder = new StringBuilder();
            foreach (var activityTag in activity.Tags)
            {
                tagsBuilder.Append($"{activityTag.Key}={activityTag.Value} |");
            }

            Log.Information($"OnActivityStopped: [OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}, Tags={tagsBuilder}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
        }

        public static ActivitySamplingResult OnSample()
        {
            return ActivitySamplingResult.AllData;
        }

        public static ActivitySamplingResult OnSampleUsingParentId()
        {
            return ActivitySamplingResult.AllData;
        }

        public static bool OnShouldListenTo<T>(T source)
            where T : ISource
        {
            foreach (var ignoreSourceName in IgnoreSourcesNames)
            {
                if (source.Name == ignoreSourceName)
                {
                    return false;
                }
            }

            if (source is IActivitySource activitySource)
            {
                Log.Information("OnShouldListenTo: [Name={SourceName}, Version={SourceVersion}]", activitySource.Name, activitySource.Version);
            }
            else
            {
                Log.Information("OnShouldListenTo: [Name={SourceName}]", source.Name);
            }

            return true;
        }
    }
}
