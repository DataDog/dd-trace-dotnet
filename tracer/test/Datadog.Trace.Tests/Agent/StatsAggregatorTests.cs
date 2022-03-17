// <copyright file="StatsAggregatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class StatsAggregatorTests
    {
        [Fact]
        public void CallFlushAutomatically()
        {
            var duration = TimeSpan.FromSeconds(1);

            var mutex = new ManualResetEventSlim();

            int invocationCount = 0;

            var api = new Mock<IApi>();
            api.Setup(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), duration.ToNanoseconds()))
                .Callback(
                    () =>
                    {
                        if (Interlocked.Increment(ref invocationCount) == 2)
                        {
                            mutex.Set();
                        }
                    })
                .Returns(Task.FromResult(true));

            var settings = new TracerSettings { TracerStatsEnabled = true };

            using var aggregator = new StatsAggregator(api.Object, settings.Build(), duration);

            var stopwatch = Stopwatch.StartNew();

            bool success = false;

            while (stopwatch.Elapsed.Minutes < 1)
            {
                // Flush is not called if no spans are processed
                aggregator.Process(new Span(new SpanContext(1, 1), DateTime.UtcNow));

                if (mutex.Wait(TimeSpan.FromMilliseconds(100)))
                {
                    success = true;
                    break;
                }
            }

            success.Should().BeTrue();
        }

        [Fact]
        public async Task EmptyBuckets()
        {
            var api = new Mock<IApi>();
            var settings = new TracerSettings { TracerStatsEnabled = true };

            using var aggregator = new StatsAggregator(api.Object, settings.Build());

            await FluentActions.Invoking(() => aggregator.Flush(new CancellationToken(canceled: true)))
                .Should()
                .ThrowAsync<OperationCanceledException>();

            // No span is pushed so SendStatsAsync shouldn't be called
            api.Verify(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), It.IsAny<long>()), Times.Never);
        }
    }
}
