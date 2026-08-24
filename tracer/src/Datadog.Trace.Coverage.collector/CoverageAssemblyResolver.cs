// <copyright file="CoverageAssemblyResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Mono.Cecil;

namespace Datadog.Trace.Coverage.Collector;

/// <summary>
/// Resolves coverage rewrite dependencies without keeping file handles open on resolved assemblies.
/// </summary>
internal sealed class CoverageAssemblyResolver : BaseAssemblyResolver
{
    private static readonly Assembly TracerAssembly = typeof(CoverageReporter).Assembly;
    private static readonly StringComparison PathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly StringComparer PathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly string[] ManagedAssemblyExtensions = [".exe", ".dll"];
    private static readonly string[] WindowsRuntimeAssemblyExtensions = [".winmd", ".dll"];
    private static readonly ConcurrentDictionary<string, Lazy<string[]>> SharedFrameworkDirectories = new(PathComparer);
    private readonly Dictionary<string, AssemblyDefinition> _cache = new(StringComparer.Ordinal);
    private readonly ICollectorLogger _logger;
    private readonly string _assemblyFilePath;
    private readonly string _preferredSearchDirectory;
    private readonly string? _sharedFrameworkRoot;
    private string _tracerAssemblyLocation;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageAssemblyResolver"/> class.
    /// </summary>
    /// <param name="logger">Logger used to report resolution failures.</param>
    /// <param name="assemblyFilePath">The target assembly currently being rewritten.</param>
    public CoverageAssemblyResolver(ICollectorLogger logger, string assemblyFilePath)
        : this(logger, assemblyFilePath, sharedFrameworkRoot: null)
    {
    }

    internal CoverageAssemblyResolver(ICollectorLogger logger, string assemblyFilePath, string? sharedFrameworkRoot)
    {
        _logger = logger;
        _assemblyFilePath = assemblyFilePath;
        _preferredSearchDirectory = Path.GetDirectoryName(assemblyFilePath) ?? string.Empty;
        _sharedFrameworkRoot = sharedFrameworkRoot;
        _tracerAssemblyLocation = string.Empty;
    }

    /// <inheritdoc />
    public override AssemblyDefinition Resolve(AssemblyNameReference name)
        => Resolve(name, new ReaderParameters());

    /// <inheritdoc />
    public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        ThrowIfDisposed();
        if (_cache.TryGetValue(name.FullName, out var cachedAssembly))
        {
            return cachedAssembly;
        }

