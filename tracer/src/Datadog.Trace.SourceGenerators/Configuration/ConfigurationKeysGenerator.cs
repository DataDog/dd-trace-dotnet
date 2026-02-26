// <copyright file="ConfigurationKeysGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using HashCode = System.HashCode;

/// <summary>
/// Source generator that reads supported-configurations.yaml and generates ConfigurationKeys
/// with proper nested classes organized by product and full XML documentation.
/// </summary>
[Generator]
public class ConfigurationKeysGenerator : IIncrementalGenerator
{
    private const string SupportedConfigurationsFileName = "supported-configurations.yaml";
    private const string GeneratedClassName = "ConfigurationKeys";
    private const string Namespace = "Datadog.Trace.Configuration";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Single YAML pipeline - all data is now in supported-configurations.yaml
        var additionalText = context.AdditionalTextsProvider
                                    .Where(static file => Path.GetFileName(file.Path).Equals(SupportedConfigurationsFileName, StringComparison.OrdinalIgnoreCase))
                                    .WithTrackingName(TrackingNames.ConfigurationKeysAdditionalText);

        var yamlContent = additionalText
                          .Select(static (file, ct) => file.GetText(ct)?.ToString())
                          .WithTrackingName(TrackingNames.ConfigurationKeysGenYamlFile);

        var parsedYaml = yamlContent
                         .Select(static (content, _) => ParseYaml(content))
                         .WithTrackingName(TrackingNames.ConfigurationKeysGenParseYaml);

        var configData = parsedYaml
                         .Select(static (result, _) => ExtractConfigurationData(result))
                         .Collect()
                         .WithTrackingName(TrackingNames.ConfigurationKeysGenParseConfiguration);

