// <copyright file="NativeEnvVarCoverageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.SourceGenerators.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests;

/// <summary>
/// Verifies that every DD_* environment variable declared in the tracer's native C++ headers
/// (those using WStr("DD_...")) has a corresponding entry in supported-configurations.yaml.
///
/// DD_INTERNAL_* variables are intentionally excluded — they are undocumented internal variables
/// that are not expected to be registered.
///
/// Note: the profiler (profiler/src/) is out of scope — it maintains its own configuration model
/// separate from supported-configurations.yaml.
/// </summary>
public class NativeEnvVarCoverageTests
{
    // Root directories to scan for native .h files (relative to repo root).
    // The profiler directory is excluded — it has a separate configuration model.
    private static readonly string[] NativeSourceRoots =
    [
        "tracer/src/Datadog.Tracer.Native",
        "shared/src",
    ];

    // Path segments that indicate a file is a test or build artifact and should be excluded.
    private static readonly string[] ExcludedPathSegments =
    [
        Path.DirectorySeparatorChar + "test" + Path.DirectorySeparatorChar,
        Path.DirectorySeparatorChar + "tests" + Path.DirectorySeparatorChar,
        Path.DirectorySeparatorChar + "_build" + Path.DirectorySeparatorChar,
        Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
        Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
    ];

    private static readonly Regex WStrPattern = new(@"WStr\(""(DD_[A-Z][A-Z0-9_]+)""\)", RegexOptions.Compiled);

    [Fact]
    public void AllNativeEnvVarsAreCoveredByRegistry()
    {
        var repoRoot = FindRepoRoot();
        var yamlPath = Path.Combine(repoRoot, "tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml");
        var yamlContent = File.ReadAllText(yamlPath);
        var parsed = YamlReader.ParseSupportedConfigurations(yamlContent);

        var nativeVars = GetNativeEnvVars(repoRoot);

        // Build a lookup: var name (key or alias) -> entry
        var keyByName = new Dictionary<string, YamlReader.ConfigurationEntry>(StringComparer.Ordinal);
        foreach (var kvp in parsed.Configurations)
        {
            keyByName[kvp.Key] = kvp.Value;
            if (kvp.Value.Aliases is not null)
            {
                foreach (var alias in kvp.Value.Aliases)
                {
                    keyByName[alias] = kvp.Value;
                }
            }
        }

        var missingFromRegistry = new List<string>();
        var missingScopeNative = new List<string>();

        foreach (var v in nativeVars.OrderBy(x => x))
        {
            if (!keyByName.TryGetValue(v, out var entry))
            {
                missingFromRegistry.Add(v);
                continue;
            }

            var hasNativeScope = entry.Scope is not null &&
                                 entry.Scope.Any(s => string.Equals(s, "native", StringComparison.OrdinalIgnoreCase));
            if (!hasNativeScope)
            {
                missingScopeNative.Add(v);
            }
        }

        using (new AssertionScope())
        {
            missingFromRegistry.Should().BeEmpty(
                because: "every DD_* variable in native env var headers must have an entry in supported-configurations.yaml. " +
                         $"Missing: {string.Join(", ", missingFromRegistry)}");

            missingScopeNative.Should().BeEmpty(
                because: "every DD_* variable in native env var headers must have 'native' in its scope array. " +
                         $"Add or update scope: [native] or scope: [managed, native] for: {string.Join(", ", missingScopeNative)}");
        }
    }

    [Fact]
    public void NativeOnlyEnvVarsDoNotGenerateCSharpConstants()
    {
        var repoRoot = FindRepoRoot();
        var yamlPath = Path.Combine(repoRoot, "tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml");
        var yamlContent = File.ReadAllText(yamlPath);

        var parsed = YamlReader.ParseSupportedConfigurations(yamlContent);

        var nativeOnlyWithConstName = parsed.Configurations.Values
            .Where(e => e.Scope is not null &&
                        e.Scope.Any(s => string.Equals(s, "native", StringComparison.OrdinalIgnoreCase)) &&
                        !e.Scope.Any(s => string.Equals(s, "managed", StringComparison.OrdinalIgnoreCase)) &&
                        !string.IsNullOrEmpty(e.ConstName))
            .Select(e => e.Key)
            .OrderBy(k => k)
            .ToList();

        nativeOnlyWithConstName.Should().BeEmpty(
            because: "scope:[native]-only entries should not have a const_name — they are not read by managed code. " +
                     $"Offending keys: {string.Join(", ", nativeOnlyWithConstName)}");
    }

    private static List<string> GetNativeEnvVars(string repoRoot)
    {
        var vars = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in NativeSourceRoots)
        {
            var rootPath = Path.Combine(repoRoot, root);
            if (!Directory.Exists(rootPath))
            {
                throw new DirectoryNotFoundException(
                    $"Native source root not found: {rootPath}. " +
                    $"Update {nameof(NativeSourceRoots)} in {nameof(NativeEnvVarCoverageTests)} if the directory was moved.");
            }

            foreach (var headerFile in Directory.EnumerateFiles(rootPath, "*.h", SearchOption.AllDirectories))
            {
                if (IsExcluded(headerFile))
                {
                    continue;
                }

                var content = File.ReadAllText(headerFile);
                foreach (var match in WStrPattern.Matches(content).Cast<Match>())
                {
                    var varName = match.Groups[1].Value;

                    // DD_INTERNAL_* are intentionally undocumented internal variables.
                    if (varName.StartsWith("DD_INTERNAL_", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    vars.Add(varName);
                }
            }
        }

        return [.. vars];
    }

    private static bool IsExcluded(string path)
    {
        foreach (var segment in ExcludedPathSegments)
        {
            if (path.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string FindRepoRoot()
    {
        var start = AppContext.BaseDirectory ?? throw new InvalidOperationException("AppContext.BaseDirectory is null");
        var current = new DirectoryInfo(start);

        while (current is not null)
        {
            // The repo root contains both AGENTS.md and the tracer subdirectory.
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "tracer")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find repository root. Started search at: {start}");
    }
}
