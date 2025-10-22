// <copyright file="ConfigurationKeysGenerator.cs" company="Datadog">
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
/// Source generator that reads supported-configurations.json and generates ConfigurationKeys
/// with proper nested classes organized by product and full XML documentation.
/// </summary>
[Generator]
public class ConfigurationKeysGenerator : IIncrementalGenerator
{
    private const string SupportedConfigurationsFileName = "supported-configurations.json";
    private const string SupportedConfigurationsDocsFileName = "supported-configurations-docs.yaml";
    private const string ConfigurationKeysMappingFileName = "configuration_keys_mapping.json";
    private const string GeneratedClassName = "ConfigurationKeys2";
    private const string Namespace = "Datadog.Trace.Configuration";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get the supported-configurations.json file
        var jsonFile = context.AdditionalTextsProvider
                              .Where(static file => Path.GetFileName(file.Path).Equals(SupportedConfigurationsFileName, StringComparison.OrdinalIgnoreCase))
                              .WithTrackingName(TrackingNames.ConfigurationKeysGenAdditionalText);

        // Get the supported-configurations-docs.yaml file (optional)
        var yamlFile = context.AdditionalTextsProvider
                              .Where(static file => Path.GetFileName(file.Path).Equals(SupportedConfigurationsDocsFileName, StringComparison.OrdinalIgnoreCase))
                              .WithTrackingName(TrackingNames.ConfigurationKeysGenYamlAdditionalText);

        // Get the configuration_keys_mapping.json file (optional)
        var mappingFile = context.AdditionalTextsProvider
                                 .Where(static file => Path.GetFileName(file.Path).Equals(ConfigurationKeysMappingFileName, StringComparison.OrdinalIgnoreCase))
                                 .WithTrackingName(TrackingNames.ConfigurationKeysGenMappingAdditionalText);

        // Combine all files
        var combinedFiles = jsonFile.Collect().Combine(yamlFile.Collect()).Combine(mappingFile.Collect());

        var configContent = combinedFiles.Select(static (files, ct) =>
                                          {
                                              var ((jsonFiles, yamlFiles), mappingFiles) = files;

                                              if (jsonFiles.Length == 0)
                                              {
                                                  return new Result<(string Json, string? Yaml, string? Mapping)>(
                                                      (string.Empty, null, null),
                                                      new EquatableArray<DiagnosticInfo>(
                                                      [
                                                          CreateDiagnosticInfo("DDSG0005", "Configuration file not found", $"The file '{SupportedConfigurationsFileName}' was not found. Make sure the supported-configurations.json file exists and is included as an AdditionalFile.", DiagnosticSeverity.Error)
                                                      ]));
                                              }

                                              var jsonResult = ExtractConfigurationContent(jsonFiles[0], ct);
                                              if (jsonResult.Errors.Count > 0)
                                              {
                                                  return new Result<(string Json, string? Yaml, string? Mapping)>((string.Empty, null, null), jsonResult.Errors);
                                              }

                                              string? yamlContent = null;
                                              if (yamlFiles.Length > 0)
                                              {
                                                  var yamlResult = ExtractConfigurationContent(yamlFiles[0], ct);
                                                  if (yamlResult.Errors.Count == 0)
                                                  {
                                                      yamlContent = yamlResult.Value;
                                                  }

                                                  // YAML is optional, so we don't fail if it has errors
                                              }

                                              string? mappingContent = null;
                                              if (mappingFiles.Length > 0)
                                              {
                                                  var mappingResult = ExtractConfigurationContent(mappingFiles[0], ct);
                                                  if (mappingResult.Errors.Count == 0)
                                                  {
                                                      mappingContent = mappingResult.Value;
                                                  }

                                                  // Mapping is optional, so we don't fail if it has errors
                                              }

                                              return new Result<(string Json, string? Yaml, string? Mapping)>((jsonResult.Value, yamlContent, mappingContent), new EquatableArray<DiagnosticInfo>());
                                          })
                                         .WithTrackingName(TrackingNames.ConfigurationKeysGenContentExtracted);

        var parsedConfig = configContent.Select(static (extractResult, ct) =>
                                         {
                                             if (extractResult.Errors.Count > 0)
                                             {
                                                 return new Result<ConfigurationData>(null!, extractResult.Errors);
                                             }

                                             return ParseConfigurationContent(extractResult.Value.Json, extractResult.Value.Yaml, extractResult.Value.Mapping, ct);
                                         })
                                        .WithTrackingName(TrackingNames.ConfigurationKeysGenParseConfiguration);

