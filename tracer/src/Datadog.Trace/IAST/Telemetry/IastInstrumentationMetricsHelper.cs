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
    private static int _instrumentedPropagations = 0;
    private static int _sinksCount = Enum.GetValues(typeof(IastInstrumentedSinks)).Length;
    private static int _sourceTypesCount = Enum.GetValues(typeof(IastInstrumentedSources)).Length;
    private static int[] _instrumentedSources = new int[_sourceTypesCount];
    private static int[] _instrumentedSinks = new int[_sinksCount];
    private static IastMetricsVerbosityLevel _verbosityLevel = Iast.Instance.Settings.IastTelemetryVerbosity;
    private static bool _iastEnabled = Iast.Instance.Settings.Enabled;

    public static void OnInstrumentedSource(SourceTypeName type, int counter = 0)
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            _instrumentedSources[(int)type] += counter;
        }
    }

    public static void OnInstrumentedPropagation(int counter = 0)
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            _instrumentedPropagations += counter;
        }
    }

    public static void OnInstrumentedSink(VulnerabilityType type, int counter = 0)
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            _instrumentedSinks[(int)type] += counter;
        }
    }

    public static void ReportMetrics()
    {
        var definitions = InstrumentationDefinitions.GetAllDefinitions(InstrumentationCategory.Iast);

        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            int[] instrumentedSinks = new int[_sinksCount];
            NativeMethods.GetIastMetrics(out int callsiteInstrumentedSources, out int callsiteInstrumentedPropagations, instrumentedSinks);

            for (int i = 0; i < _sinksCount; i++)
            {
                ReportSink(((IastInstrumentedSinks)i), instrumentedSinks[i]);
                instrumentedSinks[i] = 0;
            }

            if (callsiteInstrumentedSources > 0)
            {
                // We only have callsite calls for cookie sources
                ReportSource(IastInstrumentedSources.CookieValue, callsiteInstrumentedSources);
            }

            for (int i = 0; i < _sourceTypesCount; i++)
            {
                ReportSource(((IastInstrumentedSources)i));
                _instrumentedSources[i] = 0;
            }

            if (_instrumentedPropagations + callsiteInstrumentedPropagations > 0)
            {
                TelemetryFactory.Metrics.RecordCountIastInstrumentedPropagations(_instrumentedPropagations + callsiteInstrumentedPropagations);
                _instrumentedPropagations = 0;
            }
        }
    }

    private static void ReportSink(IastInstrumentedSinks tag, int callsiteHits = 0)
    {
        var totalHits = _instrumentedSinks[(int)tag] + callsiteHits;
        if (totalHits > 0)
        {
            TelemetryFactory.Metrics.RecordCountIastInstrumentedSinks(tag, totalHits);
        }
    }

    private static void ReportSource(IastInstrumentedSources tag, int callsiteHits = 0)
    {
        var totalHits = _instrumentedSources[(int)tag] + callsiteHits;
        if (totalHits > 0)
        {
            TelemetryFactory.Metrics.RecordCountIastInstrumentedSources(tag, totalHits);
        }
    }
}
