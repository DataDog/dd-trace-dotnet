// <copyright file="TelemetryLogsSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using Datadog.Trace.Logging.Internal.Sinks;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging;

public class TelemetryLogsSinkTests
{
    private static readonly ApplicationTelemetryData AppData = new("my-serv", "integration-tests", TracerConstants.AssemblyVersion, "dotnet", FrameworkDescription.Instance.ProductVersion);
    private static readonly HostTelemetryData HostData = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DoesNotSendBatchesUntilInitialized(bool deDuplicationEnabled)
    {
        var options = new BatchingSinkOptions(
            batchSizeLimit: 10,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options, deDuplicationEnabled: deDuplicationEnabled);

        for (var i = 0; i < 150; i++)
        {
            // adding more logs than we're allowed
            sink.EnqueueLog(new LogMessageData("This is my message " + i, TelemetryLogLevel.DEBUG));
        }

        var wasFlushed = sink.BatchSentMutex.Wait(TimeSpan.FromSeconds(5)); // plenty of time to flush if it's going to

        wasFlushed.Should().BeFalse();

        // Now initialize
        sink.Initialize(
            AppData,
            HostData,
            new TelemetryDataBuilder(),
            new[] { new TestTransport(Enumerable.Repeat(TelemetryPushResult.Success, 10).ToArray()) });

        WaitForBatchCount(sink, count: 10);

        sink.Batches.Should()
            .HaveCount(10)
            .And.OnlyContain(x => x.Length == 10);
    }

    [Fact]
    public async Task OnlySendsWithFirstTransportWhenSucceeds()
    {
        var options = new BatchingSinkOptions(
            batchSizeLimit: 1,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options);

        // initialize
        var transports = new[]
        {
            new TestTransport(TelemetryPushResult.Success),
            new TestTransport(TelemetryPushResult.Success),
        };

        sink.Initialize(AppData, HostData, new TelemetryDataBuilder(), transports);

        sink.EnqueueLog(new LogMessageData("This is my message", TelemetryLogLevel.DEBUG));

        WaitForBatchCount(sink, count: 1);

        await WaitForPushAttempts(transports[0], count: 1);
        transports[1].PushAttempts.Should().HaveCount(0);

        sink.Batches.Should()
            .HaveCount(1)
            .And.OnlyContain(x => x.Length == 1);
    }

    [Fact]
    public async Task IfTransportFails_SendsWithNextAvailable()
    {
        var options = new BatchingSinkOptions(
            batchSizeLimit: 1,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options);

        // initialize
        var transports = new[]
        {
            new TestTransport(TelemetryPushResult.TransientFailure, TelemetryPushResult.Success),
            new TestTransport(TelemetryPushResult.Success, TelemetryPushResult.TransientFailure),
        };

        sink.Initialize(AppData, HostData, new TelemetryDataBuilder(), transports);

        sink.EnqueueLog(new LogMessageData("This is my message", TelemetryLogLevel.DEBUG));

        WaitForBatchCount(sink, count: 1, seconds: 600);

        await WaitForPushAttempts(transports[0], count: 1);
        await WaitForPushAttempts(transports[1], count: 1);

        sink.EnqueueLog(new LogMessageData("This is my second message", TelemetryLogLevel.DEBUG));
        WaitForBatchCount(sink, count: 2, seconds: 600);

        await WaitForPushAttempts(transports[0], count: 2);
        await WaitForPushAttempts(transports[1], count: 2);

        sink.Batches.Should()
            .HaveCount(2)
            .And.OnlyContain(x => x.Length == 1);
    }

    [Fact]
    public async Task IfTransportFailsFatally_StartsWithSuccessfulSinkFirst()
    {
        var options = new BatchingSinkOptions(
            batchSizeLimit: 1,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options);

        // initialize
        var transports = new[]
        {
            new TestTransport(TelemetryPushResult.FatalError),
            new TestTransport(TelemetryPushResult.Success, TelemetryPushResult.Success),
        };

        sink.Initialize(AppData, HostData, new TelemetryDataBuilder(), transports);

        sink.EnqueueLog(new LogMessageData("This is my message", TelemetryLogLevel.DEBUG));

        WaitForBatchCount(sink, count: 1);

        await WaitForPushAttempts(transports[0], count: 1);
        await WaitForPushAttempts(transports[1], count: 1);

        sink.EnqueueLog(new LogMessageData("This is my second message", TelemetryLogLevel.DEBUG));
        WaitForBatchCount(sink, count: 2);

        // should send with second sink only this time, as previous success
        await WaitForPushAttempts(transports[0], count: 1);
        await WaitForPushAttempts(transports[1], count: 2);

        sink.Batches.Should()
            .HaveCount(2)
            .And.OnlyContain(x => x.Length == 1);
    }

