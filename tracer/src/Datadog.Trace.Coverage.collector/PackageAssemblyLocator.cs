// <copyright file="PackageAssemblyLocator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Coverage.Collector;

/// <summary>
/// Finds the package assemblies that the <c>*.deps.json</c> files of an output directory declare as runtime assets.
/// </summary>
/// <remarks>
/// A test output directory doesn't always contain every assembly its binaries reference: a package asset can stay in a
/// package folder that the .NET host probes (the <c>additionalProbingPaths</c> of the runtimeconfig files), or only be
/// needed for metadata, such as an enum used in a constant or an optional parameter value. Cecil must resolve those enums
/// to write the rewritten assembly, so, like coverlet, the collector also looks in the NuGet global packages folder.
/// </remarks>
internal static class PackageAssemblyLocator
{
    private static readonly Lazy<string[]> HostRuntimeIdentifiers = new(() => GetHostRuntimeIdentifiers().Distinct(StringComparer.Ordinal).ToArray());
    private static readonly ConcurrentDictionary<string, Lazy<Dictionary<string, string[]>>> CachedAssetPaths = new(FilePathComparison.Comparer);

    /// <summary>
    /// Gets the existing package files that can provide an assembly to the binaries of an output directory.
    /// </summary>
    /// <param name="outputDirectory">The directory with the target assembly and its dependency manifests.</param>
    /// <param name="assemblyName">The simple name of the requested assembly.</param>
    /// <param name="logger">Logs a failure to discover the package assemblies of the directory.</param>
    /// <returns>The candidate paths, in probing order. The array is cached and shared, so callers must not modify it.</returns>
    internal static string[] GetCandidatePaths(string outputDirectory, string assemblyName, ICollectorLogger logger)
    {
        if (StringUtil.IsNullOrEmpty(outputDirectory) || StringUtil.IsNullOrEmpty(assemblyName))
        {
            return [];
        }

        outputDirectory = Path.GetFullPath(outputDirectory);
        var assetPaths = CachedAssetPaths.GetOrAdd(
                                             outputDirectory,
                                             directory => new Lazy<Dictionary<string, string[]>>(
                                                 () => DiscoverAssetPathsOrEmpty(directory, logger),
                                                 LazyThreadSafetyMode.ExecutionAndPublication))
                                         .Value;
        return assetPaths.TryGetValue(assemblyName, out var paths) ? paths : [];
    }

    /// <summary>
    /// Discovers the package assemblies of a directory without throwing. The result is cached for the process lifetime, and
    /// a cached exception would break every later resolution from the directory, including the fallbacks after package probing.
    /// </summary>
    private static Dictionary<string, string[]> DiscoverAssetPathsOrEmpty(string outputDirectory, ICollectorLogger logger)
    {
        try
        {
            return DiscoverAssetPaths(outputDirectory, GetPackageRoots(outputDirectory, Environment.GetEnvironmentVariable, GetUserProfileDirectory()));
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Failed to discover the package assemblies declared by the dependency manifests in '{outputDirectory}'. Package folders won't be probed for this directory.");
            return new Dictionary<string, string[]>();
        }
    }

    /// <summary>
    /// Maps each package assembly declared by the output directory's dependency manifests to its existing package files.
    /// </summary>
    private static Dictionary<string, string[]> DiscoverAssetPaths(string outputDirectory, IReadOnlyList<string> packageRoots)
    {
        var candidates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var depsJsonPath in EnumerateFiles(outputDirectory, "*.deps.json"))
        {
            foreach (var assetPath in ReadPackageRuntimeAssets(depsJsonPath))
            {
                var assemblyName = Path.GetFileNameWithoutExtension(assetPath);
                foreach (var packageRoot in packageRoots)
                {
                    // File.Exists also skips a directory that has the asset's name.
                    var path = Path.Combine(packageRoot, assetPath);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    if (!candidates.TryGetValue(assemblyName, out var paths))
                    {
                        paths = [];
                        candidates[assemblyName] = paths;
                    }

                    if (!paths.Contains(path, FilePathComparison.Comparer))
                    {
                        paths.Add(path);
                    }
                }
            }
        }

