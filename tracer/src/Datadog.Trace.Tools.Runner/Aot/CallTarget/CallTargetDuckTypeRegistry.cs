// <copyright file="CallTargetDuckTypeRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// The DuckType AOT registry of the proxies the CallTarget adapters create (C1): generated with the DuckType AOT
/// generator from the duck typing constraints the integration methods bind, and read back to give the adapters the
/// proxy types and constructors.
/// </summary>
internal sealed class CallTargetDuckTypeRegistry : IDuckProxyProvider
{
    internal const string BootstrapTypeName = "Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap";

    private readonly IReadOnlyDictionary<string, DuckTypeAotMappingEmissionResult> _results;
    private readonly Func<ITypeDefOrRef, TypeDef?> _resolveType;

    private CallTargetDuckTypeRegistry(string assemblyPath, string assemblyName, ModuleDefMD module, DuckTypeAotRegistryEmissionResult emission, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        AssemblyPath = assemblyPath;
        AssemblyName = assemblyName;
        Module = module;
        _results = emission.MappingResultsByKey;
        _resolveType = resolveType;
        Mappings = _results.Count;
        Compatible = _results.Values.Count(r => r.Status == DuckTypeAotCompatibilityStatuses.Compatible && !r.ReplaysDynamicFailure);
        Warnings = emission.Warnings;
    }

    public string AssemblyPath { get; }

    public string AssemblyName { get; }

    public ModuleDefMD Module { get; }

    public int Mappings { get; }

    public int Compatible { get; }

    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Generates the registry for <paramref name="requests"/> into <paramref name="outputDirectory"/> (assembly, trimmer
    /// descriptor, props and compatibility reports), or returns null when no request names a proxy.
    /// </summary>
    /// <param name="requests">The proxies the adapters create for static types.</param>
    /// <param name="runtimeRequests">The proxies looked up by runtime type for values of static types (begin slow path).</param>
    /// <param name="recordedMappings">The mappings recorded at runtime by dynamic duck typing (C6).</param>
    /// <param name="assemblyPaths">The paths of the loaded assemblies, by name, for the recorded mappings.</param>
    /// <param name="outputDirectory">The output directory.</param>
    /// <param name="assemblyName">The name of the registry assembly.</param>
    /// <param name="datadogTracePath">The Datadog.Trace.dll the application ships.</param>
    /// <param name="resolveType">Resolves a type reference across the loaded modules.</param>
    public static CallTargetDuckTypeRegistry? Build(
        IEnumerable<(TypeSig ProxyDefinition, TypeSig Target)> requests,
        IEnumerable<(TypeSig ProxyDefinition, TypeSig Target)> runtimeRequests,
        IEnumerable<DuckTypeAotMapping> recordedMappings,
        IReadOnlyDictionary<string, string> assemblyPaths,
        string outputDirectory,
        string assemblyName,
        string datadogTracePath,
        Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        var mappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);
        var proxyAssemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Datadog.Trace"] = Path.GetFullPath(datadogTracePath) };
        var targetAssemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;

        // The proxies of static types first: a recorded mapping of the same pair comes from IntegrationMapper creating that
        // proxy for the static type (DuckType.Create records the runtime type), so it doesn't need the aliases of a
        // runtime lookup, which for System.Object would be every type of the inputs.
        foreach (var (proxyDefinition, target) in requests)
        {
            AddRequest(proxyDefinition, target, DuckTypeAotMappingSource.CallTarget);
        }

        foreach (var mapping in recordedMappings)
        {
            if (mappings.ContainsKey(mapping.Key))
            {
                continue;
            }

            // The assemblies of the generic arguments too (types of the application in a recorded closed generic type).
            var argumentAssemblies = GenericArgumentAssemblies(mapping.ProxyTypeName).Concat(GenericArgumentAssemblies(mapping.TargetTypeName)).ToList();
            if (!assemblyPaths.TryGetValue(mapping.ProxyAssemblyName, out var proxyPath)
             || !assemblyPaths.TryGetValue(mapping.TargetAssemblyName, out var targetPath)
             || argumentAssemblies.Any(a => !assemblyPaths.ContainsKey(a)))
            {
                // A catalog covers libraries the application doesn't use.
                skipped++;
                continue;
            }

            mappings[mapping.Key] = mapping;
            proxyAssemblies[mapping.ProxyAssemblyName] = proxyPath;
            targetAssemblies[mapping.TargetAssemblyName] = targetPath;
            foreach (var argumentAssembly in argumentAssemblies)
            {
                targetAssemblies[argumentAssembly] = assemblyPaths[argumentAssembly];
            }
        }

        foreach (var (proxyDefinition, target) in runtimeRequests)
        {
            AddRequest(proxyDefinition, target, DuckTypeAotMappingSource.MapFile);
        }

        if (skipped > 0)
        {
            Native.AotLog.Info($"{skipped} recorded duck typing mappings skipped: their assemblies aren't among the application's");
        }

        if (mappings.Count == 0)
        {
            return null;
        }

        void AddRequest(TypeSig proxyDefinition, TypeSig target, DuckTypeAotMappingSource source)
        {
            if (Describe(proxyDefinition, resolveType) is not { } proxy || Describe(target, resolveType) is not { } targetType)
            {
                return;
            }

            var mapping = new DuckTypeAotMapping(proxy.TypeName, proxy.AssemblyName, targetType.TypeName, targetType.AssemblyName, DuckTypeAotMappingMode.Forward, source);
            if (mappings.ContainsKey(mapping.Key))
            {
                return;
            }

            mappings[mapping.Key] = mapping;
            proxyAssemblies[proxy.AssemblyName] = proxy.Path;
            foreach (var (targetAssemblyName, path) in targetType.Assemblies)
            {
                targetAssemblies[targetAssemblyName] = path;
            }
        }

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, assemblyName + ".dll");
        var options = new DuckTypeAotGenerateOptions(
            proxyAssemblies.Values.ToList(),
            targetAssemblies.Values.ToList(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            mapFile: string.Empty,
            genericInstantiationsFile: null,
            outputPath: outputPath,
            assemblyName: assemblyName,
            trimmerDescriptorPath: Path.Combine(outputDirectory, assemblyName + ".linker.xml"),
            propsPath: Path.Combine(outputDirectory, assemblyName + ".props"));
        var resolution = new DuckTypeAotMappingResolutionResult(mappings.Values, proxyAssemblies, targetAssemblies, Array.Empty<DuckTypeAotTypeReference>(), Array.Empty<string>(), Array.Empty<string>());
        var artifactPaths = DuckTypeAotArtifactPaths.Create(options);
        // The generator replays dynamic duck typing in this process with the assemblies of the mappings: the other assemblies
        // their types need (e.g. the dependencies of the types of a closed generic argument) come from the application.
        Assembly? ResolveApplicationAssembly(AssemblyLoadContext context, AssemblyName name)
        {
            if (name.Name is not { } simpleName || !assemblyPaths.TryGetValue(simpleName, out var path))
            {
                return null;
            }

            try
            {
                return context.LoadFromAssemblyPath(path);
            }
            catch (Exception)
            {
                // E.g. another version of a framework assembly the Runner already loaded: the generator reports the type.
                return null;
            }
        }

        DuckTypeAotRegistryEmissionResult emission;
        AssemblyLoadContext.Default.Resolving += ResolveApplicationAssembly;
        try
        {
            emission = DuckTypeAotRegistryAssemblyEmitter.Emit(options, artifactPaths, resolution);
        }
        finally
        {
            AssemblyLoadContext.Default.Resolving -= ResolveApplicationAssembly;
        }

        DuckTypeAotArtifactsWriter.WriteAll(artifactPaths, resolution, emission);

        // Loaded from memory: the file stays free for the build to copy.
        var module = ModuleDefMD.Load(File.ReadAllBytes(artifactPaths.OutputAssemblyPath));
        return new CallTargetDuckTypeRegistry(artifactPaths.OutputAssemblyPath, assemblyName, module, emission, resolveType);
    }

