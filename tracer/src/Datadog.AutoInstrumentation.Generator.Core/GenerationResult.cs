// <copyright file="GenerationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.AutoInstrumentation.Generator.Core;

/// <summary>
/// Result of code generation.
/// </summary>
public class GenerationResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SourceCode { get; set; }

    public string? FileName { get; set; }

    public string? Namespace { get; set; }

    public InstrumentMethodMetadata? Metadata { get; set; }

    public static GenerationResult Error(string message) => new() { Success = false, ErrorMessage = message };
}
