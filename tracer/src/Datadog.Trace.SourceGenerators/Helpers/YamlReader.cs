// <copyright file="YamlReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Datadog.Trace.SourceGenerators.Helpers
{
    /// <summary>
    /// Simple YAML parser for reading configuration data from YAML files.
    /// Supports basic YAML features needed for configuration documentation.
    /// </summary>
    internal static class YamlReader
    {
        /// <summary>
        /// Parses the full supported-configurations.yaml file.
        /// </summary>
        public static ParsedConfigurationData ParseSupportedConfigurations(string yamlContent)
        {
            var configurations = new Dictionary<string, ConfigurationEntry>();
            var deprecations = new Dictionary<string, string>();

            string? currentConfigKey = null;
            string? currentProduct = null;
            string? currentDocumentation = null;
            string? currentConstName = null;
            var currentAliases = new List<string>();
            var inDocumentation = false;
            var inAliases = false;
            var documentationBuilder = new StringBuilder();
            var inDeprecations = false;
            var inSupportedConfigurations = false;

            var lineNumber = 0;
            foreach (var line in new LineEnumerator(yamlContent.AsSpan()))
            {
                lineNumber++;
                var lineStr = line.ToString();
                var trimmedLine = lineStr.TrimStart();

                // Skip comments
                if (trimmedLine.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                // Check for top-level sections
                if (lineStr.StartsWith("supportedConfigurations:", StringComparison.Ordinal))
                {
                    inSupportedConfigurations = true;
                    inDeprecations = false;
                    continue;
                }

                if (lineStr.StartsWith("deprecations:", StringComparison.Ordinal))
                {
                    // Save last config entry before switching sections
                    if (currentConfigKey != null)
                    {
                        var doc = inDocumentation ? documentationBuilder.ToString().TrimEnd() : currentDocumentation;
                        configurations[currentConfigKey] = new ConfigurationEntry(currentConfigKey, currentProduct ?? string.Empty, doc, currentConstName, currentAliases.Count > 0 ? currentAliases.ToArray() : null);
                    }

                    inSupportedConfigurations = false;
                    inDeprecations = true;
                    inDocumentation = false;
                    inAliases = false;
                    continue;
                }

                if (lineStr.StartsWith("version:", StringComparison.Ordinal))
                {
                    continue;
                }

                // Handle deprecations section (simple key: value pairs with 2-space indent)
                if (inDeprecations && lineStr.StartsWith("  ", StringComparison.Ordinal) && !lineStr.StartsWith("    ", StringComparison.Ordinal))
                {
                    var colonIdx = trimmedLine.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var key = trimmedLine.Substring(0, colonIdx).Trim();
                        var value = trimmedLine.Substring(colonIdx + 1).Trim();
                        deprecations[key] = value;
                    }

                    continue;
                }

                // Handle supportedConfigurations section
                if (inSupportedConfigurations)
                {
                    // Count leading spaces
                    var indent = 0;
                    while (indent < lineStr.Length && lineStr[indent] == ' ')
                    {
                        indent++;
                    }

                    // Check for new configuration key first (2-space indent, ends with :, all uppercase)
                    // This needs to be checked before documentation handling to properly end multi-line docs
                    if (indent == 2 && trimmedLine.EndsWith(":", StringComparison.Ordinal))
                    {
                        var potentialKey = trimmedLine.TrimEnd(':');
                        // Check if this looks like a config key (all uppercase with underscores/digits)
                        var isConfigKey = potentialKey.Length > 0 && potentialKey.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c));

                        if (isConfigKey)
                        {
                            // Save previous config entry
                            if (currentConfigKey != null)
                            {
                                var doc = inDocumentation ? documentationBuilder.ToString().TrimEnd() : currentDocumentation;
                                configurations[currentConfigKey] = new ConfigurationEntry(currentConfigKey, currentProduct ?? string.Empty, doc, currentConstName, currentAliases.Count > 0 ? currentAliases.ToArray() : null);
                            }

                            currentConfigKey = potentialKey;
                            currentProduct = null;
                            currentDocumentation = null;
                            currentConstName = null;
                            currentAliases.Clear();
                            inDocumentation = false;
                            inAliases = false;
                            documentationBuilder.Clear();
                            continue;
                        }
                    }

                    // Handle multi-line documentation content
                    if (inDocumentation)
                    {
                        // Check if we've reached a new property at the same level (4-space indent)
                        if (indent == 4 && !trimmedLine.StartsWith("-", StringComparison.Ordinal))
                        {
                            var propColonIdx = trimmedLine.IndexOf(':');
                            if (propColonIdx > 0)
                            {
                                var propName = trimmedLine.Substring(0, propColonIdx);
                                if (propName is "const_name" or "product" or "implementation" or "type" or "default" or "aliases" or "deprecation_message")
                                {
                                    // End of documentation, process this property
                                    inDocumentation = false;
                                    currentDocumentation = documentationBuilder.ToString().TrimEnd();
                                    documentationBuilder.Clear();
                                    // Fall through to process the property
                                }
                                else
                                {
                                    // Continue reading documentation content
                                    if (documentationBuilder.Length > 0)
                                    {
                                        documentationBuilder.AppendLine();
                                    }

                                    documentationBuilder.Append(trimmedLine);
                                    continue;
                                }
                            }
                            else
                            {
                                // Continue reading documentation content
                                if (documentationBuilder.Length > 0)
                                {
                                    documentationBuilder.AppendLine();
                                }

                                documentationBuilder.Append(trimmedLine);
                                continue;
                            }
                        }
                        else if (indent >= 6)
                        {
                            // Documentation content (6+ space indent)
                            if (documentationBuilder.Length > 0)
                            {
                                documentationBuilder.AppendLine();
                            }

                            // Remove the base indent (6 spaces) from documentation lines
                            if (lineStr.Length > 6)
                            {
                                documentationBuilder.Append(lineStr.Substring(6));
                            }

                            continue;
                        }
                        else if (trimmedLine.StartsWith("-", StringComparison.Ordinal))
                        {
                            // New list item, end documentation
                            inDocumentation = false;
                            currentDocumentation = documentationBuilder.ToString().TrimEnd();
                            documentationBuilder.Clear();
                            // Fall through
                        }
                        else
                        {
                            // Continue reading documentation content
                            if (documentationBuilder.Length > 0)
                            {
                                documentationBuilder.AppendLine();
                            }

                            documentationBuilder.Append(trimmedLine);
                            continue;
                        }
                    }

                    // Alias entry (list item starting with -)
                    if (inAliases && trimmedLine.StartsWith("- ", StringComparison.Ordinal))
                    {
                        var alias = trimmedLine.Substring(2).Trim();
                        if (!string.IsNullOrEmpty(alias))
                        {
                            currentAliases.Add(alias);
                        }

                        continue;
                    }

                    // Properties within a config entry (4-space indent, has colon, not a list item)
                    if (indent == 4 && !trimmedLine.StartsWith("-", StringComparison.Ordinal))
                    {
                        var colonIdx = trimmedLine.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            var propName = trimmedLine.Substring(0, colonIdx);
                            var propValue = trimmedLine.Substring(colonIdx + 1).Trim();

                            switch (propName)
                            {
                                case "product":
                                    currentProduct = propValue;
                                    break;
                                case "const_name":
                                    currentConstName = propValue;
                                    break;
                                case "aliases":
                                    inAliases = true;
                                    break;
                                case "documentation":
                                    if (propValue == "|-" || propValue == "|")
                                    {
                                        // Multi-line documentation
                                        inDocumentation = true;
                                        documentationBuilder.Clear();
                                    }
                                    else
                                    {
                                        // Single-line documentation
                                        currentDocumentation = propValue;
                                    }

                                    break;
                            }

                            if (propName != "aliases")
                            {
                                inAliases = false;
                            }

                            continue;
                        }
                    }

                    // List item markers at indent 2 (e.g. "- implementation: A") are expected and intentionally skipped
                    if (indent == 2 && trimmedLine.StartsWith("- ", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // If we reach here, the line was not recognized
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        throw new InvalidOperationException($"Unrecognized line {lineNumber} in supportedConfigurations section: '{trimmedLine}'");
                    }
                }
            }

            // Save last config entry
            if (currentConfigKey != null)
            {
                var doc = inDocumentation ? documentationBuilder.ToString().TrimEnd() : currentDocumentation;
                configurations[currentConfigKey] = new ConfigurationEntry(currentConfigKey, currentProduct ?? string.Empty, doc, currentConstName, currentAliases.Count > 0 ? currentAliases.ToArray() : null);
            }

            return new ParsedConfigurationData(configurations, deprecations);
        }

        /// <summary>
        /// Enumerator for iterating through lines in a ReadOnlySpan without allocations.
        /// </summary>
        private ref struct LineEnumerator
        {
            private ReadOnlySpan<char> _remaining;
            private ReadOnlySpan<char> _current;
            private bool _isEnumeratorActive;

            public LineEnumerator(ReadOnlySpan<char> text)
            {
                _remaining = text;
                _current = default;
                _isEnumeratorActive = true;
            }

            public ReadOnlySpan<char> Current => _current;

            public LineEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                if (!_isEnumeratorActive)
                {
                    return false;
                }

                if (_remaining.Length == 0)
                {
                    _isEnumeratorActive = false;
                    return false;
                }

                var idx = _remaining.IndexOfAny('\r', '\n');
                if (idx < 0)
                {
                    // Last line without line ending
                    _current = _remaining;
                    _remaining = default;
                    return true;
                }

                _current = _remaining.Slice(0, idx);

                // Skip past the line ending (\r\n or \n or \r)
                var advance = idx + 1;
                if (idx < _remaining.Length - 1 && _remaining[idx] == '\r' && _remaining[idx + 1] == '\n')
                {
                    advance = idx + 2;
                }

                _remaining = _remaining.Slice(advance);
                return true;
            }
        }

