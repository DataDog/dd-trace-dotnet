// <copyright file="ConfigurationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration;

internal enum Result
{
    LibDatadogCallError,
    LibDatadogUnavailable,
    Success,
    ApplicationMonitoringConfigFileDisabled
}

internal readonly struct ConfigurationResult(ConfigurationSuccessResult? configurationSuccessResult, string? errorMessage, Result result, Exception? exception = null)
{
    public ConfigurationSuccessResult? ConfigurationSuccessResult { get; } = configurationSuccessResult;

    public string? ErrorMessage { get; } = errorMessage;

    public Result Result { get; } = result;

    public Exception? Exception { get; } = exception;
}
