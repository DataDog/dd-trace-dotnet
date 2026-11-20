// <copyright file="SharedFrameworkLocator.cs" company="Datadog">
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
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Coverage.Collector;

internal static class SharedFrameworkLocator
{
    private static readonly StringComparer PathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly string[] DotnetRootEnvironmentVariables = ["DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86", "DOTNET_ROOT_ARM64", "DOTNET_ROOT(x86)"];
    private static readonly ConcurrentDictionary<string, Lazy<string[]>> CachedDirectories = new(PathComparer);

    internal static IEnumerable<string> GetDirectories(string outputDirectory, string? sharedFrameworkRoot = null)
    {
        if (string.IsNullOrEmpty(outputDirectory))
        {
            return [];
        }

        var roots = (sharedFrameworkRoot is null ? GetSharedFrameworkRoots() : [sharedFrameworkRoot])
                   .Where(Directory.Exists)
                   .Select(Path.GetFullPath)
                   .Distinct(PathComparer)
                   .ToArray();
        if (roots.Length == 0)
        {
            return [];
        }

        outputDirectory = Path.GetFullPath(outputDirectory);
        var rollForwardToPrerelease = RollForwardToPrerelease();
        var cacheKey = outputDirectory + "\0" + string.Join("\0", roots) + "\0" + (rollForwardToPrerelease ? "1" : "0");
        return CachedDirectories.GetOrAdd(
                                    cacheKey,
                                    _ => new Lazy<string[]>(
                                        () => DiscoverDirectories(outputDirectory, roots, rollForwardToPrerelease),
                                        LazyThreadSafetyMode.ExecutionAndPublication))
                                .Value;
    }

    internal static string[] DiscoverDirectories(string outputDirectory, IEnumerable<string> sharedFrameworkRoots, bool rollForwardToPrerelease)
    {
        var directories = new List<string>();
        var seenDirectories = new HashSet<string>(PathComparer);
        foreach (var runtimeConfigPath in GetRuntimeConfigPaths(outputDirectory))
        {
            foreach (var framework in ReadFrameworkReferences(runtimeConfigPath))
            {
                foreach (var root in sharedFrameworkRoots)
                {
                    var directory = FindCompatibleFrameworkDirectory(root, framework.Name, framework.Version, rollForwardToPrerelease);
                    if (directory is not null && seenDirectories.Add(directory))
                    {
                        directories.Add(directory);
                    }
                }
            }
        }

        return directories.ToArray();
    }

    internal static string[] GetSharedFrameworkRoots()
        => GetSharedFrameworkRoots(typeof(object).Assembly.Location, Environment.GetEnvironmentVariable);

    internal static string[] GetSharedFrameworkRoots(string coreLibraryPath, Func<string, string?> getEnvironmentVariable)
    {
        var roots = new List<string>();
        var seenRoots = new HashSet<string>(PathComparer);

        var dotnetHostPath = getEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(dotnetHostPath))
        {
            AddDotnetInstallRoot(Path.GetDirectoryName(dotnetHostPath), roots, seenRoots);
        }

        foreach (var variableName in DotnetRootEnvironmentVariables)
        {
            AddDotnetInstallRoot(getEnvironmentVariable(variableName), roots, seenRoots);
        }

