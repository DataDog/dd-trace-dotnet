// <copyright file="ReconciliationSweepTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Configuration;
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
            var (key, span) = AddAbandonedMapping(mappings, tracer, "gcd-activity");

            // Drop the strong reference (held inside AddAbandonedMapping's local frame, now gone)
            // and force GC until the WeakReference clears.
            ForceGcUntilCollected(mappings[key].Activity);

            var result = ActivityHandlerCommon.ReconcileSweepCore(mappings);

            result.GcCollected.Should().Be(1);
            result.MissedStop.Should().Be(0);
            result.Iterated.Should().Be(1);
            mappings.Should().BeEmpty();
            span.IsFinished.Should().BeTrue();
            span.GetTag("dd.activity.forced_close").Should().Be("abandoned");
        }

        [Fact]
        public async Task MissedStopActivity_IsClosedWithRealDuration()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent(new TracerSettings());

            var mappings = new ConcurrentDictionary<ActivityKey, ActivityMapping>();

            var activity = new SD.Activity("missed-stop").Start();
            try
            {
                var (key, span) = AddMapping(mappings, tracer, activity);

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

            var (gcKey, gcSpan) = AddAbandonedMapping(mappings, tracer, "gcd-activity");
            ForceGcUntilCollected(mappings[gcKey].Activity);

            var stoppedActivity = new SD.Activity("missed-stop").Start();
            var (stoppedKey, stoppedSpan) = AddMapping(mappings, tracer, stoppedActivity);
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
                gcSpan.GetTag("dd.activity.forced_close").Should().Be("abandoned");
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

        // Constructs a mapping whose Activity has no other live references — the caller never sees
        // the Activity instance, so the only path keeping it alive is the WeakReference inside the
        // mapping (which doesn't, by definition).
        private static (ActivityKey Key, Span Span) AddAbandonedMapping(
            ConcurrentDictionary<ActivityKey, ActivityMapping> mappings,
            Tracer tracer,
            string operationName)
        {
            var activity = new SD.Activity(operationName);
            activity.SetIdFormat(SD.ActivityIdFormat.W3C);
            activity.Start();
            try
            {
                return AddMapping(mappings, tracer, activity);
            }
            finally
            {
                activity.Stop();
                // intentionally do not GC.KeepAlive — we want this collectible
            }
        }

        private static (ActivityKey Key, Span Span) AddMapping(
            ConcurrentDictionary<ActivityKey, ActivityMapping> mappings,
            Tracer tracer,
            SD.Activity activity)
        {
            var span = (Span)tracer.StartSpan(activity.OperationName);
            var scope = (Scope)tracer.ActivateSpan(span, finishOnClose: false);

            var key = activity.IdFormat == SD.ActivityIdFormat.W3C
                ? new ActivityKey(activity.TraceId.ToString(), activity.SpanId.ToString())
                : new ActivityKey(activity.Id);

            mappings[key] = new ActivityMapping(new WeakReference<object>(activity), scope);
            return (key, span);
        }

        private static void ForceGcUntilCollected(WeakReference<object> reference)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (!reference.TryGetTarget(out _))
                {
                    return;
                }
            }

            // If the runtime stubbornly keeps the Activity alive (e.g. AsyncLocal residue) the test
            // becomes meaningless rather than failing in a confusing way. Surface that explicitly.
            throw new InvalidOperationException(
                "Activity could not be collected after repeated GC attempts; the test environment is keeping it alive.");
        }
    }
}
