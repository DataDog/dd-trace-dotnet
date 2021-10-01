// <copyright file="MovingAverageKeepRateCalculatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Agent;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Agent
{
    public class MovingAverageKeepRateCalculatorTests
    {
        [TestCase(0, 0, 0)]
        [TestCase(0, 10, 0)]
        [TestCase(6, 4, 0.6)]
        [TestCase(9, 1, 0.9)]
        [TestCase(1, 9, 0.1)]
        [TestCase(10, 0, 1)]
        [TestCase(100, 1, 0.99)]
        public void Calculator_ShouldCalculateKeepRateCorrectly(int keep, int drop, double expected)
        {
            const int size = 5;

            var calc = new MovingAverageKeepRateCalculator(size, Timeout.InfiniteTimeSpan);

            calc.IncrementKeeps(keep);
            calc.IncrementDrops(drop);
            calc.UpdateBucket();

            var actualRate = calc.GetKeepRate();

            Assert.AreEqual(expected, actualRate, delta: 0.01);
        }

        [Theory]
        [TestCase(1, 0.1, 0.2, 0.3, 0.4, 0.5)]
        [TestCase(2, 0.1, 0.15, 0.25, 0.35, 0.45)]
        [TestCase(3, 0.1, 0.15, 0.2, 0.3, 0.4)]
        [TestCase(4, 0.1, 0.15, 0.2, 0.25, 0.35)]
        [TestCase(5, 0.1, 0.15, 0.2, 0.25, 0.3)]
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

                Assert.AreEqual(value.expectedRate, actualRate, delta: 0.01);
            }
        }

        [Test]
        public void Calculator_ShouldHandleOverflows()
        {
            const int size = 5;

            var calc = new MovingAverageKeepRateCalculator(size, Timeout.InfiniteTimeSpan);

            calc.IncrementKeeps(int.MaxValue);
            calc.IncrementKeeps(1);
            calc.UpdateBucket();

            var actualRate = calc.GetKeepRate();

            // should not be negative!
            Assert.AreEqual(expected: 1, actualRate);
        }

        [Test]
        [Ignore("Flaky as is very timing/load dependent")]
        public void Calculator_ShouldUpdateAutomatically()
        {
            const int bucketSize = 10;
            const int bucketDuration = 10;

            var calc = new MovingAverageKeepRateCalculator(bucketSize, TimeSpan.FromMilliseconds(bucketDuration));

            // precondition
            Assert.AreEqual(0, calc.GetKeepRate());

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
            Assert.AreEqual(0, calc.GetKeepRate());
        }
    }
}
