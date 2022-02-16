// <copyright file="ActivityListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal static class ActivityListenerHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListenerHandler));
        private static readonly Dictionary<object, Scope> ActivityScope = new();
        private static readonly string[] IgnoreSourcesNames =
        {
            string.Empty,
            "System.Net.Http.Desktop",
            "Microsoft.AspNetCore",
            "HttpHandlerDiagnosticListener",
            "SqlClientDiagnosticListener",
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
            "Couchbase.DotnetSdk.RequestTracer",
            "MySqlConnector",
            "Npgsql",
        };

        public static void OnActivityStarted<T>(T activity)
            where T : IActivity
        {
            try
            {
                Log.Debug($"OnActivityStarted: [Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
                lock (ActivityScope)
                {
                    if (!ActivityScope.TryGetValue(activity.Instance, out _))
                    {
                        var scope = Tracer.Instance.StartActiveInternal(activity.OperationName, startTime: activity.StartTimeUtc, finishOnClose: false);
                        ActivityScope[activity.Instance] = scope;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStarted callback");
            }
        }

        public static void OnActivityStopped<T>(T activity)
            where T : IActivity
        {
            try
            {
                lock (ActivityScope)
                {
                    if (activity?.Instance is not null && ActivityScope.TryGetValue(activity.Instance, out var scope) && scope?.Span is not null)
                    {
                        // We have the exact scope associated with the Activity
                        Log.Debug($"OnActivityStopped: [Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
                        CloseActivityScope(activity, scope);
                        ActivityScope.Remove(activity.Instance);
                    }
                    else
                    {
                        // The listener didn't send us the Activity or the scope instance was not found
                        // In this case we are going go through the dictionary to check if we have an activity that
                        // has been closed and then close the associated scope.
                        if (activity?.Instance is not null)
                        {
                            Log.Debug($"OnActivityStopped: MISSING SCOPE [Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
                        }
                        else
                        {
                            Log.Debug($"OnActivityStopped: [Missing Activity]");
                        }

                        List<object> toDelete = null;
                        foreach (var item in ActivityScope)
                        {
                            var activityObject = item.Key;
                            var hasClosed = false;

                            if (activityObject.TryDuckCast<IActivity6>(out var activity6))
                            {
                                if (activity6.Duration != TimeSpan.Zero)
                                {
                                    CloseActivityScope(activity6, item.Value);
                                    hasClosed = true;
                                }
                            }
                            else if (activityObject.TryDuckCast<IActivity5>(out var activity5))
                            {
                                if (activity5.Duration != TimeSpan.Zero)
                                {
                                    CloseActivityScope(activity5, item.Value);
                                    hasClosed = true;
                                }
                            }
                            else if (activityObject.TryDuckCast<IActivity>(out var activity4))
                            {
                                if (activity4.Duration != TimeSpan.Zero)
                                {
                                    CloseActivityScope(activity4, item.Value);
                                    hasClosed = true;
                                }
                            }

                            if (hasClosed)
                            {
                                toDelete ??= new List<object>();
                                toDelete.Add(activityObject);
                            }
                        }

                        if (toDelete is not null)
                        {
                            foreach (var item in toDelete)
                            {
                                ActivityScope.Remove(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStopped callback");
            }

            static void CloseActivityScope<TInner>(TInner activity, Scope scope)
                where TInner : IActivity
            {
                var span = scope.Span;
                foreach (var activityTag in activity.Tags)
                {
                    span.SetTag(activityTag.Key, activityTag.Value);
                }

                foreach (var activityBag in activity.Baggage)
                {
                    span.SetTag(activityBag.Key, activityBag.Value);
                }

                if (activity is IActivity6 { Status: ActivityStatusCode.Error } activity6)
                {
                    span.Error = true;
                    span.SetTag(Tags.ErrorMsg, activity6.StatusDescription);
                }

                if (activity is IActivity5 activity5)
                {
                    var sourceName = activity5.Source.Name;
                    span.SetTag("source", sourceName);
                    span.ResourceName = $"{sourceName}.{span.OperationName}";

                    switch (activity5.Kind)
                    {
                        case ActivityKind.Client:
                            span.SetTag(Tags.SpanKind, "client");
                            break;
                        case ActivityKind.Consumer:
                            span.SetTag(Tags.SpanKind, "consumer");
                            break;
                        case ActivityKind.Internal:
                            span.SetTag(Tags.SpanKind, "internal");
                            break;
                        case ActivityKind.Producer:
                            span.SetTag(Tags.SpanKind, "producer");
                            break;
                        case ActivityKind.Server:
                            span.SetTag(Tags.SpanKind, "server");
                            break;
                    }
                }

                span.Finish(activity.StartTimeUtc.Add(activity.Duration));
                scope.Close();
            }
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
            try
            {
                foreach (var ignoreSourceName in IgnoreSourcesNames)
                {
                    if (source.Name == ignoreSourceName)
                    {
                        return false;
                    }
                }

                Log.Information("OnShouldListenTo: [Name={SourceName}]", source.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStopped callback");
            }

            return true;
        }
    }
}
