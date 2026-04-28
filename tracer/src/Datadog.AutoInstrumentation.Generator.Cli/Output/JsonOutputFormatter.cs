// <copyright file="JsonOutputFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;
using Datadog.AutoInstrumentation.Generator.Core;

namespace Datadog.AutoInstrumentation.Generator.Cli.Output;

internal static class JsonOutputFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Format(GenerationResult result, GenerationConfiguration config)
    {
        if (!result.Success)
        {
            return JsonSerializer.Serialize(
                new { success = false, errorMessage = result.ErrorMessage ?? string.Empty },
                SerializerOptions);
        }

        return JsonSerializer.Serialize(
            new
            {
                success = true,
                fileName = result.FileName ?? string.Empty,
                sourceCode = result.SourceCode ?? string.Empty,
                metadata = result.Metadata,
                configuration = config,
            },
            SerializerOptions);
    }

    public static string FormatResult(CliResult result) =>
        JsonSerializer.Serialize(result, SerializerOptions);
}