        return candidates.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the package folders to probe: the runtimeconfig probing paths used by the host, then the NuGet global packages folder.
    /// </summary>
    /// <param name="outputDirectory">The directory with the runtimeconfig files.</param>
    /// <param name="getEnvironmentVariable">Reads an environment variable.</param>
    /// <param name="userProfileDirectory">The user profile directory that contains the default NuGet global packages folder.</param>
    /// <returns>The existing package folders, in probing order.</returns>
    internal static string[] GetPackageRoots(string outputDirectory, Func<string, string?> getEnvironmentVariable, string? userProfileDirectory)
    {
        var roots = new List<string>();
        foreach (var runtimeConfigPath in EnumerateFiles(outputDirectory, "*.runtimeconfig.json").Concat(EnumerateFiles(outputDirectory, "*.runtimeconfig.dev.json")))
        {
            foreach (var probingPath in ReadAdditionalProbingPaths(runtimeConfigPath))
            {
                AddPackageRoot(probingPath, roots);
            }
        }

        // NuGet uses NUGET_PACKAGES as its global packages folder when it is set, and a folder in the user profile otherwise.
        if (getEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } nugetPackages)
        {
            AddPackageRoot(nugetPackages, roots);
        }
        else if (!StringUtil.IsNullOrEmpty(userProfileDirectory))
        {
            AddPackageRoot(Path.Combine(userProfileDirectory, ".nuget", "packages"), roots);
        }