        try
        {
            return ResolveAndCache(name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"{nameof(CoverageAssemblyResolver)} failed to resolve dependency '{name.FullName}' while processing target assembly '{_assemblyFilePath}'.");
            throw;
        }
    }

    /// <summary>
    /// Sets the Datadog.Trace assembly path that should be preferred for later resolutions.
    /// </summary>
    /// <param name="assemblyLocation">The copied Datadog.Trace assembly path.</param>
    public void SetTracerAssemblyLocation(string assemblyLocation)
    {
        ThrowIfDisposed();
        assemblyLocation ??= string.Empty;
        if (string.Equals(_tracerAssemblyLocation, assemblyLocation, PathComparison))
        {
            return;
        }

        InvalidateTracerCache();
        _tracerAssemblyLocation = assemblyLocation;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            foreach (var assembly in _cache.Values.Distinct())
            {
                assembly.Dispose();
            }

            _cache.Clear();
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    private AssemblyDefinition ResolveAndCache(AssemblyNameReference name)
    {
        var tracerAssemblyName = TracerAssembly.GetName();
        if (IsTracerAssembly(name, tracerAssemblyName) && !string.IsNullOrEmpty(_tracerAssemblyLocation))
        {
            return ReadAndCache(name.FullName, _tracerAssemblyLocation);
        }

        var assemblyFromSearchDirectory = ResolveFromSearchDirectories(name);
        if (assemblyFromSearchDirectory is not null)
        {
            return assemblyFromSearchDirectory;
        }

        var assemblyFromSharedFramework = ResolveFromDirectories(name, GetSharedFrameworkDirectories());
        if (assemblyFromSharedFramework is not null)
        {
            return assemblyFromSharedFramework;
        }

        if (IsTracerAssembly(name, tracerAssemblyName))
        {
            return ReadAndCache(name.FullName, TracerAssembly.Location);
        }

        if (name.Name == "mscorlib")
        {
            var mscorlibPath = Path.Combine(GetMscorlibBasePath(name.Version), "mscorlib.dll");
            if (File.Exists(mscorlibPath))
            {
                return ReadAndCache(name.FullName, mscorlibPath);
            }
        }

        var assembly = ResolveWithoutDirectoryFallback(name);
        return CacheAssembly(name.FullName, assembly);
    }

    private AssemblyDefinition? ResolveFromSearchDirectories(AssemblyNameReference name)
        => ResolveFromDirectories(name, GetSearchDirectoryCandidates());

    private AssemblyDefinition? ResolveFromDirectories(AssemblyNameReference name, IEnumerable<string> directories)
    {
        var extensions = name.IsWindowsRuntime ? WindowsRuntimeAssemblyExtensions : ManagedAssemblyExtensions;
        foreach (var directory in directories)
        {
            foreach (var extension in extensions)
            {
                var path = Path.Combine(directory, name.Name + extension);
                _logger.Debug($"Looking for: {path}");
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    return ReadAndCache(name.FullName, path);
                }
                catch (BadImageFormatException)
                {
                    continue;
                }
            }
        }

        return null;
    }

    private IEnumerable<string> GetSharedFrameworkDirectories()
    {
        if (string.IsNullOrEmpty(_preferredSearchDirectory))
        {
            return [];
        }

        var sharedFrameworkRoot = _sharedFrameworkRoot ?? SharedFrameworkLocator.TryGetSharedFrameworkRoot();
        if (string.IsNullOrEmpty(sharedFrameworkRoot) || !Directory.Exists(sharedFrameworkRoot))
        {
            return [];
        }

        var outputDirectory = Path.GetFullPath(_preferredSearchDirectory);
        sharedFrameworkRoot = Path.GetFullPath(sharedFrameworkRoot);
        var cacheKey = outputDirectory + "\0" + sharedFrameworkRoot;
        return SharedFrameworkDirectories.GetOrAdd(
                                              cacheKey,
                                              _ => new Lazy<string[]>(
                                                  () => SharedFrameworkLocator.DiscoverSharedFrameworkDirectories(outputDirectory, sharedFrameworkRoot),
                                                  LazyThreadSafetyMode.ExecutionAndPublication))
                                         .Value;
    }

    private IEnumerable<string> GetSearchDirectoryCandidates()
    {
        if (!string.IsNullOrEmpty(_preferredSearchDirectory))
        {
            yield return _preferredSearchDirectory;
        }

        foreach (var directory in GetSearchDirectories())
        {
            if (!string.Equals(directory, _preferredSearchDirectory, PathComparison))
            {
                yield return directory;
            }
        }
    }

    private AssemblyDefinition ReadAndCache(string requestedFullName, string assemblyPath)
    {
        using var assemblyLock = CoverageAssemblyPathLock.EnterRead(assemblyPath);
        var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, CreateDependencyReaderParameters());
        return CacheAssembly(requestedFullName, assembly);
    }

    private AssemblyDefinition CacheAssembly(string requestedFullName, AssemblyDefinition assembly)
    {
        _cache[requestedFullName] = assembly;
        _cache[assembly.Name.FullName] = assembly;
        return assembly;
    }

    private AssemblyDefinition ResolveWithoutDirectoryFallback(AssemblyNameReference name)
    {
        // Directory probing is handled by ResolveFromSearchDirectories so every output-folder read
        // goes through CoverageAssemblyPathLock. The base resolver is only used for platform/TPA
        // fallback paths that are not rewritten by the coverage collector.
        var searchDirectories = GetSearchDirectories();
        foreach (var directory in searchDirectories)
        {
            RemoveSearchDirectory(directory);
        }

        try
        {
            return base.Resolve(name, CreateDependencyReaderParameters());
        }
        finally
        {
            foreach (var directory in searchDirectories)
            {
                AddSearchDirectory(directory);
            }
        }
    }

    private ReaderParameters CreateDependencyReaderParameters()
        => new()
        {
            InMemory = true,
            AssemblyResolver = this
        };

    private void InvalidateTracerCache()
    {
        var tracerAssemblyName = TracerAssembly.GetName();
        HashSet<AssemblyDefinition>? assembliesToDispose = null;
        List<string>? cacheKeysToRemove = null;
        foreach (var entry in _cache)
        {
            if (IsTracerAssembly(entry.Value.Name, tracerAssemblyName))
            {
                cacheKeysToRemove ??= [];
                cacheKeysToRemove.Add(entry.Key);
                assembliesToDispose ??= [];
                assembliesToDispose.Add(entry.Value);
            }
        }

        if (cacheKeysToRemove?.Count > 0)
        {
            foreach (var entryKey in cacheKeysToRemove)
            {
                _cache.Remove(entryKey);
            }

            foreach (var assembly in assembliesToDispose!)
            {
                assembly.Dispose();
            }
        }
    }

    private bool IsTracerAssembly(AssemblyNameReference name, AssemblyName tracerAssemblyName)
        => name.Name == tracerAssemblyName.Name && name.Version == tracerAssemblyName.Version;

    private string GetMscorlibBasePath(Version version)
    {
        string? GetSubFolderForVersion()
            => version.Major switch
            {
                1 when version.MajorRevision == 3300 => "v1.0.3705",
                1 => "v1.1.4322",
                2 => "v2.0.50727",
                4 => "v4.0.30319",
                _ => throw new NotSupportedException("Version not supported: " + version),
            };

        var rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET");
        string[] frameworkPaths =
        [
            Path.Combine(rootPath, "Framework"),
            Path.Combine(rootPath, "Framework64")
        ];

        var folder = GetSubFolderForVersion();

        if (folder != null)
        {
            foreach (var path in frameworkPaths)
            {
                var basePath = Path.Combine(path, folder);
                if (Directory.Exists(basePath))
                {
                    return basePath;
                }
            }
        }

        throw new NotSupportedException("Version not supported: " + version);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CoverageAssemblyResolver));
        }
    }

    private static class SharedFrameworkLocator
    {
        public static string[] DiscoverSharedFrameworkDirectories(string outputDirectory, string sharedFrameworkRoot)
        {
            var directories = new List<string>();
            var seenDirectories = new HashSet<string>(PathComparer);
            foreach (var runtimeConfigPath in GetRuntimeConfigPaths(outputDirectory))
            {
                foreach (var framework in ReadFrameworkReferences(runtimeConfigPath))
                {
                    var frameworkDirectory = FindCompatibleFrameworkDirectory(sharedFrameworkRoot, framework.Name, framework.Version);
                    if (frameworkDirectory is not null && seenDirectories.Add(frameworkDirectory))
                    {
                        directories.Add(frameworkDirectory);
                    }
                }
            }

            return directories.ToArray();
        }

        public static string? TryGetSharedFrameworkRoot()
        {
            var coreLibraryPath = typeof(object).Assembly.Location;
            if (!string.Equals(Path.GetFileName(coreLibraryPath), "System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var versionDirectory = Path.GetDirectoryName(coreLibraryPath);
            var frameworkDirectory = versionDirectory is null ? null : Path.GetDirectoryName(versionDirectory);
            var sharedFrameworkRoot = frameworkDirectory is null ? null : Path.GetDirectoryName(frameworkDirectory);
            return sharedFrameworkRoot is not null && string.Equals(Path.GetFileName(sharedFrameworkRoot), "shared", StringComparison.OrdinalIgnoreCase)
                       ? sharedFrameworkRoot
                       : null;
        }

        private static IEnumerable<string> GetRuntimeConfigPaths(string outputDirectory)
        {
            try
            {
                return Directory.EnumerateFiles(outputDirectory, "*.runtimeconfig.json", SearchOption.TopDirectoryOnly)
                                .OrderBy(path => path, PathComparer)
                                .ToArray();
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

        private static string? FindCompatibleFrameworkDirectory(string sharedFrameworkRoot, string frameworkName, string requestedVersionText)
        {
            if (!TryParseRuntimeVersion(requestedVersionText, out var requestedVersion, out _))
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
                return Directory.EnumerateDirectories(frameworkRoot)
                                .Select(path => new
                                {
                                    Path = path,
                                    Parsed = TryParseRuntimeVersion(Path.GetFileName(path), out var version, out var isPrerelease),
                                    Version = version,
                                    IsPrerelease = isPrerelease
                                })
                                .Where(candidate => candidate.Parsed &&
                                                    candidate.Version.Major == requestedVersion.Major &&
                                                    candidate.Version.Minor == requestedVersion.Minor &&
                                                    candidate.Version >= requestedVersion)
                                .OrderByDescending(candidate => candidate.Version)
                                .ThenBy(candidate => candidate.IsPrerelease)
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

        private static bool TryParseRuntimeVersion(string versionText, out Version version, out bool isPrerelease)
        {
            var suffixIndex = versionText.IndexOf('-');
            isPrerelease = suffixIndex >= 0;
            var numericVersion = isPrerelease ? versionText.Substring(0, suffixIndex) : versionText;
            return Version.TryParse(numericVersion, out version!);
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
    }
}
