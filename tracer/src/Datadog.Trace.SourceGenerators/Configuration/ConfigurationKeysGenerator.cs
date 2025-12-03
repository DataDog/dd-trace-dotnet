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
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using HashCode = System.HashCode;

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
        // Create THREE separate single-value providers - each caches independently

        // JSON pipeline
        var jsonData = context.AdditionalTextsProvider
                              .Where(static file => Path.GetFileName(file.Path).Equals(SupportedConfigurationsFileName, StringComparison.OrdinalIgnoreCase))
                              .Select(static (file, ct) => file.GetText(ct))
                             .WithTrackingName(TrackingNames.ConfigurationKeysGenJsonFile)
                              .Select(static (text, _) =>
                              {
                                  var content = text?.ToString() ?? string.Empty;
                                  return ParseJsonContent(content);
                              })
                              .Collect()
                             .WithTrackingName(TrackingNames.ConfigurationKeysGenParseConfiguration); // Now it's IncrementalValueProvider<ImmutableArray<Result>>

        // YAML pipeline
        var yamlData = context.AdditionalTextsProvider
                              .Where(static file => Path.GetFileName(file.Path).Equals(SupportedConfigurationsDocsFileName, StringComparison.OrdinalIgnoreCase))
                              .Select(static (file, ct) => file.GetText(ct))
                              .WithTrackingName(TrackingNames.ConfigurationKeysGenYamlFile)
                              .Select(static (text, _) =>
                               {
                                   var content = text?.ToString() ?? string.Empty;
                                   return ParseYamlContent(content);
                               })
                              .Collect()
                              .WithTrackingName(TrackingNames.ConfigurationKeysGenParseYaml);

        // Mapping pipeline
        var mappingData = context.AdditionalTextsProvider
                                 .Where(static file => Path.GetFileName(file.Path).Equals(ConfigurationKeysMappingFileName, StringComparison.OrdinalIgnoreCase))
                                 .Select(static (file, ct) => file.GetText(ct)).WithTrackingName(TrackingNames.ConfigurationKeysGenMappingFile)
                                 .Select(static (text, _) =>
                                  {
                                      var content = text?.ToString() ?? string.Empty;
                                      return ParseMappingContent(content);
                                  })
                                 .Collect()
                                 .WithTrackingName(TrackingNames.ConfigurationKeysGenParseMapping);

        // Combine the three independent providers
        var combined = jsonData
                      .Combine(yamlData)
                      .Combine(mappingData)
                      .Select(static (data, _) =>
                      {
                          var ((jsonArray, yamlArray), mappingArray) = data;

                          var json = jsonArray.Length > 0
                              ? jsonArray[0]
                              : new Result<ConfigurationData>(null!, new EquatableArray<DiagnosticInfo>([CreateDiagnosticInfo("DDSG0005", "Missing", "JSON not found", DiagnosticSeverity.Error)]));

                          var yaml = yamlArray.Length > 0
                              ? yamlArray[0]
                              : new Result<ParsedYamlDocs>(new ParsedYamlDocs(null), new EquatableArray<DiagnosticInfo>());

                          var mapping = mappingArray.Length > 0
                              ? mappingArray[0]
                              : new Result<ParsedMappingData>(new ParsedMappingData(null), new EquatableArray<DiagnosticInfo>());

                          return MergeResults(json, yaml, mapping);
                      }).WithTrackingName(TrackingNames.ConfigurationKeysGenMergeData);

        context.RegisterSourceOutput(combined, static (spc, result) => Execute(spc, result));
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

    // Parse JSON content from string
    private static Result<ConfigurationData> ParseJsonContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new Result<ConfigurationData>(null!, new EquatableArray<DiagnosticInfo>([CreateDiagnosticInfo("DDSG0006", "Read error", "JSON content is empty", DiagnosticSeverity.Error)]));
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (!root.TryGetProperty("supportedConfigurations", out var configSection))
            {
                return new Result<ConfigurationData>(null!, new EquatableArray<DiagnosticInfo>([CreateDiagnosticInfo("DDSG0007", "JSON parse error", "Missing 'supportedConfigurations' section", DiagnosticSeverity.Error)]));
            }

            var configurations = ParseConfigurationEntries(configSection);

            // Parse deprecations
            if (root.TryGetProperty("deprecations", out var deprecationsSection))
            {
                var deprecations = ParseDeprecations(deprecationsSection);
                foreach (var kvp in deprecations)
                {
                    if (configurations.ContainsKey(kvp.Key))
                    {
                        var existing = configurations[kvp.Key];
                        configurations[kvp.Key] = new ConfigEntry(existing.Key, existing.Documentation, existing.Product, kvp.Value);
                    }
                }
            }

            return new Result<ConfigurationData>(new ConfigurationData(configurations, null), new EquatableArray<DiagnosticInfo>());
        }
        catch (Exception ex)
        {
            return new Result<ConfigurationData>(null!, new EquatableArray<DiagnosticInfo>([CreateDiagnosticInfo("DDSG0007", "JSON parse error", $"Error: {ex.Message}", DiagnosticSeverity.Error)]));
        }
    }

    // Parse YAML content from string
    private static Result<ParsedYamlDocs> ParseYamlContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new Result<ParsedYamlDocs>(new ParsedYamlDocs(null), new EquatableArray<DiagnosticInfo>());
        }

        try
        {
            var docs = YamlReader.ParseDocumentation(content);
            return new Result<ParsedYamlDocs>(new ParsedYamlDocs(docs), new EquatableArray<DiagnosticInfo>());
        }
        catch
        {
            return new Result<ParsedYamlDocs>(new ParsedYamlDocs(null), new EquatableArray<DiagnosticInfo>());
        }
    }

    // Parse mapping content from string
    private static Result<ParsedMappingData> ParseMappingContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new Result<ParsedMappingData>(new ParsedMappingData(null), new EquatableArray<DiagnosticInfo>());
        }

        try
        {
            var mapping = ParseMappingJson(content);
            return new Result<ParsedMappingData>(new ParsedMappingData(mapping), new EquatableArray<DiagnosticInfo>());
        }
        catch
        {
            return new Result<ParsedMappingData>(new ParsedMappingData(null), new EquatableArray<DiagnosticInfo>());
        }
    }

    // Merge all parsed results into final ConfigurationData
    private static Result<ConfigurationData> MergeResults(
        Result<ConfigurationData> jsonResult,
        Result<ParsedYamlDocs> yamlResult,
        Result<ParsedMappingData> mappingResult)
    {
        var diagnostics = new List<DiagnosticInfo>(jsonResult.Errors);
        diagnostics.AddRange(yamlResult.Errors);
        diagnostics.AddRange(mappingResult.Errors);

        if (jsonResult.Value is null)
        {
            return new Result<ConfigurationData>(null!, new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
        }

        var configurations = new Dictionary<string, ConfigEntry>(jsonResult.Value.Configurations);

        // Merge YAML docs
        var yamlDocs = yamlResult.Value.Docs;
        if (yamlDocs != null)
        {
            foreach (var kvp in yamlDocs)
            {
                if (configurations.ContainsKey(kvp.Key))
                {
                    var existing = configurations[kvp.Key];
                    configurations[kvp.Key] = new ConfigEntry(existing.Key, kvp.Value, existing.Product, existing.DeprecationMessage);
                }
            }
        }

        var nameMapping = mappingResult.Value.Mapping;
        return new Result<ConfigurationData>(new ConfigurationData(configurations, nameMapping), new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
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
        sb.Append("namespace ").Append(Namespace).AppendLine(";");
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

    private static Dictionary<string, ConstantNameMapping> ParseMappingJson(string mappingJson)
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
                if (pascalName.Length > productName.Length &&
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

        public override int GetHashCode() => HashCode.Combine(Key, Documentation, Product, DeprecationMessage);
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

    private readonly struct ParsedYamlDocs : IEquatable<ParsedYamlDocs>
    {
        public ParsedYamlDocs(Dictionary<string, string>? docs)
        {
            Docs = docs;
        }

        public Dictionary<string, string>? Docs { get; }

        public bool Equals(ParsedYamlDocs other)
        {
            if (Docs == null && other.Docs == null)
            {
                return true;
            }

            if (Docs == null || other.Docs == null || Docs.Count != other.Docs.Count)
            {
                return false;
            }

            foreach (var kvp in Docs)
            {
                if (!other.Docs.TryGetValue(kvp.Key, out var otherValue) || kvp.Value != otherValue)
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is ParsedYamlDocs other && Equals(other);

        public override int GetHashCode()
        {
            if (Docs == null)
            {
                return 0;
            }

            var hash = new HashCode();
            hash.Add(Docs.Count);

            // Hash keys and values in sorted order for determinism
            foreach (var kvp in Docs.OrderBy(x => x.Key))
            {
                hash.Add(kvp.Key);
                hash.Add(kvp.Value);
            }

            return hash.ToHashCode();
        }
    }

    private readonly struct ParsedMappingData : IEquatable<ParsedMappingData>
    {
        public ParsedMappingData(Dictionary<string, ConstantNameMapping>? mapping)
        {
            Mapping = mapping;
        }

        public Dictionary<string, ConstantNameMapping>? Mapping { get; }

        public bool Equals(ParsedMappingData other)
        {
            if (Mapping == null && other.Mapping == null)
            {
                return true;
            }

            if (Mapping == null || other.Mapping == null || Mapping.Count != other.Mapping.Count)
            {
                return false;
            }

            foreach (var kvp in Mapping)
            {
                if (!other.Mapping.TryGetValue(kvp.Key, out var otherValue) || !kvp.Value.Equals(otherValue))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is ParsedMappingData other && Equals(other);

        public override int GetHashCode()
        {
            if (Mapping == null)
            {
                return 0;
            }

            var hash = new HashCode();
            hash.Add(Mapping.Count);

            // Hash keys and values in sorted order for determinism
            foreach (var kvp in Mapping.OrderBy(x => x.Key))
            {
                hash.Add(kvp.Key);
                hash.Add(kvp.Value);
            }

            return hash.ToHashCode();
        }
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
            var hash = new HashCode();
            hash.Add(Configurations.Count);

            // Hash configuration keys and values in sorted order for determinism
            foreach (var kvp in Configurations.OrderBy(x => x.Key))
            {
                hash.Add(kvp.Key);
                hash.Add(kvp.Value);
            }

            // Hash name mapping
            if (NameMapping != null)
            {
                hash.Add(NameMapping.Count);
                foreach (var kvp in NameMapping.OrderBy(x => x.Key))
                {
                    hash.Add(kvp.Key);
                    hash.Add(kvp.Value);
                }
            }

            return hash.ToHashCode();
        }
    }
}
