// <copyright file="TelemetryErrorCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Telemetry;

/// <summary>
/// Specific errors that occur when parsing configuration values
/// </summary>
[EnumExtensions]
internal enum TelemetryErrorCode
{
    /// <summary>
    /// No error, should not be used
    /// </summary>
    None = 0,

    /// <summary>
    /// Error parsing value as boolean
    /// </summary>
    [Description("Error parsing value as boolean")]
    ParsingBooleanError = 1,

    /// <summary>
    /// Error parsing value as int32
    /// </summary>
    [Description("Error parsing value as int32")]
    ParsingInt32Error = 2,

    /// <summary>
    /// Error parsing value as double
    /// </summary>
    [Description("Error parsing value as double")]
    ParsingDoubleError = 3,

    /// <summary>
    /// Invalid value
    /// </summary>
    [Description("Invalid value")]
    FailedValidation = 4,

    /// <summary>
    /// Error reading value as string from JSON
    /// </summary>
    [Description("Error reading value as string from JSON")]
    JsonStringError = 5,

    /// <summary>
    /// Error reading value as int from JSON
    /// </summary>
    [Description("Error reading value as int from JSON")]
    JsonInt32Error = 6,

    /// <summary>
    /// Error reading value as double from JSON
    /// </summary>
    [Description("Error reading value as double from JSON")]
    JsonDoubleError = 7,

    /// <summary>
    /// Error reading value as boolean from JSON
    /// </summary>
    [Description("Error reading value as boolean from JSON")]
    JsonBooleanError = 8,

    /// <summary>
    /// Error reading value as dictionary from JSON
    /// </summary>
    [Description("Error reading value as dictionary from JSON")]
    JsonDictionaryError = 9,

    /// <summary>
    /// Error parsing value
    /// </summary>
    [Description("Error parsing value")]
    ParsingCustomError = 10,

    /// <summary>
    /// Error configuring Tracer
    /// </summary>
    [Description("Error configuring Tracer")]
    TracerConfigurationError = 11,

    /// <summary>
    /// Error configuring AppSec
    /// </summary>
    [Description("Error configuring AppSec")]
    AppsecConfigurationError = 12,

    /// <summary>
    /// Error configuring Continuous Profiler
    /// </summary>
    [Description("Error configuring Continuous Profiler")]
    ContinuousProfilerConfigurationError = 13,

    /// <summary>
    /// Error configuring Dynamic Instrumentation
    /// </summary>
    [Description("Error configuring Dynamic Instrumentation")]
    DynamicInstrumentationConfigurationError = 14,

    /// <summary>
    /// Potentially invalid UDS path
    /// </summary>
    [Description("Potentially invalid UDS path")]
    PotentiallyInvalidUdsPath = 15,

    /// <summary>
    /// Attempting to use UDS on unsupported runtime
    /// </summary>
    [Description("Attempting to use UDS on unsupported runtime")]
    UdsOnUnsupportedPlatform = 16,

    /// <summary>
    /// Unexpected type in configuration source
    /// </summary>
    [Description("Unexpected type in configuration source")]
    UnexpectedTypeInConfigurationSource = 17,
}
