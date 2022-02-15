// <copyright file="ActivityListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal static class ActivityListenerHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListenerHandler));

        public static void OnActivityStarted<T>(T activity)
            where T : IActivity
        {
            var tagsBuilder = new StringBuilder();
            foreach (var activityTag in activity.Tags)
            {
                tagsBuilder.Append($"{activityTag.Key}={activityTag.Value} |");
            }

            Log.Information($"OnActivityStarted: [OperationName={activity.OperationName}, StartTimeUtc={activity.StartTimeUtc}, Duration={activity.Duration}, Tags={tagsBuilder}]");
        }

        public static void OnActivityStopped<T>(T activity)
            where T : IActivity
        {
            var tagsBuilder = new StringBuilder();
            foreach (var activityTag in activity.Tags)
            {
                tagsBuilder.Append($"{activityTag.Key}={activityTag.Value} |");
            }

            Log.Information($"OnActivityStopped: [OperationName={activity.OperationName}, StartTimeUtc={activity.StartTimeUtc}, Duration={activity.Duration}, Tags={tagsBuilder}]");
        }

        public static ActivitySamplingResult OnSample()
        {
            return ActivitySamplingResult.AllData;
        }

        public static ActivitySamplingResult OnSampleUsingParentId()
        {
            return ActivitySamplingResult.AllData;
        }

        public static bool OnShouldListenTo<T>(T activitySource)
            where T : IActivitySource
        {
            Log.Information($"OnShouldListenTo: [Name={activitySource.Name}, Version={activitySource.Version}, HasListeners={activitySource.HasListeners()}]");
            return true;
        }
    }
}
