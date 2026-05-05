// <copyright file="ConfigurationKeys.DuckTyping.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal static partial class ConfigurationKeys
{
    /// <summary>
    /// Testing-only output path used to dump dynamic duck typing mappings as ducktype-aot map entries.
    /// </summary>
    public const string DuckTypeAotDiscoveryOutputPath = "DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH";
}
