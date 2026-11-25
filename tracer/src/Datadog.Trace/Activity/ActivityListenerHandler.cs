// <copyright file="ActivityListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal static class ActivityListenerHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListenerHandler));

        private static readonly ConcurrentDictionary<string, IActivityHandler> HandlerBySource = new();

        public static void OnActivityStarted<T>(T activity)
            where T : IActivity5
        {
            OnActivityWithSourceStarted(activity.Source.Name, activity);
        }

        public static void OnActivityStopped<T>(T activity)
            where T : IActivity5
        {
            OnActivityWithSourceStopped(activity.Source.Name, activity, default);
        }

        // Not an overload of OnActivityStopped: the delegate builder looks handlers up by name
        public static void OnActivityStoppedWithStatus<T>(T activity)
            where T : IActivity6
        {
            OnActivityWithSourceStopped(activity.Source.Name, activity, ActivityStatus.From(activity));
        }

        public static ActivitySamplingResult OnSample()
        {
            return ActivitySamplingResult.AllData;
        }

        public static ActivitySamplingResult OnSampleUsingParentId()
        {
            return ActivitySamplingResult.AllData;
        }

        // ActivityListener path (DiagnosticSource 5.0+). Separate methods rather than overloads,
        // because the callers look them up by name.
        public static bool OnActivitySourceShouldListenTo<T>(T source)
            where T : IActivitySource
            => ShouldListenTo(source.Name ?? "(null)", source.Version);

        // DiagnosticListener path (DiagnosticSource [4.0.4, 5.0)), where sources have no version
        public static bool OnSourceShouldListenTo<T>(T source)
            where T : ISource
            => ShouldListenTo(source.Name ?? "(null)", version: null);

        private static bool ShouldListenTo(string sName, string? version)
        {
            foreach (var handler in ActivityHandlersRegister.Handlers)
            {
                if (handler.ShouldListenTo(sName, version))
                {
                    if (handler is DisableActivityHandler)
                    {
                        Log.Information("ActivityListenerHandler: The .NET Tracer will not listen to {SourceName}.", sName);
                        return false;
                    }

                    if (HandlerBySource.TryAdd(sName, handler))
                    {
                        Log.Debug("ActivityListenerHandler: {SourceName} will be handled by {Handler}.", sName, handler);
                        return true;
                    }
                }
            }

            Log.Warning("ActivityListenerHandler: There's no handler to process the events from \"{SourceName}\".", sName);
            return false;
        }

        public static void OnActivityWithSourceStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            var sName = sourceName ?? "(null)";
            if (HandlerBySource.TryGetValue(sName, out var handler))
            {
                handler = ResolveHandler(sourceName, activity.OperationName, handler);
                handler.ActivityStarted(sName, activity);
            }
            else
            {
                Log.Debug("ActivityListenerHandler: There's no handler to process the ActivityStarted event. [Source={SourceName}]", sName);
            }
        }

        public static void OnActivityWithSourceStopped<T>(string sourceName, T activity, in ActivityStatus status)
            where T : IActivity
        {
            var sName = sourceName ?? "(null)";
            if (HandlerBySource.TryGetValue(sName, out var handler))
            {
                handler = ResolveHandler(sourceName, activity.OperationName, handler);
                handler.ActivityStopped(sName, activity, in status);
            }
            else
            {
                Log.Warning("ActivityListenerHandler: There's no handler to process the ActivityStopped event.  [Source={SourceName}]", sName);
            }
        }

        internal static IActivityHandler ResolveHandler(string? sourceName, string? operationName, IActivityHandler handler)
        {
            // If the source lookup only found the default handler, use the operation name as a fallback.
            if (StringUtil.IsNullOrEmpty(sourceName) && handler is DefaultActivityHandler)
            {
                return FindIntegrationHandlerByOperationName(operationName) ?? handler;
            }

            return handler;
        }

        private static IActivityHandler? FindIntegrationHandlerByOperationName(string? operationName)
        {
            if (StringUtil.IsNullOrEmpty(operationName))
            {
                return null;
            }

            foreach (var handler in ActivityHandlersRegister.Handlers)
            {
                if (handler is IActivityHandlerWithOperationName operationNameHandler
                 && operationNameHandler.ShouldListenToOperationName(operationName))
                {
                    return handler;
                }
            }

            return null;
        }
    }
}
