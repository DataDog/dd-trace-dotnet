// <copyright file="OtlpMetricsExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.OTelMetrics.DuckTypes;

#nullable enable

namespace Datadog.Trace.OTelMetrics
{
    internal static class OtlpMetricsExporter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpMetricsExporter));

        public static void Initialize()
        {
            try
            {
                var otelSdkType = Type.GetType("OpenTelemetry.Sdk, OpenTelemetry", throwOnError: false);
                if (otelSdkType is null)
                {
                    ThrowHelper.ThrowNullReferenceException($"The OpenTelemetry SDK type is null, make sure the nuget installed to collect metrics.");
                    return;
                }

                var otelSdkProxyResult = DuckType.GetOrCreateProxyType(typeof(IOtelSdk), otelSdkType);
                var otelSdkProxyResultType = otelSdkProxyResult.ProxyType;
                if (otelSdkProxyResultType is null)
                {
                    ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after Ducktyping attempt {otelSdkProxyResultType} is null");
                }
                else if (otelSdkProxyResult.Success)
                {
                    var otelSdkProxy = (IOtelSdk)otelSdkProxyResult.CreateInstance(null!);
                    var meterProviderBuilder = otelSdkProxy.CreateMeterProviderBuilder();
                    var builderProxy = meterProviderBuilder.DuckCast<IMeterProviderBuilder>();
                    builderProxy.AddMeter(Tracer.Instance.Settings.EnabledMeters);

                    var otlpExporterType = Type.GetType("OpenTelemetry.Metrics.OtlpMetricExporterExtensions, OpenTelemetry.Exporter.OpenTelemetryProtocol", throwOnError: false);
                    if (otlpExporterType is null)
                    {
                        ThrowHelper.ThrowNullReferenceException($"The OpenTelemetry Protocol Exporter type is null, make sure the nuget is installed to collect metrics.");
                        return;
                    }

                    var otlpExporterProxyResult = DuckType.GetOrCreateProxyType(typeof(IOtlpMetricExporterExtensions), otlpExporterType);
                    if (otlpExporterProxyResult.Success)
                    {
                        var otlpExporterProxy = (IOtlpMetricExporterExtensions)otlpExporterProxyResult.CreateInstance(null!);
                        otlpExporterProxy.AddOtlpExporter(builderProxy);

                        var meterProvider = builderProxy.Build();
                        AppDomain.CurrentDomain.ProcessExit += (_, _) => meterProvider.Dispose();

                        Log.Debug("Successfully Ducktyped and configured OTLP Metrics Exporter.");
                    }
                    else
                    {
                        Log.Error("Error Ducktyping OTLP Metrics Exporter.");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Exception when initializing OTLP Metrics Exporter: {E}", e.ToString());
                throw;
            }
        }
    }
}
#endif
