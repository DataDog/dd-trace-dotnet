// <copyright file="ConfigKeyAliasesSwitcherGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Source generator that reads supported-configurations.json and generates a switch case
/// for configuration key matching with alias support.
/// </summary>
[Generator]
public class ConfigKeyAliasesSwitcherGenerator : IIncrementalGenerator
{
    private const string SupportedConfigurationsFileName = "supported-configurations.json";
    private const string MainKeyParamName = "mainKey";
    private const string ClassName = "ConfigKeyAliasesSwitcher";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalText = context.AdditionalTextsProvider
                                    .Where(static file => Path.GetFileName(file.Path).Equals(SupportedConfigurationsFileName, StringComparison.OrdinalIgnoreCase))
                                    .WithTrackingName(TrackingNames.ConfigurationKeysAdditionalText);

        var aliasesContent = additionalText
                            .Select(static (file, ct) => ParseAliasesFromV2File(file, ct))
                            .WithTrackingName(TrackingNames.ConfigurationKeysParseConfiguration);

        // Always generate source code, even when there are errors
        // This ensures compilation doesn't fail due to missing generated types
        context.RegisterSourceOutput(
            aliasesContent,
            static (spc, result) => Execute(spc, result));
    }

    private static void Execute(SourceProductionContext context, Result<ConfigurationAliases?> result)
    {
        // Report any diagnostics first
        foreach (var diagnostic in result.Errors)
        {
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location?.ToLocation()));
        }

        // Generate source code even if there are errors (use empty configuration as fallback).
        // `result.Value` can be null when parsing failed; treat that as "no aliases" and generate an empty switch.
        var configurationAliases = result.Value ?? new ConfigurationAliases(new Dictionary<string, string[]>());
        var generatedSource = GenerateConfigurationKeyMatcher(configurationAliases);
        context.AddSource($"{ClassName}.g.cs", SourceText.From(generatedSource, Encoding.UTF8));
    }

    private static Result<ConfigurationAliases?> ParseAliasesFromV2File(AdditionalText file, CancellationToken cancellationToken)
    {
        try
        {
            var sourceText = file.GetText(cancellationToken);
            if (sourceText is null)
            {
                return new Result<ConfigurationAliases?>(
                    null,
                    new EquatableArray<DiagnosticInfo>(
                    [
                        CreateDiagnosticInfo("DDSG0003", "Configuration file not found", $"The file '{file.Path}' could not be read. Make sure the supported-configurations.json file exists and is included as an AdditionalFile.", DiagnosticSeverity.Error)
                    ]));
            }

            var jsonContent = sourceText.ToString();

            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            if (!root.TryGetProperty("supportedConfigurations", out var supportedConfigurationsElement) ||
                supportedConfigurationsElement.ValueKind != JsonValueKind.Object)
            {
                return new Result<ConfigurationAliases?>(
                    null,
                    new EquatableArray<DiagnosticInfo>(
                    [
                        CreateDiagnosticInfo("DDSG0002", "Aliases parsing error", "Missing or invalid 'supportedConfigurations' section", DiagnosticSeverity.Error)
                    ]));
            }

            var aliases = ParseAliasesFromV2SupportedConfigurations(supportedConfigurationsElement);
            return new Result<ConfigurationAliases?>(new ConfigurationAliases(aliases), default);
        }
        catch (Exception ex)
        {
            return new Result<ConfigurationAliases?>(
                null,
                new EquatableArray<DiagnosticInfo>(
                [
                    CreateDiagnosticInfo("DDSG0004", "Configuration file read error", $"Failed to read configuration file '{file.Path}': {ex.Message}", DiagnosticSeverity.Error)
                ]));
        }
    }

    private static Dictionary<string, string[]> ParseAliasesFromV2SupportedConfigurations(JsonElement supportedConfigurationsElement)
    {
        var aliases = new Dictionary<string, string[]>();

        foreach (var setting in supportedConfigurationsElement.EnumerateObject())
        {
            var mainKey = setting.Name;
            var definitions = setting.Value;

            if (definitions.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"Configuration entry '{mainKey}' must be an array of implementation objects");
            }

            List<string>? aliasList = null;
            HashSet<string>? seen = null;

            foreach (var implementation in definitions.EnumerateArray())
            {
                if (implementation.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException($"Configuration entry '{mainKey}' must be an object");
                }

                if (!implementation.TryGetProperty("aliases", out var aliasesElement) ||
                    aliasesElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var aliasElement in aliasesElement.EnumerateArray())
                {
                    if (aliasElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var alias = aliasElement.GetString();
                    if (string.IsNullOrEmpty(alias))
                    {
                        continue;
                    }

                    aliasList ??= [];
                    seen ??= new HashSet<string>(StringComparer.Ordinal);

                    if (seen.Add(alias!))
                    {
                        aliasList.Add(alias!);
                    }
                }
            }

            if (aliasList is { Count: > 0 })
            {
                aliases[mainKey] = aliasList.ToArray();
            }
        }

        return aliases;
    }

    private static DiagnosticInfo CreateDiagnosticInfo(string id, string title, string message, DiagnosticSeverity severity = DiagnosticSeverity.Warning)
    {
        var descriptor = new DiagnosticDescriptor(
            id,
            title,
            message,
            "Configuration",
            severity,
            isEnabledByDefault: true);

        return new DiagnosticInfo(descriptor, Location.None);
    }

    private static string GenerateConfigurationKeyMatcher(ConfigurationAliases configurationAliases)
    {
        var sb = new StringBuilder();

        // File header
        sb.Append(Constants.FileHeader);

        // Namespace
        sb.AppendLine("namespace Datadog.Trace.Configuration;");
        sb.AppendLine();

        // Class XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated configuration key matcher that handles main keys and aliases.");
        sb.AppendLine("/// Do not edit this file directly as it is auto-generated from supported-configurations.json and supported-configurations-docs.yaml.");
        sb.AppendLine("/// For more info, see docs/development/Configuration/AddingConfigurationKeys.md");
        sb.AppendLine("/// </summary>");

        // Class declaration
        sb.AppendLine("internal static partial class ConfigKeyAliasesSwitcher");
        sb.AppendLine("{");

        // Method XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets all aliases for the given configuration key.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"mainKey\">The configuration key.</param>");
        sb.AppendLine("    /// <returns>A read-only span of aliases for the key (on .NET Core), or an array of aliases (on .NET Framework). Returns empty if no aliases exist.</returns>");
        sb.AppendLine("#if NETCOREAPP");
        sb.AppendLine("    public static System.ReadOnlySpan<string> GetAliases(string mainKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        return mainKey switch");
        sb.AppendLine("        {");

        // Generate switch cases for each alias
        foreach (var kvp in configurationAliases.Aliases.OrderBy(a => a.Key))
        {
            var mainKey = kvp.Key;
            var aliases = kvp.Value;

            // Build the collection expression
            var aliasesStr = string.Join(", ", aliases.Select(a => $"\"{a}\""));
            sb.AppendLine($"        \"{mainKey}\" => new string[] {{{aliasesStr}}},");
        }

        // Default case
        sb.AppendLine("            _ => []");

        // Close switch and method
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("#else");
        sb.AppendLine("    public static string[] GetAliases(string mainKey) => mainKey switch");
        sb.AppendLine("    {");

        // Generate switch cases for .NET Framework
        foreach (var kvp in configurationAliases.Aliases.OrderBy(a => a.Key))
        {
            var mainKey = kvp.Key;
            var aliases = kvp.Value;

            // Build the collection expression
            var aliasesStr = string.Join(", ", aliases.Select(a => $"\"{a}\""));
            sb.AppendLine($"            \"{mainKey}\" => [{aliasesStr}],");
        }

        // Default case
        sb.AppendLine("        _ => []");

        // Close method and class
        sb.AppendLine("    };");
        sb.AppendLine("#endif");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private sealed class ConfigurationAliases(Dictionary<string, string[]> aliases) : IEquatable<ConfigurationAliases?>
    {
        public Dictionary<string, string[]> Aliases { get; } = aliases;

        public bool Equals(ConfigurationAliases? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            // Proper content comparison for change detection
            if (Aliases.Count != other.Aliases.Count)
            {
                return false;
            }

            foreach (var kvp in Aliases)
            {
                if (!other.Aliases.TryGetValue(kvp.Key, out var otherAliases))
                {
                    return false;
                }

                if (kvp.Value.Length != otherAliases.Length)
                {
                    return false;
                }

                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    if (kvp.Value[i] != otherAliases[i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is ConfigurationAliases other && Equals(other));
        }

        public override int GetHashCode()
        {
            var hash = new System.HashCode();
            hash.Add(Aliases.Count);

            // Include content in hash for proper change detection
            foreach (var kvp in Aliases.OrderBy(x => x.Key))
            {
                hash.Add(kvp.Key);
                foreach (var alias in kvp.Value.OrderBy(x => x))
                {
                    hash.Add(alias);
                }
            }

            return hash.ToHashCode();
        }
    }
}
