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
            var otelSdkType = Type.GetType("OpenTelemetry.Sdk, OpenTelemetry", throwOnError: false);
            if (otelSdkType is null)
            {
                ThrowHelper.ThrowNullReferenceException($"The OpenTelemetry SDK type is null, make sure the OpenTelemetry NuGet package is installed to collect metrics.");
            }

            var otelSdkProxyResult = DuckType.GetOrCreateProxyType(typeof(IOtelSdk), otelSdkType);
            var otelSdkProxyResultType = otelSdkProxyResult.ProxyType;
            if (otelSdkProxyResultType is null)
            {
                ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after Ducktyping attempt of {typeof(IOtelSdk)} is null.");
            }
            else if (otelSdkProxyResult.Success)
            {
                var otelSdkProxy = (IOtelSdk)otelSdkProxyResult.CreateInstance(null!);
                var meterProviderBuilder = otelSdkProxy.CreateMeterProviderBuilder();
                var meterProviderProxy = meterProviderBuilder.DuckCast<IMeterProviderBuilder>();
                meterProviderProxy.AddMeter(Tracer.Instance.Settings.OpenTelemetryMeterNames);

                var otlpMetricExporterExtensionsType = Type.GetType("OpenTelemetry.Metrics.OtlpMetricExporterExtensions, OpenTelemetry.Exporter.OpenTelemetryProtocol", throwOnError: false);
                if (otlpMetricExporterExtensionsType is null)
                {
                    ThrowHelper.ThrowNullReferenceException($"The OpenTelemetry Metrics Exporter Extensions type is null, make sure the  OpenTelemetry NuGet package is installed to collect metrics.");
                }

                var otlpMetricExporterExtensionsProxyResult = DuckType.GetOrCreateProxyType(typeof(IOtlpMetricExporterExtensions), otlpMetricExporterExtensionsType);
                var otlpMetricExporterExtensionsProxyResultType = otlpMetricExporterExtensionsProxyResult.ProxyType;
                if (otlpMetricExporterExtensionsProxyResultType is null)
                {
                    ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after Ducktyping attempt of {typeof(IOtlpMetricExporterExtensions)} is null.");
                }
                else if (otlpMetricExporterExtensionsProxyResult.Success)
                {
                    var otlpMetricExporterExtensionsProxy = (IOtlpMetricExporterExtensions)otlpMetricExporterExtensionsProxyResult.CreateInstance(null!);
                    otlpMetricExporterExtensionsProxy.AddOtlpExporter(meterProviderProxy);

                    var meterProvider = meterProviderProxy.Build();
                    LifetimeManager.Instance.AddShutdownTask(_ => meterProvider.Dispose());
                    Log.Information("Successfully Ducktyped and configured OTLP Metrics Exporter.");
                }
            }
        }
    }
}
#endif
