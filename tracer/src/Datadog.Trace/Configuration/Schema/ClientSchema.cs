// <copyright file="ClientSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal class ClientSchema
    {
        private readonly SchemaVersion _version;
        private readonly string _defaultServiceName;
        private readonly IDictionary<string, string>? _serviceNameMappings;

        public ClientSchema(SchemaVersion version, string defaultServiceName, IDictionary<string, string>? serviceNameMappings)
        {
            _version = version;
            _defaultServiceName = defaultServiceName;
            _serviceNameMappings = serviceNameMappings;
        }

        public string GetOperationNameForProtocol(string protocol) =>
            _version switch
            {
                SchemaVersion.V0 => $"{protocol}.request",
                _ => $"{protocol}.client.request",
            };

        public string GetServiceName(string component)
        {
            if (_serviceNameMappings is not null && _serviceNameMappings.TryGetValue(component, out var mappedServiceName))
            {
                return mappedServiceName;
            }

            return _version switch
            {
                SchemaVersion.V0 => $"{_defaultServiceName}-{component}",
                _ => _defaultServiceName,
            };
        }

        public HttpTags CreateHttpTags()
            => _version switch
            {
                SchemaVersion.V0 => new HttpTags(),
                _ => new HttpV1Tags(),
            };
    }
}
