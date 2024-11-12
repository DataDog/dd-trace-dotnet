// <copyright file="TelemetryErrorCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Telemetry;

[EnumExtensions]
internal enum TelemetryErrorCode
{
    /// <summary>
    /// No error, should not be used
    /// </summary>
    None = 0,

    [Description("Error parsing value as boolean")]
    ParsingBooleanError = 1,

    [Description("Error parsing value as int32")]
    ParsingInt32Error = 2,

    [Description("Error parsing value as double")]
    ParsingDoubleError = 3,

    [Description("Invalid value")]
    FailedValidation = 4,

    [Description("Error reading value as string from JSON")]
    JsonStringError = 5,

    [Description("Error reading value as int from JSON")]
    JsonInt32Error = 6,

    [Description("Error reading value as double from JSON")]
    JsonDoubleError = 7,

    [Description("Error reading value as boolean from JSON")]
    JsonBooleanError = 8,

    [Description("Error reading value as dictionary from JSON")]
    JsonDictionaryError = 9,

    [Description("Error parsing value")]
    ParsingCustomError = 10,

    [Description("Error configuring Tracer")]
    TracerConfigurationError = 11,

    [Description("Error configuring AppSec")]
    AppsecConfigurationError = 12,

    [Description("Error configuring Continuous Profiler")]
    ContinuousProfilerConfigurationError = 13,

    [Description("Error configuring Dynamic Instrumentation")]
    DynamicInstrumentationConfigurationError = 14,

    [Description("Potentially invalid UDS path")]
    PotentiallyInvalidUdsPath = 15,

    [Description("Attempting to use UDS on unsupported runtime")]
    UdsOnUnsupportedPlatform = 16,
}
