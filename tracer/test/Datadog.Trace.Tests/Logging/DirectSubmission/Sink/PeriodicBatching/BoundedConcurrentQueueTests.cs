// <copyright file="BoundedConcurrentQueueTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/serilog/serilog-sinks-periodicbatching/blob/66a74768196758200bff67077167cde3a7e346d5/test/Serilog.Sinks.PeriodicBatching.Tests/BoundedConcurrentQueueTests.cs

using System;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink.PeriodicBatching
{
    public class BoundedConcurrentQueueTests
    {
        [Fact]
        public void WhenBoundedShouldNotExceedLimit()
        {
            const int limit = 100;
            var queue = new BoundedConcurrentQueue<int>(limit);

            for (var i = 0; i < limit * 2; i++)
            {
                queue.TryEnqueue(i);
            }

            queue.InnerQueue.Count.Should().Be(limit);
        }

        [Theory]
        [InlineData(-42)]
        [InlineData(0)]
        public void WhenInvalidLimitWillThrow(int limit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedConcurrentQueue<int>(limit));
        }
    }
}
