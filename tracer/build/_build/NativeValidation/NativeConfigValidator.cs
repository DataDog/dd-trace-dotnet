// <copyright file="NativeConfigValidator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.SourceGenerators.Helpers;
using Nuke.Common.IO;
using Logger = Serilog.Log;

namespace NativeValidation;

/// <summary>
/// Validates that every DD_* / _DD_* environment variable read by native C++ code
/// has a corresponding entry in supported-configurations.yaml with "native" in its scope.
///
/// This runs as a Nuke step (rather than a managed unit test) so that developers working
/// only in the native solutions surface coverage gaps locally, not just in CI.
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
        var parsed = YamlReader.ParseSupportedConfigurations(File.ReadAllText(supportedConfigurationsPath));

        // var name (key or alias) -> entry. Keys are assigned unconditionally so a primary
        // key always wins over an alias of the same name regardless of iteration order;
        // aliases are only added when the name is not already taken.
        var entryByName = new Dictionary<string, YamlReader.ConfigurationEntry>(StringComparer.Ordinal);
        foreach (var kvp in parsed.Configurations)
        {
            entryByName[kvp.Key] = kvp.Value;
            foreach (var alias in kvp.Value.Aliases ?? Enumerable.Empty<string>())
            {
                if (!entryByName.ContainsKey(alias))
                {
                    entryByName[alias] = kvp.Value;
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

            var hasNativeScope = entry.Scope is not null &&
                                 entry.Scope.Any(s => string.Equals(s, "native", StringComparison.OrdinalIgnoreCase));
            if (!hasNativeScope)
            {
                missingNativeScope.Add(name);
            }
        }

        // Native-only entries must not declare a const_name — no C# constant is generated for them.
        var nativeOnlyWithConstName = parsed.Configurations.Values
            .Where(e => e.Scope is not null
                     && e.Scope.Any(s => string.Equals(s, "native", StringComparison.OrdinalIgnoreCase))
                     && !e.Scope.Any(s => string.Equals(s, "managed", StringComparison.OrdinalIgnoreCase))
                     && !string.IsNullOrEmpty(e.ConstName))
            .Select(e => e.Key)
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
}
