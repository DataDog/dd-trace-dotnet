// <copyright file="AotDeclarationsCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Tools.Runner.Aot.Native;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using dnlib.DotNet;
#if NET6_0_OR_GREATER
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
#endif

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Checks the NativeAOT duck typing declarations against recorded maps: the mappings applications created under JIT that a
/// NativeAOT build wouldn't generate without the recording, because Datadog's assemblies don't declare them (see
/// CallTargetDuckTypeRegistry.FindUndeclared). The mappings of the duck typing constraints of CallTarget integrations (which
/// the build generates from the instrumented methods) and the ones dynamic duck typing doesn't create (which fail without a
/// declaration too) are left out, as the recorder marks them.
/// </summary>
internal static class AotDeclarationsCheck
{
    private static readonly Regex MapFileSuffix = new("-[0-9a-f]{32}$", RegexOptions.Compiled);

    /// <summary>
    /// Runs the check.
    /// </summary>
    /// <param name="datadogTracePath">The Datadog.Trace.dll whose declarations are checked.</param>
    /// <param name="maps">The recorded maps, or directories of maps.</param>
    /// <param name="excludedNamespaces">The namespace prefixes of the Datadog proxies left out.</param>
    /// <param name="reportPath">The optional JSON report path.</param>
    /// <returns>0 when every recorded mapping is declared; otherwise, 1.</returns>
    internal static int Run(string datadogTracePath, IReadOnlyList<string> maps, IReadOnlyList<string> excludedNamespaces, string? reportPath)
    {
#if NET6_0_OR_GREATER
        var discovery = DuckTypeAotAttributeDiscovery.Discover([datadogTracePath]);
        var declared = discovery.Mappings.ToList();
        var files = maps.SelectMany(path => Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories) : [path])
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToList();
        var undeclared = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var checkedMappings = 0;
        var unreadableMaps = 0;
        foreach (var file in files)
        {
            if (!TryReadMap(file, excludedNamespaces, out var applicationDirectory, out var mappings))
            {
                unreadableMaps++;
                AotLog.Warn($"'{file}' isn't a duck typing map: skipped.");
                continue;
            }

            checkedMappings += mappings.Count;
            if (mappings.Count == 0)
            {
                continue;
            }

            var application = MapFileSuffix.Replace(Path.GetFileNameWithoutExtension(file), string.Empty);
            var resolver = new MapTypeResolver(applicationDirectory);
            try
            {
                foreach (var key in CallTargetDuckTypeRegistry.FindUndeclared([], [], declared, mappings, resolver.FindType, resolver.Resolve))
                {
                    if (!undeclared.TryGetValue(key, out var applications))
                    {
                        applications = new SortedSet<string>(StringComparer.Ordinal);
                        undeclared[key] = applications;
                    }

                    applications.Add(application);
                }
            }
            finally
            {
                resolver.Dispose();
            }
        }

        if (reportPath is not null)
        {
            var report = new
            {
                maps = files.Count,
                checkedMappings,
                declaredMappings = declared.Count,
                undeclared = undeclared.Select(entry => new { mapping = entry.Key, applications = entry.Value.ToList() }).ToList(),
            };
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(report, Formatting.Indented));
        }

        AotLog.Info($"{checkedMappings} recorded duck typing mappings of {files.Count - unreadableMaps} maps checked against the {declared.Count} declared by '{datadogTracePath}'.");
        if (undeclared.Count == 0)
        {
            return 0;
        }

        AotLog.Error($"{undeclared.Count} recorded duck typing mappings of Datadog's proxies aren't declared: a NativeAOT build doesn't generate them without a recorded map. Declare them next to their proxy ([DuckType], [DuckCopy] with their target, IncludeDerivedTypes for the types deriving from a base type, the open generic type for its instantiations; see docs/development/NativeAOT.md).");
        foreach (var entry in undeclared)
        {
            AotLog.Error($"  {entry.Key} (recorded by {string.Join(", ", entry.Value)})");
        }

        return 1;