        return roots.ToArray();
    }

    private static string? GetUserProfileDirectory()
    {
        try
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static void AddPackageRoot(string packageRoot, List<string> roots)
    {
        try
        {
            packageRoot = Path.GetFullPath(packageRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return;
        }

        if (Directory.Exists(packageRoot) && !roots.Contains(packageRoot, FilePathComparison.Comparer))
        {
            roots.Add(packageRoot);
        }
    }

    private static List<string> ReadAdditionalProbingPaths(string runtimeConfigPath)
    {
        var probingPaths = new List<string>();
        if (TryReadJson(runtimeConfigPath)?["runtimeOptions"] is JObject runtimeOptions &&
            runtimeOptions["additionalProbingPaths"] is JArray paths)
        {
            foreach (var path in paths)
            {
                if (GetString(path) is { Length: > 0 } probingPath)
                {
                    probingPaths.Add(probingPath);
                }
            }
        }

        return probingPaths;
    }

    private static List<string> ReadPackageRuntimeAssets(string depsJsonPath)
    {
        var assetPaths = new List<string>();
        if (TryReadJson(depsJsonPath) is not { } depsJson ||
            depsJson["libraries"] is not JObject libraries ||
            GetRuntimeTarget(depsJson) is not { } target)
        {
            return assetPaths;
        }

        foreach (var targetLibrary in target.Properties())
        {
            if (targetLibrary.Value is not JObject targetLibraryValue ||
                libraries[targetLibrary.Name] is not JObject library ||
                GetString(library["type"]) != "package")
            {
                continue;
            }

            // Like the host, combine the library path (or its lowercase "name/version" key) with each asset path.
            var libraryPath = GetString(library["path"]) is { Length: > 0 } path ? path : targetLibrary.Name.ToLowerInvariant();
            foreach (var asset in GetManagedRuntimeAssets(targetLibraryValue))
            {
                if (IsRelativePathInside(libraryPath) &&
                    IsRelativePathInside(asset.Name) &&
                    string.Equals(Path.GetExtension(asset.Name), ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    // deps.json paths always use '/', so switch to the platform separator to keep candidate paths canonical.
                    assetPaths.Add(Path.Combine(libraryPath, asset.Name).Replace('/', Path.DirectorySeparatorChar));
                }
            }
        }

        return assetPaths;
    }

    private static IEnumerable<JProperty> GetManagedRuntimeAssets(JObject library)
    {
        if (library["runtimeTargets"] is JObject runtimeTargets)
        {
            var groups = runtimeTargets.Properties()
                                       .Where(asset => asset.Value is JObject metadata && GetString(metadata["assetType"]) == "runtime")
                                       .ToLookup(asset => GetString(asset.Value["rid"]));
            // The host selects one managed asset group for the entire package, including an empty group marked by _._.
            var match = groups.Count > 0 ? HostRuntimeIdentifiers.Value.FirstOrDefault(groups.Contains) : null;
            if (match is not null)
            {
                return groups[match];
            }
        }

        return (library["runtime"] as JObject)?.Properties() ?? [];
    }

    private static IEnumerable<string> GetHostRuntimeIdentifiers()
    {
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var linux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var platform = windows ? "win" : linux ? "linux" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : null;
        var hostRid = AppContext.GetData("RUNTIME_IDENTIFIER") as string;

        if (hostRid is { Length: > 0 })
        {
            yield return hostRid;
        }

        var framework = AppContext.GetData("FX_DEPS_FILE") is string { Length: > 0 } depsFile ? TryReadJson(depsFile) : null;
        var targetName = GetString((framework?["runtimeTarget"] as JObject)?["name"]);
        var buildRid = targetName?.Split('/').LastOrDefault() ?? hostRid;

        // Older hosts use the expanded RID fallback list shipped with their shared framework.
        var useGraph = Environment.Version.Major < 8 || (AppContext.TryGetSwitch("System.Runtime.Loader.UseRidGraph", out var useRidGraph) && useRidGraph);
        var graphRid = hostRid ?? buildRid;
        if (useGraph && graphRid is not null && (framework?["runtimes"] as JObject)?[graphRid] is JArray fallbacks)
        {
            yield return graphRid;
            foreach (var fallback in fallbacks)
            {
                if (GetString(fallback) is { Length: > 0 } rid)
                {
                    yield return rid;
                }
            }

            yield break;
        }

        // Portable hosts probe their build RID (even with an explicit RID override), then the OS and Unix fallbacks.
        if (buildRid is { Length: > 0 })
        {
            yield return buildRid;
            var suffix = "-" + architecture;
            if (buildRid.EndsWith(suffix, StringComparison.Ordinal))
            {
                yield return buildRid.Substring(0, buildRid.Length - suffix.Length);
            }

            if (linux && buildRid.StartsWith("alpine", StringComparison.Ordinal))
            {
                yield return $"linux-musl-{architecture}";
                yield return "linux-musl";
            }
        }

        if (platform is not null)
        {
            yield return $"{platform}-{architecture}";
            yield return platform;
        }

        if (!windows)
        {
            yield return $"unix-{architecture}";
            yield return "unix";
        }

        yield return "any";
    }

    private static JObject? GetRuntimeTarget(JObject depsJson)
    {
        if (depsJson["targets"] is not JObject targets)
        {
            return null;
        }

        if (depsJson["runtimeTarget"] is JObject runtimeTarget && GetString(runtimeTarget["name"]) is { Length: > 0 } runtimeTargetName)
        {
            return targets[runtimeTargetName] as JObject;
        }

        return targets.Properties().FirstOrDefault(static target => target.Value is JObject)?.Value as JObject;
    }

    private static string? GetString(JToken? token)
        => token?.Type == JTokenType.String ? token.Value<string>() : null;

    private static bool IsRelativePathInside(string path)
        => path.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
           !Path.IsPathRooted(path) &&
           !path.Split('/', '\\').Contains("..");

    private static JObject? TryReadJson(string path)
    {
        try
        {
            return JObject.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Datadog.Trace.Vendors.Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }

    private static List<string> EnumerateFiles(string directory, string searchPattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)
                            .OrderBy(path => path, FilePathComparison.Comparer)
                            .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
