// <copyright file="DebuggerUploaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class DebuggerUploaderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_WaitsForInFlightUploadAndDrainsEveryBatch(bool diagnostics)
    {
        var batchUploader = new ControlledBatchUploader(blockFirst: true);
        using var uploader = CreateUploader(diagnostics, batchUploader, out var add);
        add(0);
        var flushing = uploader.StartFlushingAsync();
        await batchUploader.Started.Task;
        for (var i = 1; i < 6; i++)
        {
            add(i);
        }

        uploader.Dispose();
        flushing.IsCompleted.Should().BeFalse();
        batchUploader.Release.TrySetResult(true);

        await AssertCompleted(flushing);
        batchUploader.Events.Should().HaveCount(6);
        batchUploader.Calls.Should().Be(4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_WakesTheFlushDelayToSendPendingEvents(bool diagnostics)
    {
        var batchUploader = new ControlledBatchUploader(blockFirst: false);
        using var uploader = CreateUploader(diagnostics, batchUploader, out var add);
        var flushing = uploader.StartFlushingAsync();
        add(1);

        uploader.Dispose();

        await AssertCompleted(flushing);
        batchUploader.Events.Should().ContainSingle();
    }

    [Fact]
    public void Dispose_DrainsQueuedDiagnosticsWhenEachUploadOutlastsTheResendInterval()
    {
        var clock = new SimpleClock();
        using var clockLease = Clock.SetForCurrentThread(clock);
        var resendInterval = TimeSpan.FromSeconds(CreateSettings().DiagnosticsIntervalSeconds);
        var slowUploads = 0;
        var batchUploader = new ControlledBatchUploader(
            blockFirst: false,
            onUpload: () =>
            {
                // Bounding the slow uploads makes a drain that never empties fail instead of hang.
                if (slowUploads++ < 10)
                {
                    clock.UtcNow += resendInterval;
                }
            });
        using var uploader = CreateUploader(diagnostics: true, batchUploader, out var add);
        for (var i = 0; i < 5; i++)
        {
            add(i);
        }

        // Disposing before the loop starts runs the whole drain on this thread, which owns the fake clock.
        uploader.Dispose();
        var flushing = uploader.StartFlushingAsync();

        flushing.Status.Should().Be(TaskStatus.RanToCompletion);
        batchUploader.Events.Should().HaveCount(5).And.OnlyHaveUniqueItems();
        batchUploader.Calls.Should().Be(3);
    }

    private static DebuggerSettings CreateSettings()
    {
        return new DebuggerSettings(
            new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.UploadBatchSize, "2" },
                { ConfigurationKeys.Debugger.UploadFlushInterval, "2000" },
            }),
            NullConfigurationTelemetry.Instance);
    }

    private static DebuggerUploaderBase CreateUploader(bool diagnostics, IBatchUploader batchUploader, out Action<int> add)
    {
        var settings = CreateSettings();
        if (diagnostics)
        {
            var sink = DiagnosticsSink.Create(() => "service", settings);
            add = id => sink.AddProbeStatus("probe-" + id, Status.RECEIVED);
            return DiagnosticsUploader.Create(sink, batchUploader, settings);
        }

        var snapshotSink = new SnapshotSink(2, SnapshotSlicer.Create(settings), eventType: null, queueLimit: 10);
        add = id => snapshotSink.Add("probe-" + id, "{\"id\":" + id + "}");
        return SnapshotUploader.Create(snapshotSink, batchUploader, settings);
    }

    private static async Task AssertCompleted(Task task)
    {
        (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(task);
        await task;
    }

    private sealed class ControlledBatchUploader(bool blockFirst, Action onUpload = null) : IBatchUploader
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Events { get; } = [];

        public int Calls { get; private set; }

        public Task Upload(IEnumerable<string> payloads)
        {
            Events.AddRange(payloads);
            Calls++;
            onUpload?.Invoke();
            Started.TrySetResult(true);
            return blockFirst && Calls == 1 ? Release.Task : Task.CompletedTask;
        }
    }
}
