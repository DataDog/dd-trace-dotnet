// <copyright file="DuckTypeAotMappingResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Datadog.Trace.DuckTyping;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Provides helper operations for duck type aot mapping resolver.
    /// </summary>
    internal static class DuckTypeAotMappingResolver
    {
        /// <summary>
        /// Resolves resolve.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The result produced by this operation.</returns>
        internal static DuckTypeAotMappingResolutionResult Resolve(DuckTypeAotGenerateOptions options)
        {
            var profile = DuckTypeAotGenerateProcessor.IsProfilingEnabled() ? new ResolverProfile() : null;
            var warnings = new List<string>();
            var errors = new List<string>();

            var targetAssemblyPaths = Measure(profile, static p => p.GetTargetAssemblyPathsSeconds, static (p, value) => p.GetTargetAssemblyPathsSeconds = value, () => GetTargetAssemblyPaths(options, warnings));
            var proxyAssemblyPathsByName = Measure(profile, static p => p.BuildProxyAssemblyPathIndexSeconds, static (p, value) => p.BuildProxyAssemblyPathIndexSeconds = value, () => BuildAssemblyPathIndex(options.ProxyAssemblies, "--proxy-assembly", errors));
            var targetAssemblyPathsByName = Measure(profile, static p => p.BuildTargetAssemblyPathIndexSeconds, static (p, value) => p.BuildTargetAssemblyPathIndexSeconds = value, () => BuildAssemblyPathIndex(targetAssemblyPaths, "--target-folder", errors, warnings));
            var genericTypeRoots = new Dictionary<string, DuckTypeAotTypeReference>(StringComparer.Ordinal);

            var resolvedMappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);

            if (!StringUtil.IsNullOrWhiteSpace(options.MapFile))
            {
                var mapFileResult = Measure(profile, static p => p.ParseMapFileSeconds, static (p, value) => p.ParseMapFileSeconds = value, () => DuckTypeAotMapFileParser.Parse(options.MapFile!));
                errors.AddRange(mapFileResult.Errors);
                foreach (var mapping in mapFileResult.Mappings)
                {
                    resolvedMappings[mapping.Key] = mapping;
                }
            }

            if (!StringUtil.IsNullOrWhiteSpace(options.GenericInstantiationsFile))
            {
                var genericInstantiationsResult = Measure(profile, static p => p.ParseGenericInstantiationsSeconds, static (p, value) => p.ParseGenericInstantiationsSeconds = value, () => DuckTypeAotGenericInstantiationsParser.Parse(options.GenericInstantiationsFile!));
                errors.AddRange(genericInstantiationsResult.Errors);
                foreach (var typeRoot in genericInstantiationsResult.TypeRoots)
                {
                    genericTypeRoots[typeRoot.Key] = typeRoot;
                }
            }

            ExpandOpenGenericMappings(resolvedMappings, genericTypeRoots.Values, errors);
            Measure(profile, static p => p.ValidateGenericClosureSeconds, static (p, value) => p.ValidateGenericClosureSeconds = value, () => ValidateGenericClosure(resolvedMappings.Values, errors));

            Measure(profile, static p => p.ValidateResolvedAssemblyReferencesSeconds, static (p, value) => p.ValidateResolvedAssemblyReferencesSeconds = value, () =>
            {
                // A recorded map names the assembly of each side (e.g. the contracts of a library a reverse proxy implements, or
                // Datadog.Trace's own types): an input of the other kind provides it too.
                foreach (var mapping in resolvedMappings.Values)
                {
                    if (!proxyAssemblyPathsByName.ContainsKey(mapping.ProxyAssemblyName))
                    {
                        if (targetAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath))
                        {
                            proxyAssemblyPathsByName[mapping.ProxyAssemblyName] = proxyAssemblyPath;
                        }
                        else
                        {
                            errors.Add($"Mapping proxy assembly '{mapping.ProxyAssemblyName}' could not be resolved from --proxy-assembly or --target-folder inputs.");
                        }
                    }

                    if (!targetAssemblyPathsByName.ContainsKey(mapping.TargetAssemblyName))
                    {
                        if (proxyAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath))
                        {
                            targetAssemblyPathsByName[mapping.TargetAssemblyName] = targetAssemblyPath;
                        }
                        else
                        {
                            errors.Add($"Mapping target assembly '{mapping.TargetAssemblyName}' could not be resolved from --target-folder or --proxy-assembly inputs.");
                        }
                    }
                }
            });

            if (profile is not null)
            {
                DuckTypeAotGenerateProcessor.WriteProfileMetric(
                    $"resolve.profile getTargetAssemblies={profile.GetTargetAssemblyPathsSeconds:F3}s buildProxyIndex={profile.BuildProxyAssemblyPathIndexSeconds:F3}s buildTargetIndex={profile.BuildTargetAssemblyPathIndexSeconds:F3}s parseMap={profile.ParseMapFileSeconds:F3}s parseGenericInstantiations={profile.ParseGenericInstantiationsSeconds:F3}s validateGenericClosure={profile.ValidateGenericClosureSeconds:F3}s validateAssemblyRefs={profile.ValidateResolvedAssemblyReferencesSeconds:F3}s");
                DuckTypeAotGenerateProcessor.WriteProfileMetric(
                    $"resolve.profile targetAssemblyPaths={targetAssemblyPaths.Count} proxyAssemblies={proxyAssemblyPathsByName.Count} targetAssemblies={targetAssemblyPathsByName.Count} mappings={resolvedMappings.Count} genericRoots={genericTypeRoots.Count} warnings={warnings.Count} errors={errors.Count}");
            }

            return new DuckTypeAotMappingResolutionResult(
                resolvedMappings.Values,
                proxyAssemblyPathsByName,
                targetAssemblyPathsByName,
                genericTypeRoots.Values,
                warnings,
                errors);
        }

        /// <summary>
        /// Gets get target assembly paths.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<string> GetTargetAssemblyPaths(DuckTypeAotGenerateOptions options, ICollection<string> warnings)
        {
            var targetAssemblyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skippedRegistryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outputPath = StringUtil.IsNullOrWhiteSpace(options.OutputPath) ? null : Path.GetFullPath(options.OutputPath);
            foreach (var targetAssembly in options.TargetAssemblies)
            {
                AddTargetAssembly(targetAssembly);
            }

            foreach (var targetFolder in options.TargetFolders)
            {
                foreach (var targetFilter in options.TargetFilters)
                {
                    foreach (var targetAssemblyPath in Directory.EnumerateFiles(targetFolder, targetFilter, SearchOption.TopDirectoryOnly))
                    {
                        AddTargetAssembly(targetAssemblyPath);
                    }
                }
            }

            return targetAssemblyPaths.ToList();

            void AddTargetAssembly(string targetAssemblyPath)
            {
                // A registry generated by a previous run (often in the same folder as its targets) is never a target: its
                // generated types would become aliases of mappings, and then be missing from the new registry.
                if ((outputPath is not null && string.Equals(Path.GetFullPath(targetAssemblyPath), outputPath, StringComparison.OrdinalIgnoreCase)) ||
                    IsGeneratedRegistryAssembly(targetAssemblyPath))
                {
                    if (skippedRegistryPaths.Add(targetAssemblyPath))
                    {
                        warnings.Add($"Skipped '{targetAssemblyPath}' as a target assembly because it is a generated duck typing AOT registry.");
                    }

                    return;
                }

                _ = targetAssemblyPaths.Add(targetAssemblyPath);
            }
        }

        /// <summary>
        /// Determines whether an assembly is a registry generated by ducktype-aot generate.
        /// </summary>
        /// <param name="path">The assembly path.</param>
        /// <returns>true if the assembly defines the generated registry bootstrap type; otherwise, false.</returns>
        internal static bool IsGeneratedRegistryAssembly(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var peReader = new PEReader(stream);
                if (!peReader.HasMetadata)
                {
                    return false;
                }

                var metadataReader = peReader.GetMetadataReader();
                foreach (var typeDefinitionHandle in metadataReader.TypeDefinitions)
                {
                    var typeDefinition = metadataReader.GetTypeDefinition(typeDefinitionHandle);
                    if (metadataReader.StringComparer.Equals(typeDefinition.Name, "DuckTypeAotRegistryBootstrap") &&
                        metadataReader.StringComparer.Equals(typeDefinition.Namespace, "Datadog.Trace.DuckTyping.Generated"))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Not a readable managed assembly: the assembly index reports it.
            }

            return false;
        }

        private static T Measure<T>(ResolverProfile? profile, Func<ResolverProfile, double> getter, Action<ResolverProfile, double> setter, Func<T> action)
        {
            if (profile is null)
            {
                return action();
            }

            var stopwatch = Stopwatch.StartNew();
            var result = action();
            stopwatch.Stop();
            setter(profile, getter(profile) + stopwatch.Elapsed.TotalSeconds);
            return result;
        }

        private static void Measure(ResolverProfile? profile, Func<ResolverProfile, double> getter, Action<ResolverProfile, double> setter, Action action)
        {
            if (profile is null)
            {
                action();
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            setter(profile, getter(profile) + stopwatch.Elapsed.TotalSeconds);
        }

        /// <summary>
        /// Executes build assembly path index.
        /// </summary>
        /// <param name="assemblyPaths">The assembly paths value.</param>
        /// <param name="sourceName">The source name value.</param>
        /// <param name="errors">The errors value.</param>
        /// <param name="invalidAssemblyWarnings">
        /// When set, files that aren't valid managed assemblies (target folders also contain native libraries matched by the
        /// *.dll filter) are skipped: native libraries silently, anything else with a warning instead of an error.
        /// </param>
        /// <returns>The result produced by this operation.</returns>
        private static Dictionary<string, string> BuildAssemblyPathIndex(IReadOnlyList<string> assemblyPaths, string sourceName, ICollection<string> errors, ICollection<string>? invalidAssemblyWarnings = null)
        {
            var assemblyPathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assemblyPath in assemblyPaths)
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                    var normalizedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName.Name ?? string.Empty);
                    if (StringUtil.IsNullOrWhiteSpace(normalizedAssemblyName))
                    {
                        errors.Add($"{sourceName} could not read assembly name from '{assemblyPath}'.");
                        continue;
                    }

                    if (!assemblyPathByName.TryAdd(normalizedAssemblyName, assemblyPath))
                    {
                        var existingPath = assemblyPathByName[normalizedAssemblyName];
                        if (!string.Equals(existingPath, assemblyPath, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"{sourceName} has duplicate assembly identity '{normalizedAssemblyName}' with different paths: '{existingPath}' and '{assemblyPath}'.");
                        }
                    }
                }
                catch (BadImageFormatException ex) when (invalidAssemblyWarnings is not null)
                {
                    // Native libraries can't contain duck typing targets. Anything else (e.g. a truncated assembly) is
                    // reported, so a mapping that fails to resolve its assembly still shows the root cause.
                    if (!IsNativePortableExecutable(assemblyPath))
                    {
                        invalidAssemblyWarnings.Add($"{sourceName} skipped '{assemblyPath}' because it isn't a valid managed assembly: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{sourceName} failed to read assembly metadata for '{assemblyPath}': {ex.Message}");
                }
            }

            return assemblyPathByName;
        }

        /// <summary>
        /// Determines whether a file is a valid PE image without a CLI header, i.e. a native library.
        /// </summary>
        /// <param name="path">The file path value.</param>
        /// <returns>true if the file is a native PE image; otherwise, false.</returns>
        internal static bool IsNativePortableExecutable(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                var headers = new PEHeaders(stream);
                // Files that aren't PE images at all (e.g. ELF) parse as COFF-only object files.
                return !headers.IsCoffOnly && headers.CorHeader is null;
            }
            catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Expands open generic map-file rules into concrete closed mappings from closed generic roots.
        /// </summary>
        /// <param name="resolvedMappings">The resolved mappings value.</param>
        /// <param name="genericTypeRoots">The closed generic type roots value.</param>
        /// <param name="errors">The errors value.</param>
        internal static void ExpandOpenGenericMappings(
            IDictionary<string, DuckTypeAotMapping> resolvedMappings,
            IEnumerable<DuckTypeAotTypeReference> genericTypeRoots,
            ICollection<string> errors)
        {
            var closedGenericTypeRoots = genericTypeRoots
                                        .Where(root => DuckTypeAotNameHelpers.IsClosedGenericTypeName(root.TypeName))
                                        .ToList();
            if (closedGenericTypeRoots.Count == 0)
            {
                return;
            }

            foreach (var mapping in resolvedMappings.Values.ToList())
            {
                if (!DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.ProxyTypeName) &&
                    !DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.TargetTypeName))
                {
                    continue;
                }

                var expandedMappings = ExpandOpenGenericMapping(mapping, closedGenericTypeRoots, errors);
                if (expandedMappings.Count == 0)
                {
                    continue;
                }

                _ = resolvedMappings.Remove(mapping.Key);
                foreach (var expandedMapping in expandedMappings)
                {
                    resolvedMappings[expandedMapping.Key] = expandedMapping;
                }
            }
        }

        /// <summary>
        /// Expands one open generic mapping into closed mappings from the matching closed generic roots:
        /// <list type="bullet">
        /// <item>An open proxy and an open target with the same arity: each closed root of the target closes both.</item>
        /// <item>A proxy that isn't open (e.g. a non-generic proxy of the instances of every Message&lt;TKey, TValue&gt;) and an
        /// open target: each closed root of the target gets the proxy.</item>
        /// <item>An open proxy and a target that isn't open: each closed root of the proxy gets the target.</item>
        /// </list>
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="closedGenericTypeRoots">The closed generic type roots value.</param>
        /// <param name="errors">The errors value.</param>
        /// <returns>The expanded closed mappings.</returns>
        private static IReadOnlyList<DuckTypeAotMapping> ExpandOpenGenericMapping(
            DuckTypeAotMapping mapping,
            IReadOnlyList<DuckTypeAotTypeReference> closedGenericTypeRoots,
            ICollection<string> errors)
        {
            var proxyIsOpen = DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.ProxyTypeName);
            var targetIsOpen = DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.TargetTypeName);
            var proxyArity = proxyIsOpen ? DuckTypeAotNameHelpers.GetDeclaredGenericArity(mapping.ProxyTypeName) : 0;
            var targetArity = targetIsOpen ? DuckTypeAotNameHelpers.GetDeclaredGenericArity(mapping.TargetTypeName) : 0;
            if (proxyIsOpen && targetIsOpen && (proxyArity == 0 || targetArity == 0 || proxyArity != targetArity))
            {
                errors.Add(
                    $"Mapping '{mapping.Key}' contains an open generic rule with incompatible arity. " +
                    "Build-time expansion requires open proxy and target definitions with the same generic arity.");
                return Array.Empty<DuckTypeAotMapping>();
            }

            var expandedMappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);
            foreach (var typeRoot in closedGenericTypeRoots)
            {
                if (targetIsOpen)
                {
                    if (!IsClosedRootOf(typeRoot, mapping.TargetTypeName, mapping.TargetAssemblyName, targetArity, out var genericArgumentsSuffix))
                    {
                        continue;
                    }

                    var expandedMapping = new DuckTypeAotMapping(
                        proxyIsOpen ? string.Concat(mapping.ProxyTypeName, genericArgumentsSuffix) : mapping.ProxyTypeName,
                        mapping.ProxyAssemblyName,
                        typeRoot.TypeName,
                        typeRoot.AssemblyName,
                        mapping.Mode,
                        mapping.Source,
                        mapping.ScenarioId);
                    expandedMappings[expandedMapping.Key] = expandedMapping;
                }
                else if (IsClosedRootOf(typeRoot, mapping.ProxyTypeName, mapping.ProxyAssemblyName, proxyArity, out _))
                {
                    var expandedMapping = new DuckTypeAotMapping(
                        typeRoot.TypeName,
                        typeRoot.AssemblyName,
                        mapping.TargetTypeName,
                        mapping.TargetAssemblyName,
                        mapping.Mode,
                        mapping.Source,
                        mapping.ScenarioId);
                    expandedMappings[expandedMapping.Key] = expandedMapping;
                }
            }

            return expandedMappings.Values
                                   .OrderBy(item => item.Key, StringComparer.Ordinal)
                                   .ToList();

            static bool IsClosedRootOf(DuckTypeAotTypeReference typeRoot, string genericDefinitionName, string assemblyName, int arity, out string genericArgumentsSuffix)
            {
                genericArgumentsSuffix = string.Empty;
                return string.Equals(typeRoot.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase) &&
                       DuckTypeAotNameHelpers.TrySplitClosedGenericTypeName(typeRoot.TypeName, out var rootDefinitionName, out genericArgumentsSuffix, out var genericArgumentCount) &&
                       string.Equals(rootDefinitionName, genericDefinitionName, StringComparison.Ordinal) &&
                       genericArgumentCount == arity;
            }
        }

        /// <summary>
        /// Validates validate generic closure.
        /// </summary>
        /// <param name="mappings">The mappings value.</param>
        /// <param name="errors">The errors value.</param>
        internal static void ValidateGenericClosure(IEnumerable<DuckTypeAotMapping> mappings, ICollection<string> errors)
        {
            foreach (var mapping in mappings)
            {
                if (!DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.ProxyTypeName) &&
                    !DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.TargetTypeName))
                {
                    continue;
                }

                errors.Add(
                    $"Mapping '{mapping.Key}' contains an open generic type. " +
                    "NativeAOT registry emission requires closed proxy and target types. " +
                    "Use matching closed --generic-instantiations roots so open map rules can be expanded before emission.");
            }
        }

        private sealed class ResolverProfile
        {
            internal double GetTargetAssemblyPathsSeconds { get; set; }

            internal double BuildProxyAssemblyPathIndexSeconds { get; set; }

            internal double BuildTargetAssemblyPathIndexSeconds { get; set; }

            internal double ParseMapFileSeconds { get; set; }

            internal double ParseGenericInstantiationsSeconds { get; set; }

            internal double ValidateGenericClosureSeconds { get; set; }

            internal double ValidateResolvedAssemblyReferencesSeconds { get; set; }
        }
    }

    /// <summary>
    /// Represents duck type aot mapping resolution result.
    /// </summary>
    internal sealed class DuckTypeAotMappingResolutionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotMappingResolutionResult"/> class.
        /// </summary>
        /// <param name="mappings">The mappings value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="genericTypeRoots">The generic type roots value.</param>
        /// <param name="warnings">The warnings value.</param>
        /// <param name="errors">The errors value.</param>
        public DuckTypeAotMappingResolutionResult(
            IEnumerable<DuckTypeAotMapping> mappings,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            IEnumerable<DuckTypeAotTypeReference> genericTypeRoots,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> errors)
        {
            Mappings = new List<DuckTypeAotMapping>(mappings);
            ProxyAssemblyPathsByName = proxyAssemblyPathsByName;
            TargetAssemblyPathsByName = targetAssemblyPathsByName;
            GenericTypeRoots = new List<DuckTypeAotTypeReference>(genericTypeRoots);
            Warnings = warnings;
            Errors = errors;
        }

        /// <summary>
        /// Gets mappings.
        /// </summary>
        /// <value>The mappings value.</value>
        public IReadOnlyList<DuckTypeAotMapping> Mappings { get; }

        /// <summary>
        /// Gets proxy assembly paths by name.
        /// </summary>
        /// <value>The proxy assembly paths by name value.</value>
        public IReadOnlyDictionary<string, string> ProxyAssemblyPathsByName { get; }

        /// <summary>
        /// Gets target assembly paths by name.
        /// </summary>
        /// <value>The target assembly paths by name value.</value>
        public IReadOnlyDictionary<string, string> TargetAssemblyPathsByName { get; }

        /// <summary>
        /// Gets generic type roots.
        /// </summary>
        /// <value>The generic type roots value.</value>
        public IReadOnlyList<DuckTypeAotTypeReference> GenericTypeRoots { get; }

        /// <summary>
        /// Gets warnings.
        /// </summary>
        /// <value>The warnings value.</value>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Gets errors.
        /// </summary>
        /// <value>The errors value.</value>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Gets the Datadog.Trace assembly the generated registry is bound to (it validates its version and MVID at startup):
        /// the application's copy, passed as a target or as a proxy assembly, or the generator's own copy when there's none.
        /// </summary>
        /// <param name="isGeneratorCopy">Whether the generator's own copy is used.</param>
        /// <returns>The Datadog.Trace assembly path.</returns>
        public string GetDatadogTraceAssemblyPath(out bool isGeneratorCopy)
        {
            foreach (var assemblyPathsByName in new[] { TargetAssemblyPathsByName, ProxyAssemblyPathsByName })
            {
                if (assemblyPathsByName.TryGetValue("Datadog.Trace", out var datadogTraceAssemblyPath) && File.Exists(datadogTraceAssemblyPath))
                {
                    isGeneratorCopy = false;
                    return datadogTraceAssemblyPath;
                }
            }

            isGeneratorCopy = true;
            return typeof(DuckType).Assembly.Location;
        }
    }
}