        if (string.Equals(Path.GetFileName(coreLibraryPath), "System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase))
        {
            var versionDirectory = Path.GetDirectoryName(coreLibraryPath);
            var frameworkDirectory = versionDirectory is null ? null : Path.GetDirectoryName(versionDirectory);
            var sharedFrameworkRoot = frameworkDirectory is null ? null : Path.GetDirectoryName(frameworkDirectory);
            AddSharedFrameworkRoot(sharedFrameworkRoot, roots, seenRoots);
        }

        if (getEnvironmentVariable("PATH") is { Length: > 0 } path)
        {
            foreach (var pathEntry in path.Split(Path.PathSeparator))
            {
                AddDotnetInstallRoot(pathEntry, roots, seenRoots);
            }
        }

        return roots.ToArray();
    }

    private static bool RollForwardToPrerelease()
        => string.Equals(Environment.GetEnvironmentVariable("DOTNET_ROLL_FORWARD_TO_PRERELEASE"), "1", StringComparison.Ordinal);

    private static void AddDotnetInstallRoot(string? dotnetInstallRoot, List<string> roots, HashSet<string> seenRoots)
    {
        try
        {
            if (!string.IsNullOrEmpty(dotnetInstallRoot))
            {
                AddSharedFrameworkRoot(Path.Combine(dotnetInstallRoot, "shared"), roots, seenRoots);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            // Environment variables can contain invalid or inaccessible paths. Try the other roots.
        }
    }

    private static void AddSharedFrameworkRoot(string? sharedFrameworkRoot, List<string> roots, HashSet<string> seenRoots)
    {
        if (string.IsNullOrEmpty(sharedFrameworkRoot) ||
            !string.Equals(Path.GetFileName(sharedFrameworkRoot), "shared", StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(sharedFrameworkRoot))
        {
            return;
        }

        sharedFrameworkRoot = Path.GetFullPath(sharedFrameworkRoot);
        if (seenRoots.Add(sharedFrameworkRoot))
        {
            roots.Add(sharedFrameworkRoot);
        }
    }

    private static IEnumerable<string> GetRuntimeConfigPaths(string outputDirectory)
    {
        try
        {
            return Directory.EnumerateFiles(outputDirectory, "*.runtimeconfig.json", SearchOption.TopDirectoryOnly)
                            .OrderBy(path => path, PathComparer)
                            .ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<FrameworkReference> ReadFrameworkReferences(string runtimeConfigPath)
    {
        JObject runtimeConfig;
        try
        {
            runtimeConfig = JObject.Parse(File.ReadAllText(runtimeConfigPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Datadog.Trace.Vendors.Newtonsoft.Json.JsonException)
        {
            yield break;
        }

        if (runtimeConfig["runtimeOptions"] is not JObject runtimeOptions)
        {
            yield break;
        }

        if (TryReadFrameworkReference(runtimeOptions["framework"], out var framework))
        {
            yield return framework;
        }

        foreach (var propertyName in new[] { "frameworks", "includedFrameworks" })
        {
            if (runtimeOptions[propertyName] is not JArray frameworks)
            {
                continue;
            }

            foreach (var frameworkToken in frameworks)
            {
                if (TryReadFrameworkReference(frameworkToken, out framework))
                {
                    yield return framework;
                }
            }
        }
    }

    private static bool TryReadFrameworkReference(JToken? token, out FrameworkReference framework)
    {
        framework = default;
        if (token is not JObject frameworkObject ||
            frameworkObject["name"]?.Value<string>() is not { Length: > 0 } name ||
            frameworkObject["version"]?.Value<string>() is not { Length: > 0 } version ||
            Path.IsPathRooted(name) ||
            name is "." or ".." ||
            name.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            name.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            return false;
        }

        framework = new FrameworkReference(name, version);
        return true;
    }

    private static string? FindCompatibleFrameworkDirectory(string sharedFrameworkRoot, string frameworkName, string requestedVersionText, bool rollForwardToPrerelease)
    {
        if (!RuntimeVersion.TryParse(requestedVersionText, out var requestedVersion))
        {
            return null;
        }

        var frameworkRoot = Path.Combine(sharedFrameworkRoot, frameworkName);
        if (!Directory.Exists(frameworkRoot))
        {
            return null;
        }

        try
        {
            var preferStable = !requestedVersion.IsPrerelease && !rollForwardToPrerelease;
            return Directory.EnumerateDirectories(frameworkRoot)
                            .Select(path => new
                            {
                                Path = path,
                                Parsed = RuntimeVersion.TryParse(Path.GetFileName(path), out var version),
                                Version = version
                            })
                            .Where(candidate => candidate.Parsed &&
                                                candidate.Version.Major == requestedVersion.Major &&
                                                candidate.Version.Minor == requestedVersion.Minor &&
                                                candidate.Version.CompareTo(requestedVersion) >= 0)
                            .OrderBy(candidate => preferStable && candidate.Version.IsPrerelease)
                            .ThenByDescending(candidate => candidate.Version)
                            .Select(candidate => candidate.Path)
                            .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private readonly struct FrameworkReference
    {
        public FrameworkReference(string name, string version)
        {
            Name = name;
            Version = version;
        }

        public string Name { get; }

        public string Version { get; }
    }

    private readonly struct RuntimeVersion : IComparable<RuntimeVersion>
    {
        private readonly Version _version;
        private readonly string[] _prerelease;

        private RuntimeVersion(Version version, string[] prerelease)
        {
            _version = version;
            _prerelease = prerelease;
        }

        public int Major => _version.Major;

        public int Minor => _version.Minor;

        public bool IsPrerelease => _prerelease.Length > 0;

        public static bool TryParse(string value, out RuntimeVersion runtimeVersion)
        {
            runtimeVersion = default;
            var separator = value.IndexOf('-');
            var versionText = separator < 0 ? value : value.Substring(0, separator);
            if (!Version.TryParse(versionText, out var version))
            {
                return false;
            }

            var prerelease = separator < 0 ? [] : value.Substring(separator + 1).Split('.');
            if (prerelease.Any(identifier => identifier.Length == 0))
            {
                return false;
            }

            runtimeVersion = new RuntimeVersion(version, prerelease);
            return true;
        }

        public int CompareTo(RuntimeVersion other)
        {
            var result = _version.CompareTo(other._version);
            if (result != 0)
            {
                return result;
            }

            if (_prerelease.Length == 0 || other._prerelease.Length == 0)
            {
                return other._prerelease.Length.CompareTo(_prerelease.Length);
            }

            var count = Math.Min(_prerelease.Length, other._prerelease.Length);
            for (var i = 0; i < count; i++)
            {
                result = ComparePrereleaseIdentifier(_prerelease[i], other._prerelease[i]);
                if (result != 0)
                {
                    return result;
                }
            }

            return _prerelease.Length.CompareTo(other._prerelease.Length);
        }

        private static int ComparePrereleaseIdentifier(string left, string right)
        {
            var leftIsNumeric = ulong.TryParse(left, out var leftNumber);
            var rightIsNumeric = ulong.TryParse(right, out var rightNumber);
            if (leftIsNumeric && rightIsNumeric)
            {
                return leftNumber.CompareTo(rightNumber);
            }

            if (leftIsNumeric != rightIsNumeric)
            {
                return leftIsNumeric ? -1 : 1;
            }

            return string.CompareOrdinal(left, right);
        }
    }
}
