// <copyright file="PerTraceSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Sampling;

namespace Datadog.Trace.Configuration
{
    internal class PerTraceSettings
    {
        public PerTraceSettings(ITraceSampler? traceSampler, ISpanSampler? spanSampler, ServiceNames serviceNames)
        {
            TraceSampler = traceSampler;
            SpanSampler = spanSampler;
            ServiceNames = serviceNames;
        }

        public ITraceSampler? TraceSampler { get; }

        public ISpanSampler? SpanSampler { get; }

        public ServiceNames ServiceNames { get; }
    }
}
