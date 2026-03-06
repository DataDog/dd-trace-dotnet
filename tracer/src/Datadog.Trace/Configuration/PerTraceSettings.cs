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

        internal ServiceNameMetadata GetServiceNameMetadata(string integrationKey)
        {
            if (ServiceNames.TryGetValue(integrationKey, out var mappedName))
            {
                return new ServiceNameMetadata(mappedName, ServiceNameMetadata.OptServiceMapping);
            }

            if (Schema.Version != SchemaVersion.V0 || Schema.RemoveClientServiceNamesEnabled)
            {
                return new ServiceNameMetadata(Settings.DefaultServiceName, null);
            }

            if (!_serviceNameCache.TryGetValue(integrationKey, out var resolvedName))
            {
                resolvedName = $"{Settings.DefaultServiceName}-{integrationKey}";
                _serviceNameCache.TryAdd(integrationKey, resolvedName);
            }

            return new ServiceNameMetadata(resolvedName, integrationKey);
        }

        /// <summary>
        /// Returns the service name source for the given integration service name key.
        /// Returns the integration name when the resolved service name differs from the default,
        /// or null when the default service name is used.
        /// </summary>
        internal string? GetServiceNameSource(string serviceName)
        {
            var resolvedName = GetServiceName(serviceName);
            return resolvedName != Settings.DefaultServiceName ? serviceName : null;
        }
    }
}
