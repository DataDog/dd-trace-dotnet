// <copyright file="IntegrationAnalyticsSampleRateConfigKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.ConfigurationSources.Registry;

internal readonly struct IntegrationAnalyticsSampleRateConfigKey(string integrationName) : IConfigKey
{
    private readonly string _integrationName = integrationName;

#pragma warning disable 618 // App analytics is deprecated, but still used
    public string GetKey() => string.Format(IntegrationSettings.AnalyticsSampleRateKey, _integrationName.ToUpperInvariant());
#pragma warning restore 618
}
