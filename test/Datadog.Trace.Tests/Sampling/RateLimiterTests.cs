using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Sampling;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class RateLimiterTests
    {
        private const int DefaultLimitPerSecond = 100;
        private static readonly ThreadLocal<Random> Random = new ThreadLocal<Random>(() => new Random());

        [Fact]
        public void One_Is_Allowed()
        {
            var rateLimiter = new RateLimiter(maxTracesPerInterval: null);
            var allowed = rateLimiter.Allowed(1);
            Assert.True(allowed);
        }

        [Fact]
        public void All_Traces_Disabled()
        {
            var rateLimiter = new RateLimiter(maxTracesPerInterval: 0);
            var allowedCount = AskTheRateLimiterABunchOfTimes(rateLimiter, 500);
            Assert.Equal(expected: 0, actual: allowedCount);
        }

        [Fact]
        public void All_Traces_Allowed()
        {
            var rateLimiter = new RateLimiter(maxTracesPerInterval: -1);
            var allowedCount = AskTheRateLimiterABunchOfTimes(rateLimiter, 500);
            Assert.Equal(expected: 500, actual: allowedCount);
        }

        [Fact]
        public void Only_100_Allowed_In_500_Burst_For_Default()
        {
            var rateLimiter = new RateLimiter(maxTracesPerInterval: null);
            var allowedCount = AskTheRateLimiterABunchOfTimes(rateLimiter, 500);
            Assert.Equal(expected: DefaultLimitPerSecond, actual: allowedCount);
        }

        [Fact]
        public void Limits_Approximately_To_Defaults()
        {
            Run_Limit_Test(intervalLimit: null, numberPerBurst: 200, numberOfBursts: 20, millisecondsBetweenBursts: 247);
        }

        [Fact]
        public void Limits_To_Custom_Amount_Per_Second()
        {
            Run_Limit_Test(intervalLimit: 500, numberPerBurst: 200, numberOfBursts: 20, millisecondsBetweenBursts: 247);
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

            var totalMilliseconds = result.TimeElapsed.TotalMilliseconds;

            // Adjust for the second before
            var actualTotalWindowTime = totalMilliseconds + (10_000 / totalMilliseconds);

            var expectedLimit = actualTotalWindowTime * actualIntervalLimit / 1_000;

            var upperLimit = expectedLimit * 1.20;
            var lowerLimit = expectedLimit * 0.90;

            Assert.True(
                result.TotalAllowed >= lowerLimit && result.TotalAllowed <= upperLimit,
                $"Expected between {lowerLimit} and {upperLimit}, received {result.TotalAllowed} out of {result.TotalAttempted} within {totalMilliseconds} milliseconds.");

            // Rate should match for the last two intervals, which is a total of two seconds
            var numberOfBurstsWithinTwoIntervals = 2_000 / millisecondsBetweenBursts;
            var totalExpectedSent = numberOfBurstsWithinTwoIntervals * numberPerBurst;
            var totalExpectedAllowed = 2 * actualIntervalLimit;
            var expectedRate = totalExpectedAllowed / (float)totalExpectedSent;

            var lowestRate = expectedRate * 0.90;
            var highestRate = expectedRate * 1.10;

            Assert.True(
                result.ReportedRate >= lowestRate && result.ReportedRate <= highestRate,
                $"Expected rate between {lowestRate} and {highestRate}, received {result.ReportedRate}.");
        }

        private static int AskTheRateLimiterABunchOfTimes(RateLimiter rateLimiter, int howManyTimes)
        {
            var remaining = howManyTimes;
            var allowedCount = 0;
            while (remaining-- > 0)
            {
                var allowed = rateLimiter.Allowed(1);
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

            if (parallelism > 10)
            {
                parallelism = 10;
            }

            var resetEvent = new ManualResetEventSlim(initialState: false); // Start blocked

            var workerReps = Enumerable.Range(1, parallelism).ToArray();

            var registry = new ConcurrentQueue<Thread>();

            var result = new RateLimitResult();

            test.Stopwatch.Start();
            var limiter = new RateLimiter(maxTracesPerInterval: intervalLimit);

            for (var i = 0; i < test.NumberOfBursts; i++)
            {
                var remaining = test.NumberPerBurst;

                var workers =
                    workerReps
                       .Select(t => new Thread(
                                   thread =>
                                   {
                                       resetEvent.Wait();
                                       while (remaining > 0)
                                       {
                                           Interlocked.Decrement(ref remaining);
                                           var id = Random.Value.NextUInt63();
                                           if (limiter.Allowed(id))
                                           {
                                               result.Allowed.Add(id);
                                           }
                                           else
                                           {
                                               result.Denied.Add(id);
                                           }
                                       }

                                       registry.TryDequeue(out _);
                                   }));

                foreach (var worker in workers)
                {
                    registry.Enqueue(worker);
                    worker.Start();
                }

                resetEvent.Set();

                Thread.Sleep(test.TimeBetweenBursts);

                resetEvent.Reset();
            }

            while (!registry.IsEmpty)
            {
                Thread.Sleep(100);
                if (registry.TryDequeue(out var item))
                {
                    if (item.IsAlive)
                    {
                        registry.Enqueue(item);
                    }
                }
            }

            test.Stopwatch.Stop();

            result.RateLimiter = limiter;
            result.ReportedRate = limiter.GetEffectiveRate();
            result.TimeElapsed = test.Stopwatch.Elapsed;

            return result;
        }

        private class RateLimitLoadTest
        {
            public int NumberPerBurst { get; set; }

            public TimeSpan TimeBetweenBursts { get; set; }

            public int NumberOfBursts { get; set; }

            public Stopwatch Stopwatch { get; } = new Stopwatch();
        }

        private class RateLimitResult
        {
            public RateLimiter RateLimiter { get; set; }

            public TimeSpan TimeElapsed { get; set; }

            public ConcurrentBag<ulong> Allowed { get; } = new ConcurrentBag<ulong>();

            public ConcurrentBag<ulong> Denied { get; } = new ConcurrentBag<ulong>();

            public float ReportedRate { get; set; }

            public int TotalAttempted => Allowed.Count + Denied.Count;

            public int TotalAllowed => Allowed.Count;

            public int TotalDenied => Denied.Count;
        }
    }
}
