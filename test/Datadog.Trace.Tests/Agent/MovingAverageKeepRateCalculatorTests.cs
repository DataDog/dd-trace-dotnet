using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class MovingAverageKeepRateCalculatorTests
    {
        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(0, 10, 0)]
        [InlineData(6, 4, 0.6)]
        [InlineData(9, 1, 0.9)]
        [InlineData(1, 9, 0.1)]
        [InlineData(10, 0, 1)]
        [InlineData(100, 1, 0.99)]
        public void Calculator_ShouldCalculateKeepRateCorrectly(int keep, int drop, double expected)
        {
            const int size = 5;

            var calc = new MovingAverageKeepRateCalculator(size, Timeout.InfiniteTimeSpan);

            calc.IncrementKeeps(keep);
            calc.IncrementDrops(drop);
            calc.UpdateBucket();

            var actualRate = calc.GetKeepRate();

            Assert.Equal(expected, actualRate, precision: 2);
        }

        [Theory]
        [InlineData(1, 0.1, 0.2, 0.3, 0.4, 0.5)]
        [InlineData(2, 0.1, 0.15, 0.25, 0.35, 0.45)]
        [InlineData(3, 0.1, 0.15, 0.2, 0.3, 0.4)]
        [InlineData(4, 0.1, 0.15, 0.2, 0.25, 0.35)]
        [InlineData(5, 0.1, 0.15, 0.2, 0.25, 0.3)]
        public void Calculator_ShouldUpdateRates_BasedOnBucketSize(
            int size, double rate1, double rate2, double rate3, double rate4, double rate5)
        {
            var values = new[]
            {
                (keep: 1, drop: 9, expectedRate: rate1),
                (keep: 2, drop: 8, expectedRate: rate2),
                (keep: 3, drop: 7, expectedRate: rate3),
                (keep: 4, drop: 6, expectedRate: rate4),
                (keep: 5, drop: 5, expectedRate: rate5),
            };

            var calc = new MovingAverageKeepRateCalculator(size, Timeout.InfiniteTimeSpan);

            foreach (var value in values)
            {
                calc.IncrementKeeps(value.keep);
                calc.IncrementDrops(value.drop);

                calc.UpdateBucket();

                var actualRate = calc.GetKeepRate();

                Assert.Equal(value.expectedRate, actualRate, precision: 2);
            }
        }

        [Fact]
        public void Calculator_ShouldHandleOverflows()
        {
            const int size = 5;

            var calc = new MovingAverageKeepRateCalculator(size, Timeout.InfiniteTimeSpan);

            calc.IncrementKeeps(int.MaxValue);
            calc.IncrementKeeps(1);
            calc.UpdateBucket();

            var actualRate = calc.GetKeepRate();

            // should not be negative!
            Assert.Equal(expected: 1, actualRate);
        }

        [Fact(Skip = "Flaky as is very timing/load dependent")]
        public void Calculator_ShouldUpdateAutomatically()
        {
            const int bucketSize = 10;
            const int bucketDuration = 10;

            var calc = new MovingAverageKeepRateCalculator(bucketSize, TimeSpan.FromMilliseconds(bucketDuration));

            // precondition
            Assert.Equal(0, calc.GetKeepRate());

            calc.IncrementKeeps(10);
            calc.IncrementDrops(10);

            var updatedKeepRate = false;

            for (var i = 0; i < 20; i++)
            {
                var rate = calc.GetKeepRate();
                if (Math.Abs(rate - 0.5) < 0.01)
                {
                    // rate updated automatically
                    updatedKeepRate = true;
                    break;
                }

                Thread.Sleep(5);
            }

            Assert.True(updatedKeepRate, "Keep rate was not updated automatically");

            Thread.Sleep(bucketDuration * bucketSize);

            // buckets should all be empty again now
            Assert.Equal(0, calc.GetKeepRate());
        }
    }
}
