// <copyright file="ServerSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal class ServerSchema
    {
        private readonly SchemaVersion _version;

        public ServerSchema(SchemaVersion version)
        {
            _version = version;
        }

        public string GetOperationNameForProtocol(string protocol) =>
            _version switch
            {
                SchemaVersion.V0 => $"{protocol}.request",
                _ => $"{protocol}.server.request",
            };

        public string GetOperationNameForComponent(string component) =>
            _version switch
            {
                SchemaVersion.V0 => $"{component}.request",
                _ => "http.server.request",
            };

        public string GetOperationNameForRequestType(string requestType) =>
            _version switch
            {
                SchemaVersion.V0 => $"{requestType}",
                _ => $"{requestType}.request",
            };
    }
}
