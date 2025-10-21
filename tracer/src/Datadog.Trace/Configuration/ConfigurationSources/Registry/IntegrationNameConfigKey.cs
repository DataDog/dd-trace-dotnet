// <copyright file="IntegrationNameConfigKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.ConfigurationSources.Registry;

internal readonly struct IntegrationNameConfigKey(string integrationName) : IConfigKey
{
    private readonly string _integrationName = integrationName;

    public string GetKey() => string.Format(IntegrationSettings.IntegrationEnabledKey, _integrationName.ToUpperInvariant());
}
