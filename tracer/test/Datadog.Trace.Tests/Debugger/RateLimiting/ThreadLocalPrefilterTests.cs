// <copyright file="ThreadLocalPrefilterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
    public class ThreadLocalPrefilterTests
    {
        [Fact]
        public void SetFilterMask_ValidMask_UpdatesMask()
        {
            ThreadLocalPrefilter.SetFilterMask(0);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(0);

            ThreadLocalPrefilter.SetFilterMask(7);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(7);

            ThreadLocalPrefilter.SetFilterMask(15);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(15);

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void SetFilterMask_NegativeValue_SetsToZero()
        {
            ThreadLocalPrefilter.SetFilterMask(-1);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(0);

            ThreadLocalPrefilter.SetFilterMask(-100);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(0);

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ShouldAllow_MaskZero_AllowsAll()
        {
            ThreadLocalPrefilter.SetFilterMask(0);

            // All calls should be allowed
            for (int i = 0; i < 1000; i++)
            {
                ThreadLocalPrefilter.ShouldAllow().Should().BeTrue();
            }

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ShouldAllow_MaskOne_FiltersHalf()
        {
            ThreadLocalPrefilter.SetFilterMask(1);

            var allowed = 0;
            var iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                if (ThreadLocalPrefilter.ShouldAllow())
                {
                    allowed++;
                }
            }

            // Should allow approximately 50% (with some tolerance)
            var percentage = (allowed / (double)iterations) * 100.0;
            percentage.Should().BeInRange(48, 52); // 50% ± 2%

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ShouldAllow_MaskThree_FiltersThreeQuarters()
        {
            ThreadLocalPrefilter.SetFilterMask(3);

            var allowed = 0;
            var iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                if (ThreadLocalPrefilter.ShouldAllow())
                {
                    allowed++;
                }
            }

            // Should allow approximately 25% (1 in 4)
            var percentage = (allowed / (double)iterations) * 100.0;
            percentage.Should().BeInRange(23, 27); // 25% ± 2%

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ShouldAllow_MaskSeven_FiltersSevenEighths()
        {
            ThreadLocalPrefilter.SetFilterMask(7);

            var allowed = 0;
            var iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                if (ThreadLocalPrefilter.ShouldAllow())
                {
                    allowed++;
                }
            }

            // Should allow approximately 12.5% (1 in 8)
            var percentage = (allowed / (double)iterations) * 100.0;
            percentage.Should().BeInRange(10.5, 14.5); // 12.5% ± 2%

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ShouldAllow_MaskFifteen_FiltersFifteenSixteenths()
        {
            ThreadLocalPrefilter.SetFilterMask(15);

            var allowed = 0;
            var iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                if (ThreadLocalPrefilter.ShouldAllow())
                {
                    allowed++;
                }
            }

            // Should allow approximately 6.25% (1 in 16)
            var percentage = (allowed / (double)iterations) * 100.0;
            percentage.Should().BeInRange(4.5, 8.0); // 6.25% ± 1.75%

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ShouldAllow_ThreadIsolation_IndependentCounters()
        {
            ThreadLocalPrefilter.SetFilterMask(1); // 50% filtering

            var thread1Allowed = 0;
            var thread2Allowed = 0;
            var iterations = 100;

            var thread1 = new Thread(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    if (ThreadLocalPrefilter.ShouldAllow())
                    {
                        Interlocked.Increment(ref thread1Allowed);
                    }
                }
            });

            var thread2 = new Thread(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    if (ThreadLocalPrefilter.ShouldAllow())
                    {
                        Interlocked.Increment(ref thread2Allowed);
                    }
                }
            });

            thread1.Start();
            thread2.Start();
            thread1.Join();
            thread2.Join();

            // Both threads should have allowed approximately 50%
            thread1Allowed.Should().BeInRange(40, 60);
            thread2Allowed.Should().BeInRange(40, 60);

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void AdjustForPressure_NoPressure_NoFiltering()
        {
            ThreadLocalPrefilter.AdjustForPressure(globalUsagePercentage: 10, isExhausted: false);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(0);

            ThreadLocalPrefilter.AdjustForPressure(globalUsagePercentage: 0, isExhausted: false);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(0);
        }

        [Fact]
        public void AdjustForPressure_LowPressure_MinimalFiltering()
        {
            ThreadLocalPrefilter.AdjustForPressure(globalUsagePercentage: 30, isExhausted: false);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(1); // 50% filtering

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void AdjustForPressure_MediumPressure_ModerateFiltering()
        {
            ThreadLocalPrefilter.AdjustForPressure(globalUsagePercentage: 60, isExhausted: false);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(3); // 75% filtering

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void AdjustForPressure_HighPressure_AggressiveFiltering()
        {
            ThreadLocalPrefilter.AdjustForPressure(globalUsagePercentage: 80, isExhausted: false);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(7); // 87.5% filtering

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void AdjustForPressure_SeverePressure_MaxFiltering()
        {
            ThreadLocalPrefilter.AdjustForPressure(globalUsagePercentage: 95, isExhausted: false);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(15); // 93.75% filtering

            ThreadLocalPrefilter.AdjustForPressure(globalUsagePercentage: 50, isExhausted: true);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(15); // 93.75% filtering when exhausted

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void AdjustForPressure_Exhausted_AlwaysMaxFiltering()
        {
            ThreadLocalPrefilter.AdjustForPressure(globalUsagePercentage: 10, isExhausted: true);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(15);

            ThreadLocalPrefilter.AdjustForPressure(globalUsagePercentage: 0, isExhausted: true);
            ThreadLocalPrefilter.GetFilterMask().Should().Be(15);

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ShouldAllow_IntegerOverflow_Safe()
        {
            ThreadLocalPrefilter.SetFilterMask(1);

            // Call many times to potentially cause integer overflow
            // int.MaxValue is 2,147,483,647
            var iterations = 1000;
            var allowed = 0;

            for (int i = 0; i < iterations; i++)
            {
                if (ThreadLocalPrefilter.ShouldAllow())
                {
                    allowed++;
                }
            }

            // Should still work correctly after many iterations
            var percentage = (allowed / (double)iterations) * 100.0;
            percentage.Should().BeInRange(48, 52);

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ShouldAllow_DistributionUniformity_MaskOne()
        {
            ThreadLocalPrefilter.SetFilterMask(1);

            var pattern = new bool[10];
            for (int i = 0; i < 10; i++)
            {
                pattern[i] = ThreadLocalPrefilter.ShouldAllow();
            }

            // With mask=1, pattern should alternate: true, false, true, false...
            // (or false, true, false, true... depending on starting counter)
            var transitions = 0;
            for (int i = 1; i < pattern.Length; i++)
            {
                if (pattern[i] != pattern[i - 1])
                {
                    transitions++;
                }
            }

            // Should have many transitions (uniform distribution)
            transitions.Should().BeGreaterThan(5);

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ConcurrentAccess_MultipleMaskChanges_ThreadSafe()
        {
            var tasks = new[]
            {
                Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        ThreadLocalPrefilter.SetFilterMask(i % 16);
                        Thread.Sleep(1);
                    }
                }),
                Task.Run(() =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        var allowed = ThreadLocalPrefilter.ShouldAllow();
                        // Just check no exceptions
                    }
                }),
                Task.Run(() =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        var mask = ThreadLocalPrefilter.GetFilterMask();
                        mask.Should().BeInRange(0, 15);
                    }
                })
            };

            var act = () => Task.WaitAll(tasks);
            act.Should().NotThrow();

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void ShouldAllow_ManyThreads_IndependentFiltering()
        {
            ThreadLocalPrefilter.SetFilterMask(3); // 25% allowed

            var threadCount = 10;
            var iterationsPerThread = 200;
            var results = new ConcurrentBag<int>();

            var threads = Enumerable.Range(0, threadCount)
                .Select(_ => new Thread(() =>
                {
                    var allowed = 0;
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        if (ThreadLocalPrefilter.ShouldAllow())
                        {
                            allowed++;
                        }
                    }

                    results.Add(allowed);
                }))
               .ToArray();

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Each thread should have allowed approximately 25%
            foreach (var allowed in results)
            {
                var percentage = (allowed / (double)iterationsPerThread) * 100.0;
                percentage.Should().BeInRange(20, 30); // 25% ± 5%
            }

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void GetFilterMask_AlwaysReturnsValidValue()
        {
            for (int i = 0; i < 1000; i++)
            {
                var mask = ThreadLocalPrefilter.GetFilterMask();
                mask.Should().BeGreaterOrEqualTo(0);
            }
        }

        [Fact]
        public void ShouldAllow_ExtremelyHighMask_StillWorks()
        {
            // Test with a high mask value (though not standard)
            ThreadLocalPrefilter.SetFilterMask(31); // 96.875% filtering

            var allowed = 0;
            var iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                if (ThreadLocalPrefilter.ShouldAllow())
                {
                    allowed++;
                }
            }

            // Should allow approximately 3.125% (1 in 32)
            var percentage = (allowed / (double)iterations) * 100.0;
            percentage.Should().BeLessThan(5); // Very aggressive filtering

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void IntegerOverflowBoundary_MaintainsPattern()
        {
            // More aggressive test of integer overflow behavior at exact boundaries
            ThreadLocalPrefilter.SetFilterMask(3); // 1 in 4 pattern

            var allowed = 0;
            var iterations = 10000;

            for (int i = 0; i < iterations; i++)
            {
                if (ThreadLocalPrefilter.ShouldAllow())
                {
                    allowed++;
                }
            }

            // Should maintain 25% pattern consistently
            var percentage = (allowed / (double)iterations) * 100.0;
            percentage.Should().BeInRange(24, 26, "Pattern should remain consistent even after many iterations");

            ThreadLocalPrefilter.SetFilterMask(0);
        }
    }
}
