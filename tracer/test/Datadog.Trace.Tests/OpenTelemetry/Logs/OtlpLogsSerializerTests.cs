// <copyright file="OtlpLogsSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

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
        public void TrySerializeLogs_SmallBatch_SucceedsAndReportsLength()
        {
            var logs = new List<LogPoint>
            {
                new() { Message = "hello", LogLevel = 2, CategoryName = "Test" },
            };
            var buffer = new byte[InitialBufferSize];

            var result = OtlpLogsSerializer.TrySerializeLogs(logs, buffer, CreateResourceTags(), out var bytesWritten);

            result.Should().BeTrue();
            bytesWritten.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TrySerializeLogs_EmptyBatch_SucceedsWithZeroBytes()
        {
            var buffer = new byte[InitialBufferSize];

            var result = OtlpLogsSerializer.TrySerializeLogs(new List<LogPoint>(), buffer, CreateResourceTags(), out var bytesWritten);

            result.Should().BeTrue();
            bytesWritten.Should().Be(0);
        }

        [Fact]
        public void TrySerializeLogs_BufferTooSmall_ReturnsFalseWithoutThrowing()
        {
            // A batch that serializes to ~400 KB cannot fit in a 64-byte buffer. Before the fix this
            // threw ArgumentOutOfRangeException from WriteLogRecord; now it must report false so the
            // caller can grow the buffer and retry.
            var logs = CreateLogs(count: 200, messageSize: 2048);
            var buffer = new byte[64];

            var result = false;
            var bytesWritten = -1;
            var act = () => result = OtlpLogsSerializer.TrySerializeLogs(logs, buffer, CreateResourceTags(), out bytesWritten);

            act.Should().NotThrow();
            result.Should().BeFalse();
            bytesWritten.Should().Be(0);
        }

        [Fact]
        public void TrySerializeLogs_BatchFitsInLargerBuffer_Succeeds()
        {
            // ~400 KB batch that overflows the initial 64 KB size but fits in a 1 MB buffer.
            var logs = CreateLogs(count: 200, messageSize: 2048);
            var buffer = new byte[1024 * 1024];

            var result = OtlpLogsSerializer.TrySerializeLogs(logs, buffer, CreateResourceTags(), out var bytesWritten);

            result.Should().BeTrue();
            bytesWritten.Should().BeGreaterThan(InitialBufferSize);
        }

        [Fact]
        public void TrySerializeLogs_RespectsStartPosition()
        {
            var logs = new List<LogPoint>
            {
                new() { Message = "hello", LogLevel = 2, CategoryName = "Test" },
            };
            var buffer = new byte[InitialBufferSize];

            // gRPC reserves 5 bytes at the start for the frame header.
            const int startPosition = 5;
            var result = OtlpLogsSerializer.TrySerializeLogs(logs, buffer, CreateResourceTags(), out var bytesWritten, startPosition);

            result.Should().BeTrue();
            bytesWritten.Should().BeGreaterThan(startPosition);
        }

        private static List<LogPoint> CreateLogs(int count, int messageSize)
        {
            var message = new string('x', messageSize);
            var logs = new List<LogPoint>(count);
            for (var i = 0; i < count; i++)
            {
                logs.Add(new LogPoint { Message = message, LogLevel = 2, CategoryName = "Test" });
            }

            return logs;
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