    public void RequestRuntimeProxy(TypeSig proxyDefinition, TypeSig target)
    {
        // Generated in the first pass; ConvertType finds them at runtime.
    }

    public DuckProxy Resolve(TypeSig proxyDefinition, TypeSig target)
    {
        if (Describe(proxyDefinition, _resolveType) is not { } proxy || Describe(target, _resolveType) is not { } targetType)
        {
            return DuckProxy.Unavailable($"no DuckType AOT mapping for {proxyDefinition.FullName} and {target.FullName}");
        }

        var key = new DuckTypeAotMapping(proxy.TypeName, proxy.AssemblyName, targetType.TypeName, targetType.AssemblyName, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.CallTarget).Key;
        if (!_results.TryGetValue(key, out var result))
        {
            return DuckProxy.Unavailable($"the DuckType AOT registry has no mapping {key}");
        }

        if (result.ReplaysDynamicFailure || result.Status != DuckTypeAotCompatibilityStatuses.Compatible)
        {
            // Dynamic duck typing fails to create this proxy too (or the generator can't): IntegrationMapper throws.
            return DuckProxy.Failure(result.Detail ?? $"DuckType {result.Status} ({result.DiagnosticCode}) for {key}");
        }

        var proxyType = result.GeneratedProxyTypeName is { } typeName && string.Equals(result.GeneratedProxyAssemblyName, AssemblyName, StringComparison.OrdinalIgnoreCase)
                            ? Module.Find(typeName, isReflectionName: true) ?? Module.Find(typeName, isReflectionName: false)
                            : null;
        if (proxyType is null)
        {
            return DuckProxy.Unavailable($"the proxy of {key} isn't a type of {AssemblyName} ({result.GeneratedProxyAssemblyName}: {result.GeneratedProxyTypeName})");
        }

        // IntegrationMapper.WriteCreateNewProxyInstance: the first public constructor, or the internal one that also receives
        // the type the proxy reports as IDuckType.Type.
        var constructors = proxyType.Methods.Where(m => m.IsInstanceConstructor).ToList();
        var publicConstructor = constructors.FirstOrDefault(m => m.IsPublic && m.MethodSig.Params.Count == 1);
        if (publicConstructor is null)
        {
            return DuckProxy.Unavailable($"the proxy {proxyType.FullName} has no public constructor");
        }

        var instanceParameter = publicConstructor.MethodSig.Params[0];
        var reportingConstructor = constructors.FirstOrDefault(
            m => !m.IsPublic
              && m.MethodSig.Params.Count == 2
              && new SigComparer().Equals(m.MethodSig.Params[0], instanceParameter)
              && m.MethodSig.Params[1].FullName == "System.Type");
        return DuckProxy.Available(proxyType, reportingConstructor ?? publicConstructor);
    }

