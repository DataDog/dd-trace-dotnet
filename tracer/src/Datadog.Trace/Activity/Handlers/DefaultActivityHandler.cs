// <copyright file="DefaultActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// The default handler catches an activity and creates a datadog span from it.
    /// </summary>
    internal class DefaultActivityHandler : IActivityHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DefaultActivityHandler));
        internal static readonly ConcurrentDictionary<string, ActivityMapping> ActivityMappingById = new();
        private static readonly IntegrationId IntegrationId = IntegrationId.OpenTelemetry;

        public bool ShouldListenTo(string sourceName, string? version)
        {
            return true;
        }

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            Tracer.Instance.TracerManager.Telemetry.IntegrationRunning(IntegrationId);
            var activeSpan = (Span?)Tracer.Instance.ActiveScope?.Span;

            // Propagate Trace and Parent Span ids
            SpanContext? parent = null;
            ulong? traceId = null;
            ulong? spanId = null;
            string? rawTraceId = null;
            string? rawSpanId = null;
            bool remoteActivityContext = false; // TODO steven cleanup

            if (activity is IW3CActivity w3cActivity)
            {
                // If the user has specified a parent context, get the parent Datadog SpanContext
                var parentSpanIdExists = w3cActivity.ParentSpanId is not null;
                var activityParentId = w3cActivity.ParentId;

                if (w3cActivity.ParentSpanId is not null
                    && w3cActivity.ParentId is string parentId)
                {
                    if (ActivityMappingById.TryGetValue(parentId, out ActivityMapping mapping))
                    {
                        // we have the scope for the Activity
                        parent = mapping.Scope.Span.Context;
                        Log.Information("Mapped {Name} to a parent of {ParentID}", w3cActivity.OperationName, mapping.Scope.Span.TraceId);
                    }
                    else
                    {
                        Log.Information("Current ActivityMappings: {@ActivityMapping}", ActivityMappingById);
                        Log.Information("Creating a new parent for {@ActivityContext}", w3cActivity);

                        // TODO issue - Activity started with default Context nested underneath previous span - I think that is expected though?
                        remoteActivityContext = true; // using this as a way to avoid ActiveSpan below for now
                        // extract parent Activity trace/span IDs TODO this is slightly duped below (but uses w3cActivity.SpanId)
                        traceId ??= Convert.ToUInt64(w3cActivity.TraceId.Substring(16), 16);
                        spanId = Convert.ToUInt64(w3cActivity.ParentSpanId, 16);
                        Log.Information("Created {TraceID} and {SpanID}", traceId, spanId);
                        Log.Information("Raw IDs: Raw Parent ID: {RawTraceId} and Raw Span ID: {RawSpanId}, Root ID: {RootID}", w3cActivity.RawParentId, w3cActivity.RawId, w3cActivity.RootId);
                        var newParentContext = Tracer.Instance.CreateSpanContext(parent: SpanContext.None, rawTraceId: w3cActivity.RootId, rawSpanId: w3cActivity.ParentSpanId);
                        // TODO why is it that when I set the traceId and spanId in CreateSpanContext that the test hangs after running everything?
                        // TODO maybe because we do end up changing the above traceId and spanId later on?
                        // var newParentContext = Tracer.Instance.CreateSpanContext(parent: SpanContext.None, traceId: traceId, spanId: spanId, rawTraceId: w3cActivity.RootId, rawSpanId: w3cActivity.ParentSpanId);

                        Log.Information("Created a new SpanContext {@Context}", newParentContext);

                        // TODO - should we pass the "activity" here or create a new activity to represent the remote parent?
                        // TODO - can we even create an activity?
                        var newParentScope = CreateScopeFromActivity(activity, newParentContext, traceId: traceId, spanId: spanId, rawTraceId: w3cActivity.RawId, rawSpanId: w3cActivity.RawParentId, activate: false);
                        Log.Information("Created a new scope for the parent activity {@Activity}", newParentScope);
                        // TODO - should we be using the "activity.Instance" here - maybe same as above just create a new pa
                        ActivityMappingById.GetOrAdd(parentId, _ => new(activity.Instance, newParentScope));
                    }
                }

                if (parent is null && activeSpan is not null && !remoteActivityContext)
                {
                    // If this is the first activity (no parent) and we already have an active span
                    // or the span was started after the parent activity so we use the span as a parent

                    // We ensure the activity follows the same TraceId as the span
                    // And marks the ParentId the current spanId

                    if (activity.Parent is null || activity.Parent.StartTimeUtc < activeSpan.StartTime.UtcDateTime)
                    {
                        // TraceId
                        w3cActivity.TraceId = string.IsNullOrWhiteSpace(activeSpan.Context.RawTraceId) ?
                                                  activeSpan.TraceId.ToString("x32") : activeSpan.Context.RawTraceId;

                        // SpanId
                        w3cActivity.ParentSpanId = string.IsNullOrWhiteSpace(activeSpan.Context.RawSpanId) ?
                                                       activeSpan.SpanId.ToString("x16") : activeSpan.Context.RawSpanId;

                        // We clear internals Id and ParentId values to force recalculation.
                        w3cActivity.RawId = null;
                        w3cActivity.RawParentId = null;

                        // Avoid recalculation of the traceId.
                        traceId = activeSpan.TraceId;
                    }
                }

                // We convert the activity traceId and spanId to use it in the
                // Datadog span creation.
                if (w3cActivity.TraceId is { } w3cTraceId && w3cActivity.SpanId is { } w3cSpanId && !remoteActivityContext)
                {
                    // If the Activity has an ActivityIdFormat.Hierarchical instead of W3C it's TraceId & SpanId will be null
                    traceId ??= Convert.ToUInt64(w3cTraceId.Substring(16), 16);
                    spanId = Convert.ToUInt64(w3cSpanId, 16);
                    rawTraceId = w3cTraceId;
            }

            try
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("DefaultActivityHandler.ActivityStarted: [Source={SourceName}, Id={Id}, RootId={RootId}, OperationName={OperationName}, StartTimeUtc={StartTimeUtc}, Duration={Duration}]", new object[] { sourceName, activity.Id, activity.RootId, activity.OperationName!, activity.StartTimeUtc, activity.Duration });
                }

                // We check if we have to ignore the activity by the operation name value
                if (IgnoreActivityHandler.IgnoreByOperationName(activity, activeSpan))
                {
                    return;
                }

                ActivityMappingById.GetOrAdd(activity.Id, _ => new(activity.Instance!, CreateScopeFromActivity(activity, parent, traceId, spanId, rawTraceId, rawSpanId)));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStarted callback");
            }

            static Scope CreateScopeFromActivity(T activity, SpanContext? parent, ulong? traceId, ulong? spanId, string? rawTraceId, string? rawSpanId, bool activate = true)
            {
                Log.Information("StartSpan for {ActivityName}", activity.OperationName);
                var span = Tracer.Instance.StartSpan(activity.OperationName, parent: parent, startTime: activity.StartTimeUtc, traceId: traceId, spanId: spanId, rawTraceId: rawTraceId, rawSpanId: rawSpanId);
                Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);

                // TODO this is covering for creating the parent Activity scope
                if (parent is null || activate)
                {
                    Log.Information("Activating Span for {SpanName}", span.OperationName);
                    return Tracer.Instance.ActivateSpan(span, false);
                }

                Log.Information("Returning a new scope for {SpanName}", span.OperationName);
                return new Scope(null, span, Tracer.Instance.ScopeManager, false);
            }
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            try
            {
                if (activity.Instance is not null)
                {
                    if (IgnoreActivityHandler.ShouldIgnoreByOperationName(activity))
                    {
                        return;
                    }

                    // first we look for the normal ID, then the parent
                    if (ActivityMappingById.TryRemove(activity.Id, out ActivityMapping someValue) && someValue.Scope?.Span is not null)
                    {
                        Log.Information("Closing the ActivityScope! {Name}", activity.OperationName);
                        // We have the exact scope associated with the Activity
                        if (Log.IsEnabled(LogEventLevel.Debug))
                        {
                            Log.Debug("DefaultActivityHandler.ActivityStopped: [Source={SourceName}, Id={Id}, RootId={RootId}, OperationName={OperationName}, StartTimeUtc={StartTimeUtc}, Duration={Duration}]", new object[] { sourceName, activity.Id, activity.RootId, activity.OperationName!, activity.StartTimeUtc, activity.Duration });
                        }

                        CloseActivityScope(sourceName, activity, someValue.Scope);
                        return;
                    }
                    else if (activity.ParentId is { } parentId && ActivityMappingById.TryRemove(parentId, out ActivityMapping parentValue) && parentValue.Scope?.Span is not null)
                    {
                        Log.Information("Closing the PARENT ActivityScope! {Name}", activity.OperationName);
                        // We have the exact scope associated with the Activity
                        if (Log.IsEnabled(LogEventLevel.Debug))
                        {
                            Log.Debug("DefaultActivityHandler.ActivityStopped: [Source={SourceName}, Id={Id}, RootId={RootId}, OperationName={OperationName}, StartTimeUtc={StartTimeUtc}, Duration={Duration}]", new object[] { sourceName, activity.Id, activity.RootId, activity.OperationName!, activity.StartTimeUtc, activity.Duration });
                        }

                        CloseActivityScope(sourceName, activity, parentValue.Scope);
                        return;
                    }
                }

                // The listener didn't send us the Activity or the scope instance was not found
                // In this case we are going go through the dictionary to check if we have an activity that
                // has been closed and then close the associated scope.
                if (activity.Instance is not null)
                {
                    if (Log.IsEnabled(LogEventLevel.Information))
                    {
                        Log.Information("DefaultActivityHandler.ActivityStopped: MISSING SCOPE [Source={SourceName}, Id={Id}, RootId={RootId}, OperationName={OperationName}, StartTimeUtc={StartTimeUtc}, Duration={Duration}]", new object[] { sourceName, activity!.Id, activity.RootId, activity.OperationName!, activity.StartTimeUtc, activity.Duration });
                    }
                }
                else
                {
                    Log.Information($"DefaultActivityHandler.ActivityStopped: [Missing Activity]");
                }

                List<string>? toDelete = null;
                foreach (var (activityId, item) in ActivityMappingById)
                {
                    var activityObject = item.Activity;
                    var scope = item.Scope;
                    var hasClosed = false;

                    if (activityObject.TryDuckCast<IActivity6>(out var activity6))
                    {
                        if (activity6.Duration != TimeSpan.Zero)
                        {
                            CloseActivityScope(sourceName, activity6, scope);
                            hasClosed = true;
                        }
                    }
                    else if (activityObject.TryDuckCast<IActivity5>(out var activity5))
                    {
                        if (activity5.Duration != TimeSpan.Zero)
                        {
                            CloseActivityScope(sourceName, activity5, scope);
                            hasClosed = true;
                        }
                    }
                    else if (activityObject.TryDuckCast<IActivity>(out var activity4))
                    {
                        if (activity4.Duration != TimeSpan.Zero)
                        {
                            CloseActivityScope(sourceName, activity4, scope);
                            hasClosed = true;
                        }
                    }

                    if (hasClosed)
                    {
                        toDelete ??= new List<string>();
                        toDelete.Add(activityId);
                    }
                }

                if (toDelete is not null)
                {
                    foreach (var item in toDelete)
                    {
                        ActivityMappingById.TryRemove(item, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the DefaultActivityHandler.ActivityStopped callback");
            }

            static void CloseActivityScope<TInner>(string sourceName, TInner activity, Scope scope)
                where TInner : IActivity
            {
                var span = scope.Span;

                OtlpHelpers.UpdateSpanFromActivity(activity, scope.Span);

                // OpenTelemtry SDK / OTLP Fixups
                // TODO
                // 3) Add resources to spans
                // OpenTelemetry SDK resources are added to the span attributes by the configured exporter when OpenTelemetry.BaseExporter<T>.Export is called (e.g. OpenTelemetry.Exporter.ConsoleActivityExporter.Export)
                // **** Note: The exporter has a ParentProvider field that is populated with the TracerProviderSdk when everything is initially built, so this is technically per instance
                // **** To reliably get this, we might consider addding a Processor to the TracerProviderBuilder, though not sure where to invoke it
                // - service.instance.id
                // - service.name
                // - service.namespace
                // - service.version

                span.Finish(activity.StartTimeUtc.Add(activity.Duration));
                scope.Close();
            }
        }

        public readonly struct ActivityMapping
        {
            public readonly object Activity;
            public readonly Scope Scope;

            internal ActivityMapping(object activity, Scope scope)
            {
                Activity = activity;
                Scope = scope;
            }
        }
    }

#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1402 // File may only contain a single type
    internal static class ActivityMappingExtensions
    {
        public static void Deconstruct(this KeyValuePair<string, DefaultActivityHandler.ActivityMapping> item, out string key, out DefaultActivityHandler.ActivityMapping value)
        {
            key = item.Key;
            value = item.Value;
        }
    }
}