    [Fact]
    public async Task IfTransportFailsFatallyOnAllTransports_DoesntTryToResend()
    {
        var options = new BatchingSinkOptions(
            batchSizeLimit: 1,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options);

        // initialize
        var transports = new[]
        {
            new TestTransport(TelemetryPushResult.FatalError),
            new TestTransport(TelemetryPushResult.FatalError),
        };

        sink.Initialize(AppData, HostData, new TelemetryDataBuilder(), transports);

        sink.EnqueueLog(new LogMessageData("This is my message", TelemetryLogLevel.DEBUG));

        WaitForBatchCount(sink, count: 1);

        await WaitForPushAttempts(transports[0], count: 1);
        await WaitForPushAttempts(transports[1], count: 1);

        sink.EnqueueLog(new LogMessageData("This is my second message", TelemetryLogLevel.DEBUG));
        WaitForBatchCount(sink, count: 2);

        // Doesn't retry sending as no enabled transports
        await WaitForPushAttempts(transports[0], count: 1);
        await WaitForPushAttempts(transports[1], count: 1);

        sink.Batches.Should()
            .HaveCount(2)
            .And.OnlyContain(x => x.Length == 1);
    }

    [Fact]
    public async Task IfTransportFails_SendsWithNextAvailable_WhenAllFailInitially()
    {
        var options = new BatchingSinkOptions(
            batchSizeLimit: 1,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options);

        // initialize
        var transports = new[]
        {
            new TestTransport(TelemetryPushResult.FatalError),
            new TestTransport(TelemetryPushResult.TransientFailure, TelemetryPushResult.Success),
        };

        sink.Initialize(AppData, HostData, new TelemetryDataBuilder(), transports);

        sink.EnqueueLog(new LogMessageData("This is my message", TelemetryLogLevel.DEBUG));

        WaitForBatchCount(sink, count: 1, seconds: 600);

        await WaitForPushAttempts(transports[0], count: 1);
        await WaitForPushAttempts(transports[1], count: 1);

        sink.EnqueueLog(new LogMessageData("This is my second message", TelemetryLogLevel.DEBUG));
        WaitForBatchCount(sink, count: 2, seconds: 600);

        await WaitForPushAttempts(transports[0], count: 1);
        await WaitForPushAttempts(transports[1], count: 2);

        sink.Batches.Should()
            .HaveCount(2)
            .And.OnlyContain(x => x.Length == 1);
    }

    [Fact]
    public void WhenDeDupeEnabled_OnlySendsSingleLog()
    {
        var options = new BatchingSinkOptions(
            batchSizeLimit: 10,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options, deDuplicationEnabled: true);

        // Add multiple identical messages
        for (var i = 0; i < 5; i++)
        {
            sink.EnqueueLog(new LogMessageData("This is my message", TelemetryLogLevel.DEBUG));
        }

        // Now initialize
        sink.Initialize(
            AppData,
            HostData,
            new TelemetryDataBuilder(),
            new ITelemetryTransport[] { new TestTransport(TelemetryPushResult.Success) });

        WaitForBatchCount(sink, count: 1);

        sink.Batches.Should()
            .HaveCount(1)
            .And.ContainSingle()
            .Subject.Should()
            .ContainSingle()
            .Subject.Message.Should()
            .Be("This is my message. 4 additional messages skipped.");
    }

