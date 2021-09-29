// <copyright file="DatadogSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private static readonly int TinyWaitMs = 50;

        [Fact]
        public async Task SinkSendsMessagesToLogsApi()
        {
            var logsApi = new TestLogsApi();
            var options = new BatchingSinkOptions(batchSizeLimit: 2, periodMs: TinyWaitMs);
            var sink = new DatadogSink(logsApi, SettingsHelper.GetFormatter(), options);

            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, "First message"));

            for (var i = 0; i < 10; i++)
            {
                if (logsApi.Logs.Count > 0)
                {
                    // done
                    return;
                }

                await Task.Delay(TinyWaitMs);
            }

            throw new Exception("No logs were sent to the logs api");
        }

        [Fact]
        public async Task SinkRejectsGiantMessages()
        {
            var logsApi = new TestLogsApi();
            var options = new BatchingSinkOptions(batchSizeLimit: 2, periodMs: TinyWaitMs);
            var sink = new DatadogSink(logsApi, SettingsHelper.GetFormatter(), options);

            var message = new StringBuilder().Append('x', repeatCount: 1024 * 1024).ToString();
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, message));

            for (var i = 0; i < 10; i++)
            {
                if (logsApi.Logs.Count > 0)
                {
                    // done
                    return;
                }

                await Task.Delay(TinyWaitMs);
            }

            logsApi.Logs.Count.Should().Be(0);
        }

        [Fact]
        public async Task SinkSendsMessageAsJsonBatch()
        {
            var logsApi = new TestLogsApi();
            var options = new BatchingSinkOptions(batchSizeLimit: 2, periodMs: TinyWaitMs);
            var sink = new DatadogSink(logsApi, SettingsHelper.GetFormatter(), options);

            var firstMessage = "First message";
            var secondMessage = "Second message";
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, firstMessage));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, secondMessage));

            SentMessage batch = null;
            for (var i = 0; i < 10; i++)
            {
                if (logsApi.Logs.Count > 0)
                {
                    batch = logsApi.Logs.Dequeue();
                }

                await Task.Delay(20);
            }

            if (batch is null)
            {
                throw new Exception("No logs were sent to the logs api");
            }

            var serializedBatch = Encoding.UTF8.GetString(batch.Logs.Array);
            var logs = JsonConvert.DeserializeObject<List<TestLogEvent>>(serializedBatch);
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
            var options = new BatchingSinkOptions(batchSizeLimit: 2, periodMs: TinyWaitMs);
            var sink = new DatadogSink(logsApi, SettingsHelper.GetFormatter(), options);

            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Debug, "First message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Second message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Third message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Fourth message"));
            sink.EnqueueLog(new TestLogEvent(DirectSubmissionLogLevel.Information, "Fifth message"));

            List<SentMessage> batches = new();
            for (var i = 0; i < 10; i++)
            {
                if (logsApi.Logs.Count > 0)
                {
                    var batch = logsApi.Logs.Dequeue();
                    if (batch is not null)
                    {
                        batches.Add(batch);
                    }
                }

                await Task.Delay(TinyWaitMs);
            }

            batches.Should().HaveCountGreaterOrEqualTo(3); // batch size is 2, so at least 3 batches

            var logs = batches
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
            public Queue<SentMessage> Logs { get; } = new();

            public void Dispose()
            {
            }

            public Task SendLogsAsync(ArraySegment<byte> logs, int numberOfLogs)
            {
                // create a copy of it
                Logs.Enqueue(new SentMessage(new ArraySegment<byte>(logs.ToArray()), numberOfLogs));
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
