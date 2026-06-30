// <copyright file="OtlpLogsSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Datadog.Trace.OpenTelemetry.Logs;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry.Logs
{
    public class OtlpLogsSerializerTests
    {
        private const int InitialBufferSize = 64 * 1024;

        [Fact]
        public void SerializeLogs_SmallBatch_ProducesPayload()
        {
            var logs = new List<LogPoint>
            {
                new() { Message = "hello", LogLevel = 2, CategoryName = "Test" },
            };

            var payload = OtlpLogsSerializer.SerializeLogs(logs, CreateResourceTags());

            payload.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void SerializeLogs_EmptyBatch_ReturnsEmpty()
        {
            var payload = OtlpLogsSerializer.SerializeLogs(new List<LogPoint>(), CreateResourceTags());

            payload.Should().BeEmpty();
        }

        [Fact]
        public void SerializeLogs_BatchLargerThanInitialBuffer_GrowsAndSucceeds()
        {
            // Each message is ~2 KB; 200 logs serialize to well over the initial 64 KB buffer,
            // forcing multiple buffer resizes. Before the grow-and-retry fix this threw
            // ArgumentOutOfRangeException from WriteLogRecord once writePosition passed 64 KB.
            var largeMessage = new string('x', 2048);
            var logs = new List<LogPoint>();
            for (var i = 0; i < 200; i++)
            {
                logs.Add(new LogPoint { Message = largeMessage, LogLevel = 2, CategoryName = "Test" });
            }

            byte[] payload = null;
            var act = () => payload = OtlpLogsSerializer.SerializeLogs(logs, CreateResourceTags());

            act.Should().NotThrow();
            payload.Should().NotBeNull();

            // The serialized payload must be larger than the initial buffer, proving the
            // buffer grew rather than silently truncating the batch.
            payload!.Length.Should().BeGreaterThan(InitialBufferSize);
        }

        [Fact]
        public void SerializeLogs_RespectsStartPosition()
        {
            var logs = new List<LogPoint>
            {
                new() { Message = "hello", LogLevel = 2, CategoryName = "Test" },
            };

            // gRPC reserves 5 bytes at the start for the frame header.
            const int startPosition = 5;
            var payload = OtlpLogsSerializer.SerializeLogs(logs, CreateResourceTags(), startPosition);

            payload.Length.Should().BeGreaterThan(startPosition);
        }

        private static OtlpLogsSerializer.ResourceTags CreateResourceTags()
            => new(
                serviceName: "test-service",
                environment: "test-env",
                serviceVersion: "1.0.0",
                globalTags: new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));
    }
}

#endif
