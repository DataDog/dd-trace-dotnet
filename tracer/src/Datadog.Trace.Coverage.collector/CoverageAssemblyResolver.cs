// <copyright file="CoverageAssemblyResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Ci.Coverage;
using Mono.Cecil;

namespace Datadog.Trace.Coverage.Collector
{
    /// <summary>
    /// Resolves coverage rewrite dependencies without keeping file handles open on resolved assemblies.
    /// </summary>
    internal sealed class CoverageAssemblyResolver : BaseAssemblyResolver
    {
        private static readonly Assembly TracerAssembly = typeof(CoverageReporter).Assembly;
        private readonly Dictionary<string, AssemblyDefinition> _cache = new(StringComparer.Ordinal);
        private readonly ICollectorLogger _logger;
        private readonly string _assemblyFilePath;
        private readonly string _preferredSearchDirectory;
        private string _tracerAssemblyLocation;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoverageAssemblyResolver"/> class.
        /// </summary>
        /// <param name="logger">Logger used to report resolution failures.</param>
        /// <param name="assemblyFilePath">The target assembly currently being rewritten.</param>
        public CoverageAssemblyResolver(ICollectorLogger logger, string assemblyFilePath)
        {
            _logger = logger;
            _assemblyFilePath = assemblyFilePath;
            _preferredSearchDirectory = Path.GetDirectoryName(assemblyFilePath) ?? string.Empty;
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
            catch (AssemblyResolutionException arEx)
            {
                _logger.Error(arEx, $"{nameof(CoverageAssemblyResolver)} failed to resolve dependency '{name.FullName}' while processing target assembly '{_assemblyFilePath}'.");
                throw;
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
            if (string.Equals(_tracerAssemblyLocation, assemblyLocation, StringComparison.Ordinal))
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
                var assemblies = _cache.Values.Distinct().ToArray();
                _cache.Clear();
                foreach (var assembly in assemblies)
                {
                    assembly.Dispose();
                }
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
        {
            var extensions = name.IsWindowsRuntime ? new[] { ".winmd", ".dll" } : new[] { ".exe", ".dll" };
            foreach (var directory in GetSearchDirectoryCandidates())
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

        private IEnumerable<string> GetSearchDirectoryCandidates()
        {
            if (!string.IsNullOrEmpty(_preferredSearchDirectory))
            {
                yield return _preferredSearchDirectory;
            }

            foreach (var directory in GetSearchDirectories())
            {
                if (!string.Equals(directory, _preferredSearchDirectory, StringComparison.Ordinal))
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
            var cacheEntries = _cache.ToArray();
            var assembliesToDispose = new HashSet<AssemblyDefinition>();

            foreach (var entry in cacheEntries)
            {
                if (IsTracerAssembly(entry.Value.Name, tracerAssemblyName))
                {
                    _cache.Remove(entry.Key);
                    assembliesToDispose.Add(entry.Value);
                }
            }

            foreach (var assembly in assembliesToDispose)
            {
                assembly.Dispose();
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
    }
}
