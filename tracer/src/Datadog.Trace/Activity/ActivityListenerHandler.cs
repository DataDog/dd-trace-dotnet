// <copyright file="ActivityListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal static class ActivityListenerHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListenerHandler));

        private static readonly Dictionary<string, IActivityHandler> HandlerBySource = new();

        public static void OnActivityStarted<T>(T activity)
            where T : IActivity5
        {
            OnActivityWithSourceStarted(activity.Source.Name, activity);
        }

        public static void OnActivityStopped<T>(T activity)
            where T : IActivity5
        {
            OnActivityWithSourceStopped(activity.Source.Name, activity);
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
            lock (HandlerBySource)
            {
                foreach (var handler in ActivityHandlersRegister.Handlers)
                {
                    if (source is IActivitySource activitySource)
                    {
                        if (handler.ShouldListenTo(activitySource.Name, activitySource.Version))
                        {
                            HandlerBySource[source.Name ?? "(null)"] = handler;
                            return true;
                        }
                    }
                    else
                    {
                        if (handler.ShouldListenTo(source.Name, null))
                        {
                            HandlerBySource[source.Name ?? "(null)"] = handler;
                            return true;
                        }
                    }
                }
            }

            Log.Warning($"ActivityListenerHandler: There's no handler to process the events from \"{source.Name}\".");
            return false;
        }

        public static void OnActivityWithSourceStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            lock (HandlerBySource)
            {
                if (HandlerBySource.TryGetValue(sourceName ?? "(null)", out var handler))
                {
                    handler.ActivityStarted(sourceName, activity);
                }
                else
                {
                    Log.Warning($"ActivityListenerHandler: There's no handler to process the ActivityStarted event. [Source={sourceName}]");
                }
            }
        }

        public static void OnActivityWithSourceStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            lock (HandlerBySource)
            {
                if (HandlerBySource.TryGetValue(sourceName ?? "(null)", out var handler))
                {
                    handler.ActivityStopped(sourceName, activity);
                }
                else
                {
                    Log.Warning($"ActivityListenerHandler: There's no handler to process the ActivityStopped event.  [Source={sourceName}]");
                }
            }
        }
    }
}
