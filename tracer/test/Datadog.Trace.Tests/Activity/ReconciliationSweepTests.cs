// <copyright file="ReconciliationSweepTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;
using SD = System.Diagnostics;

namespace Datadog.Trace.Tests.Activity
{
    public class ReconciliationSweepTests
    {
        [Fact]
        public async Task GcdActivity_IsClosedAndRemoved()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent(new TracerSettings());

            var mappings = new ConcurrentDictionary<ActivityKey, ActivityMapping>();
            var (_, span) = AddAbandonedMapping(mappings, tracer, "gcd-activity");

            var result = ActivityHandlerCommon.ReconcileSweepCore(mappings);

            result.GcCollected.Should().Be(1);
            result.MissedStop.Should().Be(0);
            result.Iterated.Should().Be(1);
            mappings.Should().BeEmpty();
            span.IsFinished.Should().BeTrue();
            span.GetTag("closed_reason").Should().Be("garbage_collected");
        }

        [Fact]
        public async Task GcdActivity_SetsFallbackValuesOnSpan()
        {
            // The Activity is gone, so we can't run the normal OTLP→Datadog conversion. The sweep
            // applies sensible defaults so the resulting span isn't malformed when it reaches the agent.
            await using var tracer = TracerHelper.CreateWithFakeAgent(new TracerSettings());

            var mappings = new ConcurrentDictionary<ActivityKey, ActivityMapping>();
            var (_, span) = AddAbandonedMapping(mappings, tracer, "gcd-activity");

            ActivityHandlerCommon.ReconcileSweepCore(mappings);

            span.IsFinished.Should().BeTrue();
            span.GetTag("closed_reason").Should().Be("garbage_collected");

            // Span-level fallbacks
            span.OperationName.Should().Be(SpanKinds.Internal); // GetOperationName falls back to lowercased SpanKind
            span.ServiceName.Should().Be("OTLPResourceNoServiceName");
            span.ResourceName.Should().Be("ONGOING_ACTIVITY");
            span.Type.Should().Be(SpanTypes.Custom);

            // Tag-level fallbacks (live on OpenTelemetryTags)
            var otelTags = (OpenTelemetryTags)span.Tags;
            otelTags.SpanKind.Should().Be(SpanKinds.Internal);
            otelTags.OtelStatusCode.Should().Be("STATUS_CODE_UNSET");
        }

        [Fact]
        public async Task GcdActivity_UsesPeerServiceAsServiceNameWhenAvailable()
        {
            // If a peer.service tag was set on the span before the Activity was lost, prefer that
            // over the generic OTLPResourceNoServiceName fallback.
            await using var tracer = TracerHelper.CreateWithFakeAgent(new TracerSettings());

            var mappings = new ConcurrentDictionary<ActivityKey, ActivityMapping>();
            var (_, span) = AddAbandonedMapping(mappings, tracer, "gcd-with-peer-service", presetTags: s => s.SetTag("peer.service", "downstream-api"));

            ActivityHandlerCommon.ReconcileSweepCore(mappings);

            span.IsFinished.Should().BeTrue();
            span.ServiceName.Should().Be("downstream-api");
        }

        [Fact]
        public async Task MissedStopActivity_IsClosedWithRealDuration()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent(new TracerSettings());

            var mappings = new ConcurrentDictionary<ActivityKey, ActivityMapping>();

