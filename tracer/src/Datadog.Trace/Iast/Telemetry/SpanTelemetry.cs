// <copyright file="SpanTelemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Telemetry;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.Iast.Telemetry
{
    internal class SpanTelemetry
    {
        private static IastMetricsVerbosityLevel _verbosityLevel = Iast.Instance.Settings.IastTelemetryVerbosity;
        private int[] _executedSinks = new int[Enum.GetValues(typeof(IastInstrumentedSinks)).Length];
        private int[] _executedSources = new int[Enum.GetValues(typeof(IastInstrumentedSources)).Length];
        private int _executedInstrumentations = 0;

        public void AddExecutedSink(VulnerabilityType type)
        {
            if (_verbosityLevel <= IastMetricsVerbosityLevel.Information)
            {
                _executedSinks[(int)type]++;
            }
        }

        public void AddExecutedInstrumentation()
        {
            if (_verbosityLevel <= IastMetricsVerbosityLevel.Debug)
            {
                _executedInstrumentations++;
            }
        }

        public void AddExecutedSource(SourceTypeName type)
        {
            if (_verbosityLevel <= IastMetricsVerbosityLevel.Information)
            {
                _executedSources[(int)type]++;
            }
        }
    }
}