#pragma warning disable SA1202 // Elements should be ordered by access
        /// <summary>
        /// Represents a parsed configuration entry from the YAML file.
        /// </summary>
        internal readonly struct ConfigurationEntry : IEquatable<ConfigurationEntry>
        {
            public ConfigurationEntry(string key, string? product, string? documentation, string? constName, string[]? aliases = null)
            {
                Key = key;
                Product = product;
                Documentation = documentation;
                ConstName = constName;
                Aliases = aliases;
            }

            public string Key { get; }

            public string? Product { get; }

            public string? Documentation { get; }

            public string? ConstName { get; }

            public string[]? Aliases { get; }

            public bool Equals(ConfigurationEntry other)
            {
                if (Key != other.Key || Product != other.Product || Documentation != other.Documentation || ConstName != other.ConstName)
                {
                    return false;
                }

                if (ReferenceEquals(Aliases, other.Aliases))
                {
                    return true;
                }

                if (Aliases is null || other.Aliases is null || Aliases.Length != other.Aliases.Length)
                {
                    return Aliases is null && other.Aliases is null;
                }

                for (var i = 0; i < Aliases.Length; i++)
                {
                    if (Aliases[i] != other.Aliases[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public override bool Equals(object? obj) => obj is ConfigurationEntry other && Equals(other);

            public override int GetHashCode()
            {
                var hash = HashCode.Combine(Key, Product, Documentation, ConstName);
                if (Aliases is not null)
                {
                    foreach (var alias in Aliases)
                    {
                        hash = HashCode.Combine(hash, alias);
                    }
                }

                return hash;
            }
        }

        /// <summary>
        /// Represents the full parsed configuration data from supported-configurations.yaml.
        /// </summary>
        internal readonly struct ParsedConfigurationData : IEquatable<ParsedConfigurationData>
        {
            public ParsedConfigurationData(
                Dictionary<string, ConfigurationEntry> configurations,
                Dictionary<string, string> deprecations)
            {
                Configurations = configurations;
                Deprecations = deprecations;
            }

            public Dictionary<string, ConfigurationEntry> Configurations { get; }

            public Dictionary<string, string>? Deprecations { get; }

            public bool Equals(ParsedConfigurationData other)
            {
                if (Configurations.Count != other.Configurations.Count)
                {
                    return false;
                }

                foreach (var kvp in Configurations)
                {
                    if (!other.Configurations.TryGetValue(kvp.Key, out var otherEntry) || !kvp.Value.Equals(otherEntry))
                    {
                        return false;
                    }
                }

                if (Deprecations is null && other.Deprecations is null)
                {
                    return true;
                }

                if (Deprecations is null || other.Deprecations is null || Deprecations.Count != other.Deprecations.Count)
                {
                    return false;
                }

                foreach (var kvp in Deprecations)
                {
                    if (!other.Deprecations.TryGetValue(kvp.Key, out var otherValue) || kvp.Value != otherValue)
                    {
                        return false;
                    }
                }

                return true;
            }

            public override bool Equals(object? obj) => obj is ParsedConfigurationData other && Equals(other);

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(Configurations.Count);
                foreach (var kvp in Configurations.OrderBy(x => x.Key))
                {
                    hash.Add(kvp.Key);
                    hash.Add(kvp.Value);
                }

                if (Deprecations is not null)
                {
                    hash.Add(Deprecations.Count);
                    foreach (var kvp in Deprecations.OrderBy(x => x.Key))
                    {
                        hash.Add(kvp.Key);
                        hash.Add(kvp.Value);
                    }
                }

                return hash.ToHashCode();
            }
        }
    }
}
