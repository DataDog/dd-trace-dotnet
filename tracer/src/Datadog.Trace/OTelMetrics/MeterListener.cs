// <copyright file="MeterListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OTelMetrics
{
    internal static class MeterListener
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MeterListener));

        private static System.Diagnostics.Metrics.MeterListener? _meterListenerInstance;
        private static int _initialized;
        private static int _stopped;

        public static bool IsRunning
        {
            get
            {
                return Interlocked.CompareExchange(ref _initialized, 1, 1) == 1 &&
                       Interlocked.CompareExchange(ref _stopped, 0, 0) == 0;
            }
        }

        public static void Initialize()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
            {
                return;
            }

            Log.Debug("Initializing MeterListener for OTLP metrics collection.");

            var meterListener = new System.Diagnostics.Metrics.MeterListener();
            meterListener.InstrumentPublished = MeterListenerHandler.OnInstrumentPublished;

            // Handle basic synchronous instruments (as per RFC)
            meterListener.SetMeasurementEventCallback<long>(MeterListenerHandler.OnMeasurementRecordedLong);
            meterListener.SetMeasurementEventCallback<double>(MeterListenerHandler.OnMeasurementRecordedDouble);

            meterListener.Start();
            _meterListenerInstance = meterListener;

            Log.Debug("MeterListener initialized successfully.");
        }

        public static void Stop()
        {
            if (_meterListenerInstance is IDisposable disposableListener)
            {
                _meterListenerInstance = null;
                disposableListener.Dispose();
                Interlocked.Exchange(ref _stopped, 1);
                Log.Debug("MeterListener stopped.");
            }
        }
    }
}
#endif