#else
        AotLog.Error("The check of the NativeAOT declarations requires the .NET 6+ runner.");
        return 1;
#endif
    }

    /// <summary>
    /// Reads the mappings of a recorded map to check: forward and reverse mappings of Datadog's proxies dynamic duck typing
    /// created outside of CallTarget's constraints.
    /// </summary>
    /// <param name="path">The map path.</param>
    /// <param name="excludedNamespaces">The namespace prefixes of the Datadog proxies left out.</param>
    /// <param name="applicationDirectory">The directory of the application that recorded the map, if the map names it.</param>
    /// <param name="mappings">The mappings to check.</param>
    /// <returns>true if the file is a map; otherwise, false.</returns>
    internal static bool TryReadMap(string path, IReadOnlyList<string> excludedNamespaces, out string? applicationDirectory, out List<DuckTypeAotMapping> mappings)
    {
        applicationDirectory = null;
        mappings = [];
        JObject document;
        try
        {
            document = JObject.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (document["mappings"] is not JArray entries)
        {
            return false;
        }

        applicationDirectory = (string?)document["applicationDirectory"];
        foreach (var entry in entries.OfType<JObject>())
        {
            if ((bool?)entry["callTarget"] == true || (bool?)entry["canCreate"] == false)
            {
                continue;
            }

            var proxyType = (string?)entry["proxyType"];
            var proxyAssembly = (string?)entry["proxyAssembly"];
            var targetType = (string?)entry["targetType"];
            var targetAssembly = (string?)entry["targetAssembly"];
            if (proxyType is null || proxyAssembly is null || targetType is null || targetAssembly is null)
            {
                continue;
            }

            var mode = string.Equals((string?)entry["mode"], "reverse", StringComparison.OrdinalIgnoreCase) ? DuckTypeAotMappingMode.Reverse : DuckTypeAotMappingMode.Forward;
            var datadogType = mode == DuckTypeAotMappingMode.Forward ? proxyType : targetType;
            if (excludedNamespaces.Any(prefix => datadogType.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            mappings.Add(new DuckTypeAotMapping(proxyType, proxyAssembly, targetType, targetAssembly, mode, DuckTypeAotMappingSource.MapFile));
        }

        return true;
    }

    /// <summary>
    /// Resolves the types of a recorded map: from the directory of its application, then from the shared frameworks of the
    /// runtime the check runs on.
    /// </summary>
    private sealed class MapTypeResolver : IDisposable
    {
        private readonly AssemblyResolver _resolver = new() { FindExactMatch = false, EnableTypeDefCache = true };

        internal MapTypeResolver(string? applicationDirectory)
        {
            _resolver.DefaultModuleContext = new ModuleContext(_resolver);
            if (applicationDirectory is not null && Directory.Exists(applicationDirectory))
            {
                _resolver.PreSearchPaths.Add(applicationDirectory);
            }

            // <dotnet>/shared/Microsoft.NETCore.App/<version>, and the ASP.NET Core framework of the same version next to it.
            var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _resolver.PostSearchPaths.Add(runtimeDirectory);
            if (Path.GetDirectoryName(Path.GetDirectoryName(runtimeDirectory)) is { } sharedFrameworks)
            {
                var aspNetCore = Path.Combine(sharedFrameworks, "Microsoft.AspNetCore.App", Path.GetFileName(runtimeDirectory));
                if (Directory.Exists(aspNetCore))
                {
                    _resolver.PostSearchPaths.Add(aspNetCore);
                }
            }
        }

        internal TypeDef? FindType(string assemblyName, string typeName)
            => _resolver.Resolve(new AssemblyNameInfo(assemblyName), null)?.ManifestModule?.Find(typeName, isReflectionName: true);

        internal TypeDef? Resolve(ITypeDefOrRef type) => type.ResolveTypeDef();

        public void Dispose()
        {
            foreach (var assembly in _resolver.GetCachedAssemblies())
            {
                foreach (var module in assembly.Modules)
                {
                    module.Dispose();
                }
            }

            _resolver.Clear();
        }
    }
}
