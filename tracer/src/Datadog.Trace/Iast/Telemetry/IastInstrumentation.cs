// <copyright file="IastInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Telemetry
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal class IastInstrumentation : Attribute
    {
        public IastInstrumentation(
            AspectType aspectType = AspectType.Propagation,
            VulnerabilityType vulnerabilityType = VulnerabilityType.None,
            SourceTypeName sourceType = SourceTypeName.None,
            int times = 1)
        {
            if (Iast.Instance.Settings.Enabled)
            {
                switch (aspectType)
                {
                    case AspectType.Propagation:
                        IastInstrumentationMetricsHelper.OnInstrumentedPropagation(times);
                        break;

                    case AspectType.Sink:
                        IastInstrumentationMetricsHelper.OnInstrumentedSink(vulnerabilityType, times);
                        break;

                    case AspectType.Source:
                        IastInstrumentationMetricsHelper.OnInstrumentedSource(sourceType, times);
                        break;
                }
            }
        }
    }
}
