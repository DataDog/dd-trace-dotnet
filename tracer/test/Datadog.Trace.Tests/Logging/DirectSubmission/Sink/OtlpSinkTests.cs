// <copyright file="OtlpSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.OpenTelemetry.Logs;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink
{
    public class OtlpSinkTests
    {
        private const int DefaultQueueLimit = 100_000;
        private static readonly TimeSpan TinyWait = TimeSpan.FromMilliseconds(50);

        [Fact]
        public void SinkSendsLogsToOtlpExporter()
        {
            using var mutex = new ManualResetEventSlim();
            var capturedLogs = new List<LogPoint>();

            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            var exporter = new TestOtlpExporter(logs =>
            {
                capturedLogs.AddRange(logs);
                mutex.Set();
                return Task.FromResult(ExportResult.Success);
            });
            var sink = new OtlpSubmissionLogSink(options, exporter);

            sink.Start();

            var logEvent = CreateTestLogEvent("First OTLP message", logLevel: 2); // Information
            sink.EnqueueLog(logEvent);

            // Wait for the logs to be sent
            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            capturedLogs.Should().ContainSingle();
            capturedLogs[0].Message.Should().Be("First OTLP message");
            capturedLogs[0].LogLevel.Should().Be(2);
        }

        [Fact]
        public void SinkBatchesMultipleLogs()
        {
            using var mutex = new ManualResetEventSlim();
            var capturedLogs = new List<LogPoint>();
            int batchCount = 0;

            var options = new BatchingSinkOptions(batchSizeLimit: 3, queueLimit: DefaultQueueLimit, period: TinyWait);
            var exporter = new TestOtlpExporter(logs =>
            {
                capturedLogs.AddRange(logs);
                Interlocked.Increment(ref batchCount);
                if (capturedLogs.Count >= 3)
                {
                    mutex.Set();
                }

                return Task.FromResult(ExportResult.Success);
            });
            var sink = new OtlpSubmissionLogSink(options, exporter);

            sink.Start();

            sink.EnqueueLog(CreateTestLogEvent("Log 1", logLevel: 1)); // Debug
            sink.EnqueueLog(CreateTestLogEvent("Log 2", logLevel: 2)); // Information
            sink.EnqueueLog(CreateTestLogEvent("Log 3", logLevel: 3)); // Warning

            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            capturedLogs.Should().HaveCount(3);
            capturedLogs[0].Message.Should().Be("Log 1");
            capturedLogs[1].Message.Should().Be("Log 2");
            capturedLogs[2].Message.Should().Be("Log 3");
        }

        [Fact]
        public void SinkIncludesTraceContextWhenAvailable()
        {
            using var mutex = new ManualResetEventSlim();
            var capturedLogs = new List<LogPoint>();

            var options = new BatchingSinkOptions(batchSizeLimit: 1, queueLimit: DefaultQueueLimit, period: TinyWait);
            var exporter = new TestOtlpExporter(logs =>
            {
                capturedLogs.AddRange(logs);
                mutex.Set();
                return Task.FromResult(ExportResult.Success);
            });
            var sink = new OtlpSubmissionLogSink(options, exporter);

            sink.Start();

            var traceId = RandomIdGenerator.Shared.NextTraceId(useAllBits: true);
            var spanId = RandomIdGenerator.Shared.NextSpanId();
            var logEvent = CreateTestLogEvent("Log with trace context", logLevel: 4, traceId: traceId, spanId: spanId);

            sink.EnqueueLog(logEvent);

            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            capturedLogs.Should().ContainSingle();
            capturedLogs[0].TraceId.Should().Be(traceId);
            capturedLogs[0].SpanId.Should().Be(spanId);
        }

        [Fact]
        public void SinkIncludesAttributesInLogs()
        {
            using var mutex = new ManualResetEventSlim();
            var capturedLogs = new List<LogPoint>();

            var options = new BatchingSinkOptions(batchSizeLimit: 1, queueLimit: DefaultQueueLimit, period: TinyWait);
            var exporter = new TestOtlpExporter(logs =>
            {
                capturedLogs.AddRange(logs);
                mutex.Set();
                return Task.FromResult(ExportResult.Success);
            });
            var sink = new OtlpSubmissionLogSink(options, exporter);

            sink.Start();

            var attributes = new Dictionary<string, object>
            {
                ["CustomAttribute"] = "CustomValue",
                ["EventId"] = 123,
                ["UserId"] = 456
            };

            var logEvent = CreateTestLogEvent("Log with attributes", logLevel: 2, attributes: attributes);

            sink.EnqueueLog(logEvent);

            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            capturedLogs.Should().ContainSingle();
            capturedLogs[0].Attributes.Should().ContainKey("CustomAttribute");
            capturedLogs[0].Attributes["CustomAttribute"].Should().Be("CustomValue");
            capturedLogs[0].Attributes["EventId"].Should().Be(123);
            capturedLogs[0].Attributes["UserId"].Should().Be(456);
        }

        [Fact]
        public void SinkIgnoresNonOtlpLogEvents()
        {
            using var mutex = new ManualResetEventSlim();
            var capturedLogs = new List<LogPoint>();

            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            var exporter = new TestOtlpExporter(logs =>
            {
                capturedLogs.AddRange(logs);
                if (capturedLogs.Count > 0)
                {
                    mutex.Set();
                }

                return Task.FromResult(ExportResult.Success);
            });
            var sink = new OtlpSubmissionLogSink(options, exporter);

            sink.Start();

            // Create a log event without OtlpLog (like a regular Datadog log)
            var regularLogEvent = new LoggerDirectSubmissionLogEvent("Regular Datadog log");
            sink.EnqueueLog(regularLogEvent);

            // Create an OTLP log event
            var otlpLogEvent = CreateTestLogEvent("OTLP log", logLevel: 2);
            sink.EnqueueLog(otlpLogEvent);

            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            capturedLogs.Should().ContainSingle(); // Only OTLP log should be captured
            capturedLogs[0].Message.Should().Be("OTLP log");
        }

        [Fact]
        public void SinkHandlesAllLogLevels()
        {
            using var mutex = new ManualResetEventSlim();
            var capturedLogs = new List<LogPoint>();

            var options = new BatchingSinkOptions(batchSizeLimit: 6, queueLimit: DefaultQueueLimit, period: TinyWait);
            var exporter = new TestOtlpExporter(logs =>
            {
                capturedLogs.AddRange(logs);
                if (capturedLogs.Count >= 6)
                {
                    mutex.Set();
                }

                return Task.FromResult(ExportResult.Success);
            });
            var sink = new OtlpSubmissionLogSink(options, exporter);

            sink.Start();

            sink.EnqueueLog(CreateTestLogEvent("Trace", logLevel: 0));
            sink.EnqueueLog(CreateTestLogEvent("Debug", logLevel: 1));
            sink.EnqueueLog(CreateTestLogEvent("Information", logLevel: 2));
            sink.EnqueueLog(CreateTestLogEvent("Warning", logLevel: 3));
            sink.EnqueueLog(CreateTestLogEvent("Error", logLevel: 4));
            sink.EnqueueLog(CreateTestLogEvent("Critical", logLevel: 5));

            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            capturedLogs.Should().HaveCount(6);
            capturedLogs.Select(l => l.LogLevel).Should().BeEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 });
        }

        private static LoggerDirectSubmissionLogEvent CreateTestLogEvent(
            string message,
            int logLevel,
            TraceId? traceId = null,
            ulong? spanId = null,
            Dictionary<string, object> attributes = null,
            string categoryName = "TestCategory")
        {
            return new LoggerDirectSubmissionLogEvent(null)
            {
                OtlpLog = new LogPoint
                {
                    Message = message,
                    LogLevel = logLevel,
                    CategoryName = categoryName,
                    Timestamp = DateTime.UtcNow,
                    Attributes = attributes ?? new Dictionary<string, object>(),
                    TraceId = traceId ?? TraceId.Zero,
                    SpanId = spanId ?? 0,
                    Flags = traceId.HasValue ? 1 : 0
                }
            };
        }

        /// <summary>
        /// Test exporter that allows controlling export behavior via delegate
        /// </summary>
        internal class TestOtlpExporter : IOtlpExporter
        {
            private readonly Func<IReadOnlyList<LogPoint>, Task<ExportResult>> _exportFunc;

            public TestOtlpExporter(Func<IReadOnlyList<LogPoint>, Task<ExportResult>> exportFunc)
            {
                _exportFunc = exportFunc;
            }

            public async Task<ExportResult> ExportAsync(IReadOnlyList<LogPoint> logs)
            {
                return await _exportFunc(logs).ConfigureAwait(false);
            }

            public bool Shutdown(int timeoutMilliseconds)
            {
                return true;
            }
        }
    }
}

#endif

