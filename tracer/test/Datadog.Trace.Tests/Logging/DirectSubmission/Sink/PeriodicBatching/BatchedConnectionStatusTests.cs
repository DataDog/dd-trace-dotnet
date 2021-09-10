// <copyright file="BatchedConnectionStatusTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/serilog/serilog-sinks-periodicbatching/blob/66a74768196758200bff67077167cde3a7e346d5/test/Serilog.Sinks.PeriodicBatching.Tests/BatchedConnectionStatusTests.cs

using System;
using System.Globalization;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink.PeriodicBatching
{
    public class BatchedConnectionStatusTests
    {
        private readonly TimeSpan _defaultPeriod = TimeSpan.FromSeconds(2);

        [Fact]
        public void WhenNoFailuresHaveOccurredTheRegularIntervalIsUsed()
        {
            var bcs = new BatchedConnectionStatus(_defaultPeriod);
            bcs.NextInterval.Should().Be(_defaultPeriod);
        }

        [Fact]
        public void WhenOneFailureHasOccurredTheRegularIntervalIsUsed()
        {
            var bcs = new BatchedConnectionStatus(_defaultPeriod);
            bcs.MarkFailure();
            bcs.NextInterval.Should().Be(_defaultPeriod);
        }

        [Fact]
        public void WhenTwoFailuresHaveOccurredTheIntervalBacksOff()
        {
            var bcs = new BatchedConnectionStatus(_defaultPeriod);
            bcs.MarkFailure();
            bcs.MarkFailure();
            bcs.NextInterval.Should().Be(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void WhenABatchSucceedsTheStatusResets()
        {
            var bcs = new BatchedConnectionStatus(_defaultPeriod);
            bcs.MarkFailure();
            bcs.MarkFailure();
            bcs.MarkSuccess();
            bcs.NextInterval.Should().Be(_defaultPeriod);
        }

        [Fact]
        public void WhenThreeFailuresHaveOccurredTheIntervalBacksOff()
        {
            var bcs = new BatchedConnectionStatus(_defaultPeriod);
            bcs.MarkFailure();
            bcs.MarkFailure();
            bcs.MarkFailure();
            bcs.NextInterval.Should().Be(TimeSpan.FromSeconds(20));
            Assert.False(bcs.ShouldDropBatch);
        }

        [Fact]
        public void When8FailuresHaveOccurredTheIntervalBacksOffAndBatchIsDropped()
        {
            var bcs = new BatchedConnectionStatus(_defaultPeriod);
            for (var i = 0; i < 8; ++i)
            {
                Assert.False(bcs.ShouldDropBatch);
                bcs.MarkFailure();
            }

            bcs.NextInterval.Should().Be(TimeSpan.FromMinutes(10));
            Assert.True(bcs.ShouldDropBatch);
            Assert.False(bcs.ShouldDropQueue);
        }

        [Fact]
        public void When10FailuresHaveOccurredTheQueueIsDropped()
        {
            var bcs = new BatchedConnectionStatus(_defaultPeriod);
            for (var i = 0; i < 10; ++i)
            {
                Assert.False(bcs.ShouldDropQueue);
                bcs.MarkFailure();
            }

            Assert.True(bcs.ShouldDropQueue);
        }

        [Fact]
        public void AtTheDefaultIntervalRetriesFor10MinutesBeforeDroppingBatch()
        {
            var bcs = new BatchedConnectionStatus(_defaultPeriod);
            var cumulative = TimeSpan.Zero;
            do
            {
                bcs.MarkFailure();

                if (!bcs.ShouldDropBatch)
                {
                    cumulative += bcs.NextInterval;
                }
            }
            while (!bcs.ShouldDropBatch);

            Assert.False(bcs.ShouldDropQueue);
            cumulative.Should().Be(TimeSpan.Parse("00:10:32", CultureInfo.InvariantCulture));
        }

        [Fact]
        public void AtTheDefaultIntervalRetriesFor30MinutesBeforeDroppingQueue()
        {
            var bcs = new BatchedConnectionStatus(_defaultPeriod);
            var cumulative = TimeSpan.Zero;
            do
            {
                bcs.MarkFailure();

                if (!bcs.ShouldDropQueue)
                {
                    cumulative += bcs.NextInterval;
                }
            }
            while (!bcs.ShouldDropQueue);

            cumulative.Should().Be(TimeSpan.Parse("00:30:32", CultureInfo.InvariantCulture));
        }
    }
}
