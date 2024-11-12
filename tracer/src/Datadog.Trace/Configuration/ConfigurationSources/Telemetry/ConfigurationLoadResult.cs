// <copyright file="ConfigurationLoadResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

internal enum ConfigurationLoadResult
{
    /// <summary>
    /// The configuration value was found, parsed, and validated successfully
    /// </summary>
    Valid = 0,

    /// <summary>
    /// The configuration value was not found
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// The configuration value could not be parsed to the required type
    /// </summary>
    ParsingError = 2,

    /// <summary>
    /// The configuration value was found, parsed, but failed validation
    /// </summary>
    ValidationFailure = 3,
}