    [Fact]
    public void WhenDeDupeEnabled_AndIncludesExceptionFromSameLocation_OnlySendsSingleLog()
    {
        const string message = "This is my message";
        var options = new BatchingSinkOptions(
            batchSizeLimit: 10,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options, deDuplicationEnabled: true);

        // Add multiple identical messages with exception from same location
        for (var i = 0; i < 5; i++)
        {
            var ex = GetException1();
            sink.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.DEBUG) { StackTrace = ExceptionRedactor.Redact(ex) });
        }

        // Now initialize
        sink.Initialize(
            AppData,
            HostData,
            new TelemetryDataBuilder(),
            new ITelemetryTransport[] { new TestTransport(TelemetryPushResult.Success) });

        WaitForBatchCount(sink, count: 1);

        sink.Batches.Should()
            .HaveCount(1)
            .And.ContainSingle()
            .Subject.Should()
            .ContainSingle()
            .Subject.Message.Should()
            .Be("This is my message. 4 additional messages skipped.");

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
        var options = new BatchingSinkOptions(
            batchSizeLimit: 10,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options, deDuplicationEnabled: true);

        // Add multiple identical messages with exception from same location
        for (var i = 0; i < 5; i++)
        {
            var ex = GetException1();
            sink.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.DEBUG) { StackTrace = ExceptionRedactor.Redact(ex) });
        }

        // Add multiple identical messages with exception from different location
        for (var i = 0; i < 5; i++)
        {
            var ex = GetException2();
            sink.EnqueueLog(new LogMessageData(message, TelemetryLogLevel.DEBUG) { StackTrace = ExceptionRedactor.Redact(ex) });
        }

        // Now initialize
        sink.Initialize(
            AppData,
            HostData,
            new TelemetryDataBuilder(),
            new ITelemetryTransport[] { new TestTransport(TelemetryPushResult.Success) });

        WaitForBatchCount(sink, count: 1);

        var batch = sink.Batches.Should()
            .HaveCount(1)
            .And.ContainSingle()
            .Subject;
        batch.Should().HaveCount(2);
        batch[0].Message.Should().Be("This is my message. 4 additional messages skipped.");
        batch[1].Message.Should().Be("This is my message. 4 additional messages skipped.");

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
        var sendingBatchMutex = new ManualResetEventSlim();
        var options = new BatchingSinkOptions(
            batchSizeLimit: 10,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options, deDuplicationEnabled: true);
        sink.SendingBatch = _ =>
        {
            if (sendingBatchMutex is not null && !sendingBatchMutex.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new Exception("Timeout waiting for send batch signal");
            }

            return Task.CompletedTask;
        };

        // Add multiple identical messages
        for (var i = 0; i < 5; i++)
        {
            sink.EnqueueLog(new LogMessageData("This is my message", TelemetryLogLevel.DEBUG));
        }

        // Now initialize
        sink.Initialize(
            AppData,
            HostData,
            new TelemetryDataBuilder(),
            new ITelemetryTransport[] { new TestTransport(TelemetryPushResult.Success) });

        sendingBatchMutex.Set();
        WaitForBatchCount(sink, count: 1);
        sendingBatchMutex = null;

        // Add multiple identical messages
        for (var i = 0; i < 5; i++)
        {
            sink.EnqueueLog(new LogMessageData("This is my second message", TelemetryLogLevel.DEBUG));
        }

        WaitForBatchCount(sink, count: 2);

        var batches = sink.Batches.ToArray();
        batches.Should().HaveCount(2);

        batches[0].Should()
            .ContainSingle()
            .Subject.Message.Should()
            .Be("This is my message. 4 additional messages skipped.");

        batches[1].Should()
                  .ContainSingle()
                  .Subject.Message.Should()
                  .Be("This is my second message. 4 additional messages skipped.");
    }

    [Fact]
    public void WhenDeDupeEnabled_AndOverfills_GroupsAllIntoSingleBatch()
    {
        const int batchSizeLimit = 10;
        var options = new BatchingSinkOptions(
            batchSizeLimit: batchSizeLimit,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options, deDuplicationEnabled: true);

        // Add too many identical messages
        for (var i = 0; i < batchSizeLimit * 2; i++)
        {
            sink.EnqueueLog(new LogMessageData("This is my message", TelemetryLogLevel.DEBUG));
        }

        // Now initialize
        sink.Initialize(
            AppData,
            HostData,
            new TelemetryDataBuilder(),
            new ITelemetryTransport[] { new TestTransport(TelemetryPushResult.Success) });

        WaitForBatchCount(sink, count: 1);

        var batches = sink.Batches
                          .Should()
                          .ContainSingle()
                          .Subject.Should()
                          .ContainSingle()
                          .Subject.Message.Should()
                          .Be("This is my message. 19 additional messages skipped.");
    }

    private static void WaitForBatchCount(TestTelemetryLogsSink sink, int count, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (sink.Batches.Count < count && deadline > DateTime.UtcNow)
        {
            sink.BatchSentMutex.Wait(TimeSpan.FromSeconds(1));
            sink.BatchSentMutex.Reset();
        }
    }

    private static async Task WaitForPushAttempts(TestTransport transport, int count = 1, int seconds = 3)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (transport.PushAttempts.Count < count && deadline > DateTime.UtcNow)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        transport.PushAttempts.Should().HaveCount(count);
    }

    internal class TestTelemetryLogsSink : TelemetryLogsSink
    {
        public TestTelemetryLogsSink(
            BatchingSinkOptions sinkOptions,
            Action disableSinkAction = null,
            IDatadogLogger log = null,
            bool deDuplicationEnabled = false)
            : base(sinkOptions, disableSinkAction ?? (() => { }), log ?? DatadogSerilogLogger.NullLogger, deDuplicationEnabled)
        {
        }

        public Func<Queue<LogMessageData>, Task> SendingBatch { get; set; } = _ => Task.CompletedTask;

        public ManualResetEventSlim BatchSentMutex { get; } = new();

        public ConcurrentQueue<LogMessageData[]> Batches { get; } = new();

        protected override async Task<bool> EmitBatch(Queue<LogMessageData> events)
        {
            await SendingBatch.Invoke(events);
            Batches.Enqueue(events.ToArray());
            var result = await base.EmitBatch(events);
            BatchSentMutex.Set();
            return result;
        }
    }

    internal class TestTransport : ITelemetryTransport
    {
        private readonly TelemetryPushResult[] _results;
        private int _current = -1;

        public TestTransport(params TelemetryPushResult[] results)
        {
            _results = results;
        }

        public ConcurrentQueue<TelemetryData> PushAttempts { get; } = new();

        public Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
        {
            _current++;
            if (_current >= _results.Length)
            {
                throw new InvalidOperationException("Transport received unexpected request");
            }

            PushAttempts.Enqueue(data);
            return Task.FromResult(_results[_current]);
        }

        public string GetTransportInfo() => nameof(TestTransport);
    }
}
