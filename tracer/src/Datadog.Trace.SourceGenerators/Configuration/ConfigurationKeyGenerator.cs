// <copyright file="ConfigurationKeyGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.Configuration;
using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Source generator that reads supported-configurations.json and generates ConfigKey structs
/// for each configuration key.
/// </summary>
[Generator]
public class ConfigurationKeyGenerator : IIncrementalGenerator
{
    private const string SupportedConfigurationsFileName = "supported-configurations.json";
    private const string Namespace = "Datadog.Trace.Configuration.ConfigurationSources.Registry";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get the supported-configurations.json file and parse configuration keys
        var additionalText = context.AdditionalTextsProvider
                                    .Where(static file => Path.GetFileName(file.Path).Equals(SupportedConfigurationsFileName, StringComparison.OrdinalIgnoreCase))
                                    .WithTrackingName(TrackingNames.ConfigurationKeyAdditionalText);

        var supportedConfigSection = additionalText.Collect()
                                        .Select(static (files, ct) =>
                                        {
                                            if (files.Length == 0)
                                            {
                                                // No supported-configurations.json file found
                                                return new Result<string>(
                                                    string.Empty,
                                                    new EquatableArray<DiagnosticInfo>(
                                                    [
                                                        CreateDiagnosticInfo("DDSG0003", "Configuration file not found", $"The file '{SupportedConfigurationsFileName}' was not found. Make sure the supported-configurations.json file exists and is included as an AdditionalFile.", DiagnosticSeverity.Error)
                                                    ]));
                                            }

                                            // Extract from the first (and should be only) file
                                            return ExtractMainSection(files[0], ct);
                                        })
                                        .WithTrackingName(TrackingNames.ConfigurationKeySupportedSection);

        var configContent = supportedConfigSection.Select(static (extractResult, ct) =>
                                          {
                                              if (extractResult.Errors.Count > 0)
                                              {
                                                  // Return the errors from extraction
                                                  return new Result<Configuration>(null!, extractResult.Errors);
                                              }

                                              return ParseAliasesContent(extractResult.Value, ct);
                                          })
                                         .WithTrackingName(TrackingNames.ConfigurationKeyContent);

        // Always generate source code, even when there are errors
        // This ensures compilation doesn't fail due to missing generated types
        context.RegisterSourceOutput(
            configContent,
            static (spc, result) => Execute(spc, result));
    }

    private static void Execute(SourceProductionContext context, Result<Configuration> result)
    {
        // Report any errors
        foreach (var error in result.Errors)
        {
            var location = error.Location?.ToLocation() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(error.Descriptor, location));
        }

        // Generate source even if there are errors (to avoid missing type errors)
        var config = result.Value ?? new Configuration(new List<string>());
        var source = GenerateConfigKeyStructs(config);
        context.AddSource("ConfigurationKeys.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static Result<string> ExtractMainSection(AdditionalText file, CancellationToken cancellationToken)
    {
        try
        {
            var sourceText = file.GetText(cancellationToken);
            if (sourceText is null)
            {
                return CreateError<string>(
                    "DDSG0003",
                    "Configuration file not found",
                    $"The file '{file.Path}' could not be read.");
            }

            var configSection = JsonReader.ExtractJsonObjectSection(sourceText.ToString(), "supportedConfigurations");
            return new Result<string>(configSection ?? string.Empty, default);
        }
        catch (Exception ex)
        {
            return CreateError<string>(
                "DDSG0004",
                "Configuration file read error",
                $"Failed to read configuration file '{file.Path}': {ex.Message}");
        }
    }

    private static Result<Configuration> ParseAliasesContent(string configContent, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(configContent))
            {
                return new Result<Configuration>(new Configuration(new List<string>()), default);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Parse top-level keys from the supportedConfigurations object
            var keys = JsonReader.ParseTopLevelKeys(configContent);
            return new Result<Configuration>(new Configuration(keys), default);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new Result<Configuration>(
                null!,
                new EquatableArray<DiagnosticInfo>(
                [
                    CreateDiagnosticInfo("DDSG0002", "Configuration parsing error", $"Failed to parse configuration keys: {ex.Message}", DiagnosticSeverity.Error)
                ]));
        }
    }

    private static DiagnosticInfo CreateDiagnosticInfo(string id, string title, string message, DiagnosticSeverity severity = DiagnosticSeverity.Warning)
    {
        return new DiagnosticInfo(
            new DiagnosticDescriptor(id, title, message, "Configuration", severity, isEnabledByDefault: true),
            Location.None);
    }

    private static Result<T> CreateError<T>(string id, string title, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        where T : IEquatable<T>
    {
        return new Result<T>(
            default!,
            new EquatableArray<DiagnosticInfo>([CreateDiagnosticInfo(id, title, message, severity)]));
    }

    private static string GenerateConfigKeyStructs(Configuration configuration)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine();
        sb.AppendLine($"using {Namespace};");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {Namespace}.Generated;");
        sb.AppendLine();

        foreach (var key in configuration.Keys.Distinct(StringComparer.Ordinal).OrderBy(k => k))
        {
            GenerateConfigKeyStruct(sb, key);
        }

        return sb.ToString();
    }

    private static void GenerateConfigKeyStruct(StringBuilder sb, string key)
    {
        var structName = "ConfigKey" + ToPascalCase(key);

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Configuration key for {key}");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"internal readonly partial struct {structName} : IConfigKey");
        sb.AppendLine("{");
        sb.AppendLine($"    internal const string Key = \"{key}\";");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc/>");
        sb.AppendLine("    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public string GetKey() => Key;");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return string.Concat(
            input
               .Split(['_'], StringSplitOptions.RemoveEmptyEntries)
               .Select(
                    part =>
                    {
                        if (part.Length == 0)
                        {
                            return string.Empty;
                        }

                        var head = char.ToUpperInvariant(part[0]);
                        var tail = part.Length > 1 ? part.Substring(1).ToLowerInvariant() : string.Empty;
                        return head + tail;
                    }));
    }

    private sealed class Configuration(List<string> keys) : IEquatable<Configuration>
    {
        public List<string> Keys { get; } = keys;

        public bool Equals(Configuration? other)
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
            if (Keys.Count != other.Keys.Count)
            {
                return false;
            }

            for (int i = 0; i < Keys.Count; i++)
            {
                if (Keys[i] != other.Keys[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is Configuration other && Equals(other));
        }

        public override int GetHashCode()
        {
            var hash = new System.HashCode();
            hash.Add(Keys.Count);

            // Include content in hash for proper change detection
            foreach (var key in Keys.OrderBy(x => x))
            {
                hash.Add(key);
            }

            return hash.ToHashCode();
        }
    }
}
