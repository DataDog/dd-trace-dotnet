// <copyright file="TelemetryFactoryV2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Telemetry;

internal class TelemetryFactoryV2
{
    private static readonly ConfigurationTelemetry ConfigTelemetry = new();

    // when we enable config telemetry, switch this to returning the real instance
    internal static IConfigurationTelemetry GetConfigTelemetry()
        // => ConfigTelemetry;
        => NullConfigurationTelemetry.Instance;
}