            var activity = new SD.Activity("missed-stop").Start();
            try
            {
                var (_, span) = AddMapping(mappings, tracer, activity);

                // Stop sets Duration > 0 — but we never invoke ActivityHandlerCommon.ActivityStopped,
                // mimicking the case where the listener callback didn't make it through.
                activity.Stop();
                activity.Duration.Should().NotBe(TimeSpan.Zero);

                var result = ActivityHandlerCommon.ReconcileSweepCore(mappings);

                result.GcCollected.Should().Be(0);
                result.MissedStop.Should().Be(1);
                result.Iterated.Should().Be(1);
                mappings.Should().BeEmpty();
                span.IsFinished.Should().BeTrue();
            }
            finally
            {
                GC.KeepAlive(activity);
            }
        }

        [Fact]
        public async Task LiveUnstoppedActivity_IsLeftAlone()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent(new TracerSettings());

            var mappings = new ConcurrentDictionary<ActivityKey, ActivityMapping>();

            var activity = new SD.Activity("live-activity").Start();
            try
            {
                var (key, span) = AddMapping(mappings, tracer, activity);

                var result = ActivityHandlerCommon.ReconcileSweepCore(mappings);

                result.GcCollected.Should().Be(0);
                result.MissedStop.Should().Be(0);
                result.Iterated.Should().Be(1);
                mappings.Should().ContainKey(key);
                span.IsFinished.Should().BeFalse();

                // Cleanup — finalize manually so the Scope/Span don't leak past the test
                if (mappings.TryRemove(key, out var owned))
                {
                    owned.Scope?.Span.Finish();
                    owned.Scope?.Close();
                }
            }
            finally
            {
                GC.KeepAlive(activity);
                activity.Stop();
            }
        }

        [Fact]
        public async Task MixedDictionary_HandlesAllCategoriesInOnePass()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent(new TracerSettings());

            var mappings = new ConcurrentDictionary<ActivityKey, ActivityMapping>();

            var (_, gcSpan) = AddAbandonedMapping(mappings, tracer, "gcd-activity");

            var stoppedActivity = new SD.Activity("missed-stop").Start();
            var (_, stoppedSpan) = AddMapping(mappings, tracer, stoppedActivity);
            stoppedActivity.Stop();

            var liveActivity = new SD.Activity("live-activity").Start();
            var (liveKey, liveSpan) = AddMapping(mappings, tracer, liveActivity);

            try
            {
                var result = ActivityHandlerCommon.ReconcileSweepCore(mappings);

                result.GcCollected.Should().Be(1);
                result.MissedStop.Should().Be(1);
                result.Iterated.Should().Be(3);
                mappings.Should().HaveCount(1).And.ContainKey(liveKey);

                gcSpan.IsFinished.Should().BeTrue();
                gcSpan.GetTag("closed_reason").Should().Be("garbage_collected");
                stoppedSpan.IsFinished.Should().BeTrue();
                liveSpan.IsFinished.Should().BeFalse();
            }
            finally
            {
                if (mappings.TryRemove(liveKey, out var owned))
                {
                    owned.Scope?.Span.Finish();
                    owned.Scope?.Close();
                }

                GC.KeepAlive(stoppedActivity);
                GC.KeepAlive(liveActivity);
                liveActivity.Stop();
            }
        }

        // Constructs a mapping in the "abandoned" state — its Activity reference is already dead,
        // matching the production scenario where the Activity has been GC'd before the sweep runs.
        // Uses a deliberately-dead WeakReference rather than relying on real GC timing, which is
        // flaky in Debug builds where JIT keeps locals alive across method boundaries.
        private static (ActivityKey Key, Span Span) AddAbandonedMapping(
            ConcurrentDictionary<ActivityKey, ActivityMapping> mappings,
            Tracer tracer,
            string keyId,
            Action<Span> presetTags = null)
        {
            var span = tracer.StartSpan(keyId);

            // Mirror the production behaviour: spans created from Activities use OpenTelemetryTags
            // so the sweep's fallback logic (which depends on `span.Tags is OpenTelemetryTags`) fires.
            span.Tags = new OpenTelemetryTags();

            // Null the fields StartSpan populates with tracer defaults so the sweep's `is null/empty`
            // fallbacks actually fire. In production the GC'd-path fallbacks fire when these fields
            // haven't been populated by AgentConvertSpan; here we recreate that state explicitly.
            span.OperationName = null;
            span.ResourceName = null;
            span.Type = null;
#pragma warning disable CS0618
            span.ServiceName = null;
#pragma warning restore CS0618

            presetTags?.Invoke(span);

            var scope = tracer.ActivateSpan(span, finishOnClose: false);
            var key = new ActivityKey(keyId);

            // WeakReference<T>(null) constructs a reference whose TryGetTarget always returns false,
            // exactly matching the runtime state of a WeakReference whose target has been GC'd.
            mappings[key] = new ActivityMapping(new WeakReference<object>(null!), scope);
            return (key, span);
        }

        private static (ActivityKey Key, Span Span) AddMapping(
            ConcurrentDictionary<ActivityKey, ActivityMapping> mappings,
            Tracer tracer,
            SD.Activity activity,
            Action<Span> presetTags = null)
        {
            var span = tracer.StartSpan(activity.OperationName);

            // Mirror the production behaviour: spans created from Activities use OpenTelemetryTags
            // so the sweep's fallback logic (which depends on `span.Tags is OpenTelemetryTags`) fires.
            // StartSpan above doesn't set OTel tags, so swap them in by hand.
            span.Tags = new OpenTelemetryTags();

            // Null out the fields that StartSpan populates with tracer defaults so the sweep's
            // `is null/empty` fallbacks actually fire. In production the GC'd-path fallbacks fire
            // when these fields haven't been populated by AgentConvertSpan; here we recreate that
            // state explicitly.
            span.OperationName = null;
            span.ResourceName = null;
            span.Type = null;
#pragma warning disable CS0618
            span.ServiceName = null;
#pragma warning restore CS0618

            presetTags?.Invoke(span);

            var scope = tracer.ActivateSpan(span, finishOnClose: false);

            var key = activity.IdFormat == SD.ActivityIdFormat.W3C
                ? new ActivityKey(activity.TraceId.ToString(), activity.SpanId.ToString())
                : new ActivityKey(activity.Id);

            mappings[key] = new ActivityMapping(new WeakReference<object>(activity), scope);
            return (key, span);
        }
    }
}
