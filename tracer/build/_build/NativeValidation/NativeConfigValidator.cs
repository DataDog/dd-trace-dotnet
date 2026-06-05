// <copyright file="NativeConfigValidator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Nuke.Common.IO;
using YamlDotNet.RepresentationModel;
using Logger = Serilog.Log;

namespace NativeValidation;

/// <summary>
/// Validates that every DD_* / _DD_* environment variable read by native C++ code
/// has a corresponding entry in supported-configurations.yaml with "native" in its scope.
///
/// This runs as a Nuke step (rather than a managed unit test) so that developers working
/// only in the native solutions surface coverage gaps locally, not just in CI. The registry
/// is parsed with YamlDotNet (already referenced by the build project) so this validator has
/// no dependency on the managed source generators.
/// </summary>
public class NativeConfigValidator
{
    // Native source roots to scan, relative to the repository root.
    private static readonly string[] NativeSourceRoots =
    {
        "tracer/src/Datadog.Tracer.Native",
        "shared/src",
        "profiler/src",
    };

    // Path fragments that mark a file as test/build output and exclude it from the scan.
    private static readonly string[] ExcludedPathFragments =
    {
        "/test/", "/tests/", "/_build/", "/obj/", "/bin/",
    };

    // Source files excluded from the scan, by file name. crashhandler.cpp enumerates every
    // DD_* variable present in the environment at crash time, so it is not a fixed set of
    // env vars we can validate against the registry.
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "crashhandler.cpp",
    };

    // String literals that match the DD_ pattern but are not environment variables
    // (named pipes, IPC magic version strings, etc.). These are not expected in the registry.
    private static readonly HashSet<string> AllowedNonConfigLiterals = new(StringComparer.Ordinal)
    {
        "DD_ETW_DISPATCHER", // named pipe: \\.\pipe\DD_ETW_DISPATCHER
        "DD_ETW_IPC_V1",     // ETW IPC protocol magic version string
    };

    // Any double-quoted string literal whose contents start with DD_ or _DD_.
    // A plain literal scan (rather than matching WStr(...)/L"..." wrappers) ensures we don't
    // miss reads expressed through helpers; false positives are handled by the allowlist above.
    private static readonly Regex EnvVarLiteralPattern =
        new(@"""(_?DD_[A-Z][A-Z0-9_]+)""", RegexOptions.Compiled);

    /// <summary>
    /// Scans the native source tree and validates coverage against the registry.
    /// Throws if any native env var is missing from the registry or lacks "native" scope,
    /// or if any native-only entry carries a const_name.
    /// </summary>
    public void Validate(AbsolutePath rootDirectory, AbsolutePath supportedConfigurationsPath)
    {
        var registry = ParseRegistry(supportedConfigurationsPath);

        // var name (key or alias) -> entry. Keys are assigned unconditionally so a primary
        // key always wins over an alias of the same name regardless of iteration order;
        // aliases are only added when the name is not already taken.
        var entryByName = new Dictionary<string, RegistryEntry>(StringComparer.Ordinal);
        foreach (var entry in registry.Values)
        {
            entryByName[entry.Name] = entry;
            foreach (var alias in entry.Aliases)
            {
                if (!entryByName.ContainsKey(alias))
                {
                    entryByName[alias] = entry;
                }
            }
        }

        var nativeVars = ScanNativeEnvVars(rootDirectory);
        Logger.Information("Found {Count} distinct DD_* environment variables in native source", nativeVars.Count);

        var missingFromRegistry = new List<string>();
        var missingNativeScope = new List<string>();

        foreach (var name in nativeVars.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!entryByName.TryGetValue(name, out var entry))
            {
                missingFromRegistry.Add(name);
                continue;
            }

            if (!entry.Scope.Any(s => string.Equals(s, "native", StringComparison.OrdinalIgnoreCase)))
            {
                missingNativeScope.Add(name);
            }
        }

        // Native-only entries must not declare a const_name — no C# constant is generated for them.
        var nativeOnlyWithConstName = registry.Values
            .Where(e => e.Scope.Any(s => string.Equals(s, "native", StringComparison.OrdinalIgnoreCase))
                     && !e.Scope.Any(s => string.Equals(s, "managed", StringComparison.OrdinalIgnoreCase))
                     && !string.IsNullOrEmpty(e.ConstName))
            .Select(e => e.Name)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var hasErrors = false;

        if (missingFromRegistry.Count > 0)
        {
            hasErrors = true;
            Logger.Error(
                "The following DD_* environment variables are read by native code but are missing from {File}:{Break}{Vars}",
                supportedConfigurationsPath.Name,
                Environment.NewLine,
                string.Join(Environment.NewLine, missingFromRegistry.Select(v => "  - " + v)));
            Logger.Error("Add each missing variable to the registry with scope: native (or scope: managed, native if managed code reads it too).");
        }

        if (missingNativeScope.Count > 0)
        {
            hasErrors = true;
            Logger.Error(
                "The following DD_* environment variables are read by native code but their registry entry does not include 'native' in its scope:{Break}{Vars}",
                Environment.NewLine,
                string.Join(Environment.NewLine, missingNativeScope.Select(v => "  - " + v)));
            Logger.Error("Update each entry's scope to 'native' or 'managed, native'.");
        }

        if (nativeOnlyWithConstName.Count > 0)
        {
            hasErrors = true;
            Logger.Error(
                "The following entries are scoped native-only but declare a const_name (which is never used, as no C# constant is generated):{Break}{Vars}",
                Environment.NewLine,
                string.Join(Environment.NewLine, nativeOnlyWithConstName.Select(v => "  - " + v)));
        }

        if (hasErrors)
        {
            throw new Exception(
                "Native configuration validation failed. See the errors above. " +
                "If a flagged literal is not an environment variable (e.g. a named pipe), add it to the allowlist in " + nameof(NativeConfigValidator) + ".");
        }

        Logger.Information("Native configuration validation passed: all {Count} native DD_* variables are registered with native scope", nativeVars.Count);
    }

    private static Dictionary<string, RegistryEntry> ParseRegistry(AbsolutePath supportedConfigurationsPath)
    {
        var stream = new YamlStream();
        using (var reader = new StreamReader(supportedConfigurationsPath))
        {
            stream.Load(reader);
        }

        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        if (!root.Children.TryGetValue(new YamlScalarNode("supportedConfigurations"), out var supportedNode))
        {
            throw new Exception($"'supportedConfigurations' section not found in {supportedConfigurationsPath.Name}");
        }

        var result = new Dictionary<string, RegistryEntry>(StringComparer.Ordinal);
        foreach (var kvp in ((YamlMappingNode)supportedNode).Children)
        {
            var name = ((YamlScalarNode)kvp.Key).Value;
            if (name is null)
            {
                continue;
            }

            var scope = new List<string>();
            string constName = null;
            var aliases = new List<string>();

            // Each key maps to a sequence of implementation blocks (usually one).
            foreach (var implNode in (YamlSequenceNode)kvp.Value)
            {
                foreach (var prop in ((YamlMappingNode)implNode).Children)
                {
                    switch (((YamlScalarNode)prop.Key).Value)
                    {
                        case "scope":
                            // Comma-separated scalar (managed, native) or a YAML sequence.
                            if (prop.Value is YamlScalarNode scopeScalar && scopeScalar.Value is not null)
                            {
                                scope.AddRange(scopeScalar.Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
                            }
                            else if (prop.Value is YamlSequenceNode scopeSeq)
                            {
                                scope.AddRange(scopeSeq.OfType<YamlScalarNode>().Select(n => n.Value?.Trim()).Where(s => !string.IsNullOrEmpty(s)));
                            }

                            break;
                        case "const_name":
                            constName = (prop.Value as YamlScalarNode)?.Value;
                            break;
                        case "aliases":
                            if (prop.Value is YamlSequenceNode aliasSeq)
                            {
                                aliases.AddRange(aliasSeq.OfType<YamlScalarNode>().Select(n => n.Value).Where(s => !string.IsNullOrEmpty(s)));
                            }

                            break;
                    }
                }
            }

            result[name] = new RegistryEntry(name, scope, constName, aliases);
        }

        return result;
    }

    private static HashSet<string> ScanNativeEnvVars(AbsolutePath rootDirectory)
    {
        var vars = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in NativeSourceRoots)
        {
            var rootPath = rootDirectory / root;
            if (!Directory.Exists(rootPath))
            {
                throw new Exception($"Native source root not found: {rootPath}. Update {nameof(NativeSourceRoots)} in {nameof(NativeConfigValidator)} if the directory was moved.");
            }

            foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (ext is not (".h" or ".hpp" or ".cpp" or ".cc" or ".cxx" or ".c"))
                {
                    continue;
                }

                var normalized = file.Replace('\\', '/');
                if (ExcludedPathFragments.Any(fragment => normalized.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    continue;
                }

                if (ExcludedFiles.Contains(Path.GetFileName(file)))
                {
                    continue;
                }

                foreach (Match match in EnvVarLiteralPattern.Matches(File.ReadAllText(file)))
                {
                    var name = match.Groups[1].Value;
                    if (!AllowedNonConfigLiterals.Contains(name))
                    {
                        vars.Add(name);
                    }
                }
            }
        }

        return vars;
    }

    private sealed class RegistryEntry
    {
        public RegistryEntry(string name, List<string> scope, string constName, List<string> aliases)
        {
            Name = name;
            Scope = scope;
            ConstName = constName;
            Aliases = aliases;
        }

        public string Name { get; }

        public List<string> Scope { get; }

        public string ConstName { get; }

        public List<string> Aliases { get; }
    }
}
