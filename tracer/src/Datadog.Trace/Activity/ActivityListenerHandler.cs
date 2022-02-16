// <copyright file="ActivityListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal static class ActivityListenerHandler
    {
        private const string ActivityIdKey = "_dd.activity.id";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListenerHandler));
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

                var scope = Tracer.Instance.StartActiveInternal(activity.OperationName, startTime: activity.StartTimeUtc, finishOnClose: false);
                scope.Span.SetTag(ActivityIdKey, activity.Id);
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
                Log.Debug($"OnActivityStopped: [Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);

                var currentScope = Tracer.Instance.ActiveScope;
                if (currentScope?.Span is not null)
                {
                    var span = currentScope.Span;
                    var activityId = span.GetTag(ActivityIdKey);
                    if (activityId == activity.Id)
                    {
                        span.SetTag(ActivityIdKey, null);
                        foreach (var activityTag in activity.Tags)
                        {
                            span.SetTag(activityTag.Key, activityTag.Value);
                        }

                        foreach (var activityBag in activity.Baggage)
                        {
                            span.SetTag(activityBag.Key, activityBag.Value);
                        }

                        if (activity is IActivity6 activity6)
                        {
                            if (span is Span internalSpan)
                            {
                                if (activity6.Status == ActivityStatusCode.Error)
                                {
                                    internalSpan.Error = true;
                                    internalSpan.SetTag(Tags.ErrorMsg, activity6.StatusDescription);
                                }
                            }

                            var sourceName = activity6.Source.Name;
                            span.SetTag("source", sourceName);
                            span.ResourceName = $"{sourceName}.{span.OperationName}";

                            switch (activity6.Kind)
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
                        else if (activity is IActivity5 activity5)
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
                        currentScope.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStopped callback");
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
