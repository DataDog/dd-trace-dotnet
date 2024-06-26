// <copyright file="RateLimiterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class RateLimiterTests
    {
        private const int DefaultLimitPerSecond = 100;

        [Fact]
        public async Task One_Is_Allowed()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "Weeeee");
            var span = new Span(spanContext, null);
            var rateLimiter = new TracerRateLimiter(maxTracesPerInterval: null, intervalMilliseconds: null);
            var allowed = rateLimiter.Allowed(span);
            Assert.True(allowed);
        }

        [Fact]
        public async Task All_Traces_Disabled()
        {
            var rateLimiter = new TracerRateLimiter(maxTracesPerInterval: 0, intervalMilliseconds: null);
            var allowedCount = await AskTheRateLimiterABunchOfTimes(rateLimiter, 500);
            Assert.Equal(expected: 0, actual: allowedCount);
        }

        [Fact]
        public async Task All_Traces_Allowed()
        {
            var rateLimiter = new TracerRateLimiter(maxTracesPerInterval: -1, intervalMilliseconds: null);
            var allowedCount = await AskTheRateLimiterABunchOfTimes(rateLimiter, 500);
            Assert.Equal(expected: 500, actual: allowedCount);
        }

        [Fact]
        public async Task Only_100_Allowed_In_500_Burst_For_Default()
        {
            var rateLimiter = new TracerRateLimiter(maxTracesPerInterval: null, intervalMilliseconds: null);
            var allowedCount = await AskTheRateLimiterABunchOfTimes(rateLimiter, 500);
            Assert.Equal(expected: DefaultLimitPerSecond, actual: allowedCount);
        }

        [Fact]
        public void Limits_Approximately_To_Defaults()
        {
            Run_Limit_Test(intervalLimit: null, numberPerBurst: 100, numberOfBursts: 18, millisecondsBetweenBursts: 247);
        }

        [Fact]
        public void Limits_To_Custom_Amount_Per_Second()
        {
            Run_Limit_Test(intervalLimit: 500, numberPerBurst: 200, numberOfBursts: 18, millisecondsBetweenBursts: 247);
        }

        private static void Run_Limit_Test(int? intervalLimit, int numberPerBurst, int numberOfBursts, int millisecondsBetweenBursts)
        {
            var actualIntervalLimit = intervalLimit ?? DefaultLimitPerSecond;

            var test = new RateLimitLoadTest()
            {
                NumberPerBurst = numberPerBurst,
                TimeBetweenBursts = TimeSpan.FromMilliseconds(millisecondsBetweenBursts),
                NumberOfBursts = numberOfBursts
            };

            var result = RunTest(intervalLimit, test);

            var theoreticalTime = numberOfBursts * millisecondsBetweenBursts;
            var expectedLimit = theoreticalTime * actualIntervalLimit / 1_000;

            var acceptableUpperVariance = (actualIntervalLimit * 1.0);
            var acceptableLowerVariance = (actualIntervalLimit * 1.15); // Allow for increased tolerance on lower limit since the rolling window does not get dequeued as quickly as it can queued
            var upperLimit = expectedLimit + acceptableUpperVariance;
            var lowerLimit = expectedLimit - acceptableLowerVariance;

            Assert.True(
                result.TotalAllowed >= lowerLimit && result.TotalAllowed <= upperLimit,
                $"Expected between {lowerLimit} and {upperLimit}, received {result.TotalAllowed} out of {result.TotalAttempted} within {theoreticalTime} milliseconds.");

            // Rate should match for the last two intervals, which is a total of two seconds
            var numberOfBurstsWithinTwoIntervals = 2_000 / millisecondsBetweenBursts;
            var totalExpectedSent = numberOfBurstsWithinTwoIntervals * numberPerBurst;
            var totalExpectedAllowed = 2 * actualIntervalLimit;
            var expectedRate = totalExpectedAllowed / (float)totalExpectedSent;

            var lowestRate = expectedRate - 0.40f;
            if (lowestRate < 0)
            {
                lowestRate = expectedRate / 2;
            }

            var highestRate = expectedRate + 0.40f;

            Assert.True(
                result.ReportedRate >= lowestRate && result.ReportedRate <= highestRate,
                $"Expected rate between {lowestRate} and {highestRate}, received {result.ReportedRate}.");
        }

        private static async Task<int> AskTheRateLimiterABunchOfTimes(RateLimiter rateLimiter, int howManyTimes)
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "Weeeee");
            var span = new Span(spanContext, null);

            var remaining = howManyTimes;
            var allowedCount = 0;
            while (remaining-- > 0)
            {
                var allowed = rateLimiter.Allowed(span);
                if (allowed)
                {
                    allowedCount++;
                }
            }

            return allowedCount;
        }

        private static RateLimitResult RunTest(int? intervalLimit, RateLimitLoadTest test)
        {
            var parallelism = test.NumberPerBurst;

            if (parallelism > Environment.ProcessorCount)
            {
                parallelism = Environment.ProcessorCount;
            }

            var clock = new SimpleClock();

            var limiter = new TracerRateLimiter(maxTracesPerInterval: intervalLimit, intervalMilliseconds: null);
            var barrier = new Barrier(parallelism + 1, _ => clock.UtcNow += test.TimeBetweenBursts);
            var numberPerThread = test.NumberPerBurst / parallelism;
            var workers = new Task[parallelism];
            int totalAttempted = 0;
            int totalAllowed = 0;

            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = Task.Factory.StartNew(
                    () =>
                    {
                        using var lease = Clock.SetForCurrentThread(clock);

                        for (var i = 0; i < test.NumberOfBursts; i++)
                        {
                            // Wait for every worker to be ready for next burst
                            barrier.SignalAndWait();

                            for (int j = 0; j < numberPerThread; j++)
                            {
                                // trace id and span id are not used in rate-limiting
                                var spanContext = new SpanContext(traceId: 1, spanId: 1, serviceName: "Weeeee");

                                // pass a specific start time since there is no TraceContext
                                var span = new Span(spanContext, DateTimeOffset.UtcNow);

                                Interlocked.Increment(ref totalAttempted);

                                if (limiter.Allowed(span))
                                {
                                    Interlocked.Increment(ref totalAllowed);
                                }
                            }
                        }
                    },
                    TaskCreationOptions.LongRunning);
            }

            // Wait for all workers to be ready
            barrier.SignalAndWait();

            // We do not need to synchronize with workers anymore
            barrier.RemoveParticipant();

            // Wait for workers to finish
            Task.WaitAll(workers);

            var result = new RateLimitResult
            {
                RateLimiter = limiter,
                ReportedRate = limiter.GetEffectiveRate(),
                TotalAttempted = totalAttempted,
                TotalAllowed = totalAllowed
            };

            return result;
        }

        private class RateLimitLoadTest
        {
            public int NumberPerBurst { get; set; }

            public TimeSpan TimeBetweenBursts { get; set; }

            public int NumberOfBursts { get; set; }
        }

        private class RateLimitResult
        {
            public RateLimiter RateLimiter { get; set; }

            public float ReportedRate { get; set; }

            public int TotalAttempted { get; set; }

            public int TotalAllowed { get; set; }
        }
    }
}
