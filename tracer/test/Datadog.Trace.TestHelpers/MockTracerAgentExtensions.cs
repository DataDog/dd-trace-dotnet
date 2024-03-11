// <copyright file="MockTracerAgentExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using Datadog.Trace.TestHelpers.Stats;
using FluentAssertions;

namespace Datadog.Trace.TestHelpers;

public static class MockTracerAgentExtensions
{
        /// <summary>
        /// Wait for the given number of spans to appear.
        /// </summary>
        /// <param name="agent">The <see cref="MockTracerAgent"/> to use</param>
        /// <param name="count">The expected number of spans.</param>
        /// <param name="timeoutInMilliseconds">The timeout</param>
        /// <param name="operationName">The integration we're testing</param>
        /// <param name="minDateTime">Minimum time to check for spans from</param>
        /// <param name="returnAllOperations">When true, returns every span regardless of operation name</param>
        /// <param name="assertExpectedCount">When true, asserts that the number of spans to return matches the count</param>
        /// <returns>The list of spans.</returns>
        public static IImmutableList<MockSpan> WaitForSpans(
            this MockTracerAgent agent,
            int count,
            int timeoutInMilliseconds = 20000,
            string operationName = null,
            DateTimeOffset? minDateTime = null,
            bool returnAllOperations = false,
            bool assertExpectedCount = true)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);
            var minimumOffset = (minDateTime ?? DateTimeOffset.MinValue).ToUnixTimeNanoseconds();

            IImmutableList<MockSpan> relevantSpans = ImmutableList<MockSpan>.Empty;

            while (DateTime.UtcNow < deadline)
            {
                relevantSpans =
                    agent.Spans
                       .Where(s =>
                        {
                            if (!agent.SpanFilters.All(shouldReturn => shouldReturn(s)))
                            {
                                return false;
                            }

                            if (s.Start < minimumOffset)
                            {
                                // if the Start of the span is before the expected
                                // we check if is caused by the precision of the TraceClock optimization.
                                // So, if the difference is greater than 16 milliseconds (max accuracy error) we discard the span
                                if (minimumOffset - s.Start > 16000000)
                                {
                                    return false;
                                }
                            }

                            return true;
                        })
                       .ToImmutableList();

                if (relevantSpans.Count(s => operationName == null || s.Name == operationName) >= count)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            if (assertExpectedCount)
            {
                relevantSpans.Should().HaveCountGreaterThanOrEqualTo(count, "because we want to ensure that we don't timeout while waiting for spans from the mock tracer agent");
            }

            foreach (var headers in agent.TraceRequestHeaders)
            {
                // This is the place to check against headers we expect
                AssertHeader(
                    headers,
                    "X-Datadog-Trace-Count",
                    header =>
                    {
                        if (int.TryParse(header, out var traceCount))
                        {
                            return traceCount >= 0;
                        }

                        return false;
                    });

                // Ensure only one Content-Type is specified and that it is msgpack
                AssertHeader(
                    headers,
                    "Content-Type",
                    header =>
                    {
                        if (!header.Equals("application/msgpack"))
                        {
                            return false;
                        }

                        return true;
                    });
            }

            if (!returnAllOperations)
            {
                relevantSpans =
                    relevantSpans
                       .Where(s => operationName == null || s.Name == operationName)
                       .ToImmutableList();
            }

            return relevantSpans;
        }

        /// <summary>
        /// Wait for the telemetry condition to be satisfied.
        /// Note that the first telemetry that satisfies the condition is returned
        /// To retrieve all telemetry received, use <see cref="Telemetry"/>
        /// </summary>
        /// <param name="agent">The <see cref="MockTracerAgent"/> to use</param>
        /// <param name="hasExpectedValues">A predicate for the current telemetry.
        /// The object passed to the func will be a <see cref="TelemetryData"/> instance</param>
        /// <param name="timeoutInMilliseconds">The timeout</param>
        /// <param name="sleepTime">The time between checks</param>
        /// <returns>The telemetry that satisfied <paramref name="hasExpectedValues"/></returns>
        public static object WaitForLatestTelemetry(
            this MockTracerAgent agent,
            Func<object, bool> hasExpectedValues,
            int timeoutInMilliseconds = 5000,
            int sleepTime = 200)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            while (DateTime.UtcNow < deadline)
            {
                var current = agent.Telemetry;
                foreach (var telemetry in current)
                {
                    if (hasExpectedValues(telemetry))
                    {
                        return telemetry;
                    }
                }

                Thread.Sleep(sleepTime);
            }

            return null;
        }

        public static IImmutableList<MockClientStatsPayload> WaitForStats(
            this MockTracerAgent agent,
            int count,
            int timeoutInMilliseconds = 20000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            IImmutableList<MockClientStatsPayload> stats = ImmutableList<MockClientStatsPayload>.Empty;

            while (DateTime.UtcNow < deadline)
            {
                stats = agent.Stats;

                if (stats.Count >= count)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            return stats;
        }

        public static IImmutableList<MockDataStreamsPayload> WaitForDataStreams(
            this MockTracerAgent agent,
            int timeoutInMilliseconds,
            Func<IImmutableList<MockDataStreamsPayload>, bool> waitFunc)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            IImmutableList<MockDataStreamsPayload> stats = ImmutableList<MockDataStreamsPayload>.Empty;

            while (DateTime.UtcNow < deadline)
            {
                stats = agent.DataStreams;

                if (waitFunc(stats))
                {
                    break;
                }

                Thread.Sleep(500);
            }

            return stats;
        }

        public static IImmutableList<MockDataStreamsPayload> WaitForDataStreamsPoints(
            this MockTracerAgent agent,
            int statsCount,
            int timeoutInMilliseconds = 20000)
        {
            return agent.WaitForDataStreams(
                timeoutInMilliseconds,
                (stats) =>
                {
                    return stats.Sum(s => s.Stats.Sum(bucket => bucket.Stats.Length)) >= statsCount;
                });
        }

        public static IImmutableList<MockDataStreamsPayload> WaitForDataStreams(
            this MockTracerAgent agent,
            int payloadCount,
            int timeoutInMilliseconds = 20000)
        {
            return agent.WaitForDataStreams(
                timeoutInMilliseconds,
                (stats) => stats.Count == payloadCount);
        }

        /// <summary>
        /// Wait for the given number of probe snapshots to appear.
        /// </summary>
        /// <param name="agent">The <see cref="MockTracerAgent"/> to use</param>
        /// <param name="snapshotCount">The expected number of probe snapshots when more than one snapshot is expected (e.g. multiple line probes in method).</param>
        /// <param name="timeout">The timeout</param>
        /// <returns>The list of probe snapshots.</returns>
        public static async Task<string[]> WaitForSnapshots(
            this MockTracerAgent agent,
            int snapshotCount,
            TimeSpan? timeout = null)
        {
            using var cancellationSource = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));

            var isFound = false;
            while (!isFound && !cancellationSource.IsCancellationRequested)
            {
                isFound = agent.Snapshots.Count == snapshotCount;

                if (!isFound)
                {
                    await Task.Delay(100);
                }
            }

            if (!isFound)
            {
                throw new InvalidOperationException($"Snapshot count not found. Expected {snapshotCount}, actual {agent.Snapshots.Count}");
            }

            return agent.Snapshots.ToArray();
        }

        public static async Task<bool> WaitForNoSnapshots(
            this MockTracerAgent agent,
            int timeoutInMilliseconds = 10000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);
            while (DateTime.Now < deadline)
            {
                if (agent.Snapshots.Any())
                {
                    return false;
                }

                await Task.Delay(100);
            }

            return !agent.Snapshots.Any();
        }

        /// <summary>
        /// Wait for the given number of probe statuses to appear.
        /// </summary>
        /// <param name="agent">The <see cref="MockTracerAgent"/> to use</param>
        /// <param name="statusCount">The expected number of probe statuses when more than one status is expected (e.g. multiple line probes in method).</param>
        /// <param name="timeout">The timeout</param>
        /// <param name="expectedFailedStatuses">determines if we expect to see probe status failure</param>
        /// <returns>The list of probe statuses.</returns>
        public static async Task<string[]> WaitForProbesStatuses(
            this MockTracerAgent agent,
            int statusCount,
            TimeSpan? timeout = null,
            int expectedFailedStatuses = 0)
        {
            using var cancellationSource = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));

            var isFound = false;
            while (!isFound && !cancellationSource.IsCancellationRequested)
            {
                isFound = agent.ProbesStatuses.Count == statusCount;

                // If we expect failed probe statuses, then we try to reach the batch that contains it.
                // Due to complex race conditions, this requirement is not easily achieved thus what we do is
                // basically let this function spin until a batch with "Error installing probe" arrives, or we timeout and fail.
                if (isFound &&
                    expectedFailedStatuses != agent.ProbesStatuses.Count(probeStatus => probeStatus.Contains("Error installing probe")))
                {
                    agent.ClearProbeStatuses();
                    isFound = false;
                }

                if (!isFound)
                {
                    await Task.Delay(100);
                }
            }

            if (!isFound)
            {
                throw new InvalidOperationException($"Probes Status count not found. Expected {statusCount}, actual {agent.ProbesStatuses.Count}. " +
                                                    $"Expected failed statuses count is {expectedFailedStatuses}, actual failures:  {agent.ProbesStatuses.Count(probeStatus => probeStatus.Contains("Error installing probe"))} failed");
            }

            return agent.ProbesStatuses.ToArray();
        }

        public static async Task<string[]> WaitForStatsdRequests(
            this MockTracerAgent agent,
            int statsdRequestsCount,
            TimeSpan? timeout = null)
        {
            using var cancellationSource = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));

            var isFound = false;
            while (!isFound && !cancellationSource.IsCancellationRequested)
            {
                isFound = agent.StatsdRequests.Count >= statsdRequestsCount;

                if (!isFound)
                {
                    await Task.Delay(100, cancellationSource.Token);
                }
            }

            if (!isFound)
            {
                throw new InvalidOperationException($"Stats requested count not found. Expected {statsdRequestsCount}, actual {agent.ProbesStatuses.Count}");
            }

            return agent.StatsdRequests.ToArray();
        }

        private static void AssertHeader(
            NameValueCollection headers,
            string headerKey,
            Func<string, bool> assertion)
        {
            var header = headers.Get(headerKey);

            if (string.IsNullOrEmpty(header))
            {
                throw new Exception($"Every submission to the agent should have a {headerKey} header.");
            }

            if (!assertion(header))
            {
                throw new Exception($"Failed assertion for {headerKey} on {header}");
            }
        }
}
