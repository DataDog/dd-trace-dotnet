// <copyright file="JsonOutputFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.AutoInstrumentation.Generator.Core;

namespace Datadog.AutoInstrumentation.Generator.Cli.Output;

internal static class JsonOutputFormatter
{
    public static string Format(GenerationResult result, GenerationConfiguration config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        sb.AppendLine($"  \"success\": {(result.Success ? "true" : "false")},");

        if (!result.Success)
        {
            sb.AppendLine($"  \"errorMessage\": {JsonEscape(result.ErrorMessage ?? string.Empty)}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        sb.AppendLine($"  \"fileName\": {JsonEscape(result.FileName ?? string.Empty)},");
        sb.AppendLine($"  \"sourceCode\": {JsonEscape(result.SourceCode ?? string.Empty)},");

        if (result.Metadata is { } meta)
        {
            sb.AppendLine("  \"metadata\": {");
            sb.AppendLine($"    \"assemblyName\": {JsonEscape(meta.AssemblyName)},");
            sb.AppendLine($"    \"typeName\": {JsonEscape(meta.TypeName)},");
            sb.AppendLine($"    \"methodName\": {JsonEscape(meta.MethodName)},");
            sb.AppendLine($"    \"returnTypeName\": {JsonEscape(meta.ReturnTypeName)},");
            sb.AppendLine($"    \"parameterTypeNames\": {JsonEscape(meta.ParameterTypeNames)},");
            sb.AppendLine($"    \"minimumVersion\": {JsonEscape(meta.MinimumVersion)},");
            sb.AppendLine($"    \"maximumVersion\": {JsonEscape(meta.MaximumVersion)},");
            sb.AppendLine($"    \"integrationName\": {JsonEscape(meta.IntegrationName)},");
            sb.AppendLine($"    \"integrationClassName\": {JsonEscape(meta.IntegrationClassName)},");
            sb.AppendLine($"    \"isInterface\": {(meta.IsInterface ? "true" : "false")}");
            sb.AppendLine("  },");
        }

        sb.AppendLine("  \"configuration\": {");
        sb.AppendLine($"    \"createOnMethodBegin\": {(config.CreateOnMethodBegin ? "true" : "false")},");
        sb.AppendLine($"    \"createOnMethodEnd\": {(config.CreateOnMethodEnd ? "true" : "false")},");
        sb.AppendLine($"    \"createOnAsyncMethodEnd\": {(config.CreateOnAsyncMethodEnd ? "true" : "false")}");
        sb.AppendLine("  }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string JsonEscape(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}
