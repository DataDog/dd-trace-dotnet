// <copyright file="IastInstrumentationMetricsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Telemetry;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.Iast.Telemetry;

internal static class IastInstrumentationMetricsHelper
{
    private static int _sinksCount = Enum.GetValues(typeof(IastInstrumentedSinks)).Length;
    private static IastMetricsVerbosityLevel _verbosityLevel = Iast.Instance.Settings.IastTelemetryVerbosity;
    private static bool _iastEnabled = Iast.Instance.Settings.Enabled;

    public static void ReportMetrics()
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            int[] instrumentedSinks = new int[_sinksCount];
            NativeMethods.GetIastMetrics(out int callsiteInstrumentedSources, out int callsiteInstrumentedPropagations, instrumentedSinks);

            for (int i = 0; i < _sinksCount; i++)
            {
                ReportSink(((IastInstrumentedSinks)i), instrumentedSinks[i]);
                instrumentedSinks[i] = 0;
            }

            // We only have callsite calls for cookie sources
            ReportSource(IastInstrumentedSources.CookieValue, callsiteInstrumentedSources);

            ReportPropagations(callsiteInstrumentedPropagations);
        }
    }

    private static void ReportPropagations(int callsiteHits)
    {
        if (callsiteHits > 0)
        {
            TelemetryFactory.Metrics.RecordCountIastInstrumentedPropagations(callsiteHits);
        }
    }

    private static void ReportSink(IastInstrumentedSinks tag, int callsiteHits)
    {
        if (callsiteHits > 0)
        {
            TelemetryFactory.Metrics.RecordCountIastInstrumentedSinks(tag, callsiteHits);
        }
    }

    private static void ReportSource(IastInstrumentedSources tag, int callsiteHits)
    {
        if (callsiteHits > 0)
        {
            TelemetryFactory.Metrics.RecordCountIastInstrumentedSources(tag, callsiteHits);
        }
    }
}
