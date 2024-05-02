// <copyright file="PerTraceSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Configuration
{
    internal class PerTraceSettings
    {
        private readonly ConcurrentDictionary<string, string> _serviceNameCache = new();

        public PerTraceSettings(ITraceSampler? traceSampler, ISpanSampler? spanSampler, IReadOnlyDictionary<string, string> serviceNames, NamingSchema schema)
        {
            TraceSampler = traceSampler;
            SpanSampler = spanSampler;
            ServiceNames = serviceNames;
            Schema = schema;
        }

        public ITraceSampler? TraceSampler { get; }

        public ISpanSampler? SpanSampler { get; }

        public IReadOnlyDictionary<string, string> ServiceNames { get; }

        public NamingSchema Schema { get; }

        internal string GetServiceName(Tracer tracer, string serviceName)
        {
            if (ServiceNames.TryGetValue(serviceName, out var name))
            {
                return name;
            }

            if (Schema.Version != SchemaVersion.V0 || Schema.RemoveClientServiceNamesEnabled)
            {
                return tracer.DefaultServiceName;
            }

            if (!_serviceNameCache.TryGetValue(serviceName, out var finalServiceName))
            {
                finalServiceName = $"{tracer.DefaultServiceName}-{serviceName}";
                _serviceNameCache.TryAdd(serviceName, finalServiceName);
            }

            return finalServiceName;
        }
    }
}
