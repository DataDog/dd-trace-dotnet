// <copyright file="ImmutableAzureAppServiceSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Stub implementation to satisfy imported references
/// </summary>
internal static class ImmutableAzureAppServiceSettings
{
    public static bool GetIsAzureAppService(IConfigurationSource source, IConfigurationTelemetry telemetry) => false;
}
