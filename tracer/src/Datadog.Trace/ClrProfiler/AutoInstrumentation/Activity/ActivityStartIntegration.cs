// <copyright file="ActivityStartIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Activity.Helpers;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// CallTarget instrumentation for <c>System.Diagnostics.Activity.Start()</c>.
    /// Intercepts Activity.Start() to create a corresponding Datadog <see cref="Span"/>/<see cref="Scope"/>
    /// and establishes a bi-directional link between the Activity and the Span.
    /// The Activity.Start() body is not skipped — it must run to populate TraceId, SpanId, and Activity.Current.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "Start",
        ReturnTypeName = "System.Diagnostics.Activity",
        ParameterTypeNames = new string[0],
        MinimumVersion = "6.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivityStartIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityStartIntegration));

        /// <summary>
        /// OnMethodBegin callback — let Activity.Start() run so it populates TraceId, SpanId, etc.
        /// </summary>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback — called after Activity.Start() has returned with IDs populated.
        /// </summary>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            try
            {
                if (!Tracer.Instance.Settings.IsActivityInterceptionEnabled)
                {
                    return new CallTargetReturn<TReturn>(returnValue);
                }

                if (!instance.TryDuckCast<IActivity5>(out var activity5))
                {
                    return new CallTargetReturn<TReturn>(returnValue);
                }

                // Filter out sources already handled by other Datadog integrations
                var sourceName = activity5.Source.Name ?? string.Empty;
                if (ActivitySourceFilter.ShouldIgnore(sourceName, null))
                {
                    return new CallTargetReturn<TReturn>(returnValue);
                }

                // Filter by operation name prefix (e.g., System.Net.Http.*, Microsoft.AspNetCore.*)
                if (IgnoreActivityHandler.ShouldIgnoreByOperationName(activity5.OperationName))
                {
                    IgnoreActivityHandler.IgnoreActivity(activity5, Tracer.Instance.ActiveScope?.Span as Span);
                    return new CallTargetReturn<TReturn>(returnValue);
                }

                CreateAndLinkScope(instance, activity5);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in ActivityStartIntegration.OnMethodEnd");
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }

        private static void CreateAndLinkScope<TTarget>(TTarget instance, IActivity5 activity5)
        {
            var tracer = Tracer.Instance;
            tracer.TracerManager.Telemetry.IntegrationRunning(IntegrationId);

            SpanContext? parent = null;
            TraceId traceId = default;
            ulong spanId = 0;
            string? rawTraceId = null;
            string? rawSpanId = null;

            var w3cActivity = activity5 as IW3CActivity;

#pragma warning disable DDDUCK001 // IDuckType null check
            if (w3cActivity is not null)
#pragma warning restore DDDUCK001
            {
                var activityTraceId = w3cActivity.TraceId;
                var activitySpanId = w3cActivity.SpanId;

                if (!StringUtil.IsNullOrEmpty(activityTraceId))
                {
                    // Check for in-process parent via custom property on the parent Activity
                    if (w3cActivity.RawParentSpanId is { } parentSpanId)
                    {
                        // Try to get parent scope from parent Activity's custom property
                        var parentActivityIface = activity5.Parent;
                        if (parentActivityIface?.Instance is { } parentInstance
                         && parentInstance.TryDuckCast<IActivity5>(out var parentActivity5))
                        {
                            if (parentActivity5.GetCustomProperty("__dd_span__") is Scope parentScope)
                            {
                                parent = parentScope.Span.Context;
                            }
                        }

                        if (parent is null)
                        {
                            // Remote parent — construct SpanContext from TraceId + ParentSpanId
                            _ = HexString.TryParseTraceId(activityTraceId, out var remoteTraceId);
                            _ = HexString.TryParseUInt64(parentSpanId, out var remoteSpanId);
                            parent = tracer.CreateSpanContext(
                                SpanContext.None,
                                traceId: remoteTraceId,
                                spanId: remoteSpanId,
                                rawTraceId: activityTraceId,
                                rawSpanId: parentSpanId);
                        }
                    }

                    // No remote/in-process parent found — attempt reparenting with the active Datadog scope
#pragma warning disable DDDUCK001 // Checking IDuckType for null
                    if (parent is null
                     && activitySpanId is not null
                     && tracer.ActiveScope?.Span is Span activeSpan
                     && (activity5.Parent is null || activity5.Parent.StartTimeUtc <= activeSpan.StartTime.UtcDateTime))
#pragma warning restore DDDUCK001 // Checking IDuckType for null
                    {
                        // Align Activity with the active Datadog trace
                        w3cActivity.TraceId = activeSpan.Context.RawTraceId;
                        activityTraceId = w3cActivity.TraceId;
                        w3cActivity.RawParentSpanId = activeSpan.Context.RawSpanId;
                        w3cActivity.RawId = null;
                        w3cActivity.RawParentId = null;
                        traceId = activeSpan.TraceId128;
                    }

                    if (activityTraceId != null && activitySpanId != null)
                    {
                        if (traceId == TraceId.Zero)
                        {
                            _ = HexString.TryParseTraceId(activityTraceId, out traceId);
                        }

                        _ = HexString.TryParseUInt64(activitySpanId, out spanId);
                        rawTraceId = activityTraceId;
                        rawSpanId = activitySpanId;
                    }
                }
            }

            var tags = new OpenTelemetryTags();

            // Set span kind and OTel trace ID
            tags.SpanKind = OtlpHelpers.GetSpanKind(activity5.Kind);

#pragma warning disable DDDUCK001 // IDuckType null check
            if (w3cActivity?.TraceId is { } traceIdHex)
#pragma warning restore DDDUCK001
            {
                tags.OtelTraceId = traceIdHex;
            }

            // Set source name/version (library name/version tags)
            if (!StringUtil.IsNullOrEmpty(activity5.Source.Name))
            {
                tags.OtelLibraryName = activity5.Source.Name;
            }

            if (!StringUtil.IsNullOrEmpty(activity5.Source.Version))
            {
                tags.OtelLibraryVersion = activity5.Source.Version;
            }

            var span = tracer.StartSpan(
                activity5.OperationName,
                tags: tags,
                parent: parent,
                startTime: activity5.StartTimeUtc,
                traceId: traceId,
                spanId: spanId,
                rawTraceId: rawTraceId,
                rawSpanId: rawSpanId);

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            var scope = tracer.ActivateSpan(span, finishOnClose: false);

            // Set ResourceName before copying tags so that reserved tag "operation.name"
            // (which changes OperationName) doesn't accidentally override the resource.
            // ResourceName will default to OperationName at Finish() if still null, but we
            // want to preserve the original activity name as the resource.
            span.ResourceName = activity5.DisplayName is { Length: > 0 } displayName
                ? displayName
                : activity5.OperationName;

            // Save the initial operation name BEFORE copying tags so ActivityStopIntegration can
            // detect explicit overrides via "operation.name" tag. Initial tags (passed to StartActivity)
            // may include "operation.name" which changes OperationName during tag copy below.
            var initialOperationName = span.OperationName;

            // Copy existing tags from the Activity to the Span. Tags may have been set before
            // Activity.Start() returned (e.g., via initial tags passed to StartActivity()), so
            // they're in Activity.TagObjects but haven't been captured by our AddTag/SetTag integrations
            // (those require the scope to be set on the Activity's custom property, which isn't done yet).
            if (activity5.HasTagObjects())
            {
                var state = new OtelTagsEnumerationState(span);
                ActivityEnumerationHelper.EnumerateTagObjects(activity5, ref state, static (ref OtelTagsEnumerationState s, KeyValuePair<string, object?> kvp) =>
                {
                    OtlpHelpers.SetTagObject(s.Span, kvp.Key, kvp.Value);
                    return true;
                });
            }

            // Apply OTel resource attributes (service.name, service.version, etc.) to the span.
            // In the managed ActivityListener path, resource attributes are applied by the
            // ResourceAttributeProcessor.OnStart callback which fires during Activity.Start().
            // With interception, that callback fires before we create the span, so we apply them here.
            OpenTelemetry.ResourceAttributeProcessorHelper.ApplyCachedResourceAttributes(span);

            // Bi-directional link: Activity → Scope (via custom property — zero-alloc cached delegate)
            ActivityCustomPropertyAccessor<TTarget>.SetScope(instance, scope);
            ActivityCustomPropertyAccessor<TTarget>.SetInitialOperationName(instance, initialOperationName);

            // Ensure IsAllDataRequested is true so that user code guarded by
            // `if (activity.IsAllDataRequested) { activity.AddTag(...); }` will actually run.
            // The managed ActivityListener did this implicitly by returning AllData from its Sample
            // callback. Since we skip the managed listener when interception is enabled, we must
            // set it here, after Start() has populated the Activity's IDs.
            activity5.IsAllDataRequested = true;
        }
    }
}
