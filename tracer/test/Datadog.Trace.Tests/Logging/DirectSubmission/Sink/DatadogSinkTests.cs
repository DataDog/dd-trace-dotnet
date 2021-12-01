// <copyright file="DatadogSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink
{
    public class DatadogSinkTests
    {
        private const int DefaultQueueLimit = 100_000;
        private static readonly TimeSpan TinyWait = TimeSpan.FromMilliseconds(50);

        [Fact]
        public void SinkSendsMessagesToLogsApi()
        {
            var mutex = new ManualResetEventSlim();

            var logsApi = new TestLogsApi(_ => mutex.Set());
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            var sink = new DatadogSink(logsApi, SettingsHelper.GetFormatter(), options);

            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, "First message"));

            // Wait for the logs to be sent, should be done in 50ms
            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            logsApi.Logs.Should().ContainSingle();
        }

        [Fact]
        public void SinkRejectsGiantMessages()
        {
            var mutex = new ManualResetEventSlim();

            var logsApi = new TestLogsApi();
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            var sink = new DatadogSink(
                logsApi,
                SettingsHelper.GetFormatter(),
                options,
                oversizeLogCallback: _ => mutex.Set());

            var message = new StringBuilder().Append('x', repeatCount: 1024 * 1024).ToString();
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, message));

            // Wait for the logs to be sent, should be done in 50ms
            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            logsApi.Logs.Should().BeEmpty();
        }

        [Fact]
        public void SinkSendsMessageAsJsonBatch()
        {
            var mutex = new ManualResetEventSlim();
            int logsReceived = 0;
            const int expectedCount = 2;

            void LogsSentCallback(int x)
            {
                if (Interlocked.Add(ref logsReceived, x) == expectedCount)
                {
                    mutex.Set();
                }
            }

            var logsApi = new TestLogsApi(LogsSentCallback);
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            var sink = new DatadogSink(logsApi, SettingsHelper.GetFormatter(), options);

            var firstMessage = "First message";
            var secondMessage = "Second message";
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, firstMessage));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, secondMessage));

            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
        public void SinkSendsMultipleBatches()
        {
            var mutex = new ManualResetEventSlim();
            int logsReceived = 0;
            const int expectedCount = 5;

            void LogsSentCallback(int x)
            {
                if (Interlocked.Add(ref logsReceived, x) == expectedCount)
                {
                    mutex.Set();
                }
            }

            var logsApi = new TestLogsApi(LogsSentCallback);
            var options = new BatchingSinkOptions(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait);
            var sink = new DatadogSink(logsApi, SettingsHelper.GetFormatter(), options);

            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, "First message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Second message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Third message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Fourth message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Fifth message"));

            mutex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

            logsApi.Logs.Should().HaveCountGreaterOrEqualTo(3); // batch size is 2, so at least 3 batches

            var logs = logsApi.Logs
                              .Select(batch => Encoding.UTF8.GetString(batch.Logs.Array))
                              .SelectMany(batch => JsonConvert.DeserializeObject<List<TestLogEvent>>(batch))
                              .ToList();

            logs.Count.Should().Be(5);
            logs.Select(x => x.Message).Should().OnlyHaveUniqueItems();
        }

        internal class TestLogEvent : DatadogLogEvent
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
            private readonly Action<int> _logsSentCallback;

            public TestLogsApi(Action<int> logsSentCallback = null)
            {
                _logsSentCallback = logsSentCallback;
            }

            public Queue<SentMessage> Logs { get; } = new();

            public void Dispose()
            {
            }

            public Task SendLogsAsync(ArraySegment<byte> logs, int numberOfLogs)
            {
                // create a copy of it
                Logs.Enqueue(new SentMessage(new ArraySegment<byte>(logs.ToArray()), numberOfLogs));
                _logsSentCallback?.Invoke(numberOfLogs);
                return Task.FromResult(0);
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
    }
}
