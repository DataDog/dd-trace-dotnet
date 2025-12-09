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
    internal sealed class PerTraceSettings
    {
        private readonly ConcurrentDictionary<string, string> _serviceNameCache = new();

        public PerTraceSettings(ITraceSampler? traceSampler, ISpanSampler? spanSampler, NamingSchema schema, MutableSettings mutableSettings)
        {
            TraceSampler = traceSampler;
            SpanSampler = spanSampler;
            Schema = schema;
            HasResourceBasedSamplingRule = (traceSampler?.HasResourceBasedSamplingRule ?? false) || (spanSampler?.HasResourceBasedSamplingRule ?? false);
            Settings = mutableSettings;
        }

        public ITraceSampler? TraceSampler { get; }

        public ISpanSampler? SpanSampler { get; }

        public IReadOnlyDictionary<string, string> ServiceNames => Settings.ServiceNameMappings;

        public NamingSchema Schema { get; }

        public bool HasResourceBasedSamplingRule { get; }

        public MutableSettings Settings { get; }

        internal string GetServiceName(string serviceName)
        {
            if (ServiceNames.TryGetValue(serviceName, out var name))
            {
                return name;
            }

            if (Schema.Version != SchemaVersion.V0 || Schema.RemoveClientServiceNamesEnabled)
            {
                return Settings.DefaultServiceName;
            }

            if (!_serviceNameCache.TryGetValue(serviceName, out var finalServiceName))
            {
                finalServiceName = $"{Settings.DefaultServiceName}-{serviceName}";
                _serviceNameCache.TryAdd(serviceName, finalServiceName);
            }

            return finalServiceName;
        }
    }
}
