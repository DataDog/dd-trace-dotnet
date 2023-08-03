// <copyright file="DiagnosticLogCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Collectors;
using Datadog.Trace.Telemetry.DTOs;
using FluentAssertions;
using Xunit;
using JsonConvert = Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert;

namespace Datadog.Trace.Tests.Telemetry.Collectors;

public class DiagnosticLogCollectorTests
{
    [Fact]
    public void DoesNotQueueMoreThanMaximumQueueSize()
    {
        var collector = new DiagnosticLogCollector();
        var messagesToSend = DiagnosticLogCollector.MaximumQueueSize * 2;
        while (messagesToSend > 0)
        {
            collector.EnqueueLog(new("Something", TelemetryLogLevel.WARN, DateTimeOffset.UtcNow));
            messagesToSend--;
        }

        var logs = collector.GetLogs();
        logs.Should().NotBeNull();
        logs.Sum(x => x.Count).Should().Be(DiagnosticLogCollector.MaximumQueueSize);
    }

    [Fact]
    public void CompletesTaskWhenQueueSizeTriggerIsReached()
    {
        var collector = new DiagnosticLogCollector();
        var threshold = DiagnosticLogCollector.QueueSizeTrigger;
        var task = collector.WaitForLogsAsync();
        var messages = 0;
        while (messages < threshold)
        {
            collector.EnqueueLog(new("Something", TelemetryLogLevel.WARN, DateTimeOffset.UtcNow));
            messages++;
            task.IsCompleted.Should().BeFalse();
        }

        // sending one more message should complete the task
        // and should remain completed subsequently
        messages = 0;
        while (messages < 100)
        {
            collector.EnqueueLog(new("Something", TelemetryLogLevel.WARN, DateTimeOffset.UtcNow));
            messages++;
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
        var data = new DiagnosticLogMessageData(message, TelemetryLogLevel.DEBUG, DateTimeOffset.UtcNow)
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
        var data = new DiagnosticLogMessageData(message, TelemetryLogLevel.DEBUG, DateTimeOffset.UtcNow)
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
        var data = new DiagnosticLogMessageData(message, TelemetryLogLevel.DEBUG, DateTimeOffset.UtcNow);

        var serializedString = JsonConvert.SerializeObject(data);
        var bytes = Encoding.UTF8.GetBytes(serializedString);
        var expectedLength = bytes.Length;

        data.GetApproximateSerializationSize().Should().BeGreaterThanOrEqualTo(expectedLength);
    }

    [Fact]
    public void SeparatesLogsIntoBatches()
    {
        var collector = new DiagnosticLogCollector();

        // using a big string to make sure we don't exceed queue size
        var log = new DiagnosticLogMessageData(RandomString(10_000), TelemetryLogLevel.WARN, DateTimeOffset.UtcNow);
        var logSize = log.GetApproximateSerializationSize();
        var logsPerBatch = DiagnosticLogCollector.MaximumBatchSizeBytes / logSize;

        // Filling 3.5 to avoid rounding issues etc
        var logsToSend = (logsPerBatch * 3) + (logsPerBatch / 2);
        var expectedBatches = 4;
        logsToSend.Should().BeLessThan(DiagnosticLogCollector.MaximumQueueSize);

        for (var i = 0; i < logsToSend; i++)
        {
            collector.EnqueueLog(log);
        }

        var logs = collector.GetLogs();
        logs.Should().NotBeNull();
        logs.Sum(x => x.Count).Should().Be(logsToSend);
        logs.Count.Should().Be(expectedBatches);
        var batchSizes = logs.Select(batch => batch.Sum(log => log.GetApproximateSerializationSize()));
        batchSizes.Should().OnlyContain(size => size <= DiagnosticLogCollector.MaximumBatchSizeBytes);
    }

    [Fact]
    public void RejectsOverSizedLogs()
    {
        var collector = new DiagnosticLogCollector();

        var log = new DiagnosticLogMessageData(RandomString(2_000_000), TelemetryLogLevel.WARN, DateTimeOffset.UtcNow);
        log.GetApproximateSerializationSize().Should().BeGreaterOrEqualTo(DiagnosticLogCollector.MaximumBatchSizeBytes);

        collector.EnqueueLog(log);

        var logs = collector.GetLogs();
        logs.Should().BeNull();
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
