// <copyright file="MetricReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OTelMetrics;

internal static class MetricReader
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricReader));

    private static System.Diagnostics.Metrics.MeterListener? _meterListenerInstance;
    private static int _initialized;
    private static int _stopped;

    public static bool IsRunning =>
        Interlocked.CompareExchange(ref _initialized, 1, 1) == 1 &&
        Interlocked.CompareExchange(ref _stopped, 0, 0) == 0;

    public static void Initialize()
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
        {
            return;
        }

        var meterListener = new System.Diagnostics.Metrics.MeterListener();

#if NET6_0 || NET7_0 || NET8_0
        // Ensures instruments are fully de-registered on Dispose() for 6–8
        // Static lambda => no captures/allocations
        meterListener.MeasurementsCompleted = static (_, __) => { };
#endif

        meterListener.InstrumentPublished = MetricReaderHandler.OnInstrumentPublished;

        meterListener.SetMeasurementEventCallback<byte>(MetricReaderHandler.OnMeasurementRecordedByte);
        meterListener.SetMeasurementEventCallback<short>(MetricReaderHandler.OnMeasurementRecordedShort);
        meterListener.SetMeasurementEventCallback<int>(MetricReaderHandler.OnMeasurementRecordedInt);
        meterListener.SetMeasurementEventCallback<long>(MetricReaderHandler.OnMeasurementRecordedLong);
        meterListener.SetMeasurementEventCallback<float>(MetricReaderHandler.OnMeasurementRecordedFloat);
        meterListener.SetMeasurementEventCallback<double>(MetricReaderHandler.OnMeasurementRecordedDouble);

        meterListener.Start();

        Interlocked.Exchange(ref _meterListenerInstance, meterListener);
        Interlocked.Exchange(ref _stopped, 0);

        Log.Debug("MeterListener initialized successfully.");
    }

    public static void Stop()
    {
        var listener = Interlocked.Exchange(ref _meterListenerInstance, null);
        if (listener is IDisposable disposableListener)
        {
            disposableListener.Dispose();
            Interlocked.Exchange(ref _stopped, 1);
            Interlocked.Exchange(ref _initialized, 0);
            Log.Debug("MeterListener stopped.");
        }
    }

    internal static void CollectObservableInstruments()
    {
        if (_meterListenerInstance != null)
        {
            try
            {
                _meterListenerInstance.RecordObservableInstruments();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error collecting observable instruments.");
            }
        }
    }
}
#endif

