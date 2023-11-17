// <copyright file="RedactedErrorLogCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Text;
using Datadog.Trace.Logging.Internal;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Collectors;
using Datadog.Trace.Telemetry.DTOs;
using FluentAssertions;
using Xunit;
using JsonConvert = Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert;

namespace Datadog.Trace.Tests.Telemetry.Collectors;

public class RedactedErrorLogCollectorTests
{
    [Fact]
    public void DoesNotQueueMoreThanMaximumQueueSize()
    {
        var collector = new RedactedErrorLogCollector();
        var messagesToSend = RedactedErrorLogCollector.MaximumQueueSize * 2;
        for (var i = messagesToSend; i > 0; i--)
        {
            // Make messages unique to avoid de-duplication
            collector.EnqueueLog(new($"Something {i}", TelemetryLogLevel.WARN, DateTimeOffset.UtcNow));
        }

        var logs = collector.GetLogs();
        logs.Should().NotBeNull();
        logs.Sum(x => x.Count).Should().Be(RedactedErrorLogCollector.MaximumQueueSize);
    }

    [Fact]
    public void CompletesTaskWhenQueueSizeTriggerIsReached()
    {
        var collector = new RedactedErrorLogCollector();
        var threshold = RedactedErrorLogCollector.QueueSizeTrigger;
        var task = collector.WaitForLogsAsync();
        for (var i = 0; i < threshold; i++)
        {
            // make the messages unique to avoid de-duplication
            collector.EnqueueLog(new($"Something {i}", TelemetryLogLevel.WARN, DateTimeOffset.UtcNow));
            task.IsCompleted.Should().BeFalse();
        }

        // sending one more message should complete the task
        // and should remain completed subsequently
        for (var i = 0; i < 100; i++)
        {
            // make the messages unique to avoid de-duplication
            collector.EnqueueLog(new($"Something {i + threshold}", TelemetryLogLevel.WARN, DateTimeOffset.UtcNow));
            task.IsCompleted.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void GetApproximateSerializationSizeIsLargerThanRealWithAscii(int stringLength)
    {
        var message = RandomString(stringLength);
        var data = new LogMessageData(message, TelemetryLogLevel.DEBUG, DateTimeOffset.UtcNow)
        {
            StackTrace = RandomString(stringLength),
        };

        var serializedString = JsonConvert.SerializeObject(data);
        var bytes = Encoding.UTF8.GetBytes(serializedString);
        var expectedLength = bytes.Length;

        data.GetApproximateSerializationSize().Should().BeGreaterThanOrEqualTo(expectedLength);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void GetApproximateSerializationSizeIsRoughlySimilarWithAscii(int stringLength)
    {
        var message = RandomString(stringLength);
        var data = new LogMessageData(message, TelemetryLogLevel.DEBUG, DateTimeOffset.UtcNow)
        {
            StackTrace = RandomString(stringLength),
        };

        var serializedString = JsonConvert.SerializeObject(data);
        var bytes = Encoding.UTF8.GetBytes(serializedString);
        var realLength = bytes.Length;

        // expected for ascii is actually ~3x real life, so we end up being pretty conservative in our estimates
        data.GetApproximateSerializationSize().Should().BeLessThan(3 * realLength);
    }

    [Fact]
    public void GetApproximateSerializationSizeIsLargerThanRealWithUnicode()
    {
        var message = "☚℈⏁⽐⇔⮕⭝⹊✆⯹⸘Ⲿ⍤⹯❾⼢⟞⾨⃄▔⨿Ⲳⷽⱋ⛠⢙✍₥╓ↆ☂⠈⽧⤌⨔◬⽨⌤⩊⪠⎱Ⅳ⻙⑪⅙⪢⒌➇⮡⭎∣⼴➴⠹⭺⮐❢∂⅚⵩⥕⍎⭡✌⤭≁⢺Ⓑ⩏⭈⫋₡┐⛉℺⒤☙⎇ⴥ▁❚ⴁⲃ⾪⿪⺯⢋⼲╙⚱“ↇ⟴⏻⺭ⱀ⢜⧇⁇⡶";
        var data = new LogMessageData(message, TelemetryLogLevel.DEBUG, DateTimeOffset.UtcNow);

        var serializedString = JsonConvert.SerializeObject(data);
        var bytes = Encoding.UTF8.GetBytes(serializedString);
        var expectedLength = bytes.Length;

        data.GetApproximateSerializationSize().Should().BeGreaterThanOrEqualTo(expectedLength);
    }

    [Fact]
    public void SeparatesLogsIntoBatches()
    {
        var collector = new RedactedErrorLogCollector();

        // using a big string to make sure we don't exceed queue size
        var randomString = RandomString(10_000);
        var log = new LogMessageData(randomString + "_", TelemetryLogLevel.WARN, DateTimeOffset.UtcNow);
        var logSize = log.GetApproximateSerializationSize();
        var logsPerBatch = RedactedErrorLogCollector.MaximumBatchSizeBytes / logSize;

        // Filling 3.5 to avoid rounding issues etc
        var logsToSend = (logsPerBatch * 3) + (logsPerBatch / 2);
        var expectedBatches = 4;
        logsToSend.Should().BeLessThan(RedactedErrorLogCollector.MaximumQueueSize);

        for (var i = 0; i < logsToSend; i++)
        {
            // using a different log each time to avoid de-duplication
            collector.EnqueueLog(new LogMessageData(randomString + i, TelemetryLogLevel.WARN, DateTimeOffset.UtcNow));
        }

        var logs = collector.GetLogs();
        logs.Should().NotBeNull();
        logs.Sum(x => x.Count).Should().Be(logsToSend);
        logs.Count.Should().Be(expectedBatches);
        var batchSizes = logs.Select(batch => batch.Sum(log => log.GetApproximateSerializationSize()));
        batchSizes.Should().OnlyContain(size => size <= RedactedErrorLogCollector.MaximumBatchSizeBytes);
    }

    [Fact]
    public void RejectsOverSizedLogs()
    {
        var collector = new RedactedErrorLogCollector();

        var log = new LogMessageData(RandomString(2_000_000), TelemetryLogLevel.WARN, DateTimeOffset.UtcNow);
        log.GetApproximateSerializationSize().Should().BeGreaterOrEqualTo(RedactedErrorLogCollector.MaximumBatchSizeBytes);

        collector.EnqueueLog(log);

        var logs = collector.GetLogs();
        logs.Should().BeNull();
    }

    [Fact]
    public void WhenDeDupeEnabled_OnlySendsSingleLog()
    {
        var collector = new RedactedErrorLogCollector();
        const string message = "This is my message";

        // Add multiple identical messages
        const int count = 5;
        for (var i = 0; i < count; i++)
        {
            collector.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.ERROR, DateTimeOffset.UtcNow));
        }

        var logs = collector.GetLogs();

        var log = logs.Should()
            .NotBeNull()
            .And.ContainSingle()
            .Which.Should()
            .ContainSingle()
            .Subject;
        log.Message.Should().Be(message);
        log.Count.Should().Be(count);
    }

    [Fact]
    public void WhenDeDupeEnabled_AndIncludesExceptionFromSameLocation_OnlySendsSingleLog()
    {
        const string message = "This is my message";
        var collector = new RedactedErrorLogCollector();

        // Add multiple identical messages with exception from same location
        const int count = 5;
        for (var i = 0; i < count; i++)
        {
            var ex = GetException1();
            collector.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.ERROR, DateTimeOffset.UtcNow)
            {
                StackTrace = ExceptionRedactor.Redact(ex)
            });
        }