        context.RegisterSourceOutput(configData, static (spc, results) =>
        {
            var result = results.Length > 0
                ? results[0]
                : new Result<ConfigurationData>(null!, new EquatableArray<DiagnosticInfo>([CreateDiagnosticInfo("DDSG0005", "Missing", "YAML configuration file not found", DiagnosticSeverity.Error)]));

            Execute(spc, result);
        });
    }

    private static void Execute(SourceProductionContext context, Result<ConfigurationData> result)
    {
        // Report any diagnostics
        foreach (var diagnostic in result.Errors)
        {
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location?.ToLocation()));
        }

        // Generate source code even if there are errors (use empty configuration as fallback)
        var configData = result.Value ?? new ConfigurationData(new Dictionary<string, ConfigEntry>());

        // Group by product
        var productGroups = configData.Configurations
                                      .GroupBy(kvp => kvp.Value.Product)
                                      .OrderBy(g => g.Key)
                                      .ToList();

        // Generate partial class files for each product (or empty main class if no products)
        foreach (var productGroup in productGroups)
        {
            var productSource = GenerateProductPartialClass(productGroup.Key, productGroup.ToList());
            var fileName = string.IsNullOrEmpty(productGroup.Key)
                               ? $"{GeneratedClassName}.g.cs"
                               : $"{GeneratedClassName}.{productGroup.Key}.g.cs";
            context.AddSource(fileName, SourceText.From(productSource, Encoding.UTF8));
        }
    }

    private static Result<YamlReader.ParsedConfigurationData> ParseYaml(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new Result<YamlReader.ParsedConfigurationData>(default, new EquatableArray<DiagnosticInfo>([CreateDiagnosticInfo("DDSG0006", "Read error", "YAML content is empty", DiagnosticSeverity.Error)]));
        }

        try
        {
            var parsedData = YamlReader.ParseSupportedConfigurations(content!);
            return new Result<YamlReader.ParsedConfigurationData>(parsedData, []);
        }
        catch (Exception ex)
        {
            return new Result<YamlReader.ParsedConfigurationData>(default, new EquatableArray<DiagnosticInfo>([CreateDiagnosticInfo("DDSG0007", "YAML parse error", $"Error: {ex.Message}", DiagnosticSeverity.Error)]));
        }
    }

    private static Result<ConfigurationData> ExtractConfigurationData(Result<YamlReader.ParsedConfigurationData> parseResult)
    {
        if (parseResult.Errors.Count > 0)
        {
            return new Result<ConfigurationData>(null!, parseResult.Errors);
        }

        var parsedData = parseResult.Value;

        var configurations = new Dictionary<string, ConfigEntry>();
        var diagnostics = new List<DiagnosticInfo>();

        foreach (var kvp in parsedData.Configurations)
        {
            var entry = kvp.Value;
            string? deprecationMessage = null;

            // Check if this key has a deprecation message
            parsedData.Deprecations?.TryGetValue(kvp.Key, out deprecationMessage);

            // Documentation is mandatory for all configuration keys
            if (string.IsNullOrEmpty(entry.Documentation))
            {
                diagnostics.Add(CreateDiagnosticInfo("DDSG0008", "Missing documentation", $"Configuration key '{kvp.Key}' is missing a 'documentation' field in supported-configurations.yaml", DiagnosticSeverity.Error));
            }

            configurations[kvp.Key] = new ConfigEntry(
                entry.Key,
                entry.Documentation ?? string.Empty,
                entry.Product ?? string.Empty,
                deprecationMessage,
                entry.ConstName);
        }

        return new Result<ConfigurationData>(new ConfigurationData(configurations), new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    private static string GenerateProductPartialClass(string product, List<KeyValuePair<string, ConfigEntry>> entries)
    {
        var sb = new StringBuilder();
        sb.Append(Constants.FileHeader);
        sb.Append("namespace ").Append(Namespace).AppendLine(";");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// String constants for standard Datadog configuration keys.");
        sb.AppendLine("/// Do not edit this file directly as it's auto-generated from supported-configurations.yaml");
        sb.AppendLine("/// For more info, see docs/development/Configuration/AddingConfigurationKeys.md");
        sb.AppendLine("/// </summary>");

        if (string.IsNullOrEmpty(product))
        {
            // Generate main class without nested product class
            sb.AppendLine($"internal static partial class {GeneratedClassName}");
            sb.AppendLine("{");

            var sortedEntries = entries.OrderBy(kvp => kvp.Key).ToList();
            for (var i = 0; i < sortedEntries.Count; i++)
            {
                GenerateConstDeclaration(sb, sortedEntries[i].Value, 1, product);
                if (i < sortedEntries.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("}");
        }
        else
        {
            // Generate partial class with nested product class
            sb.AppendLine($"internal static partial class {GeneratedClassName}");
            sb.AppendLine("{");
            sb.AppendLine($"    internal static class {product}");
            sb.AppendLine("    {");

            var sortedEntries = entries.OrderBy(kvp => kvp.Key).ToList();
            for (var i = 0; i < sortedEntries.Count; i++)
            {
                GenerateConstDeclaration(sb, sortedEntries[i].Value, 2, product);
                if (i < sortedEntries.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static void GenerateConstDeclaration(StringBuilder sb, ConfigEntry entry, int indentLevel, string? productName = null)
    {
        var indent = new string(' ', indentLevel * 4);

        // Add XML documentation
        if (!string.IsNullOrEmpty(entry.Documentation))
        {
            var docLines = entry.Documentation.Split(["\r\n", "\n"], StringSplitOptions.None);
            sb.AppendLine($"{indent}/// <summary>");
            var seeAlsoLines = new List<string>();
            foreach (var line in docLines)
            {
                var trimmedLine = line.TrimStart();

                // Check if line contains seealso tag (self-closing /> or closing </seealso>) as we need to extract it
                if (trimmedLine.StartsWith("<seealso", StringComparison.Ordinal) &&
                    (trimmedLine.IndexOf("/>", StringComparison.Ordinal) >= 0 ||
                     trimmedLine.IndexOf("</seealso>", StringComparison.Ordinal) >= 0))
                {
                    // seealso tags go outside summary - save for later
                    seeAlsoLines.Add(line.Trim());
                    continue;
                }

                sb.AppendLine($"{indent}/// {line}");
            }

            sb.AppendLine($"{indent}/// </summary>");

            // Add seealso tags after summary
            foreach (var seeAlsoLine in seeAlsoLines)
            {
                sb.AppendLine($"{indent}/// {seeAlsoLine}");
            }
        }

        // Add Obsolete attribute if deprecated
        if (!string.IsNullOrEmpty(entry.DeprecationMessage))
        {
            // Escape quotes in the deprecation message and trim whitespace
            var escapedMessage = entry.DeprecationMessage!.Trim().Replace("\"", "\\\"");
            sb.AppendLine($"{indent}[System.Obsolete(\"{escapedMessage}\")]");
        }

        // Add const declaration
        var constName = KeyToConstName(entry.Key, ProductNameEquivalents(productName), entry.ConstName);
        sb.AppendLine($"{indent}public const string {constName} = \"{entry.Key}\";");
    }

    private static string KeyToConstName(string key, string[]? productNames = null, string? constName = null)
    {
        // First, check if we have an explicit const name
        if (!string.IsNullOrEmpty(constName))
        {
            return constName!;
        }

        // Fallback to the original implementation
        // Remove DD_ or OTEL_ prefix
        var name = key;
        string[] prefixes = ["DD_", "_DD_"];
        foreach (var prefix in prefixes)
        {
            if (name.StartsWith(prefix) && !char.IsDigit(name[0]))
            {
                var result = name.Substring(prefix.Length);
                if (!char.IsDigit(result[0]))
                {
                    name = result;
                }

                break;
            }
        }

        // Convert to PascalCase
        var parts = name.Split('_');
        var pascalName = string.Concat(parts.Select(p => p.Length > 0 ? char.ToUpper(p[0]) + p.Substring(1).ToLower() : string.Empty));

        // Handle special case: Dotnet => DotNet
        pascalName = pascalName.Replace("Dotnet", "DotNet");

        // Strip product prefix if the const name starts with it
        if (productNames != null)
        {
            foreach (var productName in productNames)
            {
                if (pascalName.Length > productName.Length &&
                    pascalName.StartsWith(productName, StringComparison.Ordinal))
                {
                    pascalName = pascalName.Substring(productName.Length);
                    break;
                }
            }
        }

        return pascalName;
    }

    private static string[] ProductNameEquivalents(string? productName)
        => productName switch
        {
            null or "" => [string.Empty],
            "AppSec" => ["Appsec"],
            "Tracer" => ["Trace"],
            "CiVisibility" => ["Civisibility"],
            "OpenTelemetry" => ["Otel"],
            _ => [productName]
        };

    private static DiagnosticInfo CreateDiagnosticInfo(string id, string title, string message, DiagnosticSeverity severity)
    {
        var descriptor = new DiagnosticDescriptor(
            id: id,
            title: title,
            messageFormat: message,
            category: nameof(ConfigurationKeysGenerator),
            defaultSeverity: severity,
            isEnabledByDefault: true);

        return new DiagnosticInfo(descriptor, (LocationInfo?)null);
    }

    private readonly struct ConfigEntry : IEquatable<ConfigEntry>
    {
        public ConfigEntry(string key, string documentation, string product, string? deprecationMessage = null, string? constName = null)
        {
            Key = key;
            Documentation = documentation;
            Product = product;
            DeprecationMessage = deprecationMessage;
            ConstName = constName;
        }

        public string Key { get; }

        public string Documentation { get; }

        public string Product { get; }

        public string? DeprecationMessage { get; }

        public string? ConstName { get; }

        public bool Equals(ConfigEntry other) => Key == other.Key && Documentation == other.Documentation && Product == other.Product && DeprecationMessage == other.DeprecationMessage && ConstName == other.ConstName;

        public override bool Equals(object? obj) => obj is ConfigEntry other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Key, Documentation, Product, DeprecationMessage, ConstName);
    }

    private sealed class ConfigurationData : IEquatable<ConfigurationData>
    {
        public ConfigurationData(Dictionary<string, ConfigEntry> configurations)
        {
            Configurations = configurations;
        }

        public Dictionary<string, ConfigEntry> Configurations { get; }

        public bool Equals(ConfigurationData? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Configurations.Count != other.Configurations.Count)
            {
                return false;
            }

            foreach (var kvp in Configurations)
            {
                if (!other.Configurations.TryGetValue(kvp.Key, out var otherEntry))
                {
                    return false;
                }

                if (!kvp.Value.Equals(otherEntry))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ConfigurationData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Configurations.Count);

            // Hash configuration keys and values in sorted order for determinism
            foreach (var kvp in Configurations.OrderBy(x => x.Key))
            {
                hash.Add(kvp.Key);
                hash.Add(kvp.Value);
            }

            return hash.ToHashCode();
        }
    }
}
