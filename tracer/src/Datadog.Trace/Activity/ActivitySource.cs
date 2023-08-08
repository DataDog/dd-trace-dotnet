// <copyright file="ActivitySource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Activity
{
    internal static class ActivitySource
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivitySource));

        private static object? _activitySourceInstance;
        private static IActivitySource? _activitySourceProxy;

        /// <summary>
        /// <see cref="ActivityListener"/>
        /// </summary>
        /// <param name="activitySourceType">the actiivty srourc</param>
        public static void CreateActivitySourceInstance(Type activitySourceType)
        {
            Log.Information("About to create an activity source");
            // new ActivitySource(string name, string? version)
            var activitySource = Activator.CreateInstance(activitySourceType, "Datadog.ActivitySource.InternalUseOnly", null); // TODO version
            Log.Information("ActivitySource - ACTIVATED");

            var activitySourceProxy = activitySource.DuckCast<IActivitySource>();
            if (activitySourceProxy is null)
            {
                ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after ducktyping {activitySourceType} is null");
            }

            Log.Information("ActivitySource - DUCKCASTED");

            // set the global field for the activity source
            _activitySourceInstance = activitySource;
            _activitySourceProxy = activitySourceProxy;
        }

        public static IActivity? CreateActivity(string name, ActivityKind kind)
        {
            if (_activitySourceProxy is null)
            {
                Log.Error("why are you null ActivitySource");
                return null;
            }

            IActivity? activity = null;
            try
            {
                activity = _activitySourceProxy.CreateActivity(name, kind);
            }
            catch (Exception e)
            {
                Log.Error("Exception thrown in CreateActivity {Message}", e.Message);
                throw;
            }

            return activity;
        }

        public static IActivity? StartActivity(string name, ActivityKind kind)
        {
            if (_activitySourceProxy is null)
            {
                Log.Error("why are you null ActivitySource");
                return null;
            }

            IActivity? activity = null;
            try
            {
                activity = _activitySourceProxy.StartActivity(name, kind);
            }
            catch (Exception e)
            {
                Log.Error("Exception thrown in StartActivity {Message}", e.Message);
                throw;
            }

            return activity;
        }
    }
}
