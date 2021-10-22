// <copyright file="AppStartedPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Telemetry
{
    internal class AppStartedPayload : IPayload
    {
        public ICollection<IntegrationTelemetryData> Integrations { get; set; }

        public ICollection<DependencyTelemetryData> Dependencies { get; set; }

        public ConfigTelemetryData Configuration { get; set; } = new();

        public Dictionary<string, object> AdditionalPayload { get; set; } = new();
    }
}