    /// <summary>
    /// The reflection name, assembly and path of a closed type (generic arguments assembly qualified), plus the
    /// assemblies of its generic arguments. Arrays, pointers and open types can't be named in a mapping yet.
    /// </summary>
    /// <summary>
    /// The assemblies named by the generic arguments of a reflection type name ("Type`1[[Argument, Assembly, …]]"), nested
    /// ones included. Arguments without an assembly are looked up where the type is.
    /// </summary>
    private static IEnumerable<string> GenericArgumentAssemblies(string typeName)
    {
        var start = DuckTypeAotNameHelpers.FindGenericArgumentsStart(typeName);
        if (start < 0 || !DuckTypeAotNameHelpers.TrySplitGenericArguments(typeName, start, out var arguments))
        {
            yield break;
        }

        foreach (var argument in arguments)
        {
            var (argumentType, argumentAssembly) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(argument);
            if (!StringUtil.IsNullOrEmpty(argumentAssembly))
            {
                yield return argumentAssembly;
            }

            foreach (var nested in GenericArgumentAssemblies(argumentType))
            {
                yield return nested;
            }
        }
    }

    private static TypeDescription? Describe(TypeSig type, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        if (type.ContainsGenericParameter)
        {
            return null;
        }

        var assemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var name = Name(type, assemblies, resolveType);
        if (name is null)
        {
            return null;
        }

        var definition = type is GenericInstSig instance ? resolveType(instance.GenericType.TypeDefOrRef) : resolveType(((TypeDefOrRefSig)type).TypeDefOrRef);
        return new TypeDescription(name, definition!.Module.Assembly.Name, definition.Module.Location, assemblies);
    }

    private static string? Name(TypeSig type, IDictionary<string, string> assemblies, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        TypeDef? definition;
        IList<TypeSig> arguments;
        switch (type)
        {
            case GenericInstSig instance:
                definition = resolveType(instance.GenericType.TypeDefOrRef);
                arguments = instance.GenericArguments;
                if (definition is null || definition.GenericParameters.Count != arguments.Count)
                {
                    return null;
                }

                break;
            case TypeDefOrRefSig { TypeDefOrRef: { } reference }:
                definition = resolveType(reference);
                arguments = Array.Empty<TypeSig>();
                if (definition is null || definition.HasGenericParameters)
                {
                    return null;
                }

                break;
            default:
                return null;
        }

        if (definition.Module?.Assembly is not { } assembly || StringUtil.IsNullOrEmpty(definition.Module.Location))
        {
            return null;
        }

        assemblies[assembly.Name] = definition.Module.Location;
        if (arguments.Count == 0)
        {
            return definition.ReflectionFullName;
        }

        var names = new List<string>();
        foreach (var argument in arguments)
        {
            var argumentName = Name(argument, assemblies, resolveType);
            var argumentDefinition = argument is GenericInstSig argumentInstance ? resolveType(argumentInstance.GenericType.TypeDefOrRef) : resolveType(((TypeDefOrRefSig)argument).TypeDefOrRef);
            if (argumentName is null || argumentDefinition is null)
            {
                return null;
            }

            names.Add($"[{argumentName}, {argumentDefinition.Module.Assembly.Name}]");
        }

        return $"{definition.ReflectionFullName}[{string.Join(",", names)}]";
    }

    private sealed class TypeDescription
    {
        public TypeDescription(string typeName, string assemblyName, string path, IReadOnlyDictionary<string, string>? assemblies = null)
        {
            TypeName = typeName;
            AssemblyName = assemblyName;
            Path = path;
            Assemblies = assemblies ?? new Dictionary<string, string> { [assemblyName] = path };
        }

        public string TypeName { get; }

        public string AssemblyName { get; }

        public string Path { get; }

        /// <summary>Gets the assemblies of the type and its generic arguments, by name.</summary>
        public IReadOnlyDictionary<string, string> Assemblies { get; }
    }
}
#endif
