// <copyright file="CliResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.AutoInstrumentation.Generator.Cli.Output;

internal class CliResult
{
    public bool Success { get; init; }

    public string Command { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public object? Data { get; init; }
}
