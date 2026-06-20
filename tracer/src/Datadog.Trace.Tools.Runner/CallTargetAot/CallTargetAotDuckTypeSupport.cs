// <copyright file="CallTargetAotDuckTypeSupport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Mono.Cecil;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Plans and emits the DuckType AOT dependency required by CallTarget NativeAOT bindings that use duck-typed
/// instance, parameter, return, or async-result constraints.
/// </summary>
internal static class CallTargetAotDuckTypeSupport
{
    private const string IdDuckTypeFullName = "Datadog.Trace.DuckTyping.IDuckType";
    private const string BootstrapTypeFullName = "Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap";

    /// <summary>
    /// Discovers the DuckType mappings implied by the matched CallTarget bindings, emits the dependent DuckType AOT
    /// registry when needed, and returns the generated proxy metadata required by the CallTarget emitter.
    /// </summary>
    /// <param name="options">The CallTarget generation options.</param>
    /// <param name="artifactPaths">The CallTarget artifact paths used to colocate the dependent DuckType artifacts.</param>
    /// <param name="matchedDefinitions">The matched CallTarget bindings selected for generation.</param>
    /// <returns>The generated DuckType dependency metadata when duck-constrained bindings were discovered; otherwise <see langword="null"/>.</returns>
    internal static CallTargetAotDuckTypeGenerationResult? GenerateIfNeeded(
        CallTargetAotGenerateOptions options,
        CallTargetAotArtifactPaths artifactPaths,
        IReadOnlyList<CallTargetAotMatchedDefinition> matchedDefinitions)
    {
        var plan = BuildPlan(options.TracerAssemblyPath, matchedDefinitions);
        if (plan.Mappings.Count == 0)
        {
            return null;
        }

        var duckArtifacts = CreateDuckTypeArtifactPaths(artifactPaths.OutputAssemblyPath);
        Directory.CreateDirectory(Path.GetDirectoryName(duckArtifacts.MapFilePath) ?? Directory.GetCurrentDirectory());
        WriteCanonicalMapFile(duckArtifacts.MapFilePath, plan.Mappings);

        var duckOptions = new DuckTypeAotGenerateOptions(
            proxyAssemblies: [options.TracerAssemblyPath],
            targetAssemblies: [],
            targetFolders: options.TargetFolders,
            targetFilters: options.TargetFilters,
            mapFile: duckArtifacts.MapFilePath,
            genericInstantiationsFile: null,
            outputPath: duckArtifacts.OutputAssemblyPath,
            assemblyName: duckArtifacts.AssemblyName,
            trimmerDescriptorPath: duckArtifacts.TrimmerDescriptorPath,
            propsPath: duckArtifacts.PropsPath,
            strongNameKeyFile: null,
            discoverMappings: false);

        var mappingResolution = DuckTypeAotMappingResolver.Resolve(duckOptions);
        if (mappingResolution.Errors.Count > 0)
        {
            throw new InvalidOperationException($"The dependent DuckType AOT mappings could not be resolved:{Environment.NewLine}{string.Join(Environment.NewLine, mappingResolution.Errors)}");
        }

        var duckArtifactPaths = DuckTypeAotArtifactPaths.Create(duckOptions);
        var emissionResult = DuckTypeAotRegistryAssemblyEmitter.Emit(duckOptions, duckArtifactPaths, mappingResolution);
        _ = DuckTypeAotArtifactsWriter.WriteAll(duckArtifactPaths, mappingResolution, emissionResult);

        var incompatibleMappings = emissionResult.MappingResultsByKey.Values
                                              .Where(static result => !string.Equals(result.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal))
                                              .ToList();
        if (incompatibleMappings.Count > 0)
        {
            throw new InvalidOperationException(
                "The dependent DuckType AOT registry could not emit compatible proxies for all CallTarget duck-typing bindings:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, incompatibleMappings.Select(static result => $"- {result.Mapping.Key}: {result.Status} ({result.DiagnosticCode}) {result.Detail}")));
        }

        var proxyTypesByMappingKey = emissionResult.MappingResultsByKey.ToDictionary(
            static pair => pair.Key,
            static pair => new CallTargetAotDuckTypeProxyInfo(pair.Value.GeneratedProxyAssemblyName!, pair.Value.GeneratedProxyTypeName!),
            StringComparer.Ordinal);

        return new CallTargetAotDuckTypeGenerationResult(
            new CallTargetAotDuckTypeDependency(
                emissionResult.RegistryAssemblyInfo.OutputAssemblyPath,
                emissionResult.RegistryAssemblyInfo.AssemblyName,
                BootstrapTypeFullName,
                duckArtifacts.PropsPath,
                duckArtifacts.TrimmerDescriptorPath),
            proxyTypesByMappingKey);
    }

    /// <summary>
    /// Computes the exact DuckType proxy mappings implied by the matched CallTarget bindings and annotates the matched
    /// definitions with the canonical mapping keys needed later during CallTarget registry emission.
    /// </summary>
    /// <param name="tracerAssemblyPath">The tracer assembly path that contains the integration definitions and duck proxy contracts.</param>
    /// <param name="matchedDefinitions">The matched CallTarget bindings selected for generation.</param>
    /// <returns>The planned DuckType mappings.</returns>
    private static CallTargetAotDuckTypePlan BuildPlan(string tracerAssemblyPath, IReadOnlyList<CallTargetAotMatchedDefinition> matchedDefinitions)
    {
        using var tracerAssembly = AssemblyDefinition.ReadAssembly(tracerAssemblyPath, new ReaderParameters { ReadSymbols = false });
        var targetAssemblies = new Dictionary<string, AssemblyDefinition>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var mappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);
            foreach (var match in matchedDefinitions)
            {
                if (!targetAssemblies.TryGetValue(match.TargetAssemblyPath, out var targetAssembly))
                {
                    targetAssembly = AssemblyDefinition.ReadAssembly(match.TargetAssemblyPath, new ReaderParameters { ReadSymbols = false });
                    targetAssemblies[match.TargetAssemblyPath] = targetAssembly;
                }

                var integrationType = ResolveType(tracerAssembly.MainModule, match.IntegrationTypeName)
                                      ?? throw new InvalidOperationException($"The integration type '{match.IntegrationTypeName}' could not be resolved from '{tracerAssemblyPath}'.");
                var targetType = ResolveType(targetAssembly.MainModule, match.TargetTypeName)
                                 ?? throw new InvalidOperationException($"The matched target type '{match.TargetTypeName}' could not be resolved from '{match.TargetAssemblyPath}'.");
                var targetMethod = ResolveMatchedMethod(targetType, match)
                                   ?? throw new InvalidOperationException($"The matched target method '{match.TargetTypeName}.{match.TargetMethodName}' could not be resolved from '{match.TargetAssemblyPath}'.");

                AnnotateBeginMappings(match, integrationType, targetType, targetMethod, mappings);
                AnnotateEndMappings(match, integrationType, targetType, targetMethod, mappings);
                AnnotateAsyncMappings(match, integrationType, targetType, targetMethod, mappings);
            }

            return new CallTargetAotDuckTypePlan(mappings.Values.ToList());
        }
        finally
        {
            foreach (var assembly in targetAssemblies.Values)
            {
                assembly.Dispose();
            }
        }
    }

    /// <summary>
    /// Computes duck-typed instance and argument bindings for the selected begin handler shape.
    /// </summary>
    private static void AnnotateBeginMappings(
        CallTargetAotMatchedDefinition match,
        TypeDefinition integrationType,
        TypeDefinition targetType,
        MethodDefinition targetMethod,
        IDictionary<string, DuckTypeAotMapping> mappings)
    {
        var beginMethod = integrationType.Methods.Single(candidate =>
            string.Equals(candidate.Name, "OnMethodBegin", StringComparison.Ordinal) &&
            candidate.Parameters.Count == match.BeginArgumentCount + 1 &&
            candidate.GenericParameters.Count == match.BeginArgumentCount + 1);
        var mustLoadInstance = beginMethod.Parameters.Count != targetMethod.Parameters.Count;
        if (mustLoadInstance)
        {
            match.DuckInstanceMappingKey ??= TryAddMapping(beginMethod.GenericParameters[0], targetType, mappings, ignoreIdDuckTypeConstraint: false);
        }

        match.DuckParameterMappingKeys = Enumerable.Repeat<string?>(null, targetMethod.Parameters.Count).ToList();
        for (var parameterIndex = mustLoadInstance ? 1 : 0; parameterIndex < beginMethod.Parameters.Count; parameterIndex++)
        {
            var generatedParameterIndex = mustLoadInstance ? parameterIndex - 1 : parameterIndex;
            var sourceParameterType = targetMethod.Parameters[generatedParameterIndex].ParameterType;
            var targetParameterType = beginMethod.Parameters[parameterIndex].ParameterType;
            if (targetParameterType is GenericParameter genericParameter)
            {
                var constraint = GetDuckConstraint(beginMethod.GenericParameters[genericParameter.Position], ignoreIdDuckTypeConstraint: true);
                if (constraint is null)
                {
                    continue;
                }

                if (sourceParameterType is ByReferenceType)
                {
                    throw new InvalidOperationException($"DuckType constraints on by-ref CallTarget arguments are not supported for '{match.IntegrationTypeName}' and '{match.TargetTypeName}.{match.TargetMethodName}'.");
                }

                match.DuckParameterMappingKeys[generatedParameterIndex] = AddMapping(constraint, sourceParameterType, mappings);
                continue;
            }

            if (targetParameterType is ByReferenceType byReferenceTargetParameter && byReferenceTargetParameter.ElementType is GenericParameter byReferenceGenericParameter)
            {
                var byRefConstraint = GetDuckConstraint(beginMethod.GenericParameters[byReferenceGenericParameter.Position], ignoreIdDuckTypeConstraint: true);
                if (byRefConstraint is not null)
                {
                    throw new InvalidOperationException($"DuckType constraints on by-ref CallTarget arguments are not supported for '{match.IntegrationTypeName}' and '{match.TargetTypeName}.{match.TargetMethodName}'.");
                }
            }
        }
    }

    /// <summary>
    /// Computes duck-typed instance and return bindings for the selected end handler shape.
    /// </summary>
    private static void AnnotateEndMappings(
        CallTargetAotMatchedDefinition match,
        TypeDefinition integrationType,
        TypeDefinition targetType,
        MethodDefinition targetMethod,
        IDictionary<string, DuckTypeAotMapping> mappings)
    {
        var endMethod = integrationType.Methods.Single(candidate =>
            string.Equals(candidate.Name, "OnMethodEnd", StringComparison.Ordinal) &&
            candidate.Parameters.Count == (match.ReturnsValue ? 4 : 3) &&
            candidate.GenericParameters.Count == (match.ReturnsValue ? 2 : 1));

        if (endMethod.Parameters.Count == (match.ReturnsValue ? 4 : 3))
        {
            match.DuckInstanceMappingKey ??= TryAddMapping(endMethod.GenericParameters[0], targetType, mappings, ignoreIdDuckTypeConstraint: false);
        }

        if (!match.ReturnsValue || endMethod.GenericParameters.Count != 2)
        {
            return;
        }

        var returnConstraint = GetDuckConstraint(endMethod.GenericParameters[1], ignoreIdDuckTypeConstraint: false);
        if (returnConstraint is not null)
        {
            match.DuckReturnMappingKey = AddMapping(returnConstraint, targetMethod.ReturnType, mappings);
        }
    }

    /// <summary>
    /// Computes duck-typed instance and completed async-result bindings for the selected async continuation shape.
    /// </summary>
    private static void AnnotateAsyncMappings(
        CallTargetAotMatchedDefinition match,
        TypeDefinition integrationType,
        TypeDefinition targetType,
        MethodDefinition targetMethod,
        IDictionary<string, DuckTypeAotMapping> mappings)
    {
        if (!match.RequiresAsyncContinuation)
        {
            return;
        }

        var asyncMethod = integrationType.Methods.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, "OnAsyncMethodEnd", StringComparison.Ordinal) &&
            candidate.Parameters.Count == 4 &&
            candidate.GenericParameters.Count == (match.AsyncResultTypeName is null ? 1 : 2));
        if (asyncMethod is null)
        {
            return;
        }

        match.DuckInstanceMappingKey ??= TryAddMapping(asyncMethod.GenericParameters[0], targetType, mappings, ignoreIdDuckTypeConstraint: false);
        if (match.AsyncResultTypeName is null || asyncMethod.GenericParameters.Count != 2)
        {
            return;
        }

        var asyncConstraint = GetDuckConstraint(asyncMethod.GenericParameters[1], ignoreIdDuckTypeConstraint: false);
        if (asyncConstraint is null)
        {
            return;
        }

        var asyncResultType = (targetMethod.ReturnType as GenericInstanceType)?.GenericArguments.FirstOrDefault()
                              ?? throw new InvalidOperationException($"The async result type for '{match.TargetTypeName}.{match.TargetMethodName}' could not be resolved from '{targetMethod.ReturnType.FullName}'.");
        match.DuckAsyncResultMappingKey = AddMapping(asyncConstraint, asyncResultType, mappings);
    }

    /// <summary>
    /// Adds a canonical forward DuckType mapping for the supplied proxy contract and runtime target type.
    /// </summary>
    private static string AddMapping(TypeReference proxyDefinitionType, TypeReference targetType, IDictionary<string, DuckTypeAotMapping> mappings)
    {
        var proxyAssemblyName = ResolveAssemblyName(proxyDefinitionType);
        var targetAssemblyName = ResolveAssemblyName(targetType);
        var mapping = new DuckTypeAotMapping(
            proxyDefinitionType.FullName,
            proxyAssemblyName,
            targetType.FullName,
            targetAssemblyName,
            DuckTypeAotMappingMode.Forward,
            DuckTypeAotMappingSource.MapFile);
        mappings[mapping.Key] = mapping;
        return mapping.Key;
    }

    /// <summary>
    /// Adds a canonical forward DuckType mapping when the selected generic parameter has a duck-typing constraint.
    /// </summary>
    private static string? TryAddMapping(GenericParameter genericParameter, TypeReference targetType, IDictionary<string, DuckTypeAotMapping> mappings, bool ignoreIdDuckTypeConstraint)
    {
        var constraint = GetDuckConstraint(genericParameter, ignoreIdDuckTypeConstraint);
        return constraint is null ? null : AddMapping(constraint, targetType, mappings);
    }

    /// <summary>
    /// Resolves the duck-typing contract type from the selected generic parameter using the same rule as the runtime
    /// mapper: instance and return generics use the first constraint, while argument generics ignore <c>IDuckType</c>.
    /// </summary>
    private static TypeReference? GetDuckConstraint(GenericParameter genericParameter, bool ignoreIdDuckTypeConstraint)
    {
        foreach (var constraint in genericParameter.Constraints)
        {
            var constraintType = constraint.ConstraintType;
            if (ignoreIdDuckTypeConstraint && string.Equals(constraintType.FullName, IdDuckTypeFullName, StringComparison.Ordinal))
            {
                continue;
            }

            return constraintType;
        }

        return null;
    }

    /// <summary>
    /// Resolves the simple assembly name for a Cecil type reference.
    /// </summary>
    private static string ResolveAssemblyName(TypeReference typeReference)
    {
        if (typeReference.Scope is AssemblyNameReference assemblyReference)
        {
            return DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyReference.Name);
        }

        if (typeReference.Module?.Assembly?.Name?.Name is { Length: > 0 } moduleAssemblyName)
        {
            return DuckTypeAotNameHelpers.NormalizeAssemblyName(moduleAssemblyName);
        }

        return DuckTypeAotNameHelpers.NormalizeAssemblyName(typeReference.Resolve()?.Module.Assembly.Name.Name ?? string.Empty);
    }

    /// <summary>
    /// Resolves a type by full name from the supplied module and its exported forwards.
    /// </summary>
    private static TypeDefinition? ResolveType(ModuleDefinition module, string fullName)
    {
        var typeDefinition = module.Types.FirstOrDefault(type => string.Equals(type.FullName, fullName, StringComparison.Ordinal));
        return typeDefinition ?? module.ExportedTypes.FirstOrDefault(type => string.Equals(type.FullName, fullName, StringComparison.Ordinal))?.Resolve();
    }

    /// <summary>
    /// Resolves the concrete matched target method from the selected target type.
    /// </summary>
    private static MethodDefinition? ResolveMatchedMethod(TypeDefinition targetType, CallTargetAotMatchedDefinition match)
    {
        return targetType.Methods.FirstOrDefault(method =>
            string.Equals(method.Name, match.TargetMethodName, StringComparison.Ordinal) &&
            string.Equals(method.ReturnType.FullName, match.ReturnTypeName, StringComparison.Ordinal) &&
            method.Parameters.Count == match.ParameterTypeNames.Count &&
            method.Parameters.Select(static parameter => parameter.ParameterType.FullName).SequenceEqual(match.ParameterTypeNames, StringComparer.Ordinal));
    }

    /// <summary>
    /// Creates the companion DuckType artifact paths colocated with the CallTarget registry output.
    /// </summary>
    private static CallTargetAotDuckTypeArtifactPaths CreateDuckTypeArtifactPaths(string callTargetOutputAssemblyPath)
    {
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(callTargetOutputAssemblyPath)) ?? Directory.GetCurrentDirectory();
        var callTargetAssemblyName = Path.GetFileNameWithoutExtension(callTargetOutputAssemblyPath) ?? "Datadog.Trace.CallTarget.AotRegistry";
        var duckAssemblyName = callTargetAssemblyName + ".DuckType";
        return new CallTargetAotDuckTypeArtifactPaths(
            duckAssemblyName,
            Path.Combine(outputDirectory, duckAssemblyName + ".dll"),
            Path.Combine(outputDirectory, duckAssemblyName + ".props"),
            Path.Combine(outputDirectory, duckAssemblyName + ".linker.xml"),
            Path.Combine(outputDirectory, duckAssemblyName + ".map.json"));
    }

    /// <summary>
    /// Writes the canonical DuckType map file consumed by the existing DuckType AOT resolver.
    /// </summary>
    private static void WriteCanonicalMapFile(string path, IReadOnlyList<DuckTypeAotMapping> mappings)
    {
        var document = new
        {
            schemaVersion = "1",
            mappings = mappings.Select(static mapping => new
            {
                mode = mapping.Mode == DuckTypeAotMappingMode.Reverse ? "reverse" : "forward",
                proxyType = mapping.ProxyTypeName,
                proxyAssembly = mapping.ProxyAssemblyName,
                targetType = mapping.TargetTypeName,
                targetAssembly = mapping.TargetAssemblyName,
            }).ToList(),
        };

        File.WriteAllText(path, JsonConvert.SerializeObject(document, Formatting.Indented));
    }
}
