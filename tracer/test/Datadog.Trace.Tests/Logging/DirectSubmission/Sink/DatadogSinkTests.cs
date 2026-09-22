// <copyright file="DatadogSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink
{
    public class DatadogSinkTests
    {
        private const int FailuresBeforeCircuitBreak = 10;
        private const int DefaultQueueLimit = 100_000;
        private static readonly TimeSpan TinyWait = TimeSpan.FromMilliseconds(50);

        [Fact]
        public async Task SinkSendsMessagesToLogsApi()
        {
            var logsApi = new TestLogsApi();
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            await using var sink = new DirectSubmissionLogSink(logsApi, LogSettingsHelper.GetFormatter(), options, oversizeLogCallback: null, sinkDisabledCallback: null, startBackgroundLoop: false);
            sink.Start();

            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, "First message"));
            await sink.RunOneIterationAsync(noDelay: true);

            logsApi.Logs.Should().ContainSingle();
        }

        [Fact]
        public async Task SinkRejectsGiantMessages()
        {
            var oversizeFired = 0;
            var logsApi = new TestLogsApi();
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            await using var sink = new DirectSubmissionLogSink(
                logsApi,
                LogSettingsHelper.GetFormatter(),
                options,
                oversizeLogCallback: _ => Interlocked.Increment(ref oversizeFired),
                sinkDisabledCallback: () => { },
                startBackgroundLoop: false);
            sink.Start();

            var message = new StringBuilder().Append('x', repeatCount: 1024 * 1024).ToString();
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, message));
            await sink.RunOneIterationAsync(noDelay: true);

            oversizeFired.Should().Be(1);
            logsApi.Logs.Should().BeEmpty();
        }

        [Fact]
        public async Task SinkSendsMessageAsJsonBatch()
        {
            var logsApi = new TestLogsApi();
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            await using var sink = new DirectSubmissionLogSink(logsApi, LogSettingsHelper.GetFormatter(), options, oversizeLogCallback: null, sinkDisabledCallback: null, startBackgroundLoop: false);
            sink.Start();

            var firstMessage = "First message";
            var secondMessage = "Second message";
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, firstMessage));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, secondMessage));
            await sink.RunOneIterationAsync(noDelay: true);

            logsApi.Logs.Should().NotBeEmpty();

            var logs = logsApi.Logs
                              .Select(batch => Encoding.UTF8.GetString(batch.Logs.Array))
                              .SelectMany(batch => JsonConvert.DeserializeObject<List<TestLogEvent>>(batch))
                              .ToList();

            logs.Should().NotBeNull();
            logs.Count.Should().Be(2);
            logs[0].Level.Should().Be(DirectSubmissionLogLevel.Debug);
            logs[0].Message.Should().Be(firstMessage);
            logs[1].Level.Should().Be(DirectSubmissionLogLevel.Information);
            logs[1].Message.Should().Be(secondMessage);
        }

        [Fact]
        public async Task SinkSendsMultipleBatches()
        {
            var logsApi = new TestLogsApi();
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            await using var sink = new DirectSubmissionLogSink(logsApi, LogSettingsHelper.GetFormatter(), options, oversizeLogCallback: null, sinkDisabledCallback: null, startBackgroundLoop: false);
            sink.Start();

            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, "First message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Second message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Third message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Fourth message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Fifth message"));

            await sink.RunOneIterationAsync(noDelay: true);

            logsApi.Logs.Should().HaveCount(3); // batch size is 2, so exactly 3 batches (2+2+1)

            var logs = logsApi.Logs
                              .Select(batch => Encoding.UTF8.GetString(batch.Logs.Array))
                              .SelectMany(batch => JsonConvert.DeserializeObject<List<TestLogEvent>>(batch))
                              .ToList();

            logs.Count.Should().Be(5);
            logs.Select(x => x.Message).Should().OnlyHaveUniqueItems();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task EmitBatchEchoesLogsApiReturnValue(bool logsApiResponse)
        {
            var logsApi = new TestLogsApi(_ => logsApiResponse);
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            var sink = new TestSink(logsApi, LogSettingsHelper.GetFormatter(), options);
            sink.Start();
            var log = new TestLogEvent(DirectSubmissionLogLevel.Debug, "First message");
            var queue = new Queue<DirectSubmissionLogEvent>();
            queue.Enqueue(log);

            var result = await sink.CallEmitBatch(queue);

            result.Should().Be(logsApiResponse);
        }

        [Fact]
        public async Task IfCircuitBreakerBreaksThenNoApiRequestsAreSent()
        {
            var logsApi = new TestLogsApi(_ => false);
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            await using var sink = new DirectSubmissionLogSink(logsApi, LogSettingsHelper.GetFormatter(), options, oversizeLogCallback: null, sinkDisabledCallback: null, startBackgroundLoop: false);
            sink.Start();

            CircuitStatus lastStatus = default;
            for (var i = 0; i < FailuresBeforeCircuitBreak; i++)
            {
                sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, "A message"));
                lastStatus = await sink.RunOneIterationAsync(noDelay: true);
            }

            lastStatus.Should().Be(CircuitStatus.PermanentlyBroken);
            logsApi.Logs.Should().HaveCount(FailuresBeforeCircuitBreak);

            // circuit is permanently broken — further enqueues and iterations should not reach the API
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, "A message"));
            await sink.RunOneIterationAsync(noDelay: true);

            logsApi.Logs.Should().HaveCount(FailuresBeforeCircuitBreak);
        }

        internal class TestLogEvent : DirectSubmissionLogEvent
        {
            public TestLogEvent(DirectSubmissionLogLevel level, string message)
            {
                Level = level;
                Message = message;
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public DirectSubmissionLogLevel Level { get; }

            public string Message { get; }

            public override void Format(StringBuilder sb, LogFormatter formatter)
            {
                sb.Append(JsonConvert.SerializeObject(this));
            }
        }

        internal class TestLogsApi : ILogsApi
        {
            private readonly Func<int, bool> _logsSentCallback;

            public TestLogsApi(Func<int, bool> logsSentCallback = null)
            {
                _logsSentCallback = logsSentCallback;
            }

            public Queue<SentMessage> Logs { get; } = new();

            public void Dispose()
            {
            }

            public Task<bool> SendLogsAsync(ArraySegment<byte> logs, int numberOfLogs)
            {
                // create a copy of it
                Logs.Enqueue(new SentMessage(new ArraySegment<byte>(logs.ToArray()), numberOfLogs));
                var result = _logsSentCallback?.Invoke(numberOfLogs) ?? true;
                return Task.FromResult(result);
            }
        }

        internal class SentMessage
        {
            public SentMessage(ArraySegment<byte> logs, int numberOfLogs)
            {
                Logs = logs;
                NumberOfLogs = numberOfLogs;
            }

            public ArraySegment<byte> Logs { get; }

            public int NumberOfLogs { get; }
        }

        internal class TestSink
        {
            private readonly DirectSubmissionLogSink _sink;

            public TestSink(ILogsApi api, LogFormatter formatter, BatchingSinkOptions sinkOptions)
            {
                _sink = new(api, formatter, sinkOptions, oversizeLogCallback: null, sinkDisabledCallback: null, startBackgroundLoop: false);
            }

            public Task<bool> CallEmitBatch(Queue<DirectSubmissionLogEvent> events)
            {
                return _sink.EmitBatchForTesting(events);
            }

            public void Start() => _sink.Start();
        }
    }
}
