// <copyright file="CallTargetAotMethodMatcher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.ClrProfiler;
using Mono.Cecil;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Matches concrete CallTarget definitions against candidate target assemblies using Cecil metadata.
/// </summary>
internal static class CallTargetAotMethodMatcher
{
    private const string CompatibleStatus = "compatible";
    private const string IncompatibleStatus = "incompatible";

    /// <summary>
    /// Matches the supplied definitions against the candidate target assemblies selected by the generation options.
    /// </summary>
    /// <param name="definitions">The discovered CallTarget definitions.</param>
    /// <param name="options">The generation options that define the candidate target assemblies.</param>
    /// <returns>The evaluated target methods, including unsupported bindings and their diagnostics.</returns>
    internal static List<CallTargetAotMatchedDefinition> Match(IReadOnlyList<CallTargetAotDefinition> definitions, CallTargetAotGenerateOptions options)
    {
        var matches = new List<CallTargetAotMatchedDefinition>();
        foreach (var assemblyPath in EnumerateCandidateAssemblies(options.TargetFolders, options.TargetFilters))
        {
            using var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = false });
            var assemblyVersion = assemblyDefinition.Name.Version ?? new Version(0, 0, 0, 0);
            var module = assemblyDefinition.MainModule;

            foreach (var definition in definitions)
            {
                if (!string.Equals(assemblyDefinition.Name.Name, definition.TargetAssemblyName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (assemblyVersion < definition.MinimumVersion || assemblyVersion > definition.MaximumVersion)
                {
                    continue;
                }

                foreach (var targetType in ResolveCandidateTypes(module, definition))
                {
                    foreach (var targetMethod in targetType.Methods.Where(method => string.Equals(method.Name, definition.TargetMethodName, StringComparison.Ordinal)))
                    {
                        if (!SignatureMatches(targetMethod, definition))
                        {
                            continue;
                        }

                        var returnsValue = !string.Equals(targetMethod.ReturnType.FullName, "System.Void", StringComparison.Ordinal);
                        var (requiresAsyncContinuation, asyncResultTypeName) = GetAsyncContinuationShape(targetMethod.ReturnType);
                        var usesSlowBegin = targetMethod.Parameters.Count > 8;
                        var evaluation = new CallTargetAotMatchedDefinition
                        {
                            IsSupported = true,
                            Status = CompatibleStatus,
                            TargetAssemblyName = assemblyDefinition.Name.Name,
                            TargetAssemblyPath = Path.GetFullPath(assemblyPath),
                            TargetTypeName = targetType.FullName,
                            TargetMethodName = targetMethod.Name,
                            ReturnTypeName = targetMethod.ReturnType.FullName,
                            ParameterTypeNames = targetMethod.Parameters.Select(static parameter => parameter.ParameterType.FullName).ToList(),
                            IntegrationTypeName = definition.IntegrationTypeName,
                            BeginArgumentCount = targetMethod.Parameters.Count,
                            UsesSlowBegin = usesSlowBegin,
                            ReturnsValue = returnsValue,
                            HandlerKind = $"{(usesSlowBegin ? "BeginSlow" : $"Begin{targetMethod.Parameters.Count}")}{(returnsValue ? "EndReturn" : "EndVoid")}",
                            RequiresAsyncContinuation = requiresAsyncContinuation,
                            AsyncResultTypeName = asyncResultTypeName,
                        };

                        var unsupportedDiagnostic = GetUnsupportedHandlerShapeDiagnostic(targetMethod);
                        if (unsupportedDiagnostic is not null)
                        {
                            evaluation.IsSupported = false;
                            evaluation.Status = IncompatibleStatus;
                            evaluation.DiagnosticCode = unsupportedDiagnostic.Value.Code;
                            evaluation.DiagnosticMessage = unsupportedDiagnostic.Value.Message;
                        }

                        matches.Add(evaluation);

                        break;
                    }
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// Enumerates the candidate target assemblies selected by the generation options.
    /// </summary>
    /// <param name="targetFolders">The target folders to scan.</param>
    /// <param name="targetFilters">The file globs to apply in each target folder.</param>
    /// <returns>The candidate target assembly paths.</returns>
    private static IEnumerable<string> EnumerateCandidateAssemblies(IReadOnlyList<string> targetFolders, IReadOnlyList<string> targetFilters)
    {
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var targetFolder in targetFolders)
        {
            foreach (var targetFilter in targetFilters)
            {
                foreach (var assemblyPath in Directory.EnumerateFiles(targetFolder, targetFilter, SearchOption.TopDirectoryOnly))
                {
                    if (!string.Equals(Path.GetExtension(assemblyPath), ".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (seenPaths.Add(Path.GetFullPath(assemblyPath)))
                    {
                        yield return assemblyPath;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resolves a default-kind target type from the module or its exported type forwards.
    /// </summary>
    /// <param name="module">The module to search.</param>
    /// <param name="targetTypeName">The fully qualified target type name.</param>
    /// <returns>The resolved target type, if found.</returns>
    private static TypeDefinition? ResolveDefaultType(ModuleDefinition module, string targetTypeName)
    {
        var typeDefinition = module.Types.FirstOrDefault(type => string.Equals(type.FullName, targetTypeName, StringComparison.Ordinal));
        if (typeDefinition is not null)
        {
            return typeDefinition;
        }

        if (!module.HasExportedTypes)
        {
            return null;
        }

        var exportedType = module.ExportedTypes.FirstOrDefault(type => string.Equals(type.FullName, targetTypeName, StringComparison.Ordinal));
        return exportedType?.Resolve();
    }

    /// <summary>
    /// Resolves the candidate concrete types for the supplied CallTarget kind.
    /// </summary>
    /// <param name="module">The module being scanned.</param>
    /// <param name="definition">The normalized CallTarget definition.</param>
    /// <returns>The concrete target types that should be inspected for matching methods.</returns>
    private static IEnumerable<TypeDefinition> ResolveCandidateTypes(ModuleDefinition module, CallTargetAotDefinition definition)
    {
        switch (definition.Kind)
        {
            case CallTargetKind.Default:
                var defaultType = ResolveDefaultType(module, definition.TargetTypeName);
                if (defaultType is not null)
                {
                    yield return defaultType;
                }

                yield break;

            case CallTargetKind.Derived:
                foreach (var candidateType in EnumerateAllTypes(module).Where(type => InheritsFrom(type, definition.TargetTypeName)))
                {
                    yield return candidateType;
                }

                yield break;

            case CallTargetKind.Interface:
                foreach (var candidateType in EnumerateAllTypes(module).Where(type => ImplementsInterface(type, definition.TargetTypeName)))
                {
                    yield return candidateType;
                }

                yield break;

            default:
                yield break;
        }
    }

    /// <summary>
    /// Enumerates every top-level and nested type defined in the module.
    /// </summary>
    private static IEnumerable<TypeDefinition> EnumerateAllTypes(ModuleDefinition module)
    {
        foreach (var type in module.Types)
        {
            foreach (var nestedType in EnumerateTypeAndNestedTypes(type))
            {
                yield return nestedType;
            }
        }
    }

    /// <summary>
    /// Enumerates the supplied type and all nested types recursively.
    /// </summary>
    private static IEnumerable<TypeDefinition> EnumerateTypeAndNestedTypes(TypeDefinition type)
    {
        yield return type;
        foreach (var nestedType in type.NestedTypes)
        {
            foreach (var descendant in EnumerateTypeAndNestedTypes(nestedType))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the supplied type inherits from the requested base type anywhere in its base chain.
    /// </summary>
    private static bool InheritsFrom(TypeDefinition type, string targetTypeName)
    {
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        var baseType = type.BaseType;
        while (baseType is not null && seenTypes.Add(baseType.FullName))
        {
            if (string.Equals(baseType.FullName, targetTypeName, StringComparison.Ordinal))
            {
                return true;
            }

            try
            {
                baseType = baseType.Resolve()?.BaseType;
            }
            catch
            {
                break;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the supplied type implements the requested interface directly, through inherited
    /// interfaces, or through its base type chain.
    /// </summary>
    private static bool ImplementsInterface(TypeDefinition type, string targetTypeName)
    {
        var pendingTypes = new Stack<TypeDefinition>();
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        pendingTypes.Push(type);

        while (pendingTypes.Count > 0)
        {
            var currentType = pendingTypes.Pop();
            if (!seenTypes.Add(currentType.FullName))
            {
                continue;
            }

            foreach (var interfaceImplementation in currentType.Interfaces)
            {
                if (string.Equals(interfaceImplementation.InterfaceType.FullName, targetTypeName, StringComparison.Ordinal))
                {
                    return true;
                }

                try
                {
                    var resolvedInterface = interfaceImplementation.InterfaceType.Resolve();
                    if (resolvedInterface is not null)
                    {
                        pendingTypes.Push(resolvedInterface);
                    }
                }
                catch
                {
                    // Ignore resolution failures and continue scanning the remaining type graph.
                }
            }

            try
            {
                var resolvedBaseType = currentType.BaseType?.Resolve();
                if (resolvedBaseType is not null)
                {
                    pendingTypes.Push(resolvedBaseType);
                }
            }
            catch
            {
                // Ignore resolution failures and continue scanning the remaining type graph.
            }
        }

        return false;
    }

    /// <summary>
    /// Applies the exact signature comparison rules used by the legacy AOT matcher.
    /// </summary>
    /// <param name="targetMethod">The candidate target method.</param>
    /// <param name="definition">The normalized CallTarget definition.</param>
    /// <returns><see langword="true"/> when the signature matches; otherwise <see langword="false"/>.</returns>
    private static bool SignatureMatches(MethodDefinition targetMethod, CallTargetAotDefinition definition)
    {
        if (!string.Equals(GetComparableTypeName(targetMethod.ReturnType), definition.ReturnTypeName, StringComparison.Ordinal))
        {
            return false;
        }

        if (targetMethod.Parameters.Count != definition.ParameterTypeNames.Count)
        {
            return false;
        }

        for (var i = 0; i < targetMethod.Parameters.Count; i++)
        {
            var expectedParameterType = definition.ParameterTypeNames[i];
            if (expectedParameterType == "_")
            {
                continue;
            }

            if (!string.Equals(GetComparableTypeName(targetMethod.Parameters[i].ParameterType), expectedParameterType, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Converts Cecil type references into the stable metadata notation used by InstrumentMethodAttribute definitions.
    /// </summary>
    /// <param name="typeReference">The Cecil type reference to normalize.</param>
    /// <returns>The comparable type name used by the NativeAOT matcher.</returns>
    private static string GetComparableTypeName(TypeReference typeReference)
    {
        switch (typeReference)
        {
            case GenericParameter genericParameter when genericParameter.Type == GenericParameterType.Method:
                return $"!!{genericParameter.Position}";

            case GenericParameter genericParameter:
                return $"!{genericParameter.Position}";

            case ByReferenceType byReferenceType:
                return GetComparableTypeName(byReferenceType.ElementType) + "&";

            case GenericInstanceType genericInstanceType:
                return $"{genericInstanceType.ElementType.FullName}<{string.Join(",", genericInstanceType.GenericArguments.Select(GetComparableTypeName))}>";

            default:
                return typeReference.FullName;
        }
    }

    /// <summary>
    /// Restricts generation to instance methods that can use either the direct begin handlers or the slow-begin
    /// object-array fallback together with the current end adapter families.
    /// </summary>
    /// <param name="targetMethod">The candidate target method.</param>
    /// <param name="definition">The normalized CallTarget definition.</param>
    /// <returns><see langword="true"/> when the match can use the current generated adapter set; otherwise <see langword="false"/>.</returns>
    private static (string Code, string Message)? GetUnsupportedHandlerShapeDiagnostic(MethodDefinition targetMethod)
    {
        if (targetMethod.IsStatic ||
            targetMethod.HasGenericParameters ||
            targetMethod.ReturnType is ByReferenceType)
        {
            if (targetMethod.IsStatic)
            {
                return ("CTAOT001", "Static target methods are not supported by the NativeAOT CallTarget adapter generator.");
            }

            if (targetMethod.HasGenericParameters)
            {
                return ("CTAOT002", "Generic target methods are not supported by the NativeAOT CallTarget adapter generator.");
            }

            return ("CTAOT003", "Target methods with by-ref return values are not supported by the NativeAOT CallTarget adapter generator.");
        }

        if (targetMethod.Parameters.Any(static parameter => parameter.ParameterType is ByReferenceType))
        {
            return ("CTAOT004", "Target methods with by-ref parameters are not supported by the NativeAOT CallTarget adapter generator.");
        }

        return null;
    }

    /// <summary>
    /// Classifies Task and ValueTask target returns into the async continuation shape required by the generator.
    /// </summary>
    /// <param name="returnType">The matched target method return type.</param>
    /// <returns>A flag indicating whether async continuation generation is required and the optional typed result name.</returns>
    private static (bool RequiresAsyncContinuation, string? AsyncResultTypeName) GetAsyncContinuationShape(TypeReference returnType)
    {
        if (string.Equals(returnType.FullName, "System.Threading.Tasks.Task", StringComparison.Ordinal) ||
            string.Equals(returnType.FullName, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal))
        {
            return (true, null);
        }

        if (returnType is GenericInstanceType genericInstanceType)
        {
            var genericTypeName = genericInstanceType.ElementType.FullName;
            if ((string.Equals(genericTypeName, "System.Threading.Tasks.Task`1", StringComparison.Ordinal) ||
                 string.Equals(genericTypeName, "System.Threading.Tasks.ValueTask`1", StringComparison.Ordinal)) &&
                genericInstanceType.GenericArguments.Count == 1)
            {
                return (true, genericInstanceType.GenericArguments[0].FullName);
            }
        }

        return (false, null);
    }
}
