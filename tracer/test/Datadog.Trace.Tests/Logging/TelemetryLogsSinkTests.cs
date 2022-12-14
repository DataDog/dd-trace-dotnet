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
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging;

public class TelemetryLogsSinkTests
{
    private static readonly ApplicationTelemetryData AppData = new("my-serv", "integration-tests", TracerConstants.AssemblyVersion, "dotnet", FrameworkDescription.Instance.ProductVersion);
    private static readonly HostTelemetryData HostData = new();

    [Fact]
    public void DoesNotSendBatchesUntilInitialized()
    {
        var options = new BatchingSinkOptions(
            batchSizeLimit: 10,
            queueLimit: 100,
            period: TimeSpan.FromMilliseconds(100));
        var sink = new TestTelemetryLogsSink(options);

        for (var i = 0; i < 150; i++)
        {
            // adding more logs than we're allowed
            sink.EnqueueLog(new LogMessageData("This is my message", TelemetryLogLevel.DEBUG));
        }

        var wasFlushed = sink.Mutex.Wait(TimeSpan.FromSeconds(5)); // plenty of time to flush if it's going to

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

    private static void WaitForBatchCount(TestTelemetryLogsSink sink, int count, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (sink.Batches.Count < count && deadline > DateTime.UtcNow)
        {
            sink.Mutex.Wait(TimeSpan.FromSeconds(1));
            sink.Mutex.Reset();
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
        public TestTelemetryLogsSink(BatchingSinkOptions sinkOptions, Action disableSinkAction = null, IDatadogLogger log = null)
            : base(sinkOptions, disableSinkAction ?? (() => { }), log ?? DatadogSerilogLogger.NullLogger)
        {
        }

        public ManualResetEventSlim Mutex { get; } = new();

        public ConcurrentQueue<LogMessageData[]> Batches { get; } = new();

        protected override async Task<bool> EmitBatch(Queue<LogMessageData> events)
        {
            Batches.Enqueue(events.ToArray());
            var result = await base.EmitBatch(events);
            Mutex.Set();
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
