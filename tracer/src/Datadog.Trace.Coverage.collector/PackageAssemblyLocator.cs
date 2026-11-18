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
    private static readonly StringComparer PathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly ConcurrentDictionary<string, Lazy<Dictionary<string, string[]>>> CachedAssetPaths = new(PathComparer);

    /// <summary>
    /// Gets the existing package files that can provide an assembly to the binaries of an output directory.
    /// </summary>
    /// <param name="outputDirectory">The directory with the target assembly and its dependency manifests.</param>
    /// <param name="assemblyName">The simple name of the requested assembly.</param>
    /// <returns>The candidate paths, in probing order.</returns>
    internal static IEnumerable<string> GetCandidatePaths(string outputDirectory, string assemblyName)
    {
        if (StringUtil.IsNullOrEmpty(outputDirectory) || StringUtil.IsNullOrEmpty(assemblyName))
        {
            return [];
        }

        outputDirectory = Path.GetFullPath(outputDirectory);
        var assetPaths = CachedAssetPaths.GetOrAdd(
                                             outputDirectory,
                                             directory => new Lazy<Dictionary<string, string[]>>(
                                                 () => DiscoverAssetPaths(directory, GetPackageRoots(directory, Environment.GetEnvironmentVariable, GetUserProfileDirectory())),
                                                 LazyThreadSafetyMode.ExecutionAndPublication))
                                         .Value;
        return assetPaths.TryGetValue(assemblyName, out var paths) ? paths.Where(File.Exists) : [];
    }

    /// <summary>
    /// Maps each package assembly declared by the output directory's dependency manifests to its possible package paths.
    /// </summary>
    private static Dictionary<string, string[]> DiscoverAssetPaths(string outputDirectory, IReadOnlyList<string> packageRoots)
    {
        var candidates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var depsJsonPath in EnumerateFiles(outputDirectory, "*.deps.json"))
        {
            foreach (var assetPath in ReadPackageRuntimeAssets(depsJsonPath))
            {
                var assemblyName = Path.GetFileNameWithoutExtension(assetPath);
                if (!candidates.TryGetValue(assemblyName, out var paths))
                {
                    paths = [];
                    candidates[assemblyName] = paths;
                }

                foreach (var packageRoot in packageRoots)
                {
                    var path = Path.Combine(packageRoot, assetPath);
                    if (!paths.Contains(path, PathComparer))
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

        if (Directory.Exists(packageRoot) && !roots.Contains(packageRoot, PathComparer))
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
                targetLibraryValue["runtime"] is not JObject runtimeAssets ||
                libraries[targetLibrary.Name] is not JObject library ||
                GetString(library["type"]) != "package")
            {
                continue;
            }

            // Like the host, combine the library path (or its lowercase "name/version" key) with each asset path.
            var libraryPath = GetString(library["path"]) is { Length: > 0 } path ? path : targetLibrary.Name.ToLowerInvariant();
            foreach (var asset in runtimeAssets.Properties())
            {
                if (IsRelativePathInside(libraryPath) &&
                    IsRelativePathInside(asset.Name) &&
                    string.Equals(Path.GetExtension(asset.Name), ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    assetPaths.Add(Path.Combine(libraryPath, asset.Name));
                }
            }
        }

        return assetPaths;
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

        return targets.Properties().Select(target => target.Value).OfType<JObject>().FirstOrDefault();
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

    private static IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)
                            .OrderBy(path => path, PathComparer)
                            .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
