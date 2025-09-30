// <copyright file="DebuggerUploaderBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Sink;

internal abstract class DebuggerUploaderBase : IDebuggerUploader
{
    private const double FreeCapacityLowerThreshold = 0.25;
    private const double FreeCapacityUpperThreshold = 0.75;

    private const int MinFlushInterval = 100;
    private const int MaxFlushInterval = 2000;
    private const int InitialFlushInterval = 1000;
    private const int Capacity = 1000;

    private const int StepSize = 200;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DebuggerUploaderBase>();

    private readonly CancellationTokenSource _cancellationSource;
    private readonly int _uploadFlushInterval;
    private readonly int _initialFlushInterval;

    protected DebuggerUploaderBase(DebuggerSettings settings)
    {
        var uploadInterval = settings.UploadFlushIntervalMilliseconds;
        var initialInterval =
            uploadInterval != 0
                ? Math.Max(MinFlushInterval, Math.Min(uploadInterval, MaxFlushInterval))
                : InitialFlushInterval;

        _uploadFlushInterval = uploadInterval;
        _initialFlushInterval = initialInterval;
        _cancellationSource = new CancellationTokenSource();
    }

    public async Task StartFlushingAsync()
    {
        while (!_cancellationSource.IsCancellationRequested)
        {
            var currentInterval = _initialFlushInterval;
            try
            {
                await Upload().ConfigureAwait(false);
            }
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to upload debugger snapshot and/or diagnostics.");
            }
            finally
            {
                currentInterval = ReconsiderFlushInterval(currentInterval);
                await Delay(currentInterval).ConfigureAwait(false);
            }
        }

        async Task Delay(int delay)
        {
            if (_cancellationSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delay), _cancellationSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // We are shutting down, so don't do anything about it
            }
        }
    }

    protected abstract Task Upload();

    private int ReconsiderFlushInterval(int currentInterval)
    {
        if (_uploadFlushInterval != 0)
        {
            return currentInterval;
        }

        var remainingPercent = GetRemainingCapacity() * 1D / Capacity;
        var newInterval = remainingPercent switch
        {
            <= FreeCapacityLowerThreshold => Math.Max(currentInterval - StepSize, MinFlushInterval),
            >= FreeCapacityUpperThreshold => Math.Min(currentInterval + StepSize, MaxFlushInterval),
            _ => currentInterval
        };

        if (newInterval != currentInterval)
        {
            Log.Debug<double, int>("Changing flush interval. Remaining available capacity in upload queue {Remaining}%, new flush interval {NewInterval}ms", remainingPercent * 100, newInterval);
        }

        return newInterval;
    }

    protected abstract int GetRemainingCapacity();

    public void Dispose()
    {
        _cancellationSource.Cancel();
    }
}
