// <copyright file="MeterListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.OTelMetrics.DuckTypes;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.OTelMetrics
{
    internal static class MeterListenerHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MeterListenerHandler));

        private static readonly HashSet<string> EnabledMeters = new();

        private static readonly IDogStatsd _dogstatsd = TracerManagerFactory.CreateDogStatsdClient(Tracer.Instance.Settings, TracerManager.Instance.DefaultServiceName, constantTags: null);

        static MeterListenerHandler()
        {
            try
            {
                var otelSdkType = Type.GetType("OpenTelemetry.Sdk, OpenTelemetry", throwOnError: false);
                if (otelSdkType is null)
                {
                    Log.Error("The OpenTelemetry SDK type cannot be found.");
                    return;
                }

                var otelSdkProxyResult = DuckType.GetOrCreateProxyType(typeof(IOtelSdk), otelSdkType);
                var otelSdkProxyResultType = otelSdkProxyResult.ProxyType;
                if (otelSdkProxyResultType is null)
                {
                    ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after ducktyping {otelSdkProxyResultType} is null");
                }
                else if (otelSdkProxyResult.Success)
                {
                    var otelSdkProxy = (IOtelSdk)otelSdkProxyResult.CreateInstance(null!);

                    if (otelSdkProxy is null)
                    {
                        Console.WriteLine("otelSdkProxy is null");
                    }
                    else
                    {
                        var meterProviderBuilder = otelSdkProxy.CreateMeterProviderBuilder();
                        var builderProxy = meterProviderBuilder.DuckCast<IMeterProviderBuilder>();
                        builderProxy.AddMeter("OpenTelemetryMetricsMeter");

                        try
                        {
                            var consoleExporterType = Type.GetType("OpenTelemetry.Metrics.ConsoleExporterMetricsExtensions, OpenTelemetry.Exporter.Console", throwOnError: false);

                            if (consoleExporterType is not null)
                            {
                                Log.Debug("consoleExporterType found is ==== {ConsoleExporterType}", consoleExporterType);
                                var consoleExporterProxyResult = DuckType.GetOrCreateProxyType(typeof(IConsoleExporterMetricsExtensions), consoleExporterType);

                                if (consoleExporterProxyResult.Success)
                                {
                                    var consoleExporterProxy = (IConsoleExporterMetricsExtensions)consoleExporterProxyResult.CreateInstance(null!);
                                    consoleExporterProxy.AddConsoleExporter(builderProxy);
                                    Log.Debug("Successfully added console exporter for metrics");
                                }
                                else
                                {
                                    Log.Error("Failed to create console exporter proxy");
                                }
                            }
                            else
                            {
                                Log.Error("Console exporter type not found for metrics");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Console exporter not available for metrics: {E}", ex.Message);
                        }

                        var meterProvider = builderProxy.Build();
                        AppDomain.CurrentDomain.ProcessExit += (_, _) => meterProvider.Dispose();

                        // EnabledMeters.Add("OpenTelemetryMetricsMeter");
                        Log.Debug("OpenTelemetryMetricsMeter is now enabled.");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Exception when trying to get OpenTelemetry.Sdk type: {E}", e.ToString());
                throw;
            }
        }

        public static void OnInstrumentPublished(System.Diagnostics.Metrics.Instrument instrument, System.Diagnostics.Metrics.MeterListener listener)
        {
            var meterName = instrument.Meter.Name;
            if (meterName is not null && EnabledMeters.Contains(meterName))
            {
                listener.EnableMeasurementEvents(instrument, state: null);
            }
            else
            {
                Log.Warning("MeterListenerHandler: The instrument will not be handled by the InstrumentPublished event. [Meter={MeterName}] [Instrument={InstrumentName}]", meterName, instrument.Name);
            }
        }

        public static void OnMeasurementRecordedDouble(System.Diagnostics.Metrics.Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            var tagsArray = new string[tags.Length];
            for (int i = 0; i < tags.Length; i++)
            {
                tagsArray[i] = tags[i].Key + ":" + tags[i].Value;
            }

            var fullName = instrument.GetType().FullName;
            if (fullName is null)
            {
                throw new Exception("Unable to get the full name of the instrument type.");
            }

            // Do type comparison via type name, so we can capture types like Gauge and UpDownCounter which are introduced in .NET 7 or later
            if (fullName.StartsWith("System.Diagnostics.Metrics.Counter`1")
                || fullName.StartsWith("System.Diagnostics.Metrics.ObservableCounter`1")
                || fullName.StartsWith("System.Diagnostics.Metrics.UpDownCounter`1")
                || fullName.StartsWith("System.Diagnostics.Metrics.ObservableUpDownCounter`1"))
            {
                _dogstatsd.Counter(statName: instrument.Name + ".dd", value: value, tags: tagsArray);
            }
            else if (fullName.StartsWith("System.Diagnostics.Metrics.Gauge`1")
                || fullName.StartsWith("System.Diagnostics.Metrics.ObservableGauge`1"))
            {
                _dogstatsd.Gauge(statName: instrument.Name + ".dd", value: value, tags: tagsArray);
            }
            else if (fullName.StartsWith("System.Diagnostics.Metrics.Histogram"))
            {
                _dogstatsd.Distribution(statName: instrument.Name + ".dd", value: value, tags: tagsArray);
            }
            else
            {
                Log.Warning("MeterListenerHandler: The instrument will not be handled by the MeasurementRecorded event. [Instrument={InstrumentName}]", instrument.Name);
            }

            Console.WriteLine($"{instrument.Name} recorded measurement {value} with tags {string.Join(",", tags.ToArray())}");
        }

        public static void OnMeasurementRecordedLong(System.Diagnostics.Metrics.Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            var tagsArray = new string[tags.Length];
            for (int i = 0; i < tags.Length; i++)
            {
                tagsArray[i] = tags[i].Key + ":" + tags[i].Value;
            }

            var fullName = instrument.GetType().FullName;
            if (fullName is null)
            {
                throw new Exception("Unable to get the full name of the instrument type.");
            }

            // Do type comparison via type name, so we can capture types like Gauge and UpDownCounter which are introduced in .NET 7 or later
            if (fullName.StartsWith("System.Diagnostics.Metrics.Counter`1")
                || fullName.StartsWith("System.Diagnostics.Metrics.ObservableCounter`1")
                || fullName.StartsWith("System.Diagnostics.Metrics.UpDownCounter`1")
                || fullName.StartsWith("System.Diagnostics.Metrics.ObservableUpDownCounter`1"))
            {
                _dogstatsd.Counter(statName: instrument.Name + ".dd", value: value, tags: tagsArray);
            }
            else if (fullName.StartsWith("System.Diagnostics.Metrics.Gauge`1")
                || fullName.StartsWith("System.Diagnostics.Metrics.ObservableGauge`1"))
            {
                _dogstatsd.Gauge(statName: instrument.Name + ".dd", value: value, tags: tagsArray);
            }
            else if (fullName.StartsWith("System.Diagnostics.Metrics.Histogram"))
            {
                _dogstatsd.Distribution(statName: instrument.Name + ".dd", value: value, tags: tagsArray);
            }
            else
            {
                Log.Warning("MeterListenerHandler: The instrument will not be handled by the MeasurementRecorded event. [Instrument={InstrumentName}]", instrument.Name);
            }

            Console.WriteLine($"{instrument.Name} recorded measurement {value} with tags {string.Join(",", tags.ToArray())}");
        }
    }
}
#endif
