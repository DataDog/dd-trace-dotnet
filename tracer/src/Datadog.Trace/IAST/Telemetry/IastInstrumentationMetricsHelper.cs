// <copyright file="IastInstrumentationMetricsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Iast.Telemetry;

internal static class IastInstrumentationMetricsHelper
{
    private static int instrumentedSources = 0;
    private static int instrumentedPropagations = 0;
    private static int instrumentedSinks = 0;
    private static IastMetricsVerbosityLevel _verbosityLevel = Iast.Instance.Settings.IastTelemetryVerbosity;
    private static bool _iastEnabled = Iast.Instance.Settings.Enabled;

    public static void OnInstrumentedSource()
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            instrumentedSources++;
        }
    }

    public static void OnInstrumentedPropagation()
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            instrumentedPropagations++;
        }
    }

    public static void OnInstrumentedSink()
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            instrumentedSinks++;
        }
    }

    public static void ReportMetrics()
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            NativeMethods.GetIastMetrics(out var callsiteSources, out var callsitePropagations, out var callsiteSinks);

            if (instrumentedSinks + callsiteSinks > 0)
            {
                TelemetryFactory.Metrics.RecordCountIastInstrumentedSinks(instrumentedSinks + (int)callsiteSinks);
                instrumentedSinks = 0;
            }

            if (instrumentedSources + callsiteSources > 0)
            {
                TelemetryFactory.Metrics.RecordCountIastInstrumentedSources(instrumentedSources + (int)callsiteSources);
                instrumentedSources = 0;
            }

            if (instrumentedPropagations + callsitePropagations > 0)
            {
                TelemetryFactory.Metrics.RecordCountIastInstrumentedPropagations(instrumentedPropagations + (int)callsitePropagations);
                instrumentedPropagations = 0;
            }
        }
    }
}
