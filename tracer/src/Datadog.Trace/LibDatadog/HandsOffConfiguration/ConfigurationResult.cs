// <copyright file="ConfigurationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration;

internal readonly struct ConfigurationResult(ConfigurationSuccessResult? configurationSuccessResult, string? errorMessage)
{
    public ConfigurationSuccessResult? ConfigurationSuccessResult { get; } = configurationSuccessResult;

    public string? ErrorMessage { get; } = errorMessage;
}
