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
    private static int instrumentedPropagations = 0;
    private static int vulnerabilityTypesCount = Enum.GetValues(typeof(IastInstrumentedSinks)).Length;
    private static int sourceTypesCount = Enum.GetValues(typeof(IastInstrumentedSources)).Length;
    private static int[] instrumentedSources = new int[sourceTypesCount];
    private static int[] instrumentedSinks = new int[vulnerabilityTypesCount];
    private static IastMetricsVerbosityLevel _verbosityLevel = Iast.Instance.Settings.IastTelemetryVerbosity;
    private static bool _iastEnabled = Iast.Instance.Settings.Enabled;

    public static void OnInstrumentedSource(SourceTypeName type)
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            instrumentedSources[(int)type]++;
        }
    }

    public static void OnInstrumentedPropagation()
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            instrumentedPropagations++;
        }
    }

    public static void OnInstrumentedSink(VulnerabilityType type)
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            instrumentedSinks[(int)type]++;
        }
    }

    public static void ReportMetrics()
    {
        if (_iastEnabled && _verbosityLevel != IastMetricsVerbosityLevel.Off)
        {
            NativeMethods.GetIastMetrics(out int callsiteInstrumentedSources, out int callsiteInstrumentedPropagations, out int instrumentedSinksWeakCipher, out int instrumentedSinksWeakHash, out int instrumentedSinksSqlI, out int instrumentedSinksCmdI, out int instrumentedSinksPathTraversal, out int instrumentedSinksLdapI, out int instrumentedSinksSsrf);

            for (int i = 0; i < vulnerabilityTypesCount; i++)
            {
                switch ((IastInstrumentedSinks)i)
                {
                    case IastInstrumentedSinks.SqlInjection:
                        ReportSink(IastInstrumentedSinks.SqlInjection, instrumentedSinksSqlI);
                        break;
                    case IastInstrumentedSinks.LdapInjection:
                        ReportSink(IastInstrumentedSinks.LdapInjection, instrumentedSinksLdapI);
                        break;
                    case IastInstrumentedSinks.PathTraversal:
                        ReportSink(IastInstrumentedSinks.PathTraversal, instrumentedSinksPathTraversal);
                        break;
                    case IastInstrumentedSinks.CommandInjection:
                        ReportSink(IastInstrumentedSinks.CommandInjection, instrumentedSinksCmdI);
                        break;
                    case IastInstrumentedSinks.WeakCipher:
                        ReportSink(IastInstrumentedSinks.WeakCipher, instrumentedSinksWeakCipher);
                        break;
                    case IastInstrumentedSinks.WeakHash:
                        ReportSink(IastInstrumentedSinks.WeakHash, instrumentedSinksWeakHash);
                        break;
                    case IastInstrumentedSinks.Ssrf:
                        ReportSink(IastInstrumentedSinks.Ssrf, instrumentedSinksSsrf);
                        break;
                    default:
                        ReportSink((IastInstrumentedSinks)i);
                        break;
                }

                instrumentedSinks[i] = 0;
            }

            for (int i = 0; i < sourceTypesCount; i++)
            {
                if (((IastInstrumentedSources)i) == IastInstrumentedSources.CookieValue)
                {
                    ReportSource(IastInstrumentedSources.CookieValue, callsiteInstrumentedSources);
                }
                else
                {
                    ReportSource(((IastInstrumentedSources)i));
                }

                instrumentedSinks[i] = 0;
            }

            if (instrumentedPropagations + callsiteInstrumentedPropagations > 0)
            {
                TelemetryFactory.Metrics.RecordCountIastInstrumentedPropagations(instrumentedPropagations + callsiteInstrumentedPropagations);
                instrumentedPropagations = 0;
            }
        }
    }

    private static void ReportSink(IastInstrumentedSinks tag, int callsiteHits = 0)
    {
        var totalHits = instrumentedSinks[(int)tag] + callsiteHits;
        if (totalHits > 0)
        {
            TelemetryFactory.Metrics.RecordCountIastInstrumentedSinks(tag, totalHits);
        }
    }

    private static void ReportSource(IastInstrumentedSources tag, int callsiteHits = 0)
    {
        var totalHits = instrumentedSinks[(int)tag] + callsiteHits;
        if (totalHits > 0)
        {
            TelemetryFactory.Metrics.RecordCountIastInstrumentedSources(tag, totalHits);
        }
    }
}
