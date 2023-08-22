using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.IAST.Telemetry
{
    internal static class IastInstrumentationMetricsHelper
    {
        private static int instrumentedSources = 0;
        private static int instrumentedPropagations = 0;
        private static int instrumentedSinks = 0;
        private static IastMetricsVerbosityLevel verbosityLevel = Iast.Iast.Instance.Settings.IastTelemetryVerbosity;
        private static bool _iastEnabled = Iast.Iast.Instance.Settings.Enabled;

        public static void OnInstrumentedSource()
        {
            if (_iastEnabled && verbosityLevel != IastMetricsVerbosityLevel.Off)
            {
                instrumentedSources++;
            }
        }

        public static void OnInstrumentedPropagation()
        {
            if (_iastEnabled && verbosityLevel != IastMetricsVerbosityLevel.Off)
            {
                instrumentedPropagations++;
            }
        }

        public static void OnInstrumentedSink()
        {
            if (_iastEnabled && verbosityLevel != IastMetricsVerbosityLevel.Off)
            {
                instrumentedSinks++;
            }
        }

        public static void ReportMetrics()
        {
            if (_iastEnabled && verbosityLevel != IastMetricsVerbosityLevel.Off)
            {
                var callsiteSinks = GetCallsiteInstrumentedSinks();
                if (instrumentedSinks + callsiteSinks > 0)
                {
                    TelemetryFactory.Metrics.RecordCountIastInstrumentedSinks(instrumentedSinks + callsiteSinks);
                    instrumentedSinks = 0;
                }

                var callsiteSources = GetCallsiteInstrumentedSources();
                if (instrumentedSources + callsiteSources > 0)
                {
                    TelemetryFactory.Metrics.RecordCountIastInstrumentedSources(instrumentedSources + callsiteSources);
                    instrumentedSources = 0;
                }

                var callsitePropagations = GetCallsiteInstrumentedPropagations();
                if (instrumentedPropagations + callsitePropagations > 0)
                {
                    TelemetryFactory.Metrics.RecordCountIastInstrumentedPropagations(instrumentedPropagations + callsitePropagations);
                    instrumentedPropagations = 0;
                }
            }
        }

        private static int GetCallsiteInstrumentedPropagations()
        {
            return 0;
        }

        private static int GetCallsiteInstrumentedSources()
        {
            return 0;
        }

        private static int GetCallsiteInstrumentedSinks()
        {
            return 0;
        }
    }
}