        var logs = collector.GetLogs();

        var log = logs.Should()
                      .NotBeNull()
                      .And.ContainSingle()
                      .Which.Should()
                      .ContainSingle()
                      .Subject;
        log.Message.Should().Be(message);
        log.Count.Should().Be(count);

        Exception GetException1()
        {
            try
            {
                throw new Exception(nameof(GetException1));
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }

    [Fact]
    public void WhenDeDupeEnabled_AndIncludesExceptionFromDifferentLocation_SendsDifferentLog()
    {
        const string message = "This is my message";
        var collector = new RedactedErrorLogCollector();

        // Add multiple identical messages with exception from same location
        const int count = 5;
        for (var i = 0; i < count; i++)
        {
            var ex = GetException1();
            collector.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.ERROR, DateTimeOffset.UtcNow)
            {
                StackTrace = ExceptionRedactor.Redact(ex)
            });
        }

        // Add multiple identical messages with exception from different location
        for (var i = 0; i < count; i++)
        {
            var ex = GetException2();
            collector.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.ERROR, DateTimeOffset.UtcNow)
            {
                StackTrace = ExceptionRedactor.Redact(ex)
            });
        }

        var batch = collector.GetLogs()
                             .Should()
                             .HaveCount(1)
                             .And.ContainSingle()
                             .Subject;

        batch.Should().HaveCount(2);
        batch[0].Message.Should().Be("This is my message");
        batch[0].Count.Should().Be(count);
        batch[1].Message.Should().Be("This is my message");
        batch[1].Count.Should().Be(count);

        Exception GetException1()
        {
            try
            {
                throw new Exception(nameof(GetException1));
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        Exception GetException2()
        {
            try
            {
                throw new Exception(nameof(GetException1));
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }

    [Fact]
    public void WhenDeDupeEnabled_AndSendsBatchInBetween_SendsSingleLogPerBatch()
    {
        const string message = "This is my message";
        var collector = new RedactedErrorLogCollector();

        // Add multiple identical messages
        const int count = 5;
        for (var i = 0; i < count; i++)
        {
            collector.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.ERROR, DateTimeOffset.UtcNow));
        }

        var firstBatch = collector.GetLogs();

        // Add multiple identical messages
        for (var i = 0; i < count; i++)
        {
            collector.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.ERROR, DateTimeOffset.UtcNow));
        }

        var secondBatch = collector.GetLogs();
        var thirdBatch = collector.GetLogs();

        var log = firstBatch.Should()
                  .NotBeNull()
                  .And.ContainSingle()
                  .Which.Should()
                  .ContainSingle()
                  .Subject;
        log.Message.Should().Be(message);
        log.Count.Should().Be(count);

        log = secondBatch.Should()
                   .NotBeNull()
                   .And.ContainSingle()
                   .Which.Should()
                   .ContainSingle()
                   .Subject;
        log.Message.Should().Be(message);
        log.Count.Should().Be(count);

        thirdBatch.Should().BeNull();
    }

    [Fact]
    public void WhenDeDupeEnabled_AndOverfills_GroupsAllIntoSingleBatch()
    {
        const string message = "This is my message";
        var collector = new RedactedErrorLogCollector();
        var messagesToSend = RedactedErrorLogCollector.MaximumQueueSize * 2;

        // Add too many identical messages
        for (var i = 0; i < messagesToSend; i++)
        {
            collector.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.ERROR, DateTimeOffset.UtcNow));
        }

        var logs = collector.GetLogs();
        collector.GetLogs().Should().BeNull();

        var log = logs.Should()
                      .NotBeNull()
                      .And.ContainSingle()
                      .Subject.Should()
                      .ContainSingle()
                      .Subject;
        log.Message.Should().Be(message);
        log.Count.Should().Be(messagesToSend);
    }

    [Fact]
    public void WhenDeDupeEnabled_AndUniqueMessages_DoesNotSetCount()
    {
        var collector = new RedactedErrorLogCollector();
        var messagesToSend = RedactedErrorLogCollector.MaximumQueueSize * 2;
        for (var i = messagesToSend; i > 0; i--)
        {
            // Make messages unique to avoid de-duplication
            collector.EnqueueLog(new($"Something {i}", TelemetryLogLevel.WARN, DateTimeOffset.UtcNow));
        }

        var logs = collector.GetLogs();
        logs.Should()
            .NotBeNull()
            .And.ContainSingle()
            .Subject
            .Should()
            .HaveCount(RedactedErrorLogCollector.MaximumQueueSize)
            .And
            .OnlyContain(x => x.Count == null);
    }

    private static string RandomString(int length)
    {
        const string options = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ,!\"£$%^&*()[]{}<>`¬";
        var chars = new char[length];
        var random = new Random();

        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = options[random.Next(options.Length)];
        }

        return new string(chars);
    }
}
