// <copyright file="PerTraceSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.Configuration.Schema;

namespace Datadog.Trace.Configuration
{
    internal sealed class PerTraceSettings
    {
        private readonly ConcurrentDictionary<string, string> _serviceNameCache = new();

        public PerTraceSettings(NamingSchema schema, MutableSettings mutableSettings)
        {
            Schema = schema;
            Settings = mutableSettings;
        }

        public IReadOnlyDictionary<string, string> ServiceNames => Settings.ServiceNameMappings;

        public NamingSchema Schema { get; }

        public bool HasResourceBasedSamplingRule => false;

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
    }
}
