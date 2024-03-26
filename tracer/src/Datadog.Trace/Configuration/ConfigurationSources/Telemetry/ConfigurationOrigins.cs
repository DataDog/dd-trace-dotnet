// <copyright file="ConfigurationOrigins.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration.Telemetry;

[EnumExtensions]
internal enum ConfigurationOrigins
{
    /// <summary>
    /// Configuration that is set through environment variables
    /// </summary>
    [Description("env_var")]
    EnvVars,

    /// <summary>
    /// Configuration that is set through the customer application
    /// </summary>
    [Description("code")]
    Code,

    /// <summary>
    /// Configuration that is set by the dd.yaml file or json
    /// </summary>
    [Description("dd_config")]
    DdConfig,

    /// <summary>
    /// Configuration that is set using remote config
    /// </summary>
    [Description("remote_config")]
    RemoteConfig,

    /// <summary>
    /// Configuration that is set using web.config or app.config
    /// </summary>
    [Description("app.config")]
    AppConfig,

    /// <summary>
    /// Set when the user has not set any configuration for the key, or when the configuration
    /// is erroneous and we fallback to the default
    /// </summary>
    [Description("default")]
    Default,

    /// <summary>
    /// Set when the value is calculated from multiple sources
    /// </summary>
    [Description("calculated")]
    Calculated,

    /// <summary>
    /// Set where it is difficult/not possible to determine the source of a config
    /// </summary>
    [Description("unknown")]
    Unknown,
}
