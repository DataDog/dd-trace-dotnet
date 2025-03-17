// <copyright file="MeterListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.OTelMetrics.DuckTypes;
using Datadog.Trace.Util;

namespace Datadog.Trace.OTelMetrics
{
    internal static class MeterListener
    {
        private const int InitializationBackoffPerRetry = 10000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MeterListener));

        private static object? _meterListenerInstance;

        private static int _initialized = 0;
        private static int _stopped = 0;

        public static bool IsRunning
        {
            get
            {
                return Interlocked.CompareExchange(ref _initialized, 1, 1) == 1 &&
                       Interlocked.CompareExchange(ref _stopped, 0, 0) == 0;
            }
        }

        public static void Initialize() => Initialize(CancellationToken.None);

        public static void Initialize(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
            {
                return;
            }

            // Initialize
            var meterListenerType = Type.GetType("System.Diagnostics.Metrics.MeterListener, System.Diagnostics.DiagnosticSource", throwOnError: false);
            if (meterListenerType is null)
            {
                Log.Error("The MeterListener type cannot be found.");
                return;
            }

            CreateMeterListenerInstance(meterListenerType);
        }

        public static void StopListeners()
        {
            // If there's an activity listener instance we dispose the instance and clear it.
            if (_meterListenerInstance is IDisposable disposableListener)
            {
                _meterListenerInstance = null;
                disposableListener.Dispose();
                Interlocked.Exchange(ref _stopped, 1);
            }
            else
            {
                Log.Error("MeterListener cannot be cast as IDisposable.");
            }
        }

        private static void CreateMeterListenerInstance(Type meterListenerType)
        {
            var instrumentType = Type.GetType("System.Diagnostics.Metrics.Instrument, System.Diagnostics.DiagnosticSource", throwOnError: true)!;

            var onInstrumentPublishedMethodInfo = typeof(MeterListenerHandler).GetMethod("OnInstrumentPublished", BindingFlags.Static | BindingFlags.Public)!;

            // Create the MeterListener instance
            /* // Comment out all the proxy code because these facilities are only on .NET 6 anyways so let's try to use them
            var meterListener = Activator.CreateInstance(meterListenerType);
            var meterListenerProxy = meterListener.DuckCast<IMeterListener>();
            if (meterListenerProxy is null)
            {
                ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after ducktyping {meterListenerType} is null");
            }

            meterListenerProxy.InstrumentPublished = MeterListenerDelegatesBuilder.InstrumentPublishedDelegate(meterListenerType, instrumentType, onInstrumentPublishedMethodInfo);

            meterListenerProxy.SetMeasurementEventCallback<double>(MeterListenerDelegatesBuilder.SetMeasurementEventCallbackDelegate(meterListenerType, instrumentType, onInstrumentPublishedMethodInfo));
            meterListenerProxy.SetMeasurementEventCallback<float>(MeterListenerDelegatesBuilder.SetMeasurementEventCallbackDelegate(meterListenerType, instrumentType, onInstrumentPublishedMethodInfo));

            meterListenerProxy.SetMeasurementEventCallback<long>(MeterListenerDelegatesBuilder.SetMeasurementEventCallbackDelegate(meterListenerType, instrumentType, onInstrumentPublishedMethodInfo));
            meterListenerProxy.SetMeasurementEventCallback<int>(MeterListenerDelegatesBuilder.SetMeasurementEventCallbackDelegate(meterListenerType, instrumentType, onInstrumentPublishedMethodInfo));
            meterListenerProxy.SetMeasurementEventCallback<short>(MeterListenerDelegatesBuilder.SetMeasurementEventCallbackDelegate(meterListenerType, instrumentType, onInstrumentPublishedMethodInfo));
            meterListenerProxy.SetMeasurementEventCallback<byte>(MeterListenerDelegatesBuilder.SetMeasurementEventCallbackDelegate(meterListenerType, instrumentType, onInstrumentPublishedMethodInfo));

            // meterListenerProxy.MeasurementsCompleted = MeasurementsCompleted;

            meterListenerProxy.Start();
            */

            var meterListener = new System.Diagnostics.Metrics.MeterListener();
            meterListener.InstrumentPublished = MeterListenerHandler.OnInstrumentPublished;

            meterListener.SetMeasurementEventCallback<double>(MeterListenerHandler.OnMeasurementRecordedDouble);
            meterListener.SetMeasurementEventCallback<float>(static (instrument, value, tags, state) => MeterListenerHandler.OnMeasurementRecordedDouble(instrument, value, tags, state));
            // Does not cover decimal, but neither does OpenTelemetry. Maybe this is fine

            meterListener.SetMeasurementEventCallback<long>(MeterListenerHandler.OnMeasurementRecordedLong);
            meterListener.SetMeasurementEventCallback<int>(static (instrument, value, tags, state) => MeterListenerHandler.OnMeasurementRecordedLong(instrument, value, tags, state));
            meterListener.SetMeasurementEventCallback<short>(static (instrument, value, tags, state) => MeterListenerHandler.OnMeasurementRecordedLong(instrument, value, tags, state));
            meterListener.SetMeasurementEventCallback<byte>(static (instrument, value, tags, state) => MeterListenerHandler.OnMeasurementRecordedLong(instrument, value, tags, state));

            meterListener.Start();

            // Set the global field after calling the `Start` method
            _meterListenerInstance = meterListener;
        }
    }
}
#endif
