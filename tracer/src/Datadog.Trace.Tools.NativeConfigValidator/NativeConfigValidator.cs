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

namespace Datadog.Trace.Tools.NativeConfigValidator;

/// <summary>
/// Validates that every DD_* / _DD_* environment variable read by native C++ code
/// has a corresponding entry in supported-configurations.yaml with "native" in its scope.
///
/// The registry is parsed with the source generator's own <see cref="YamlReader"/>
/// (source-linked into this project) so the validator and the ConfigurationKeys
/// generator can never drift out of sync.
/// </summary>
internal sealed class NativeConfigValidator
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
    /// </summary>
    /// <param name="rootDirectory">The repository root.</param>
    /// <param name="supportedConfigurationsPath">The path to supported-configurations.yaml.</param>
    /// <returns><c>true</c> if every native env var is registered with native scope; otherwise <c>false</c>.</returns>
    public bool Validate(string rootDirectory, string supportedConfigurationsPath)
    {
        var parsed = YamlReader.ParseSupportedConfigurations(File.ReadAllText(supportedConfigurationsPath));

        // var name (key or alias) -> entry. Keys are assigned unconditionally so a primary
        // key always wins over an alias of the same name regardless of iteration order;
        // aliases are only added when the name is not already taken.
        var entryByName = new Dictionary<string, YamlReader.ConfigurationEntry>();
        foreach (var entry in parsed.Configurations.Values)
        {
            entryByName[entry.Key] = entry;
            foreach (var alias in entry.Aliases)
            {
                if (!entryByName.ContainsKey(alias))
                {
                    entryByName[alias] = entry;
                }
            }
        }

        var nativeVars = ScanNativeEnvVars(rootDirectory);
        Console.Out.WriteLine($"Found {nativeVars.Count} distinct DD_* environment variables in native source");

        var missingFromRegistry = new List<string>();
        var missingNativeScope = new List<string>();

        foreach (var name in nativeVars.OrderBy(x => x))
        {
            if (!entryByName.TryGetValue(name, out var entry))
            {
                missingFromRegistry.Add(name);
                continue;
            }

            if (!entry.Scope.AsSpan().Contains("native"))
            {
                missingNativeScope.Add(name);
            }
        }

        var hasErrors = false;

        if (missingFromRegistry.Count > 0)
        {
            hasErrors = true;
            Console.Error.WriteLine($"The following DD_* environment variables are read by native code but are missing from {supportedConfigurationsPath}:");
            foreach (var name in missingFromRegistry)
            {
                Console.Error.WriteLine("  - " + name);
            }

            Console.Error.WriteLine("Add each missing variable to that file with scope: native (or scope: managed, native if managed code reads it too).");
        }

        if (missingNativeScope.Count > 0)
        {
            hasErrors = true;
            Console.Error.WriteLine("The following DD_* environment variables are read by native code but their registry entry does not include 'native' in its scope:");
            foreach (var name in missingNativeScope)
            {
                Console.Error.WriteLine("  - " + name);
            }

            Console.Error.WriteLine($"Update each entry's scope to 'native' or 'managed, native' in {supportedConfigurationsPath}.");
        }

        if (hasErrors)
        {
            Console.Error.WriteLine(
                "Native configuration validation failed. See the errors above. " +
                "If a flagged literal is not an environment variable (e.g. a named pipe), add it to the allowlist in " + nameof(NativeConfigValidator) + ".");
            return false;
        }

        Console.Out.WriteLine($"Native configuration validation passed: all {nativeVars.Count} native DD_* variables are registered with native scope");
        return true;
    }

    private static HashSet<string> ScanNativeEnvVars(string rootDirectory)
    {
        var vars = new HashSet<string>();

        foreach (var root in NativeSourceRoots)
        {
            var rootPath = Path.Combine(rootDirectory, root.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(rootPath))
            {
                throw new DirectoryNotFoundException($"Native source root not found: {rootPath}. Update {nameof(NativeSourceRoots)} in {nameof(NativeConfigValidator)} if the directory was moved.");
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