        context.RegisterSourceOutput(
            parsedConfig,
            static (spc, result) => Execute(spc, result));
    }

    private static void Execute(SourceProductionContext context, Result<ConfigurationData> result)
    {
        // Report any diagnostics
        foreach (var diagnostic in result.Errors)
        {
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location?.ToLocation()));
        }

        // Generate source code even if there are errors (use empty configuration as fallback)
        var configData = result.Value ?? new ConfigurationData(new Dictionary<string, ConfigEntry>(), null);

        // Group by product
        var productGroups = configData.Configurations
                                      .GroupBy(kvp => kvp.Value.Product)
                                      .OrderBy(g => g.Key)
                                      .ToList();

        // Generate partial class files for each product (or empty main class if no products)
        foreach (var productGroup in productGroups)
        {
            var productSource = GenerateProductPartialClass(productGroup.Key, productGroup.ToList(), configData.NameMapping);
            var fileName = string.IsNullOrEmpty(productGroup.Key)
                               ? $"{GeneratedClassName}.g.cs"
                               : $"{GeneratedClassName}.{productGroup.Key}.g.cs";
            context.AddSource(fileName, SourceText.From(productSource, Encoding.UTF8));
        }
    }

    private static Result<string> ExtractConfigurationContent(AdditionalText file, CancellationToken cancellationToken)
    {
        try
        {
            var sourceText = file.GetText(cancellationToken);
            if (sourceText is null)
            {
                return new Result<string>(
                    string.Empty,
                    new EquatableArray<DiagnosticInfo>(
                    [
                        CreateDiagnosticInfo("DDSG0006", "Configuration file read error", $"Unable to read the content of '{SupportedConfigurationsFileName}'.", DiagnosticSeverity.Error)
                    ]));
            }

            return new Result<string>(sourceText.ToString(), new EquatableArray<DiagnosticInfo>());
        }
        catch (Exception ex)
        {
            return new Result<string>(
                string.Empty,
                new EquatableArray<DiagnosticInfo>(
                [
                    CreateDiagnosticInfo("DDSG0006", "Configuration file read error", $"Error reading '{SupportedConfigurationsFileName}': {ex.Message}", DiagnosticSeverity.Error)
                ]));
        }
    }

    private static Result<ConfigurationData> ParseConfigurationContent(string jsonContent, string? yamlContent, string? mappingContent, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            // Extract the supportedConfigurations section from JSON
            if (!root.TryGetProperty("supportedConfigurations", out var configSection))
            {
                return new Result<ConfigurationData>(
                    null!,
                    new EquatableArray<DiagnosticInfo>(
                    [
                        CreateDiagnosticInfo("DDSG0007", "JSON parse error", "Failed to find 'supportedConfigurations' section in supported-configurations.json.", DiagnosticSeverity.Error)
                    ]));
            }

            // Parse mapping file if available
            Dictionary<string, ConstantNameMapping>? nameMapping = null;
            if (mappingContent != null && !string.IsNullOrEmpty(mappingContent))
            {
                try
                {
                    nameMapping = ParseMappingFile(mappingContent);
                }
                catch
                {
                    // Mapping is optional, continue without it
                }
            }

            // Parse YAML documentation if available
            Dictionary<string, string>? yamlDocs = null;
            if (yamlContent != null && !string.IsNullOrEmpty(yamlContent))
            {
                try
                {
                    yamlDocs = YamlReader.ParseDocumentation(yamlContent);
                }
                catch
                {
                    // YAML parsing is optional, continue without it
                }
            }

            // Parse each configuration entry from JSON
            var configurations = ParseConfigurationEntries(configSection);

            // Parse deprecations section
            Dictionary<string, string>? deprecations = null;
            if (root.TryGetProperty("deprecations", out var deprecationsSection))
            {
                deprecations = ParseDeprecations(deprecationsSection);
            }

            // Override documentation from YAML if available
            if (yamlDocs != null)
            {
                foreach (var key in yamlDocs.Keys)
                {
                    if (configurations.ContainsKey(key))
                    {
                        var existing = configurations[key];
                        configurations[key] = new ConfigEntry(existing.Key, yamlDocs[key], existing.Product, existing.DeprecationMessage);
                    }
                }
            }

            // Add deprecation messages to entries
            if (deprecations != null)
            {
                foreach (var kvp in deprecations)
                {
                    if (configurations.ContainsKey(kvp.Key))
                    {
                        var existing = configurations[kvp.Key];
                        configurations[kvp.Key] = new ConfigEntry(existing.Key, existing.Documentation, existing.Product, kvp.Value);
                    }
                }
            }

            return new Result<ConfigurationData>(new ConfigurationData(configurations, nameMapping), new EquatableArray<DiagnosticInfo>());
        }
        catch (Exception ex)
        {
            return new Result<ConfigurationData>(
                null!,
                new EquatableArray<DiagnosticInfo>(
                [
                    CreateDiagnosticInfo("DDSG0007", "JSON parse error", $"Error parsing supported-configurations.json: {ex.Message}", DiagnosticSeverity.Error)
                ]));
        }
    }

    private static Dictionary<string, string> ParseDeprecations(JsonElement deprecationsElement)
    {
        var deprecations = new Dictionary<string, string>();

        foreach (var property in deprecationsElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (value != null)
                {
                    deprecations[property.Name] = value;
                }
            }
        }

        return deprecations;
    }

    private static Dictionary<string, ConfigEntry> ParseConfigurationEntries(JsonElement configSection)
    {
        var configurations = new Dictionary<string, ConfigEntry>();

        foreach (var property in configSection.EnumerateObject())
        {
            var key = property.Name;
            var value = property.Value;

            // Validate that the value is an object
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Configuration entry '{key}' must be an object");
            }

            // Extract the product field if it exists
            var product = string.Empty;
            if (value.TryGetProperty("product", out var productElement) &&
                productElement.ValueKind == JsonValueKind.String)
            {
                product = productElement.GetString() ?? string.Empty;
            }

            configurations[key] = new ConfigEntry(key, string.Empty, product);
        }

        return configurations;
    }

    private static void AppendFileHeader(StringBuilder sb)
    {
        sb.Append(Constants.FileHeader);
        sb.Append(Constants.ConfigurationGeneratorComment);
    }

    private static string GenerateProductPartialClass(string product, List<KeyValuePair<string, ConfigEntry>> entries, Dictionary<string, ConstantNameMapping>? nameMapping)
    {
        var sb = new StringBuilder();

        AppendFileHeader(sb);
        sb.AppendLine($"namespace {Namespace};");
        sb.AppendLine();

        if (string.IsNullOrEmpty(product))
        {
            // Generate main class without nested product class
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// String constants for standard Datadog configuration keys.");
            sb.AppendLine("/// Auto-generated from supported-configurations.json and supported-configurations-docs.yaml");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"internal static partial class {GeneratedClassName}");
            sb.AppendLine("{");

            var sortedEntries = entries.OrderBy(kvp => kvp.Key).ToList();
            for (int i = 0; i < sortedEntries.Count; i++)
            {
                GenerateConstDeclaration(sb, sortedEntries[i].Value, 1, product, nameMapping);
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
                GenerateConstDeclaration(sb, sortedEntries[i].Value, 2, product, nameMapping);
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

    private static Dictionary<string, ConstantNameMapping> ParseMappingFile(string mappingJson)
    {
        var mapping = new Dictionary<string, ConstantNameMapping>();

        using var document = JsonDocument.Parse(mappingJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("mappings", out var mappingsArray))
        {
            return mapping;
        }

        foreach (var item in mappingsArray.EnumerateArray())
        {
            if (!item.TryGetProperty("env_var", out var envVarElement))
            {
                continue;
            }

            var envVar = envVarElement.GetString();
            if (string.IsNullOrEmpty(envVar))
            {
                continue;
            }

            // const_name can be null, so check if it exists and is not null
            if (item.TryGetProperty("const_name", out var constNameElement) &&
                constNameElement.ValueKind == JsonValueKind.String)
            {
                var constName = constNameElement.GetString();
                if (!string.IsNullOrEmpty(constName) && !string.IsNullOrEmpty(envVar))
                {
                    mapping[envVar!] = new ConstantNameMapping(constName!);
                }
            }
        }

        return mapping;
    }

    private static void GenerateConstDeclaration(StringBuilder sb, ConfigEntry entry, int indentLevel, string? productName = null, Dictionary<string, ConstantNameMapping>? nameMapping = null)
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
                if (trimmedLine.StartsWith("<seealso") && (trimmedLine.Contains("/>") || trimmedLine.Contains("</seealso>")))
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
        var constName = KeyToConstName(entry.Key, ProductNameEquivalents(productName), nameMapping);
        sb.AppendLine($"{indent}public const string {constName} = \"{entry.Key}\";");
    }

    private static string KeyToConstName(string key, string[]? productNames = null, Dictionary<string, ConstantNameMapping>? nameMapping = null)
    {
        // First, check if we have a mapping for this key
        if (nameMapping != null && nameMapping.TryGetValue(key, out var mapping))
        {
            // Use the mapped name from ConfigurationKeys
            return mapping.ConstantName;
        }

        // Fallback to the original implementation
        // Remove DD_ or OTEL_ prefix
        var name = key;
        var prefixes = new[] { "DD_", "_DD_" };
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

        // Strip product prefix if the const name starts with it
        if (productNames != null)
        {
            foreach (var productName in productNames)
            {
                if (pascalName!.Length > productName.Length &&
                    pascalName.StartsWith(productName, StringComparison.InvariantCulture))
                {
                    pascalName = pascalName.Substring(productName.Length);
                    break;
                }
            }
        }

        return pascalName;
    }

    private static string[] ProductNameEquivalents(string? productName)
    {
        if (string.IsNullOrEmpty(productName))
        {
            return new[] { string.Empty };
        }

        // we need to keep comparison case-sensitive as there are keys like TraceRemoveIntegrationServiceNamesEnabled and we don't want to strip Tracer
        switch (productName)
        {
            case "AppSec":
                return new[] { "Appsec" };
            case "Tracer":
                return new[] { "Trace" };
            case "CiVisibility":
                return new[] { "Civisibility" };
            case "OpenTelemetry":
                return new[] { "Otel" };
            default:
                return new[] { productName! };
        }
    }

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
        public ConfigEntry(string key, string documentation, string product, string? deprecationMessage = null)
        {
            Key = key;
            Documentation = documentation;
            Product = product;
            DeprecationMessage = deprecationMessage;
        }

        public string Key { get; }

        public string Documentation { get; }

        public string Product { get; }

        public string? DeprecationMessage { get; }

        public bool Equals(ConfigEntry other) => Key == other.Key && Documentation == other.Documentation && Product == other.Product && DeprecationMessage == other.DeprecationMessage;

        public override bool Equals(object? obj) => obj is ConfigEntry other && Equals(other);

        public override int GetHashCode() => System.HashCode.Combine(Key, Documentation, Product, DeprecationMessage);
    }

    private readonly struct ConstantNameMapping : IEquatable<ConstantNameMapping>
    {
        public ConstantNameMapping(string constantName)
        {
            ConstantName = constantName;
        }

        public string ConstantName { get; }

        public bool Equals(ConstantNameMapping other) => ConstantName == other.ConstantName;

        public override bool Equals(object? obj) => obj is ConstantNameMapping other && Equals(other);

        public override int GetHashCode() => ConstantName.GetHashCode();
    }

    private sealed class ConfigurationData : IEquatable<ConfigurationData>
    {
        public ConfigurationData(Dictionary<string, ConfigEntry> configurations, Dictionary<string, ConstantNameMapping>? nameMapping)
        {
            Configurations = configurations;
            NameMapping = nameMapping;
        }

        public Dictionary<string, ConfigEntry> Configurations { get; }

        public Dictionary<string, ConstantNameMapping>? NameMapping { get; }

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

            // Compare name mappings
            if (NameMapping == null && other.NameMapping == null)
            {
                return true;
            }

            if (NameMapping == null || other.NameMapping == null)
            {
                return false;
            }

            if (NameMapping.Count != other.NameMapping.Count)
            {
                return false;
            }

            foreach (var kvp in NameMapping)
            {
                if (!other.NameMapping.TryGetValue(kvp.Key, out var otherMapping))
                {
                    return false;
                }

                if (!kvp.Value.Equals(otherMapping))
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
            return System.HashCode.Combine(Configurations.Count, NameMapping?.Count ?? 0);
        }
    }
}
