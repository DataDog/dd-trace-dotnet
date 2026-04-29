// <copyright file="GlobalConfigurationSourceResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Configuration.ConfigurationSources;

internal enum Result
{
    /// <summary>Success</summary>
    Success,

    /// <summary>Application monitoring config file is disabled</summary>
    ApplicationMonitoringConfigFileDisabled,

    /// <summary>LibDatadog is not available</summary>
    LibDatadogNotAvailable,

    /// <summary>An error occurred</summary>
    Error,
}

internal readonly struct GlobalConfigurationSourceResult(CompositeConfigurationSource configurationSource, Result result, string? errorMessage = null, Exception? exception = null)
{
    internal string? ErrorMessage { get; } = errorMessage;

    public Exception? Exception { get; } = exception;

    internal CompositeConfigurationSource ConfigurationSource { get; } = configurationSource;

    internal Result Result { get; } = result;
}
