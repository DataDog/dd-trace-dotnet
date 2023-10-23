// <copyright file="AppClientConfigurationChangedPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.Telemetry;

internal class AppClientConfigurationChangedPayload : IPayload
{
    public AppClientConfigurationChangedPayload(ICollection<ConfigurationKeyValue> configuration)
    {
        Configuration = configuration;
    }

    public ICollection<ConfigurationKeyValue> Configuration { get; set; }
}
