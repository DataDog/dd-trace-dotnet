// <copyright file="DuckTypeAotRegistryAssemblyEmitter.TypeBinding.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Type binding: binds the members of a proxy type to its target type (properties, fields, methods, [DuckCopy] struct fields,
    /// reverse implementations) and checks the result with dynamic duck typing in the generator's process, whose member
    /// selection and failures the registry reproduces.
    /// </summary>
    internal static partial class DuckTypeAotRegistryAssemblyEmitter
    {
        /// <summary>
        /// Defines the status code property cant be written constant.
        /// </summary>
        private const string StatusCodePropertyCantBeWritten = "DTAOT0212";

        /// <summary>
        /// Defines the status code reverse custom attribute named arguments constant.
        /// </summary>
        private const string StatusCodeCustomAttributeNamedArguments = "DTAOT0214";

        /// <summary>
        /// Defines named constants for forward binding kind.
        /// </summary>
        private enum ForwardBindingKind
        {
            /// <summary>
            /// Represents method.
            /// </summary>
            Method,

            /// <summary>
            /// Represents field get.
            /// </summary>
            FieldGet,

            /// <summary>
            /// Represents field set.
            /// </summary>
            FieldSet
        }

        /// <summary>
        /// Defines named constants for struct copy source kind.
        /// </summary>
        private enum StructCopySourceKind
        {
            /// <summary>
            /// Represents property.
            /// </summary>
            Property,

            /// <summary>
            /// Represents field.
            /// </summary>
            Field
        }

        /// <summary>
        /// Validates a successful metadata plan against the dynamic engine before emitting IL.
        /// </summary>
        /// <param name="moduleDef">The generated module.</param>
        /// <param name="mapping">The mapping to validate.</param>
        /// <param name="proxyType">The proxy definition.</param>
        /// <param name="targetType">The target definition.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name.</param>
        /// <param name="evaluatedByDynamicEngine">Whether dynamic duck typing could evaluate the mapping in the generator.</param>
        /// <returns>The dynamic validation failure, or null when no failure was resolved.</returns>
        private static DuckTypeAotMappingEmissionResult? ValidateMappingWithDynamicEngine(
            ModuleDef moduleDef,
            DuckTypeAotMapping mapping,
            TypeDef proxyType,
            TypeDef targetType,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            out bool evaluatedByDynamicEngine)
        {
            evaluatedByDynamicEngine = false;
            if (mapping.Mode == DuckTypeAotMappingMode.Reverse)
            {
                var attributePlan = GetOrCreateReverseCustomAttributePlan(moduleDef, targetType);
                if (!attributePlan.Succeeded)
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(mapping, attributePlan.FailureStatus!, attributePlan.FailureDiagnosticCode!, attributePlan.FailureDetail!);
                }
            }

            // Standalone AOT contracts can declare metadata equivalents of the tracer's internal attributes.
            // Reflection in the dynamic engine cannot bind those attributes by type identity.
            if (UsesMetadataOnlyDuckAttributes(mapping, proxyType, targetType))
            {
                _currentExecutionContext?.MetadataOnlyEvaluations.Add(mapping.Key);
                return null;
            }

            // The proxy type dynamic duck typing creates: a mapping it can't create fails in the registry too, even when the
            // AOT emitter could bind it (e.g. Reflection.Emit can't create the type).
            var outcome = GetDynamicCreationOutcome(mapping, proxyAssemblyPathsByName, targetAssemblyPathsByName, out var exceptionType, out var exceptionMessage);
            evaluatedByDynamicEngine = outcome != DynamicCreationOutcome.Unavailable;
            if (outcome != DynamicCreationOutcome.Failed)
            {
                return null;
            }

            // The generic DuckTypeException is the one Reflection.Emit failures to create the type are wrapped in.
            var cantCreateType = exceptionType == typeof(DuckTypeException) &&
                                 exceptionMessage?.StartsWith(CreatingDuckTypeFailurePrefix, StringComparison.Ordinal) == true;
            return DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                cantCreateType ? DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind : DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                cantCreateType ? StatusCodeUnloadableProxyType : StatusCodeIncompatibleSignature,
                $"Dynamic duck typing can't create the proxy: {exceptionType?.Name}: {exceptionMessage}");
        }

        /// <summary>
        /// Gets where a type hierarchy uses duck attributes defined outside the tracer assembly, which dynamic duck typing
        /// doesn't read: on the types and their members, or only on the types of their members.
        /// </summary>
        /// <param name="type">The type definition to inspect.</param>
        /// <returns>Where the hierarchy uses such attributes.</returns>
        private static MetadataOnlyDuckAttributes GetMetadataOnlyDuckAttributes(TypeDef type)
        {
            var hierarchy = new List<TypeDef>();
            var pending = new Stack<TypeDef>();
            var visited = new HashSet<TypeDef>();
            pending.Push(type);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                hierarchy.Add(current);
                if (current.BaseType?.ResolveTypeDef() is { } baseType)
                {
                    pending.Push(baseType);
                }

                foreach (var implementedInterface in current.Interfaces)
                {
                    if (implementedInterface.Interface.ResolveTypeDef() is { } interfaceType)
                    {
                        pending.Push(interfaceType);
                    }
                }
            }

            foreach (var current in hierarchy)
            {
                var attributes = current.CustomAttributes
                                        .Concat(current.Methods.SelectMany(method => method.CustomAttributes))
                                        .Concat(current.Properties.SelectMany(property => property.CustomAttributes))
                                        .Concat(current.Fields.SelectMany(field => field.CustomAttributes));
                if (attributes.Any(IsMetadataOnlyDuckAttribute))
                {
                    return MetadataOnlyDuckAttributes.Own;
                }
            }

            foreach (var current in hierarchy)
            {
                // E.g. the [DuckCopy] struct a member is duck chained to.
                var memberTypes = current.Methods.SelectMany(method => method.MethodSig.Params.Concat(new[] { method.MethodSig.RetType }))
                                         .Concat(current.Fields.Select(field => field.FieldSig.Type));
                foreach (var memberType in memberTypes.SelectMany(GetSignatureTypes))
                {
                    if (memberType.ResolveTypeDef() is { } memberTypeDef && memberTypeDef.CustomAttributes.Any(IsMetadataOnlyDuckAttribute))
                    {
                        return MetadataOnlyDuckAttributes.MemberTypes;
                    }
                }
            }

            return MetadataOnlyDuckAttributes.None;
        }

        /// <summary>
        /// Determines whether an attribute is a duck typing attribute declared by another assembly than Datadog.Trace, which
        /// dynamic duck typing doesn't read (standalone contracts can declare their own).
        /// </summary>
        /// <param name="attribute">The custom attribute.</param>
        /// <returns>true if the attribute is a metadata-only duck typing attribute; otherwise, false.</returns>
        private static bool IsMetadataOnlyDuckAttribute(CustomAttribute attribute)
        {
            return IsDuckAttributeReadByType(attribute.TypeFullName) &&
                   !string.Equals(attribute.AttributeType.DefinitionAssembly?.Name.String, DatadogTraceAssemblyName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Enumerates the types a signature type is made of (element types, generic types and arguments).
        /// </summary>
        /// <param name="typeSig">The signature type.</param>
        /// <returns>The types.</returns>
        private static IEnumerable<ITypeDefOrRef> GetSignatureTypes(TypeSig? typeSig)
        {
            switch (typeSig)
            {
                case GenericInstSig genericInstSig:
                    foreach (var type in GetSignatureTypes(genericInstSig.GenericType).Concat(genericInstSig.GenericArguments.SelectMany(GetSignatureTypes)))
                    {
                        yield return type;
                    }

                    break;
                case TypeDefOrRefSig typeDefOrRefSig:
                    yield return typeDefOrRefSig.TypeDefOrRef;
                    break;
                case NonLeafSig nonLeafSig:
                    foreach (var type in GetSignatureTypes(nonLeafSig.Next))
                    {
                        yield return type;
                    }

                    break;
            }
        }

        /// <summary>
        /// Gets what dynamic duck typing does with a mapping: it binds the members and creates the proxy type like dynamic duck
        /// typing does when a proxy is first requested, so the outcome also covers the shapes Reflection.Emit can't create.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="exceptionType">The exception type dynamic duck typing throws, when it fails.</param>
        /// <param name="exceptionMessage">The exception message dynamic duck typing throws, when it fails.</param>
        /// <returns>The outcome of the mapping in dynamic duck typing.</returns>
        private static DynamicCreationOutcome GetDynamicCreationOutcome(
            DuckTypeAotMapping mapping,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            out Type? exceptionType,
            out string? exceptionMessage)
        {
            exceptionType = null;
            exceptionMessage = null;
            if (_currentExecutionContext?.TryGetFailureProbe(mapping.Key, out var cachedFailureProbe) == true)
            {
                exceptionType = cachedFailureProbe.ExceptionType;
                exceptionMessage = cachedFailureProbe.ExceptionMessage;
                return cachedFailureProbe.Outcome;
            }

            var firstProbeWarning = _currentExecutionContext?.Warnings.Count ?? 0;
            var phaseStopwatch = StartProfilePhase();
            var outcome = DynamicCreationOutcome.Unavailable;
            if (proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath) &&
                targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) &&
                TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) &&
                TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) &&
                proxyRuntimeType is not null &&
                targetRuntimeType is not null)
            {
                outcome = GetDynamicCreationOutcome(mapping, proxyRuntimeType, targetRuntimeType, out exceptionType, out exceptionMessage);
            }
            else
            {
                // E.g. an application targeting a newer runtime than the generator runs on: every selection of the mapping falls
                // back to metadata, which approximates dynamic duck typing.
                _currentExecutionContext?.MetadataOnlyEvaluations.Add(mapping.Key);
                _currentExecutionContext?.Warnings.Add(
                    $"Mapping '{mapping.Key}' couldn't be evaluated with dynamic duck typing in the generator, because its types couldn't be loaded: its members are bound from metadata, which may differ from dynamic duck typing. Run the generator on a runtime that can load the application's assemblies.");
            }

            _currentExecutionContext?.CacheFailureProbe(mapping.Key, outcome, exceptionType, exceptionMessage, firstProbeWarning);
            StopProfilePhase(
                phaseStopwatch,
                seconds =>
                {
                    _currentProfile!.DynamicFailureProbeSeconds += seconds;
                    _currentProfile!.DynamicFailureProbeCount++;
                });
            return outcome;
        }

        /// <summary>
        /// Gets what dynamic duck typing does with the runtime types of a mapping (see <see cref="GetDynamicCreationOutcome(DuckTypeAotMapping, IReadOnlyDictionary{string, string}, IReadOnlyDictionary{string, string}, out Type?, out string?)"/>).
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyRuntimeType">The runtime proxy type.</param>
        /// <param name="targetRuntimeType">The runtime target type.</param>
        /// <param name="exceptionType">The exception type dynamic duck typing throws, when it fails.</param>
        /// <param name="exceptionMessage">The exception message dynamic duck typing throws, when it fails.</param>
        /// <param name="forwardShapeKey">The shape of the forward proxy type, when it isn't the one of the target type.</param>
        /// <returns>The outcome of the mapping in dynamic duck typing.</returns>
        private static DynamicCreationOutcome GetDynamicCreationOutcome(
            DuckTypeAotMapping mapping,
            Type proxyRuntimeType,
            Type targetRuntimeType,
            out Type? exceptionType,
            out string? exceptionMessage,
            object? forwardShapeKey = null)
        {
            exceptionType = null;
            exceptionMessage = null;
            if (_currentExecutionContext?.TryGetFailureProbe(mapping.Key, out var cachedFailureProbe) == true)
            {
                exceptionType = cachedFailureProbe.ExceptionType;
                exceptionMessage = cachedFailureProbe.ExceptionMessage;
                return cachedFailureProbe.Outcome;
            }

            DynamicCreationOutcome outcome;
            var firstProbeWarning = _currentExecutionContext?.Warnings.Count ?? 0;
            var reverse = mapping.Mode == DuckTypeAotMappingMode.Reverse;
            var exception = DuckType.GetDynamicProxyTypeFailureForAot(proxyRuntimeType, targetRuntimeType, reverse, dryRun: true);
            var forwardShape = reverse ? null : Tuple.Create(proxyRuntimeType, forwardShapeKey ?? DuckType.GetForwardProxyShapeKeyForAot(targetRuntimeType));
            if (exception is null &&
                (forwardShape is null || _currentExecutionContext?.CreatableForwardProxyShapes.Contains(forwardShape) != true))
            {
                // The members bind: create the type, which Reflection.Emit can still fail to do. Forward proxy types of the same
                // shape (the proxy's members, and what they take from the target, see GetForwardProxyShapeKeyForAot) are created
                // once. A reverse proxy overrides the members its delegation type implements, so each pair is created.
                exception = DuckType.GetDynamicProxyTypeFailureForAot(proxyRuntimeType, targetRuntimeType, reverse, dryRun: false, out var dynamicProxyType);
                if (exception is null && forwardShape is not null)
                {
                    _currentExecutionContext?.CreatableForwardProxyShapes.Add(forwardShape);
                }

                // The reverse proxy type dynamic duck typing creates, whose forward proxies the generator checks too.
                if (reverse && dynamicProxyType is not null && _currentExecutionContext is { } context)
                {
                    context.DynamicReverseProxyTypes[mapping.Key] = dynamicProxyType;
                }
            }

            if (exception is null)
            {
                outcome = DynamicCreationOutcome.Created;
            }
            else if (IsGeneratorEnvironmentFailure(exception, proxyRuntimeType, targetRuntimeType))
            {
                // Not what dynamic duck typing does in the application: the generator process misses something it needs.
                outcome = DynamicCreationOutcome.Unavailable;
                _currentExecutionContext?.MetadataOnlyEvaluations.Add(mapping.Key);
                _currentExecutionContext?.Warnings.Add(
                    $"Mapping '{mapping.Key}' couldn't be evaluated with dynamic duck typing in the generator, so it is checked against metadata only: {exception.GetBaseException().GetType().Name}: {exception.GetBaseException().Message}");
            }
            else
            {
                outcome = DynamicCreationOutcome.Failed;
                exceptionType = exception.GetType();
                exceptionMessage = exception.Message;
                if (exception.InnerException is not null && _currentExecutionContext is { } failureContext)
                {
                    // E.g. the TypeLoadException of Reflection.Emit a DuckTypeException wraps (and the exceptions it wraps): the
                    // registry replays them too.
                    var innerExceptions = new List<KeyValuePair<string, string>>();
                    for (var innerException = exception.InnerException; innerException?.GetType().FullName is { } innerExceptionTypeName; innerException = innerException.InnerException)
                    {
                        innerExceptions.Add(new KeyValuePair<string, string>(innerExceptionTypeName, innerException.Message));
                    }

                    failureContext.DynamicFailureInnerExceptions[mapping.Key] = innerExceptions;
                }

                if (TryGetMissingTypeAssemblyName(exception, out var missingTypeAssemblyName))
                {
                    // Type names in duck attributes are looked up in the loaded assemblies: the failure is replayed, but the
                    // application may have the assembly the generator doesn't, so the registry doesn't vouch for it.
                    _currentExecutionContext?.UnverifiedDynamicFailures.Add(mapping.Key);
                    _currentExecutionContext?.Warnings.Add(
                        $"Mapping '{mapping.Key}' fails in dynamic duck typing in the generator because a type isn't found ({exceptionMessage}), and '{missingTypeAssemblyName}' isn't one of the generator's input assemblies: the registry replays the failure, but if the application has that assembly, add it to the inputs.");
                }
            }

            _currentExecutionContext?.CacheFailureProbe(mapping.Key, outcome, exceptionType, exceptionMessage, firstProbeWarning);
            return outcome;
        }

        /// <summary>
        /// Determines whether dynamic duck typing failed because of the generator process (an assembly it can't load), not
        /// because of the mapping.
        /// </summary>
        /// <param name="exception">The exception dynamic duck typing threw.</param>
        /// <param name="proxyRuntimeType">The proxy definition type (the type to derive from for a reverse proxy).</param>
        /// <param name="targetRuntimeType">The target type (the delegation type for a reverse proxy).</param>
        /// <returns>true if the failure comes from the generator process; otherwise, false.</returns>
        private static bool IsGeneratorEnvironmentFailure(Exception exception, Type proxyRuntimeType, Type targetRuntimeType)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                switch (current)
                {
                    // A type name of a duck attribute that can't be loaded with its generic arguments (a constraint violation):
                    // dynamic duck typing fails the same way in the application.
                    case ArgumentException { InnerException: TypeLoadException }:
                        return false;
                    // The name of the dynamic assembly dynamic duck typing creates for a type that isn't visible is made of the
                    // type's name: one AssemblyName rejects (e.g. "__StaticArrayInitTypeSize=3") fails in every process. Before
                    // .NET 7, the exception doesn't name the file.
                    case FileLoadException fileLoadException when fileLoadException.FileName?.StartsWith("Datadog.DuckType", StringComparison.Ordinal) == true ||
                                                                 (fileLoadException.FileName is null && fileLoadException.HResult == InvalidAssemblyNameHResult):
                        return false;
                    case FileNotFoundException:
                    case FileLoadException:
                    case BadImageFormatException:
                        return true;
                    // The proxy type itself can't be loaded (a sealed base type, a missing implementation...): dynamic duck
                    // typing fails the same way in the application. Any other type is missing from the generator process.
                    case TypeLoadException typeLoadException when !DuckType.IsDynamicProxyTypeLoadFailureForAot(typeLoadException, proxyRuntimeType, targetRuntimeType):
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the assembly of the type dynamic duck typing didn't find (a type named in a duck attribute), when it isn't one of
        /// the generator's input assemblies: the generator can't tell whether the application has it.
        /// </summary>
        /// <param name="exception">The exception dynamic duck typing threw.</param>
        /// <param name="assemblyName">The name of the assembly of the type.</param>
        /// <returns>true if the type wasn't found in an assembly the generator doesn't have; otherwise, false.</returns>
        private static bool TryGetMissingTypeAssemblyName(Exception exception, out string? assemblyName)
        {
            assemblyName = null;
            const string TypeNotFoundPrefix = "Type not found: ";
            if (exception.GetType() != typeof(DuckTypeException) || !exception.Message.StartsWith(TypeNotFoundPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            // "Namespace.Type[[...]], Assembly, Version=...": a name without an assembly is looked up where the application does.
            assemblyName = DuckTypeAotNameHelpers.ParseTypeAndAssembly(exception.Message.Substring(TypeNotFoundPrefix.Length)).AssemblyName;
            return !StringUtil.IsNullOrEmpty(assemblyName) && runtimeTypeResolutionAssemblyPathsByName?.ContainsKey(assemblyName) != true;
        }

        /// <summary>
        /// Attempts to collect struct copy bindings.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyStructType">The proxy struct type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="closedGenericProxyTypeArguments">The generic arguments of a closed generic proxy struct.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of a closed generic target type.</param>
        /// <param name="bindings">The bindings value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCollectStructCopyBindings(
            DuckTypeAotMapping mapping,
            TypeDef proxyStructType,
            TypeDef targetType,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            out IReadOnlyList<StructCopyFieldBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var collectedBindings = new List<StructCopyFieldBinding>();
                bindings = collectedBindings;
                failure = null;

                foreach (var proxyField in proxyStructType.Fields)
                {
                    if (proxyField.IsStatic || proxyField.IsInitOnly || !proxyField.IsPublic)
                    {
                        continue;
                    }

                    if (proxyField.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, "Datadog.Trace.DuckTyping.DuckIgnoreAttribute", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    if (!TryResolveStructCopyFieldBinding(mapping, targetType, proxyField, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, out var binding, out failure))
                    {
                        return false;
                    }

                    collectedBindings.Add(binding);
                }

                if (collectedBindings.Count == 0 && proxyStructType.Properties.Count > 0)
                {
                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        $"DuckCopy struct '{mapping.ProxyTypeName}' does not expose any writable public fields.");
                    return false;
                }

                return true;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.StructCopyBindingCollectionSeconds += seconds);
            }
        }

        /// <summary>
        /// Attempts to resolve struct copy field binding.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyField">The proxy field value.</param>
        /// <param name="closedGenericProxyTypeArguments">The generic arguments of a closed generic proxy struct.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of a closed generic target type.</param>
        /// <param name="binding">The binding value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryResolveStructCopyFieldBinding(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            FieldDef proxyField,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            out StructCopyFieldBinding binding,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var bindingPlanCacheKey = BuildStructCopyBindingPlanCacheKey(targetType, proxyField, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments);
            var proxyFieldType = SubstituteTypeAndMethodGenericTypeArguments(proxyField.FieldSig.Type, closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
            if (_currentExecutionContext?.TryGetStructCopyBindingPlan(bindingPlanCacheKey, out var cachedPlan) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ForwardBindingPlanCacheHits++;
                }

                if (cachedPlan.Succeeded)
                {
                    binding = cachedPlan.Binding;
                    failure = null;
                    return true;
                }

                binding = default;
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    cachedPlan.FailureStatus ?? DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                    cachedPlan.FailureDiagnosticCode ?? StatusCodeIncompatibleSignature,
                    cachedPlan.FailureDetail ?? $"Target member for proxy struct field '{proxyField.FullName}' was not found.");
                return false;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.ForwardBindingPlanCacheMisses++;
            }

            binding = default;
            failure = null;

            StructCopyFieldBindingPlanCacheEntry CreateFailurePlan(DuckTypeAotMappingEmissionResult failureResult)
            {
                return new StructCopyFieldBindingPlanCacheEntry(
                    failureResult.Status,
                    failureResult.DiagnosticCode ?? StatusCodeIncompatibleSignature,
                    failureResult.Detail ?? string.Empty);
            }

            bool TryBindTargetProperty(PropertyDef targetProperty, out StructCopyFieldBinding propertyBinding, out DuckTypeAotMappingEmissionResult? propertyFailure)
            {
                var targetPropertyType = SubstituteTypeAndMethodGenericTypeArguments(targetProperty.PropertySig.RetType, GetClassMemberGenericTypeArguments(targetType, targetProperty.DeclaringType, closedGenericTargetTypeArguments), closedGenericMethodArguments: null);
                if (!TryCreateReturnConversion(proxyFieldType, targetPropertyType, out var returnConversion))
                {
                    propertyBinding = default;
                    propertyFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        $"Return type mismatch between proxy struct field '{proxyField.FullName}' and target property '{targetProperty.FullName}'.");
                    return false;
                }

                propertyBinding = StructCopyFieldBinding.ForProperty(proxyField, targetProperty, returnConversion);
                propertyFailure = null;
                return true;
            }

            bool TryBindTargetField(FieldDef targetField, out StructCopyFieldBinding fieldBinding, out DuckTypeAotMappingEmissionResult? fieldFailure)
            {
                var targetFieldType = SubstituteTypeAndMethodGenericTypeArguments(targetField.FieldSig.Type, GetClassMemberGenericTypeArguments(targetType, targetField.DeclaringType, closedGenericTargetTypeArguments), closedGenericMethodArguments: null);
                if (!TryCreateReturnConversion(proxyFieldType, targetFieldType, out var returnConversion))
                {
                    fieldBinding = default;
                    fieldFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        $"Return type mismatch between proxy struct field '{proxyField.FullName}' and target field '{targetField.FullName}'.");
                    return false;
                }

                fieldBinding = StructCopyFieldBinding.ForField(proxyField, targetField, returnConversion);
                fieldFailure = null;
                return true;
            }

            bool Complete(bool succeeded, StructCopyFieldBinding completedBinding, DuckTypeAotMappingEmissionResult? completedFailure, out StructCopyFieldBinding resultBinding, out DuckTypeAotMappingEmissionResult? resultFailure)
            {
                resultBinding = completedBinding;
                resultFailure = completedFailure;
                _currentExecutionContext?.CacheStructCopyBindingPlan(
                    bindingPlanCacheKey,
                    succeeded ? new StructCopyFieldBindingPlanCacheEntry(completedBinding) : CreateFailurePlan(completedFailure!));
                return succeeded;
            }

            // Copy exactly the member the dynamic engine selects (ExplicitInterfaceTypeName, FallbackToBaseTypes, hidden
            // members...) when the generator can load the runtime types. When it selects none, or a property that can't be
            // read, its proxy creation fails: the metadata resolution below keeps the usual diagnostics, and the dynamic
            // validation replays the dynamic failure.
            if (TryGetDynamicDuckCopyTargetMember(mapping, targetType, proxyField, out var dynamicTargetMember))
            {
                switch (dynamicTargetMember)
                {
                    case PropertyDef dynamicTargetProperty when dynamicTargetProperty.GetMethod is { } dynamicGetter && dynamicGetter.MethodSig.Params.Count == 0:
                    {
                        var succeeded = TryBindTargetProperty(dynamicTargetProperty, out var dynamicBinding, out var dynamicFailure);
                        return Complete(succeeded, dynamicBinding, dynamicFailure, out binding, out failure);
                    }

                    case FieldDef dynamicTargetField:
                    {
                        var succeeded = TryBindTargetField(dynamicTargetField, out var dynamicBinding, out var dynamicFailure);
                        return Complete(succeeded, dynamicBinding, dynamicFailure, out binding, out failure);
                    }
                }
            }

            var hasFieldOnlyAttribute = false;
            var allowFieldFallback = false;
            var allowPrivateBaseMembers = IsFallbackToBaseTypesEnabled(proxyField.CustomAttributes);
            var duckBindingFlags = GetDuckBindingFlags(proxyField.CustomAttributes);
            var useIgnoreCaseMemberMatching = (duckBindingFlags & BindingFlags.IgnoreCase) != 0;
            foreach (var attribute in proxyField.CustomAttributes)
            {
                if (!IsDuckAttribute(attribute))
                {
                    continue;
                }

                var kind = ResolveDuckKind(attribute);
                switch (kind)
                {
                    case DuckKindField:
                        hasFieldOnlyAttribute = true;
                        allowFieldFallback = true;
                        break;
                    case DuckKindPropertyOrField:
                        allowFieldFallback = true;
                        break;
                }

                if (hasFieldOnlyAttribute)
                {
                    break;
                }
            }

            var candidateNames = TryGetDuckAttributeNames(proxyField.CustomAttributes, out var configuredNames)
                                     ? configuredNames
                                     : new[] { proxyField.Name.String ?? proxyField.Name.ToString() };

            // Prefer property source binding when field-only mode is not requested and a matching property exists.
            if (!hasFieldOnlyAttribute &&
                TryFindStructCopyTargetProperty(targetType, candidateNames, allowPrivateBaseMembers, duckBindingFlags, useIgnoreCaseMemberMatching, out var targetProperty))
            {
                var succeeded = TryBindTargetProperty(targetProperty!, out var propertyBinding, out var propertyFailure);
                return Complete(succeeded, propertyBinding, propertyFailure, out binding, out failure);
            }

            if (hasFieldOnlyAttribute || allowFieldFallback)
            {
                if (TryFindStructCopyTargetField(targetType, candidateNames, allowPrivateBaseMembers, duckBindingFlags, useIgnoreCaseMemberMatching, out var targetField))
                {
                    var succeeded = TryBindTargetField(targetField!, out var fieldBinding, out var fieldFailure);
                    return Complete(succeeded, fieldBinding, fieldFailure, out binding, out failure);
                }
            }

            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                StatusCodeMissingMethod,
                $"Target member for proxy struct field '{proxyField.FullName}' was not found.");
            _currentExecutionContext?.CacheStructCopyBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
            return false;
        }

        /// <summary>
        /// Attempts to find struct copy target property.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="candidateNames">The candidate names value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <param name="duckBindingFlags">The effective Duck binding flags.</param>
        /// <param name="useIgnoreCaseMemberMatching">The use ignore case member matching value.</param>
        /// <param name="targetProperty">The target property value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryFindStructCopyTargetProperty(TypeDef targetType, IReadOnlyList<string> candidateNames, bool allowPrivateBaseMembers, BindingFlags duckBindingFlags, bool useIgnoreCaseMemberMatching, out PropertyDef? targetProperty)
        {
            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
            foreach (var candidateName in candidateNames)
            {
                var propertyCandidates = targetTypePlan?.GetPropertyCandidates(candidateName, useIgnoreCaseMemberMatching)
                                         ?? Array.Empty<TargetPropertyCandidate>();
                foreach (var propertyCandidate in propertyCandidates)
                {
                    var property = propertyCandidate.Property;
                    if (property.GetMethod is null || property.GetMethod.MethodSig.Params.Count != 0)
                    {
                        continue;
                    }

                    if (!allowPrivateBaseMembers &&
                        propertyCandidate.IsInherited &&
                        property.GetMethod.IsPrivate)
                    {
                        continue;
                    }

                    if (!IsReadablePropertyCandidateAllowedByBindingFlags(propertyCandidate, duckBindingFlags, allowPrivateBaseMembers))
                    {
                        continue;
                    }

                    targetProperty = property;
                    return true;
                }
            }

            targetProperty = null;
            return false;
        }

        /// <summary>
        /// Attempts to find struct copy target field.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="candidateNames">The candidate names value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <param name="duckBindingFlags">The effective Duck binding flags.</param>
        /// <param name="useIgnoreCaseMemberMatching">The use ignore case member matching value.</param>
        /// <param name="targetField">The target field value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryFindStructCopyTargetField(TypeDef targetType, IReadOnlyList<string> candidateNames, bool allowPrivateBaseMembers, BindingFlags duckBindingFlags, bool useIgnoreCaseMemberMatching, out FieldDef? targetField)
        {
            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
            foreach (var candidateName in candidateNames)
            {
                var fieldCandidates = targetTypePlan?.GetFieldCandidates(candidateName, useIgnoreCaseMemberMatching)
                                     ?? Array.Empty<TargetFieldCandidate>();
                foreach (var fieldCandidate in fieldCandidates)
                {
                    var field = fieldCandidate.Field;
                    if (!allowPrivateBaseMembers &&
                        fieldCandidate.IsInherited &&
                        field.IsPrivate)
                    {
                        continue;
                    }

                    if (!IsFieldCandidateAllowedByBindingFlags(fieldCandidate, duckBindingFlags, allowPrivateBaseMembers))
                    {
                        continue;
                    }

                    targetField = field;
                    return true;
                }
            }

            targetField = null;
            return false;
        }

        /// <summary>
        /// Attempts to collect forward bindings.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="isInterfaceProxy">The is interface proxy value.</param>
        /// <param name="bindings">The bindings value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCollectForwardBindings(
            DuckTypeAotMapping mapping,
            TypeDef proxyType,
            TypeDef targetType,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            bool isInterfaceProxy,
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            if (isInterfaceProxy)
            {
                return TryCollectForwardInterfaceBindings(mapping, proxyType, targetType, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, out bindings, out failure);
            }

            return TryCollectForwardClassBindings(mapping, proxyType, targetType, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, out bindings, out failure);
        }

        /// <summary>
        /// Attempts to collect forward interface bindings.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyInterfaceType">The proxy interface type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="bindings">The bindings value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCollectForwardInterfaceBindings(
            DuckTypeAotMapping mapping,
            TypeDef proxyInterfaceType,
            TypeDef targetType,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var collectedBindings = new List<ForwardBinding>();
                bindings = collectedBindings;
                failure = null;
                var targetUsesReverseMethodAttributes =
                    mapping.Mode == DuckTypeAotMappingMode.Reverse && HasReverseMethodAttributes(targetType);

                var proxyMethods = GetInterfaceMethods(proxyInterfaceType, mapping.Mode == DuckTypeAotMappingMode.Reverse).ToList();
                AppendDuckIncludeTargetMethods(proxyMethods, targetType);

                // Like dynamic duck typing, a property with a private setter (a helper of a default implementation) is writable
                // for reflection, so the target property has to be writable; the setter itself isn't an interface member the
                // proxy implements.
                if (mapping.Mode == DuckTypeAotMappingMode.Forward &&
                    _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyInterfaceType).ImplementedAccessors is { } implementedAccessors)
                {
                    foreach (var privateSetter in implementedAccessors.Where(accessor => accessor.IsPrivate && accessor.IsSetter && !accessor.IsStatic))
                    {
                        if (TryResolvePropertyCantBeWrittenFailure(targetType, privateSetter, allowPrivateBaseMembers: false, out var privateSetterFailureDetail))
                        {
                            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                                mapping,
                                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                                StatusCodePropertyCantBeWritten,
                                privateSetterFailureDetail!);
                            return false;
                        }
                    }
                }

                foreach (var proxyMethod in proxyMethods)
                {
                    if (proxyMethod.IsStatic)
                    {
                        failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                            mapping,
                            DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                            StatusCodeIncompatibleSignature,
                            $"Proxy method '{proxyMethod.FullName}' is static. Static interface members are not emitted in this phase.");
                        return false;
                    }

                    var methodTypeArguments = GetProxyMethodTypeArguments(proxyInterfaceType, targetType, proxyMethod, isInterfaceProxy: true, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments);
                    if (!TryResolveForwardBinding(mapping, targetType, methodTypeArguments, closedGenericTargetTypeArguments, proxyMethod, out var binding, out failure))
                    {
                        return false;
                    }

                    collectedBindings.Add(binding);
                }

                if (targetUsesReverseMethodAttributes &&
                    !TryValidateReverseImplementationCoverage(mapping, proxyInterfaceType, targetType, collectedBindings, out failure))
                {
                    return false;
                }

                return true;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardBindingCollectionSeconds += seconds);
            }
        }

        /// <summary>
        /// Attempts to collect forward class bindings.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyClassType">The proxy class type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="bindings">The bindings value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCollectForwardClassBindings(
            DuckTypeAotMapping mapping,
            TypeDef proxyClassType,
            TypeDef targetType,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var collectedBindings = new List<ForwardBinding>();
                bindings = collectedBindings;
                failure = null;
                var targetUsesReverseMethodAttributes =
                    mapping.Mode == DuckTypeAotMappingMode.Reverse && HasReverseMethodAttributes(targetType);

                var proxyMethods = GetClassProxyMethods(proxyClassType, mapping.Mode == DuckTypeAotMappingMode.Reverse).ToList();
                AppendDuckIncludeTargetMethods(proxyMethods, targetType);
                if (proxyMethods.Count == 0)
                {
                    return true;
                }

                foreach (var proxyMethod in proxyMethods)
                {
                    var methodTypeArguments = GetProxyMethodTypeArguments(proxyClassType, targetType, proxyMethod, isInterfaceProxy: false, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments);
                    if (!TryResolveForwardBinding(mapping, targetType, methodTypeArguments, closedGenericTargetTypeArguments, proxyMethod, out var binding, out failure))
                    {
                        if (mapping.Mode == DuckTypeAotMappingMode.Reverse &&
                            failure?.Status == DuckTypeAotCompatibilityStatuses.MissingTargetMethod &&
                            !proxyMethod.IsAbstract)
                        {
                            continue;
                        }

                        if (mapping.Mode == DuckTypeAotMappingMode.Reverse &&
                            IsInheritedOpenGenericAbstractMethodWithConcreteOverride(proxyClassType, proxyMethod))
                        {
                            continue;
                        }

                        return false;
                    }

                    collectedBindings.Add(binding);
                }

                if (targetUsesReverseMethodAttributes &&
                    !TryValidateReverseImplementationCoverage(mapping, proxyClassType, targetType, collectedBindings, out failure))
                {
                    return false;
                }

                return true;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardBindingCollectionSeconds += seconds);
            }
        }

        private static ITypeDefOrRef ImportMemberDeclaringType(
            ModuleDef module,
            TypeDef rootType,
            TypeDef declaringType,
            IReadOnlyList<TypeSig>? closedRootTypeArguments)
        {
            var arguments = GetClassMemberGenericTypeArguments(rootType, declaringType, closedRootTypeArguments);
            TypeSig signature = declaringType.ToTypeSig();
            if (declaringType.HasGenericParameters && arguments is { Count: > 0 })
            {
                signature = new GenericInstSig((ClassOrValueTypeSig)signature, arguments.ToArray());
            }

            return ImportTypeDefOrRefCached(module, signature.ToTypeDefOrRef(), $"declaring type '{signature.FullName}'");
        }

        private static IField ImportTargetField(
            ModuleDef module,
            FieldDef field,
            TypeDef rootType,
            IReadOnlyList<TypeSig>? closedRootTypeArguments)
        {
            var importedField = ImportFieldCached(module, field, $"target field '{field.FullName}'");
            var declaringType = ImportMemberDeclaringType(module, rootType, field.DeclaringType, closedRootTypeArguments);
            return string.Equals(importedField.DeclaringType.FullName, declaringType.FullName, StringComparison.Ordinal)
                       ? importedField
                       : module.UpdateRowId(new MemberRefUser(module, importedField.Name, importedField.FieldSig, declaringType));
        }

        /// <summary>
        /// Ensures every [DuckReverseMethod] implementation member is bound to a generated reverse proxy method.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The type the reverse proxy derives from.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="bindings">The bindings value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryValidateReverseImplementationCoverage(
            DuckTypeAotMapping mapping,
            TypeDef proxyType,
            TypeDef targetType,
            IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            failure = null;

            // When the dynamic engine selects the property implementations, it has checked that every [DuckReverseMethod]
            // property implements a property: one that another property implements too isn't an error.
            var accessorSelectionKnown = TryResolveDynamicSelectionTypes(mapping, proxyType, targetType, out var proxyRuntimeType, out var targetRuntimeType) &&
                                         _currentExecutionContext?.GetOrCreateDynamicReverseSelection(
                                             mapping.Key + "|accessors",
                                             () => DuckType.SelectReverseImplementationAccessorsForAot(proxyRuntimeType!, targetRuntimeType!)) is not null;
            var boundTargetMethodKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.Kind != ForwardBindingKind.Method || binding.TargetMethod is null)
                {
                    continue;
                }

                boundTargetMethodKeys.Add(GetMethodCandidateKey(binding.TargetMethod));
            }

            foreach (var reverseImplementationMethod in EnumerateDeclaredReverseImplementationMethods(targetType))
            {
                if (boundTargetMethodKeys.Contains(GetMethodCandidateKey(reverseImplementationMethod)) ||
                    (accessorSelectionKnown && (reverseImplementationMethod.IsGetter || reverseImplementationMethod.IsSetter)))
                {
                    continue;
                }

                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                    StatusCodeMissingMethod,
                    $"Target member for proxy method '{reverseImplementationMethod.FullName}' was not found.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Enumerates reverse implementation methods declared on the target type.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<MethodDef> EnumerateDeclaredReverseImplementationMethods(TypeDef targetType)
        {
            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
            return targetTypePlan?.DeclaredReverseImplementationMethods ?? Array.Empty<MethodDef>();
        }

        /// <summary>
        /// Attempts to resolve forward binding.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="binding">The binding value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryResolveForwardBinding(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            MethodDef proxyMethod,
            out ForwardBinding binding,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                binding = default;
                failure = null;
                var proxyMethodPlan = GetOrCreateProxyMethodPlan(proxyMethod);
                var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
                var cacheKeyStopwatch = StartProfilePhase();
                // Array mappings bind System.Array: the array type tells their dynamic selections (and failures) apart.
                var targetTypeIdentityKey = targetTypePlan?.IdentityKey ?? BuildTypeIdentityKey(targetType);
                var bindingPlanCacheKey = BuildForwardBindingPlanCacheKey(
                    mapping,
                    IsArrayTargetMapping(mapping) ? string.Concat(targetTypeIdentityKey, "|", mapping.TargetAssemblyName, "|", mapping.TargetTypeName) : targetTypeIdentityKey,
                    proxyMethodPlan,
                    closedGenericProxyTypeArguments,
                    closedGenericTargetTypeArguments);
                StopProfilePhase(cacheKeyStopwatch, seconds => _currentProfile!.ForwardBindingPlanCacheKeyBuildSeconds += seconds);
                if (_currentExecutionContext?.TryGetForwardBindingPlan(bindingPlanCacheKey, out var cachedPlan) == true)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.ForwardBindingPlanCacheHits++;
                    }

                    if (cachedPlan.Succeeded)
                    {
                        binding = cachedPlan.Binding;
                        return true;
                    }

                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        cachedPlan.FailureStatus ?? DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        cachedPlan.FailureDiagnosticCode ?? StatusCodeIncompatibleSignature,
                        cachedPlan.FailureDetail ?? $"Target member for proxy method '{proxyMethod.FullName}' was not found.");
                    return false;
                }

                if (_currentProfile is not null)
                {
                    _currentProfile.ForwardBindingPlanCacheMisses++;
                }

                var fieldResolutionMode = proxyMethodPlan.FieldResolutionMode;
                var fieldOnly = fieldResolutionMode == FieldResolutionMode.FieldOnly;
                var allowFieldFallback = fieldResolutionMode != FieldResolutionMode.Disabled;
                var allowPrivateBaseMembers = proxyMethodPlan.AllowPrivateBaseMembers;
                var isReverseMapping = mapping.Mode == DuckTypeAotMappingMode.Reverse;
                // Dynamic ducktyping only applies FallbackToBaseTypes semantics to property/field binding paths.
                // In AOT, property bindings are represented as accessor methods, so keep private-base fallback
                // enabled only for accessor method resolution to preserve runtime parity.
                var allowPrivateBaseMethodCandidates = proxyMethodPlan.AllowPrivateBaseMethodCandidates;

                ForwardBindingPlanCacheEntry CreateFailurePlan(DuckTypeAotMappingEmissionResult failureResult)
                {
                    return new ForwardBindingPlanCacheEntry(
                        failureResult.Status,
                        failureResult.DiagnosticCode ?? StatusCodeIncompatibleSignature,
                        failureResult.Detail ?? string.Empty);
                }

                if (!isReverseMapping &&
                    !StringUtil.IsNullOrWhiteSpace(proxyMethodPlan.ReverseUsageFailureDetail))
                {
                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        proxyMethodPlan.ReverseUsageFailureDetail!);
                    _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                    return false;
                }

                MethodCompatibilityFailure? firstMethodFailure = null;
                if (!TryResolveForwardClosedGenericMethodArguments(targetType, proxyMethodPlan, out var closedGenericMethodArguments, out var closedGenericMethodArgumentsFailureReason))
                {
                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        closedGenericMethodArgumentsFailureReason ?? $"Unable to resolve generic type arguments for proxy method '{proxyMethod.FullName}'.");
                    _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                    return false;
                }

                // Property accessors: bind exactly the member the dynamic engine selects (hidden members, FallbackToBaseTypes,
                // ExplicitInterfaceTypeName, field fallbacks...) when the generator can load the runtime types. When the dynamic
                // engine binds none, its proxy creation fails: the metadata resolution below keeps the usual diagnostics, and if it
                // binds a member anyway the dynamic validation replays the dynamic failure.
                if (!isReverseMapping &&
                    proxyMethodPlan.DeclaringProperty is { } proxyProperty &&
                    (proxyProperty.GetMethod == proxyMethod || proxyProperty.SetMethod == proxyMethod) &&
                    TryGetDynamicForwardTargetPropertyMember(mapping, targetType, proxyMethod, isGetter: proxyProperty.GetMethod == proxyMethod, out var dynamicTargetMember, out var dynamicProxyRuntimeType, out var dynamicTargetRuntimeType) &&
                    dynamicTargetMember is not null)
                {
                    if (TryCreateDynamicPropertyMemberBinding(proxyMethod, isGetter: proxyProperty.GetMethod == proxyMethod, dynamicTargetMember, targetType, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, closedGenericMethodArguments, out var dynamicMemberBinding, out var dynamicMemberFailureDetail))
                    {
                        if (_currentProfile is not null)
                        {
                            _currentProfile.ForwardResolutionMethodSuccessCount++;
                        }

                        binding = dynamicMemberBinding;
                        _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, new ForwardBindingPlanCacheEntry(binding));
                        return true;
                    }

                    if (GetDynamicCreationOutcome(mapping, dynamicProxyRuntimeType!, dynamicTargetRuntimeType!, out _, out _) == DynamicCreationOutcome.Created)
                    {
                        if (_currentProfile is not null)
                        {
                            _currentProfile.ForwardResolutionFirstFailureCount++;
                        }

                        failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                            mapping,
                            DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                            StatusCodeIncompatibleSignature,
                            $"Dynamic duck typing binds proxy property accessor '{proxyMethod.FullName}' to '{dynamicTargetMember.FullName}', which the AOT registry can't bind: {dynamicMemberFailureDetail}");
                        _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                        return false;
                    }
                }

                if (!fieldOnly)
                {
                    // Reverse proxies: implement the method with exactly the [DuckReverseMethod] method the dynamic engine pairs it
                    // with (it selects from the delegation type: ParameterTypeNames, IgnoreCase, declaration order...). When it
                    // pairs none, a virtual method keeps its base implementation, and an abstract one fails the proxy creation.
                    MethodDef? reverseImplementationMethod = null;
                    var reverseSelectionKnown = isReverseMapping &&
                                                TryGetDynamicReverseImplementationMethod(mapping, targetType, proxyMethod, out reverseImplementationMethod);
                    if (reverseSelectionKnown && reverseImplementationMethod is null)
                    {
                        failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                            mapping,
                            DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                            StatusCodeMissingMethod,
                            $"No [DuckReverseMethod] method implements proxy method '{proxyMethod.FullName}' in dynamic duck typing.");
                        _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                        return false;
                    }

                    if (reverseSelectionKnown && reverseImplementationMethod is not null)
                    {
                        if (TryCreateForwardMethodBinding(
                                proxyMethod,
                                reverseImplementationMethod,
                                closedGenericProxyTypeArguments,
                                GetClassMethodGenericTypeArguments(targetType, reverseImplementationMethod, closedGenericTargetTypeArguments),
                                closedGenericMethodArguments,
                                isReverseMapping: true,
                                enforceMethodSelectionRules: false,
                                out var reverseMethodBinding,
                                out var reverseMethodFailure))
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.ForwardResolutionMethodSuccessCount++;
                            }

                            binding = ForwardBinding.ForMethod(proxyMethod, reverseImplementationMethod, reverseMethodBinding);
                            _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, new ForwardBindingPlanCacheEntry(binding));
                            return true;
                        }

                        // Like forward proxies: if the dynamic engine creates the reverse proxy with a method the AOT emitter can't
                        // bind, fail rather than implement the proxy method with another one.
                        if (proxyMethod.DeclaringType is { } reverseProxyDeclaringType &&
                            TryResolveDynamicSelectionTypes(mapping, reverseProxyDeclaringType, targetType, out var reverseProxyRuntimeType, out var reverseTargetRuntimeType) &&
                            GetDynamicCreationOutcome(mapping, reverseProxyRuntimeType!, reverseTargetRuntimeType!, out _, out _) == DynamicCreationOutcome.Created)
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.ForwardResolutionFirstFailureCount++;
                            }

                            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                                mapping,
                                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                                StatusCodeIncompatibleSignature,
                                $"Dynamic duck typing implements proxy method '{proxyMethod.FullName}' with '{reverseImplementationMethod.FullName}', which the AOT registry can't bind: {reverseMethodFailure?.Detail ?? $"Method '{reverseImplementationMethod.FullName}' is not compatible with proxy method '{proxyMethod.FullName}'."}");
                            _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                            return false;
                        }
                    }

                    // When the generator can load the runtime types, bind exactly the method the dynamic engine selects (it
                    // runs the same selection code), so both engines call the same overload or explicit implementation.
                    // The candidate scan below only approximates that selection for contracts the dynamic engine can't load.
                    // When the dynamic engine selects no method, its proxy creation fails: the candidate scan keeps the
                    // usual diagnostics, and if it binds a method anyway the dynamic validation replays the dynamic failure.
                    if (!isReverseMapping &&
                        proxyMethodPlan.DeclaringProperty is null &&
                        TryGetDynamicForwardTargetMethod(mapping, targetType, proxyMethod, out var dynamicTargetMethod, out var proxyRuntimeType, out var targetRuntimeType) &&
                        dynamicTargetMethod is not null)
                    {
                        var dynamicDeclaringTypeArguments = GetClassMethodGenericTypeArguments(targetType, dynamicTargetMethod, closedGenericTargetTypeArguments);
                        string? dynamicBindingFailureDetail = null;
                        if (!TryCreateForwardMethodBinding(proxyMethod, dynamicTargetMethod, closedGenericProxyTypeArguments, dynamicDeclaringTypeArguments, closedGenericMethodArguments, isReverseMapping: false, enforceMethodSelectionRules: false, out var dynamicMethodBinding, out var dynamicMethodFailure))
                        {
                            dynamicBindingFailureDetail = dynamicMethodFailure?.Detail ?? $"Target method '{dynamicTargetMethod.FullName}' is not compatible with proxy method '{proxyMethod.FullName}'.";
                        }
                        else if (TryGetStructMemberMutationFailureDetail(proxyMethod, dynamicTargetMethod, out var dynamicStructMutationFailureDetail))
                        {
                            dynamicBindingFailureDetail = dynamicStructMutationFailureDetail!;
                        }

                        if (dynamicBindingFailureDetail is null)
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.ForwardResolutionMethodSuccessCount++;
                            }

                            binding = ForwardBinding.ForMethod(proxyMethod, dynamicTargetMethod, dynamicMethodBinding);
                            _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, new ForwardBindingPlanCacheEntry(binding));
                            return true;
                        }

                        // If the dynamic engine creates the proxy with a method the AOT emitter can't bind, fail rather than
                        // bind another method. Otherwise the mapping fails in both engines: keep the usual diagnostics below.
                        if (GetDynamicCreationOutcome(mapping, proxyRuntimeType!, targetRuntimeType!, out _, out _) == DynamicCreationOutcome.Created)
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.ForwardResolutionFirstFailureCount++;
                            }

                            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                                mapping,
                                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                                StatusCodeIncompatibleSignature,
                                $"Dynamic duck typing binds proxy method '{proxyMethod.FullName}' to '{dynamicTargetMethod.FullName}', which the AOT registry can't bind: {dynamicBindingFailureDetail}");
                            _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                            return false;
                        }
                    }

                    // Before its own candidate scan (which the one below approximates), dynamic duck typing asks Type.GetMethod with
                    // the proxy's parameter types (its default binder), for each name: when it selects a method, so does the registry,
                    // and the failure to convert an argument or the return value is dynamic duck typing's.
                    if (!isReverseMapping &&
                        proxyMethodPlan.DeclaringProperty is null &&
                        proxyMethod.MethodSig.GenParamCount == 0 &&
                        closedGenericMethodArguments is null &&
                        proxyMethodPlan.ConfiguredParameterTypeNames.Count == 0 &&
                        TrySelectWithDefaultBinder(mapping, targetType, proxyMethodPlan, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, allowPrivateBaseMethodCandidates, out var binderMethod))
                    {
                        var binderDeclaringTypeArguments = GetClassMethodGenericTypeArguments(targetType, binderMethod!, closedGenericTargetTypeArguments);
                        string? binderFailureDetail;
                        if (TryCreateForwardMethodBinding(proxyMethod, binderMethod!, closedGenericProxyTypeArguments, binderDeclaringTypeArguments, closedGenericMethodArguments: null, isReverseMapping: false, enforceMethodSelectionRules: false, out var binderBinding, out var binderFailure))
                        {
                            if (!TryGetStructMemberMutationFailureDetail(proxyMethod, binderMethod!, out binderFailureDetail))
                            {
                                if (_currentProfile is not null)
                                {
                                    _currentProfile.ForwardResolutionMethodSuccessCount++;
                                }

                                binding = ForwardBinding.ForMethod(proxyMethod, binderMethod!, binderBinding);
                                _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, new ForwardBindingPlanCacheEntry(binding));
                                return true;
                            }
                        }
                        else
                        {
                            binderFailureDetail = GetInvalidTypeConversionDetail(proxyMethod, binderMethod!, closedGenericProxyTypeArguments, binderDeclaringTypeArguments) ??
                                                  binderFailure?.Detail ??
                                                  $"Target method '{binderMethod!.FullName}' is not compatible with proxy method '{proxyMethod.FullName}'.";
                        }

                        failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                            mapping,
                            DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                            StatusCodeIncompatibleSignature,
                            binderFailureDetail!);
                        _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                        return false;
                    }

                    var hasSuccessfulMethodBinding = false;
                    var successfulMethodBinding = default(ForwardBinding);
                    MethodDef? successfulTargetMethod = null;
                    var successfulMethodNameOrdinal = -1;
                    var candidateCount = 0;
                    MethodDef? firstFailedTargetMethod = null;
                    foreach (var targetMethodCandidate in FindForwardTargetMethodCandidates(mapping, targetType, proxyMethodPlan, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, closedGenericMethodArguments, allowPrivateBaseMethodCandidates))
                    {
                        candidateCount++;
                        var targetMethod = targetMethodCandidate.Method;
                        var declaringTypeArguments = GetClassMethodGenericTypeArguments(targetType, targetMethod, closedGenericTargetTypeArguments);
                        if (!isReverseMapping && proxyMethodPlan.ConfiguredParameterTypeNames.Count > 0 &&
                            !IsForwardCandidateParameterTypeNameMatch(targetMethod, proxyMethodPlan.ConfiguredParameterTypeNames, declaringTypeArguments))
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.ForwardCandidateParameterTypeRejectCount++;
                            }

                            continue;
                        }

                        if (TryCreateForwardMethodBinding(proxyMethod, targetMethod, closedGenericProxyTypeArguments, declaringTypeArguments, closedGenericMethodArguments, isReverseMapping, enforceMethodSelectionRules: true, out var methodBinding, out var methodFailure))
                        {
                            if (TryGetStructMemberMutationFailureDetail(proxyMethod, targetMethod, out var structMutationFailureDetail))
                            {
                                firstMethodFailure ??= new MethodCompatibilityFailure(structMutationFailureDetail!);
                                continue;
                            }

                            if (hasSuccessfulMethodBinding)
                            {
                                if (targetMethodCandidate.NameOrdinal > successfulMethodNameOrdinal)
                                {
                                    continue;
                                }

                                var candidateIsDeclaredOnTargetType = IsMethodDeclaredOnType(targetMethod, targetType);
                                var successfulIsDeclaredOnTargetType = IsMethodDeclaredOnType(successfulTargetMethod!, targetType);
                                if (candidateIsDeclaredOnTargetType != successfulIsDeclaredOnTargetType)
                                {
                                    if (candidateIsDeclaredOnTargetType)
                                    {
                                        successfulMethodBinding = ForwardBinding.ForMethod(proxyMethod, targetMethod, methodBinding);
                                        successfulTargetMethod = targetMethod;
                                        successfulMethodNameOrdinal = targetMethodCandidate.NameOrdinal;
                                    }

                                    continue;
                                }

                                if (_currentProfile is not null)
                                {
                                    _currentProfile.ForwardResolutionAmbiguousCount++;
                                }

                                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                                    mapping,
                                    DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                                    StatusCodeIncompatibleSignature,
                                    $"Ambiguous target method match for proxy method '{proxyMethod.FullName}' between '{successfulTargetMethod!.FullName}' and '{targetMethod.FullName}'.");
                                _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                                return false;
                            }

                            successfulMethodBinding = ForwardBinding.ForMethod(proxyMethod, targetMethod, methodBinding);
                            successfulTargetMethod = targetMethod;
                            successfulMethodNameOrdinal = targetMethodCandidate.NameOrdinal;
                            hasSuccessfulMethodBinding = true;
                            continue;
                        }

                        if (firstMethodFailure is null)
                        {
                            firstMethodFailure = methodFailure;
                            firstFailedTargetMethod = targetMethod;
                        }
                    }

                    // The accessor of the only property of the name: dynamic duck typing selects that property by name, and fails to
                    // convert its value (or index).
                    if (!hasSuccessfulMethodBinding &&
                        !isReverseMapping &&
                        proxyMethodPlan.DeclaringProperty is not null &&
                        candidateCount == 1 &&
                        firstFailedTargetMethod is not null &&
                        GetInvalidTypeConversionDetail(proxyMethod, firstFailedTargetMethod, closedGenericProxyTypeArguments, GetClassMethodGenericTypeArguments(targetType, firstFailedTargetMethod, closedGenericTargetTypeArguments)) is { } accessorConversionFailure)
                    {
                        firstMethodFailure = new MethodCompatibilityFailure(accessorConversionFailure);
                    }

                    if (hasSuccessfulMethodBinding)
                    {
                        if (_currentProfile is not null)
                        {
                            _currentProfile.ForwardResolutionMethodSuccessCount++;
                        }

                        binding = successfulMethodBinding;
                        _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, new ForwardBindingPlanCacheEntry(binding));
                        return true;
                    }
                }

                if (allowFieldFallback)
                {
                    if (!proxyMethodPlan.HasFieldAccessorKind)
                    {
                        if (fieldOnly)
                        {
                            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                                mapping,
                                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                                StatusCodeIncompatibleSignature,
                                $"Proxy member '{proxyMethod.FullName}' uses DuckField semantics but is not a supported property accessor.");
                            _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                            return false;
                        }
                    }
                    else
                    {
                        var fieldAccessorKind = proxyMethodPlan.FieldAccessorKind;
                        if (TryFindForwardTargetField(targetType, proxyMethod, fieldAccessorKind, allowPrivateBaseMembers, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, isReverseMapping, out var targetField, out var fieldBinding, out var fieldFailureReason))
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.ForwardResolutionFieldSuccessCount++;
                            }

                            binding = fieldAccessorKind == FieldAccessorKind.Getter
                                          ? ForwardBinding.ForFieldGet(proxyMethod, targetField!, fieldBinding)
                                          : ForwardBinding.ForFieldSet(proxyMethod, targetField!, fieldBinding);
                            _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, new ForwardBindingPlanCacheEntry(binding));
                            return true;
                        }

                        if (!StringUtil.IsNullOrWhiteSpace(fieldFailureReason))
                        {
                            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                                mapping,
                                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                                StatusCodeIncompatibleSignature,
                                fieldFailureReason ?? $"Field binding for proxy method '{proxyMethod.FullName}' is not compatible.");
                            _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                            return false;
                        }
                    }
                }

                if (firstMethodFailure is not null)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.ForwardResolutionFirstFailureCount++;
                    }

                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        firstMethodFailure.Value.Detail);
                    _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                    return false;
                }

                if (TryResolvePropertyCantBeWrittenFailure(targetType, proxyMethod, allowPrivateBaseMembers, out var propertyCantBeWrittenDetail))
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.ForwardResolutionPropertyCantBeWrittenCount++;
                    }

                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                        StatusCodePropertyCantBeWritten,
                        propertyCantBeWrittenDetail!);
                    _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                    return false;
                }

                if (_currentProfile is not null)
                {
                    _currentProfile.ForwardResolutionMissingTargetCount++;
                }

                var missingTargetDetail = TryGetDynamicRuntimeArrayMethod(mapping, targetType, proxyMethod, out var runtimeArrayMethod, out var runtimeArrayType)
                                              ? $"Proxy method '{proxyMethod.FullName}' binds the method '{runtimeArrayMethod}' the runtime adds to array type '{runtimeArrayType}' in dynamic duck typing. NativeAOT duck typing binds the members of System.Array only."
                                              : CreateMissingTargetMemberMessage(proxyMethod, targetType);
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                    StatusCodeMissingMethod,
                    missingTargetDetail);
                _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                return false;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardBindingResolutionSeconds += seconds);
            }
        }

        /// <summary>
        /// Creates the binding of a proxy property accessor to the target member the dynamic engine selects.
        /// </summary>
        /// <param name="proxyAccessor">The proxy property accessor.</param>
        /// <param name="isGetter">Whether the proxy accessor is a getter.</param>
        /// <param name="targetMember">The target accessor or field.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed generic proxy type arguments value.</param>
        /// <param name="closedGenericTargetTypeArguments">The closed generic target type arguments value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="binding">The binding value.</param>
        /// <param name="failureDetail">Why the member can't be bound.</param>
        /// <returns>true if the binding was created; otherwise, false.</returns>
        private static bool TryCreateDynamicPropertyMemberBinding(
            MethodDef proxyAccessor,
            bool isGetter,
            IMemberDef targetMember,
            TypeDef targetType,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            out ForwardBinding binding,
            out string? failureDetail)
        {
            binding = default;
            failureDetail = null;
            switch (targetMember)
            {
                case MethodDef targetAccessor:
                {
                    var declaringTypeArguments = GetClassMethodGenericTypeArguments(targetType, targetAccessor, closedGenericTargetTypeArguments);
                    if (!TryCreateForwardMethodBinding(proxyAccessor, targetAccessor, closedGenericProxyTypeArguments, declaringTypeArguments, closedGenericMethodArguments, isReverseMapping: false, enforceMethodSelectionRules: false, out var accessorBinding, out var accessorFailure))
                    {
                        failureDetail = accessorFailure?.Detail ?? $"Target accessor '{targetAccessor.FullName}' is not compatible with proxy accessor '{proxyAccessor.FullName}'.";
                        return false;
                    }

                    if (TryGetStructMemberMutationFailureDetail(proxyAccessor, targetAccessor, out failureDetail))
                    {
                        return false;
                    }

                    binding = ForwardBinding.ForMethod(proxyAccessor, targetAccessor, accessorBinding);
                    return true;
                }

                case FieldDef targetField when proxyAccessor.MethodSig.Params.Count == (isGetter ? 0 : 1):
                {
                    var accessorKind = isGetter ? FieldAccessorKind.Getter : FieldAccessorKind.Setter;
                    var declaringTypeArguments = GetClassMemberGenericTypeArguments(targetType, targetField.DeclaringType, closedGenericTargetTypeArguments);
                    if (!AreFieldAccessorSignatureCompatible(proxyAccessor, targetField, accessorKind, closedGenericProxyTypeArguments, declaringTypeArguments, isReverseMapping: false, out var fieldBinding, out failureDetail))
                    {
                        return false;
                    }

                    binding = isGetter
                                  ? ForwardBinding.ForFieldGet(proxyAccessor, targetField, fieldBinding)
                                  : ForwardBinding.ForFieldSet(proxyAccessor, targetField, fieldBinding);
                    return true;
                }

                default:
                    failureDetail = $"Proxy accessor '{proxyAccessor.FullName}' can't be bound to '{targetMember.FullName}'.";
                    return false;
            }
        }

        /// <summary>
        /// Attempts to detect value-type member mutation semantics that dynamic ducktyping rejects.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        /// <param name="detail">The detail value.</param>
        /// <returns>true when the setter mutates a value-type member; otherwise, false.</returns>
        private static bool TryGetStructMemberMutationFailureDetail(MethodDef proxyMethod, MethodDef targetMethod, out string? detail)
        {
            detail = null;
            var proxyMethodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();
            if (!proxyMethodName.StartsWith("set_", StringComparison.Ordinal))
            {
                return false;
            }

            if (targetMethod.IsStatic || targetMethod.DeclaringType is null || !targetMethod.DeclaringType.IsValueType)
            {
                return false;
            }

            // The message of the DuckTypeStructMembersCannotBeChangedException dynamic duck typing throws.
            detail = $"Modifying struct members is not supported. [{targetMethod.DeclaringType.ReflectionFullName}]";
            return true;
        }

        /// <summary>
        /// Determines whether field failure detail corresponds to readonly-field setter mismatch.
        /// </summary>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true when the failure reason indicates readonly-field mismatch; otherwise, false.</returns>
        private static bool IsReadonlyFieldFailure(string failureReason)
        {
            return failureReason.IndexOf("is readonly and cannot be set", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Determines whether failure detail corresponds to return-type mismatch semantics.
        /// </summary>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true when the detail indicates return-type mismatch; otherwise, false.</returns>
        private static bool IsReturnTypeFailure(string failureReason)
        {
            return failureReason.IndexOf("Return type mismatch", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Attempts to resolve a dynamic-equivalent property-cannot-be-written failure for setter accessors.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <param name="detail">The detail value.</param>
        /// <returns>true when the proxy setter targets a get-only property; otherwise, false.</returns>
        private static bool TryResolvePropertyCantBeWrittenFailure(
            TypeDef targetType,
            MethodDef proxyMethod,
            bool allowPrivateBaseMembers,
            out string? detail)
        {
            var phaseStopwatch = StartProfilePhase();
            if (_currentProfile is not null)
            {
                _currentProfile.PropertyCantBeWrittenResolutionCount++;
            }

            try
            {
            detail = null;
            var proxyMethodPlan = GetOrCreateProxyMethodPlan(proxyMethod);
            var proxyMethodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();
            if (!proxyMethodName.StartsWith("set_", StringComparison.Ordinal) ||
                proxyMethod.MethodSig.Params.Count != 1 ||
                proxyMethod.MethodSig.RetType.ElementType != ElementType.Void)
            {
                return false;
            }

            var candidatePropertyNames = proxyMethodPlan.SetterTargetPropertyNames;
            if (candidatePropertyNames.Count == 0)
            {
                return false;
            }

            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
            foreach (var propertyName in candidatePropertyNames)
            {
                var propertyCandidates = targetTypePlan?.GetPropertyCandidates(propertyName, proxyMethodPlan.UseIgnoreCaseMemberMatching)
                                     ?? Array.Empty<TargetPropertyCandidate>();
                foreach (var propertyCandidate in propertyCandidates)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.PropertyCantBeWrittenCandidateCount++;
                    }

                    var property = propertyCandidate.Property;

                    if (!allowPrivateBaseMembers &&
                        propertyCandidate.IsInherited &&
                        propertyCandidate.IsEffectivelyPrivate)
                    {
                        continue;
                    }

                    if (!IsPropertyCandidateAllowedByBindingFlags(propertyCandidate, proxyMethodPlan.DuckBindingFlags, allowPrivateBaseMembers))
                    {
                        continue;
                    }

                    if (property.SetMethod is null)
                    {
                        // The message of dynamic duck typing's DuckTypePropertyCantBeWrittenException.
                        detail = $"The property '{property.Name}' can't be written, you should remove the setter from the proxy definition base type class or interface.";
                        return true;
                    }
                }
            }

            return false;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.PropertyCantBeWrittenResolutionSeconds += seconds);
            }
        }

        private static bool IsFieldCandidateAllowedByBindingFlags(TargetFieldCandidate candidate, BindingFlags bindingFlags, bool treatInheritedAsDeclared = false)
        {
            var field = candidate.Field;
            return IsMemberAllowedByBindingFlags(field.IsStatic, field.IsPublic, candidate.IsInherited && !treatInheritedAsDeclared, bindingFlags);
        }

        private static bool IsReadablePropertyCandidateAllowedByBindingFlags(TargetPropertyCandidate candidate, BindingFlags bindingFlags, bool treatInheritedAsDeclared = false)
        {
            var getter = candidate.Property.GetMethod;
            return getter is not null &&
                   IsMemberAllowedByBindingFlags(getter.IsStatic, getter.IsPublic, candidate.IsInherited && !treatInheritedAsDeclared, bindingFlags);
        }

        private static bool IsPropertyCandidateAllowedByBindingFlags(TargetPropertyCandidate candidate, BindingFlags bindingFlags, bool treatInheritedAsDeclared = false)
        {
            var property = candidate.Property;
            return (property.GetMethod is not null &&
                    IsMemberAllowedByBindingFlags(property.GetMethod.IsStatic, property.GetMethod.IsPublic, candidate.IsInherited && !treatInheritedAsDeclared, bindingFlags)) ||
                   (property.SetMethod is not null &&
                    IsMemberAllowedByBindingFlags(property.SetMethod.IsStatic, property.SetMethod.IsPublic, candidate.IsInherited && !treatInheritedAsDeclared, bindingFlags));
        }

        private static string BuildForwardBindingPlanCacheKey(
            DuckTypeAotMapping mapping,
            string targetTypeIdentityKey,
            ProxyMethodPlan proxyMethodPlan,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            // The proxy type matters, not only the type declaring the proxy method: dynamic duck typing binds an inherited proxy
            // member from the properties of the proxy type (e.g. one per name, or a [Duck] on a redeclaration).
            return string.Concat(
                mapping.Mode.ToString(),
                "|",
                mapping.ProxyAssemblyName,
                "|",
                mapping.ProxyTypeName,
                "|",
                targetTypeIdentityKey,
                "|",
                proxyMethodPlan.IdentityKey,
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericProxyTypeArguments),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericTargetTypeArguments));
        }

        private static string BuildStructCopyBindingPlanCacheKey(TypeDef targetType, FieldDef proxyField, IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments, IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            return string.Concat(
                BuildTypeIdentityKey(targetType),
                "|",
                BuildFieldIdentityKey(proxyField),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericProxyTypeArguments),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericTargetTypeArguments));
        }

        /// <summary>
        /// Resolves the runtime types of a mapping, so the generator can use the dynamic engine's own member selection.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The proxy type (or the type declaring the proxy member) value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyRuntimeType">The runtime proxy type.</param>
        /// <param name="targetRuntimeType">The runtime target type.</param>
        /// <returns>
        /// true when both runtime types are loaded; false when they can't be used (e.g. contracts with metadata-only duck
        /// attributes, or assemblies the generator can't load), so the metadata resolution applies.
        /// </returns>
        private static bool TryResolveDynamicSelectionTypes(
            DuckTypeAotMapping mapping,
            TypeDef proxyType,
            TypeDef targetType,
            out Type? proxyRuntimeType,
            out Type? targetRuntimeType)
        {
            proxyRuntimeType = null;
            targetRuntimeType = null;
            var assemblyPaths = runtimeTypeResolutionAssemblyPathsByName;

            // A reverse proxy type the registry generates: dynamic duck typing binds the one it creates for the same pair.
            if (_currentExecutionContext?.GeneratedTypeRuntimeTypes.TryGetValue(targetType, out var generatedTypeRuntimeType) == true)
            {
                return assemblyPaths is not null &&
                       !(_currentExecutionContext.MetadataOnlyMappings.TryGetValue(mapping.Key, out var generatedMetadataOnly) && generatedMetadataOnly) &&
                       assemblyPaths.TryGetValue(DuckTypeAotNameHelpers.NormalizeAssemblyName(mapping.ProxyAssemblyName), out var generatedProxyAssemblyPath) &&
                       TryResolveRuntimeType(mapping.ProxyAssemblyName, generatedProxyAssemblyPath, mapping.ProxyTypeName, out proxyRuntimeType) &&
                       proxyRuntimeType is not null &&
                       (targetRuntimeType = generatedTypeRuntimeType) is not null;
            }

            // The decision of the mapping (see EmitResolvedTypeMapping), not of the type declaring the member.
            var metadataOnly = _currentExecutionContext?.MetadataOnlyMappings.TryGetValue(mapping.Key, out var decided) == true
                                   ? decided
                                   : GetMetadataOnlyDuckAttributesCached(proxyType) != MetadataOnlyDuckAttributes.None ||
                                     GetMetadataOnlyDuckAttributesCached(targetType) == MetadataOnlyDuckAttributes.Own;
            return assemblyPaths is not null &&
                   !metadataOnly &&
                   assemblyPaths.TryGetValue(DuckTypeAotNameHelpers.NormalizeAssemblyName(mapping.ProxyAssemblyName), out var proxyAssemblyPath) &&
                   assemblyPaths.TryGetValue(DuckTypeAotNameHelpers.NormalizeAssemblyName(mapping.TargetAssemblyName), out var targetAssemblyPath) &&
                   TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out proxyRuntimeType) &&
                   TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out targetRuntimeType) &&
                   proxyRuntimeType is not null &&
                   targetRuntimeType is not null;
        }

        /// <summary>
        /// Determines whether a mapping was checked against metadata only, not with dynamic duck typing in the generator: it uses
        /// duck attributes dynamic duck typing doesn't read, or its types couldn't be evaluated with it.
        /// </summary>
        /// <param name="mappingKey">The mapping key.</param>
        /// <returns>true if the mapping was checked against metadata only; otherwise, false.</returns>
        private static bool IsCheckedAgainstMetadataOnly(string mappingKey)
        {
            return _currentExecutionContext is { } context &&
                   (context.MetadataOnlyEvaluations.Contains(mappingKey) ||
                    (context.MetadataOnlyMappings.TryGetValue(mappingKey, out var metadataOnly) && metadataOnly));
        }

        /// <summary>
        /// Determines whether dynamic duck typing in the generator can't see the duck attributes a mapping depends on: standalone
        /// contracts can declare their own, which it doesn't read. Those are the attributes of the proxy and of the types of its
        /// members (e.g. a [DuckCopy] struct a member is duck chained to), of its closed generic arguments and, for a reverse
        /// mapping, of the delegation type and its member types too.
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <param name="proxyType">The proxy type definition.</param>
        /// <param name="targetType">The target type definition.</param>
        /// <returns>true if the mapping depends on duck attributes dynamic duck typing doesn't read; otherwise, false.</returns>
        private static bool UsesMetadataOnlyDuckAttributes(DuckTypeAotMapping mapping, TypeDef proxyType, TypeDef targetType)
        {
            var context = _currentExecutionContext;
            if (context is not null && context.MetadataOnlyMappings.TryGetValue(mapping.Key, out var cached))
            {
                return cached;
            }

            var reverse = mapping.Mode == DuckTypeAotMappingMode.Reverse;
            var targetAttributes = GetMetadataOnlyDuckAttributesCached(targetType);
            var result = GetMetadataOnlyDuckAttributesCached(proxyType) != MetadataOnlyDuckAttributes.None ||
                         targetAttributes == MetadataOnlyDuckAttributes.Own ||
                         (reverse && targetAttributes == MetadataOnlyDuckAttributes.MemberTypes) ||
                         GenericArgumentsUseMetadataOnlyDuckAttributes(mapping.ProxyAssemblyName, mapping.ProxyTypeName) ||
                         (reverse && GenericArgumentsUseMetadataOnlyDuckAttributes(mapping.TargetAssemblyName, mapping.TargetTypeName));
            if (context is not null)
            {
                context.MetadataOnlyMappings[mapping.Key] = result;
            }

            return result;
        }

        private static MetadataOnlyDuckAttributes GetMetadataOnlyDuckAttributesCached(TypeDef type)
        {
            var cache = metadataOnlyDuckAttributesByType ??= new Dictionary<TypeDef, MetadataOnlyDuckAttributes>();
            if (!cache.TryGetValue(type, out var metadataOnlyDuckAttributes))
            {
                metadataOnlyDuckAttributes = GetMetadataOnlyDuckAttributes(type);
                cache[type] = metadataOnlyDuckAttributes;
            }

            return metadataOnlyDuckAttributes;
        }

        /// <summary>
        /// Determines whether the generic arguments of a closed generic mapping type use duck attributes dynamic duck typing
        /// doesn't read, from their runtime types (see <see cref="UsesMetadataOnlyDuckAttributes(DuckTypeAotMapping, TypeDef, TypeDef)"/>).
        /// </summary>
        /// <param name="assemblyName">The assembly name of the mapping type.</param>
        /// <param name="typeName">The name of the mapping type.</param>
        /// <returns>true if a generic argument uses such attributes; otherwise, false.</returns>
        private static bool GenericArgumentsUseMetadataOnlyDuckAttributes(string assemblyName, string typeName)
        {
            return DuckTypeAotNameHelpers.IsClosedGenericTypeName(typeName) &&
                   runtimeTypeResolutionAssemblyPathsByName is { } assemblyPaths &&
                   assemblyPaths.TryGetValue(DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName), out var assemblyPath) &&
                   TryResolveRuntimeType(assemblyName, assemblyPath, typeName, out var runtimeType) &&
                   runtimeType is not null &&
                   runtimeType.GetGenericArguments().Any(UsesMetadataOnlyDuckAttributesCached);

            static bool UsesMetadataOnlyDuckAttributesCached(Type type)
            {
                var cache = _currentExecutionContext?.MetadataOnlyRuntimeTypes;
                if (cache is null || !cache.TryGetValue(type, out var usesMetadataOnlyDuckAttributes))
                {
                    usesMetadataOnlyDuckAttributes = UsesMetadataOnlyDuckAttributes(type);
                    if (cache is not null)
                    {
                        cache[type] = usesMetadataOnlyDuckAttributes;
                    }
                }

                return usesMetadataOnlyDuckAttributes;
            }

            static bool UsesMetadataOnlyDuckAttributes(Type type)
            {
                try
                {
                    while (type.HasElementType)
                    {
                        type = type.GetElementType()!;
                    }

                    if (type.IsGenericType && type.GetGenericArguments().Any(UsesMetadataOnlyDuckAttributesCached))
                    {
                        return true;
                    }

                    const BindingFlags AllDeclaredMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                    return type.GetCustomAttributesData()
                               .Concat(type.GetMembers(AllDeclaredMembers).SelectMany(member => member.GetCustomAttributesData()))
                               .Any(attribute => IsDuckAttributeReadByType(attribute.AttributeType.FullName) &&
                                                 !string.Equals(attribute.AttributeType.Assembly.GetName().Name, DatadogTraceAssemblyName, StringComparison.Ordinal));
                }
                catch (Exception)
                {
                    // Attributes the generator can't load: dynamic duck typing in the generator can't read them either.
                    return true;
                }
            }
        }

        /// <summary>
        /// Gets the target method the dynamic engine selects for a forward proxy method, using its own selection code
        /// (DuckType.SelectForwardTargetMethodForAot) on the runtime types.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="targetMethod">The selected target method, or null when the dynamic engine selects none.</param>
        /// <param name="proxyRuntimeType">The runtime proxy type.</param>
        /// <param name="targetRuntimeType">The runtime target type.</param>
        /// <returns>
        /// true when the dynamic selection is known; false when the runtime types can't be used, so the metadata resolution applies.
        /// </returns>
        private static bool TryGetDynamicForwardTargetMethod(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            MethodDef proxyMethod,
            out MethodDef? targetMethod,
            out Type? proxyRuntimeType,
            out Type? targetRuntimeType)
        {
            targetMethod = null;
            targetRuntimeType = null;
            proxyRuntimeType = null;
            if (proxyMethod.DeclaringType is not { } proxyDeclaringType ||
                !TryResolveDynamicSelectionTypes(mapping, proxyDeclaringType, targetType, out proxyRuntimeType, out targetRuntimeType))
            {
                return false;
            }

            try
            {
                // The dynamic engine reads duck attributes (and ValueWithType<T>) by type identity, so its selection only
                // applies when the proxy is bound to the same Datadog.Trace the generator runs.
                if (FindRuntimeMethod(proxyRuntimeType!, proxyMethod) is not { } proxyRuntimeMethod ||
                    !IsBoundToGeneratorDatadogTrace(proxyRuntimeMethod))
                {
                    return false;
                }

                if (DuckType.SelectForwardTargetMethodForAot(targetRuntimeType!, proxyRuntimeMethod) is not { } selectedRuntimeMethod)
                {
                    return true;
                }

                // A selected method that can't be mapped back to metadata leaves the decision to the metadata resolution.
                targetMethod = FindMethodDefInHierarchy(targetType, selectedRuntimeMethod);
                return targetMethod is not null;
            }
            catch (Exception)
            {
                // E.g. a target member whose dependencies can't be loaded: fall back to the metadata resolution.
                targetMethod = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the message of the exception dynamic duck typing throws when it finds no target member for a proxy member:
        /// DuckTypePropertyOrFieldNotFoundException for a property, DuckTypeTargetMethodNotFoundException for a method.
        /// </summary>
        /// <param name="proxyMethod">The proxy method (a property accessor, or a method).</param>
        /// <param name="targetType">The target type.</param>
        /// <returns>The message.</returns>
        private static string CreateMissingTargetMemberMessage(MethodDef proxyMethod, TypeDef targetType)
        {
            if (proxyMethod.IsSpecialName && FindPropertyFromAccessor(proxyMethod) is { } proxyProperty)
            {
                var duckName = proxyProperty.CustomAttributes
                                            .Where(attribute => IsDuckAttributeType(attribute.TypeFullName))
                                            .Select(attribute => attribute.NamedArguments.FirstOrDefault(argument => argument.Name == "Name")?.Argument.Value?.ToString())
                                            .FirstOrDefault(name => !StringUtil.IsNullOrEmpty(name)) ?? proxyProperty.Name.String;
                return $"The property or field '{duckName}' for the proxy property '{proxyProperty.Name}' was not found in the instance of type '{targetType.ReflectionFullName}'.";
            }

            // MethodInfo.ToString(): the return and parameter types, primitive ones by their name.
            return $"The target method for the proxy method '{FormatTypeName(proxyMethod.MethodSig.RetType)} {proxyMethod.Name}({string.Join(", ", proxyMethod.MethodSig.Params.Select(FormatTypeName))})' was not found.";

            static bool IsDuckAttributeType(string attributeTypeName)
                => attributeTypeName is "Datadog.Trace.DuckTyping.DuckAttribute" or "Datadog.Trace.DuckTyping.DuckFieldAttribute" or "Datadog.Trace.DuckTyping.DuckPropertyOrFieldAttribute";

            static string FormatTypeName(TypeSig type)
                => type.ElementType is >= ElementType.Void and <= ElementType.R8 or ElementType.I or ElementType.U
                       ? type.TypeName
                       : type.ReflectionFullName ?? type.FullName;
        }

        /// <summary>
        /// Determines whether dynamic duck typing binds a proxy method of an array mapping to one of the methods the runtime adds
        /// to array types (Get, Set, Address), which aren't in metadata: the generated proxy binds the members of System.Array.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type definition (System.Array for an array mapping).</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="runtimeArrayMethod">The runtime array method dynamic duck typing binds.</param>
        /// <param name="runtimeArrayType">The array type of the mapping.</param>
        /// <returns>true if dynamic duck typing binds a runtime array method; otherwise, false.</returns>
        private static bool TryGetDynamicRuntimeArrayMethod(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            MethodDef proxyMethod,
            out MethodInfo? runtimeArrayMethod,
            out Type? runtimeArrayType)
        {
            runtimeArrayMethod = null;
            runtimeArrayType = null;
            if (!IsArrayTargetMapping(mapping) ||
                proxyMethod.DeclaringType is not { } proxyDeclaringType ||
                !TryResolveDynamicSelectionTypes(mapping, proxyDeclaringType, targetType, out var proxyRuntimeType, out runtimeArrayType) ||
                runtimeArrayType?.IsArray != true)
            {
                return false;
            }

            try
            {
                runtimeArrayMethod = FindRuntimeMethod(proxyRuntimeType!, proxyMethod) is { } proxyRuntimeMethod &&
                                     IsBoundToGeneratorDatadogTrace(proxyRuntimeMethod)
                                         ? DuckType.SelectForwardTargetMethodForAot(runtimeArrayType, proxyRuntimeMethod)
                                         : null;
                return runtimeArrayMethod?.DeclaringType?.IsArray == true;
            }
            catch (Exception)
            {
                runtimeArrayMethod = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the [DuckReverseMethod] method of the delegation type the dynamic engine implements a reverse proxy method with
        /// (DuckType.SelectReverseImplementationMethodsForAot).
        /// </summary>
        /// <param name="mapping">The reverse mapping value.</param>
        /// <param name="targetType">The delegation type value.</param>
        /// <param name="proxyMethod">The overridden method of the type the reverse proxy derives from.</param>
        /// <param name="implementationMethod">The implementation method, or null when the dynamic engine implements none.</param>
        /// <returns>true when the dynamic selection is known; false when the metadata resolution applies.</returns>
        private static bool TryGetDynamicReverseImplementationMethod(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            MethodDef proxyMethod,
            out MethodDef? implementationMethod)
        {
            implementationMethod = null;
            if (proxyMethod.DeclaringType is not { } proxyDeclaringType ||
                !TryResolveDynamicSelectionTypes(mapping, proxyDeclaringType, targetType, out var proxyRuntimeType, out var targetRuntimeType))
            {
                return false;
            }

            try
            {
                // Property accessors are implemented by the [DuckReverseMethod] properties, the other methods by the methods.
                var accessor = proxyMethod.IsGetter || proxyMethod.IsSetter;
                if (FindRuntimeMethod(proxyRuntimeType!, proxyMethod) is not { } overriddenRuntimeMethod ||
                    !IsBoundToGeneratorDatadogTrace(overriddenRuntimeMethod) ||
                    _currentExecutionContext?.GetOrCreateDynamicReverseSelection(
                        accessor ? mapping.Key + "|accessors" : mapping.Key,
                        () => accessor
                                  ? DuckType.SelectReverseImplementationAccessorsForAot(proxyRuntimeType!, targetRuntimeType!)
                                  : DuckType.SelectReverseImplementationMethodsForAot(proxyRuntimeType!, targetRuntimeType!)) is not { } selection)
                {
                    return false;
                }

                var overriddenDefinition = overriddenRuntimeMethod.GetBaseDefinition();
                foreach (var selectedMethod in selection)
                {
                    var selectedDefinition = selectedMethod.Key.GetBaseDefinition();
                    if (selectedDefinition.MetadataToken == overriddenDefinition.MetadataToken && selectedDefinition.Module == overriddenDefinition.Module)
                    {
                        implementationMethod = FindMethodDefInHierarchy(targetType, selectedMethod.Value);
                        return implementationMethod is not null;
                    }
                }

                // Known: no [DuckReverseMethod] method implements it.
                return true;
            }
            catch (Exception)
            {
                implementationMethod = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the target member the dynamic engine binds a forward proxy property accessor to: the matching accessor of the
        /// target property it selects (DuckType.SelectForwardTargetPropertyOrFieldForAot), or the target field.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyAccessor">The proxy property accessor.</param>
        /// <param name="isGetter">Whether the proxy accessor is a getter.</param>
        /// <param name="targetMember">The target accessor (<see cref="MethodDef"/>) or <see cref="FieldDef"/>, or null when the dynamic engine binds none.</param>
        /// <param name="proxyRuntimeType">The runtime proxy type.</param>
        /// <param name="targetRuntimeType">The runtime target type.</param>
        /// <returns>true when the dynamic selection is known; false when the metadata resolution applies.</returns>
        private static bool TryGetDynamicForwardTargetPropertyMember(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            MethodDef proxyAccessor,
            bool isGetter,
            out IMemberDef? targetMember,
            out Type? proxyRuntimeType,
            out Type? targetRuntimeType)
        {
            targetMember = null;
            proxyRuntimeType = null;
            targetRuntimeType = null;
            if (proxyAccessor.DeclaringType is not { } proxyDeclaringType ||
                !TryResolveDynamicSelectionTypes(mapping, proxyDeclaringType, targetType, out proxyRuntimeType, out targetRuntimeType))
            {
                return false;
            }

            try
            {
                if (FindRuntimeMethod(proxyRuntimeType!, proxyAccessor) is not { } proxyRuntimeAccessor ||
                    !IsBoundToGeneratorDatadogTrace(proxyRuntimeAccessor))
                {
                    return false;
                }

                switch (DuckType.SelectForwardTargetPropertyOrFieldForAot(proxyRuntimeType!, targetRuntimeType!, proxyRuntimeAccessor))
                {
                    case PropertyInfo targetProperty:
                        // A target property without this accessor makes dynamic duck typing fail (can't be read or written).
                        if ((isGetter ? targetProperty.GetGetMethod(nonPublic: true) : targetProperty.GetSetMethod(nonPublic: true)) is not { } targetAccessor)
                        {
                            return true;
                        }

                        targetMember = FindMethodDefInHierarchy(targetType, targetAccessor);
                        return targetMember is not null;
                    case FieldInfo targetField:
                        targetMember = FindFieldDefInHierarchy(targetType, targetField);
                        return targetMember is not null;
                    default:
                        return true;
                }
            }
            catch (Exception)
            {
                targetMember = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the target member (property or field) the dynamic engine copies into a [DuckCopy] struct field
        /// (DuckType.SelectDuckCopyTargetMemberForAot).
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyField">The [DuckCopy] struct field.</param>
        /// <param name="targetMember">The target <see cref="PropertyDef"/> or <see cref="FieldDef"/>, or null when the dynamic engine binds none.</param>
        /// <returns>true when the dynamic selection is known; false when the metadata resolution applies.</returns>
        private static bool TryGetDynamicDuckCopyTargetMember(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            FieldDef proxyField,
            out IMemberDef? targetMember)
        {
            targetMember = null;
            if (proxyField.DeclaringType is not { } proxyStructType ||
                !TryResolveDynamicSelectionTypes(mapping, proxyStructType, targetType, out var proxyRuntimeType, out var targetRuntimeType))
            {
                return false;
            }

            try
            {
                if (FindRuntimeField(proxyRuntimeType!, proxyField) is not { } proxyRuntimeField ||
                    !IsBoundToGeneratorDatadogTrace(proxyRuntimeField))
                {
                    return false;
                }

                switch (DuckType.SelectDuckCopyTargetMemberForAot(targetRuntimeType!, proxyRuntimeField))
                {
                    case PropertyInfo targetProperty:
                        targetMember = FindPropertyDefInHierarchy(targetType, targetProperty);
                        return targetMember is not null;
                    case FieldInfo targetField:
                        targetMember = FindFieldDefInHierarchy(targetType, targetField);
                        return targetMember is not null;
                    default:
                        return true;
                }
            }
            catch (Exception)
            {
                targetMember = null;
                return false;
            }
        }

        /// <summary>
        /// Determines whether a proxy member only uses Datadog.Trace types (duck attributes, ValueWithType, proxy contracts)
        /// from the Datadog.Trace assembly the generator runs, so the dynamic engine sees them as it would at runtime.
        /// </summary>
        /// <param name="proxyMember">The runtime proxy method or field.</param>
        /// <returns>true if no other copy of Datadog.Trace is involved; otherwise, false.</returns>
        private static bool IsBoundToGeneratorDatadogTrace(MemberInfo proxyMember)
        {
            var generatorDatadogTrace = typeof(DuckType).Assembly;
            var generatorDatadogTraceName = generatorDatadogTrace.GetName().Name;

            bool IsGeneratorCopy(Assembly assembly)
                => assembly == generatorDatadogTrace || !string.Equals(assembly.GetName().Name, generatorDatadogTraceName, StringComparison.Ordinal);

            bool IsTypeBound(Type type)
            {
                if (type.HasElementType)
                {
                    return IsTypeBound(type.GetElementType()!);
                }

                if (type.IsGenericParameter)
                {
                    return true;
                }

                return IsGeneratorCopy(type.Assembly) && (!type.IsGenericType || type.GetGenericArguments().All(IsTypeBound));
            }

            bool AreAttributesBound(MemberInfo member) => member.CustomAttributes.All(attribute => IsReadByGeneratorDuckTyping(attribute.AttributeType));

            if (proxyMember.DeclaringType is not { } declaringType || !IsTypeBound(declaringType) || !AreAttributesBound(proxyMember))
            {
                return false;
            }

            if (proxyMember is FieldInfo field)
            {
                return IsTypeBound(field.FieldType);
            }

            if (proxyMember is not MethodInfo method ||
                !IsTypeBound(method.ReturnType) ||
                !method.GetParameters().All(parameter => IsTypeBound(parameter.ParameterType)))
            {
                return false;
            }

            // An accessor's duck attributes are on its property.
            const BindingFlags AllDeclared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return declaringType.GetProperties(AllDeclared)
                                .Where(property => property.GetMethod == method || property.SetMethod == method)
                                .All(AreAttributesBound);
        }

        /// <summary>
        /// Finds the runtime field of a type (including base types) defined by a metadata field.
        /// </summary>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <param name="field">The metadata field value.</param>
        /// <returns>The runtime field, or null when it isn't found.</returns>
        private static FieldInfo? FindRuntimeField(Type runtimeType, FieldDef field)
        {
            if (field.Module?.Mvid is not { } moduleVersionId)
            {
                return null;
            }

            var metadataToken = unchecked((int)field.MDToken.Raw);
            foreach (var type in EnumerateRuntimeTypeHierarchy(runtimeType))
            {
                foreach (var runtimeField in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (runtimeField.MetadataToken == metadataToken && runtimeField.Module.ModuleVersionId == moduleVersionId)
                    {
                        return runtimeField;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the metadata field of a type hierarchy that defines a runtime field.
        /// </summary>
        /// <param name="type">The type definition value.</param>
        /// <param name="runtimeField">The runtime field value.</param>
        /// <returns>The metadata field, or null when it isn't found.</returns>
        private static FieldDef? FindFieldDefInHierarchy(TypeDef type, FieldInfo runtimeField)
            => FindMemberDefInHierarchy(type, runtimeField, static current => current.Fields);

        /// <summary>
        /// Finds the metadata property of a type hierarchy that defines a runtime property.
        /// </summary>
        /// <param name="type">The type definition value.</param>
        /// <param name="runtimeProperty">The runtime property value.</param>
        /// <returns>The metadata property, or null when it isn't found.</returns>
        private static PropertyDef? FindPropertyDefInHierarchy(TypeDef type, PropertyInfo runtimeProperty)
            => FindMemberDefInHierarchy(type, runtimeProperty, static current => current.Properties);

        /// <summary>
        /// Creates value with type value field ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
        /// <param name="innerTypeSig">The inner type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IField CreateValueWithTypeValueFieldRef(ModuleDef moduleDef, TypeSig wrapperTypeSig, TypeSig innerTypeSig)
        {
            var cacheKey = string.Concat(BuildTypeSigCacheKey(wrapperTypeSig), "|", BuildTypeSigCacheKey(innerTypeSig));
            if (_currentExecutionContext?.TryGetValueWithTypeValueFieldRef(cacheKey, out var cachedField) == true)
            {
                return cachedField!;
            }

            var importedWrapperTypeSig = ImportTypeSigCached(moduleDef, wrapperTypeSig, $"ValueWithType wrapper field '{wrapperTypeSig.FullName}'");
            var typeSpec = moduleDef.UpdateRowId(new TypeSpecUser(importedWrapperTypeSig));
            var fieldRef = new MemberRefUser(moduleDef, "Value", new FieldSig(new GenericVar(0)), typeSpec);
            var importedField = moduleDef.UpdateRowId(fieldRef);
            _currentExecutionContext?.CacheValueWithTypeValueFieldRef(cacheKey, importedField);
            return importedField;
        }

        /// <summary>
        /// Attempts to find forward target field.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="accessorKind">The accessor kind value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <param name="targetField">The target field value.</param>
        /// <param name="fieldBinding">The field binding value.</param>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryFindForwardTargetField(
            TypeDef targetType,
            MethodDef proxyMethod,
            FieldAccessorKind accessorKind,
            bool allowPrivateBaseMembers,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            bool isReverseMapping,
            out FieldDef? targetField,
            out ForwardFieldBindingInfo fieldBinding,
            out string? failureReason)
        {
            var phaseStopwatch = StartProfilePhase();
            if (_currentProfile is not null)
            {
                _currentProfile.ForwardFieldResolutionCount++;
            }

            try
            {
            targetField = null;
            fieldBinding = default;
            failureReason = null;
            var proxyMethodPlan = GetOrCreateProxyMethodPlan(proxyMethod);
            var candidateFieldNames = proxyMethodPlan.ForwardTargetFieldNames;
            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);

            foreach (var candidateFieldName in candidateFieldNames)
            {
                var fieldCandidates = targetTypePlan?.GetFieldCandidates(candidateFieldName, proxyMethodPlan.UseIgnoreCaseMemberMatching) ?? Array.Empty<TargetFieldCandidate>();
                foreach (var fieldCandidate in fieldCandidates)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.ForwardFieldCandidateEnumeratedCount++;
                    }

                    var candidate = fieldCandidate.Field;
                    if (!allowPrivateBaseMembers &&
                        fieldCandidate.IsInherited &&
                        candidate.IsPrivate)
                    {
                        continue;
                    }

                    if (!IsFieldCandidateAllowedByBindingFlags(fieldCandidate, proxyMethodPlan.DuckBindingFlags, allowPrivateBaseMembers))
                    {
                        continue;
                    }

                    if (!AreFieldAccessorSignatureCompatible(proxyMethod, candidate, accessorKind, closedGenericProxyTypeArguments, GetClassMemberGenericTypeArguments(targetType, candidate.DeclaringType, closedGenericTargetTypeArguments), isReverseMapping, out var candidateFieldBinding, out failureReason))
                    {
                        continue;
                    }

                    targetField = candidate;
                    fieldBinding = candidateFieldBinding;
                    return true;
                }
            }

            return false;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardFieldResolutionSeconds += seconds);
            }
        }

        /// <summary>
        /// Determines whether a field accessor signature is compatible with the target field.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="targetField">The target field value.</param>
        /// <param name="accessorKind">The accessor kind value.</param>
        /// <param name="fieldBinding">The field binding value.</param>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool AreFieldAccessorSignatureCompatible(
            MethodDef proxyMethod,
            FieldDef targetField,
            FieldAccessorKind accessorKind,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            bool isReverseMapping,
            out ForwardFieldBindingInfo fieldBinding,
            out string? failureReason)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
            fieldBinding = ForwardFieldBindingInfo.None();
            failureReason = null;
            var targetFieldType = SubstituteTypeAndMethodGenericTypeArguments(targetField.FieldSig.Type, closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
            switch (accessorKind)
            {
                case FieldAccessorKind.Getter:
                {
                    var proxyReturnType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                    if (TryCreateReturnConversion(proxyReturnType, targetFieldType, out var returnConversion))
                    {
                        fieldBinding = ForwardFieldBindingInfo.FromReturnConversion(returnConversion);
                        return true;
                    }

                    failureReason = $"Return type mismatch between proxy method '{proxyMethod.FullName}' and target field '{targetField.FullName}'.";
                    return false;
                }

                case FieldAccessorKind.Setter:
                {
                    if (targetField.IsLiteral || targetField.IsInitOnly)
                    {
                        failureReason = $"Target field '{targetField.FullName}' is readonly and cannot be set by proxy method '{proxyMethod.FullName}'.";
                        return false;
                    }

                    if (targetField.DeclaringType is not null && targetField.DeclaringType.IsValueType && !targetField.IsStatic)
                    {
                        // The message of the DuckTypeStructMembersCannotBeChangedException dynamic duck typing throws.
                        failureReason = $"Modifying struct members is not supported. [{targetField.DeclaringType.ReflectionFullName}]";
                        return false;
                    }

                    var proxyParameterType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.Params[0], closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                    if (TryCreateMethodArgumentConversion(proxyParameterType, targetFieldType, isReverseMapping, enforceMethodSelectionRules: false, out var argumentConversion))
                    {
                        fieldBinding = ForwardFieldBindingInfo.FromArgumentConversion(argumentConversion);
                        return true;
                    }

                    failureReason = $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target field '{targetField.FullName}'.";
                    return false;
                }

                default:
                    failureReason = $"Proxy method '{proxyMethod.FullName}' does not map to a supported field accessor.";
                    return false;
            }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardFieldSignatureCompatibilitySeconds += seconds);
            }
        }

        /// <summary>
        /// Determines whether reverse target custom attribute should be ignored during proxy emission.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ShouldSkipReverseCopiedCustomAttribute(CustomAttribute customAttribute)
        {
            var fullName = customAttribute.TypeFullName;
            return string.Equals(fullName, DuckAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckCopyAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckFieldAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckPropertyOrFieldAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckIgnoreAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckIncludeAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckReverseMethodAttributeTypeName, StringComparison.Ordinal)
                || fullName is "Datadog.Trace.DuckTyping.DuckTypeAttribute" or "Datadog.Trace.DuckTyping.DuckReverseAttribute" or "Datadog.Trace.DuckTyping.DuckReverseDelegationAttribute";
        }

        private static ReverseCustomAttributePlan GetOrCreateReverseCustomAttributePlan(ModuleDef moduleDef, TypeDef targetType)
        {
            if (_currentExecutionContext?.TryGetReverseCustomAttributePlan(targetType, out var cachedPlan) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ReverseCustomAttributePlanCacheHits++;
                }

                return cachedPlan!;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.ReverseCustomAttributePlanCacheMisses++;
            }

            var clonedAttributes = new List<CustomAttributeClonePlan>();
            foreach (var customAttribute in targetType.CustomAttributes)
            {
                if (ShouldSkipReverseCopiedCustomAttribute(customAttribute))
                {
                    continue;
                }

                if (customAttribute.NamedArguments.Count > 0)
                {
                    var namedArgumentsFailure = new ReverseCustomAttributePlan(
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeCustomAttributeNamedArguments,
                        $"Custom attribute '{customAttribute.TypeFullName}' on target type '{targetType.FullName}' contains named arguments and is not supported.");
                    _currentExecutionContext?.CacheReverseCustomAttributePlan(targetType, namedArgumentsFailure);
                    return namedArgumentsFailure;
                }

                if (!TryCreateCustomAttributeClonePlan(moduleDef, customAttribute, out var clonePlan))
                {
                    var cloneFailure = new ReverseCustomAttributePlan(
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        $"Unable to copy custom attribute '{customAttribute.TypeFullName}' from target type '{targetType.FullName}'.");
                    _currentExecutionContext?.CacheReverseCustomAttributePlan(targetType, cloneFailure);
                    return cloneFailure;
                }

                clonedAttributes.Add(clonePlan!);
            }

            var reversePlan = new ReverseCustomAttributePlan(clonedAttributes);
            _currentExecutionContext?.CacheReverseCustomAttributePlan(targetType, reversePlan);
            return reversePlan;
        }

        private static bool TryCreateCustomAttributeClonePlan(ModuleDef moduleDef, CustomAttribute sourceAttribute, out CustomAttributeClonePlan? clonePlan)
        {
            clonePlan = null;
            if (!TryImportCustomAttributeTypeCached(moduleDef, sourceAttribute.Constructor, out var importedCtor))
            {
                return false;
            }

            var importedArguments = new CAArgument[sourceAttribute.ConstructorArguments.Count];
            for (var argumentIndex = 0; argumentIndex < sourceAttribute.ConstructorArguments.Count; argumentIndex++)
            {
                importedArguments[argumentIndex] = ImportCustomAttributeArgument(moduleDef, sourceAttribute.ConstructorArguments[argumentIndex]);
            }

            clonePlan = new CustomAttributeClonePlan(importedCtor, importedArguments);
            return true;
        }

        private static bool TryImportCustomAttributeTypeCached(ModuleDef moduleDef, ICustomAttributeType sourceAttributeType, out ICustomAttributeType importedAttributeType)
        {
            if (_currentExecutionContext?.TryGetImportedCustomAttributeType(sourceAttributeType, out importedAttributeType) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ImportCacheHits++;
                }

                return true;
            }

            if (moduleDef.Import(sourceAttributeType) is not ICustomAttributeType importedCtor)
            {
                importedAttributeType = null!;
                return false;
            }

            _currentExecutionContext?.CacheImportedCustomAttributeType(sourceAttributeType, importedCtor);
            if (_currentProfile is not null)
            {
                _currentProfile.ImportCacheMisses++;
            }

            importedAttributeType = importedCtor;
            return true;
        }

        /// <summary>
        /// Imports a custom attribute constructor argument into the generated module context.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="argument">The argument value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static CAArgument ImportCustomAttributeArgument(ModuleDef moduleDef, CAArgument argument)
        {
            var importedType = moduleDef.Import(argument.Type);
            object? importedValue = argument.Value;
            if (argument.Value is IList<CAArgument> arrayArguments)
            {
                var importedArrayArguments = new List<CAArgument>(arrayArguments.Count);
                for (var index = 0; index < arrayArguments.Count; index++)
                {
                    importedArrayArguments.Add(ImportCustomAttributeArgument(moduleDef, arrayArguments[index]));
                }

                importedValue = importedArrayArguments;
            }
            else if (argument.Value is CAArgument nestedArgument)
            {
                importedValue = ImportCustomAttributeArgument(moduleDef, nestedArgument);
            }

            return new CAArgument(importedType, importedValue);
        }

        /// <summary>
        /// Determines whether a proxy interface is explicitly marked to emit as a class.
        /// </summary>
        /// <param name="typeDef">Proxy type definition to inspect.</param>
        /// <returns><see langword="true"/> when <c>Datadog.Trace.DuckTyping.DuckAsClassAttribute</c> is present.</returns>
        private static bool HasDuckAsClassAttribute(TypeDef typeDef)
        {
            foreach (var customAttribute in typeDef.CustomAttributes)
            {
                if (string.Equals(customAttribute.TypeFullName, DuckAsClassAttributeTypeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Represents struct copy field binding.
        /// </summary>
        private readonly struct StructCopyFieldBinding
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="StructCopyFieldBinding"/> struct.
            /// </summary>
            /// <param name="proxyField">The proxy field value.</param>
            /// <param name="sourceKind">The source kind value.</param>
            /// <param name="sourceProperty">The source property value.</param>
            /// <param name="sourceField">The source field value.</param>
            /// <param name="returnConversion">The return conversion value.</param>
            private StructCopyFieldBinding(
                FieldDef proxyField,
                StructCopySourceKind sourceKind,
                PropertyDef? sourceProperty,
                FieldDef? sourceField,
                MethodReturnConversion returnConversion)
            {
                ProxyField = proxyField;
                SourceKind = sourceKind;
                SourceProperty = sourceProperty;
                SourceField = sourceField;
                ReturnConversion = returnConversion;
            }

            /// <summary>
            /// Gets proxy field.
            /// </summary>
            /// <value>The proxy field value.</value>
            internal FieldDef ProxyField { get; }

            /// <summary>
            /// Gets source kind.
            /// </summary>
            /// <value>The source kind value.</value>
            internal StructCopySourceKind SourceKind { get; }

            /// <summary>
            /// Gets source property.
            /// </summary>
            /// <value>The source property value.</value>
            internal PropertyDef? SourceProperty { get; }

            /// <summary>
            /// Gets source field.
            /// </summary>
            /// <value>The source field value.</value>
            internal FieldDef? SourceField { get; }

            /// <summary>
            /// Gets return conversion.
            /// </summary>
            /// <value>The return conversion value.</value>
            internal MethodReturnConversion ReturnConversion { get; }

            /// <summary>
            /// Creates a binding for property-based struct-copy projection.
            /// </summary>
            /// <param name="proxyField">The proxy field value.</param>
            /// <param name="sourceProperty">The source property value.</param>
            /// <param name="returnConversion">The return conversion value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static StructCopyFieldBinding ForProperty(FieldDef proxyField, PropertyDef sourceProperty, MethodReturnConversion returnConversion)
            {
                return new StructCopyFieldBinding(proxyField, StructCopySourceKind.Property, sourceProperty, sourceField: null, returnConversion);
            }

            /// <summary>
            /// Creates a binding for field-based struct-copy projection.
            /// </summary>
            /// <param name="proxyField">The proxy field value.</param>
            /// <param name="sourceField">The source field value.</param>
            /// <param name="returnConversion">The return conversion value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static StructCopyFieldBinding ForField(FieldDef proxyField, FieldDef sourceField, MethodReturnConversion returnConversion)
            {
                return new StructCopyFieldBinding(proxyField, StructCopySourceKind.Field, sourceProperty: null, sourceField, returnConversion);
            }
        }

        /// <summary>
        /// Represents forward binding.
        /// </summary>
        private readonly struct ForwardBinding
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ForwardBinding"/> struct.
            /// </summary>
            /// <param name="kind">The kind value.</param>
            /// <param name="proxyMethod">The proxy method value.</param>
            /// <param name="targetMethod">The target method value.</param>
            /// <param name="targetField">The target field value.</param>
            /// <param name="methodBinding">The method binding value.</param>
            /// <param name="fieldBinding">The field binding value.</param>
            private ForwardBinding(
                ForwardBindingKind kind,
                MethodDef proxyMethod,
                MethodDef? targetMethod,
                FieldDef? targetField,
                ForwardMethodBindingInfo? methodBinding,
                ForwardFieldBindingInfo? fieldBinding)
            {
                Kind = kind;
                ProxyMethod = proxyMethod;
                TargetMethod = targetMethod;
                TargetField = targetField;
                MethodBinding = methodBinding;
                FieldBinding = fieldBinding;
            }

            /// <summary>
            /// Gets kind.
            /// </summary>
            /// <value>The kind value.</value>
            internal ForwardBindingKind Kind { get; }

            /// <summary>
            /// Gets proxy method.
            /// </summary>
            /// <value>The proxy method value.</value>
            internal MethodDef ProxyMethod { get; }

            /// <summary>
            /// Gets target method.
            /// </summary>
            /// <value>The target method value.</value>
            internal MethodDef? TargetMethod { get; }

            /// <summary>
            /// Gets target field.
            /// </summary>
            /// <value>The target field value.</value>
            internal FieldDef? TargetField { get; }

            /// <summary>
            /// Gets method binding.
            /// </summary>
            /// <value>The method binding value.</value>
            internal ForwardMethodBindingInfo? MethodBinding { get; }

            /// <summary>
            /// Gets field binding.
            /// </summary>
            /// <value>The field binding value.</value>
            internal ForwardFieldBindingInfo? FieldBinding { get; }

            /// <summary>
            /// Creates a forward binding for method delegation.
            /// </summary>
            /// <param name="proxyMethod">The proxy method value.</param>
            /// <param name="targetMethod">The target method value.</param>
            /// <param name="methodBinding">The method binding value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardBinding ForMethod(MethodDef proxyMethod, MethodDef targetMethod, ForwardMethodBindingInfo methodBinding)
            {
                return new ForwardBinding(ForwardBindingKind.Method, proxyMethod, targetMethod, targetField: null, methodBinding, fieldBinding: null);
            }

            /// <summary>
            /// Creates a forward binding for field getter delegation.
            /// </summary>
            /// <param name="proxyMethod">The proxy method value.</param>
            /// <param name="targetField">The target field value.</param>
            /// <param name="fieldBinding">The field binding value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardBinding ForFieldGet(MethodDef proxyMethod, FieldDef targetField, ForwardFieldBindingInfo fieldBinding)
            {
                return new ForwardBinding(ForwardBindingKind.FieldGet, proxyMethod, targetMethod: null, targetField, methodBinding: null, fieldBinding);
            }

            /// <summary>
            /// Creates a forward binding for field setter delegation.
            /// </summary>
            /// <param name="proxyMethod">The proxy method value.</param>
            /// <param name="targetField">The target field value.</param>
            /// <param name="fieldBinding">The field binding value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardBinding ForFieldSet(MethodDef proxyMethod, FieldDef targetField, ForwardFieldBindingInfo fieldBinding)
            {
                return new ForwardBinding(ForwardBindingKind.FieldSet, proxyMethod, targetMethod: null, targetField, methodBinding: null, fieldBinding);
            }
        }

        /// <summary>
        /// Represents forward field binding info.
        /// </summary>
        private readonly struct ForwardFieldBindingInfo
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ForwardFieldBindingInfo"/> struct.
            /// </summary>
            /// <param name="argumentConversion">The argument conversion value.</param>
            /// <param name="returnConversion">The return conversion value.</param>
            private ForwardFieldBindingInfo(MethodArgumentConversion argumentConversion, MethodReturnConversion returnConversion)
            {
                ArgumentConversion = argumentConversion;
                ReturnConversion = returnConversion;
            }

            /// <summary>
            /// Gets argument conversion.
            /// </summary>
            /// <value>The argument conversion value.</value>
            internal MethodArgumentConversion ArgumentConversion { get; }

            /// <summary>
            /// Gets return conversion.
            /// </summary>
            /// <value>The return conversion value.</value>
            internal MethodReturnConversion ReturnConversion { get; }

            /// <summary>
            /// Creates a no-op conversion descriptor.
            /// </summary>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo None()
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.None());
            }

            /// <summary>
            /// Creates field binding from an argument conversion descriptor.
            /// </summary>
            /// <param name="argumentConversion">The argument conversion value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo FromArgumentConversion(MethodArgumentConversion argumentConversion)
            {
                return new ForwardFieldBindingInfo(argumentConversion, MethodReturnConversion.None());
            }

            /// <summary>
            /// Creates field binding from a return conversion descriptor.
            /// </summary>
            /// <param name="returnConversion">The return conversion value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo FromReturnConversion(MethodReturnConversion returnConversion)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), returnConversion);
            }

            /// <summary>
            /// Creates a conversion that unwraps ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo UnwrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.UnwrapValueWithType(wrapperTypeSig, innerTypeSig), MethodReturnConversion.None());
            }

            /// <summary>
            /// Creates a conversion that wraps a value into ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo WrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.WrapValueWithType(wrapperTypeSig, innerTypeSig));
            }

            /// <summary>
            /// Creates a conversion that extracts IDuckType.Instance.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.ExtractDuckTypeInstance(wrapperTypeSig, innerTypeSig), MethodReturnConversion.None());
            }

            /// <summary>
            /// Creates a conversion that chains through DuckType.CreateCache<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo DuckChainToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.DuckChainToProxy(wrapperTypeSig, innerTypeSig));
            }

            /// <summary>
            /// Creates a conversion that applies runtime type adaptation.
            /// </summary>
            /// <param name="actualTypeSig">The actual type sig value.</param>
            /// <param name="expectedTypeSig">The expected type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo TypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.TypeConversion(actualTypeSig, expectedTypeSig), MethodReturnConversion.None());
            }
        }

        /// <summary>
        /// Represents by ref write back plan.
        /// </summary>
        private readonly struct ByRefWriteBackPlan
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ByRefWriteBackPlan"/> struct.
            /// </summary>
            /// <param name="proxyParameter">The proxy parameter value.</param>
            /// <param name="targetLocal">The target local value.</param>
            /// <param name="parameterBinding">The parameter binding value.</param>
            internal ByRefWriteBackPlan(Parameter proxyParameter, Local targetLocal, MethodParameterBinding parameterBinding)
            {
                ProxyParameter = proxyParameter;
                TargetLocal = targetLocal;
                ParameterBinding = parameterBinding;
            }

            /// <summary>
            /// Gets proxy parameter.
            /// </summary>
            /// <value>The proxy parameter value.</value>
            internal Parameter ProxyParameter { get; }

            /// <summary>
            /// Gets target local.
            /// </summary>
            /// <value>The target local value.</value>
            internal Local TargetLocal { get; }

            /// <summary>
            /// Gets parameter binding.
            /// </summary>
            /// <value>The parameter binding value.</value>
            internal MethodParameterBinding ParameterBinding { get; }
        }

        private sealed class ForwardBindingPlanCacheEntry
        {
            internal ForwardBindingPlanCacheEntry(ForwardBinding binding)
            {
                Succeeded = true;
                Binding = binding;
            }

            internal ForwardBindingPlanCacheEntry(string status, string diagnosticCode, string detail)
            {
                Succeeded = false;
                FailureStatus = status;
                FailureDiagnosticCode = diagnosticCode;
                FailureDetail = detail;
            }

            internal bool Succeeded { get; }

            internal ForwardBinding Binding { get; }

            internal string? FailureStatus { get; }

            internal string? FailureDiagnosticCode { get; }

            internal string? FailureDetail { get; }
        }

        private sealed class StructCopyFieldBindingPlanCacheEntry
        {
            internal StructCopyFieldBindingPlanCacheEntry(StructCopyFieldBinding binding)
            {
                Succeeded = true;
                Binding = binding;
            }

            internal StructCopyFieldBindingPlanCacheEntry(string status, string diagnosticCode, string detail)
            {
                Succeeded = false;
                FailureStatus = status;
                FailureDiagnosticCode = diagnosticCode;
                FailureDetail = detail;
            }

            internal bool Succeeded { get; }

            internal StructCopyFieldBinding Binding { get; }

            internal string? FailureStatus { get; }

            internal string? FailureDiagnosticCode { get; }

            internal string? FailureDetail { get; }
        }

        private sealed class ReverseCustomAttributePlan
        {
            internal ReverseCustomAttributePlan(IReadOnlyList<CustomAttributeClonePlan> clonedAttributes)
            {
                Succeeded = true;
                ClonedAttributes = clonedAttributes;
            }

            internal ReverseCustomAttributePlan(string status, string diagnosticCode, string detail)
            {
                Succeeded = false;
                FailureStatus = status;
                FailureDiagnosticCode = diagnosticCode;
                FailureDetail = detail;
                ClonedAttributes = Array.Empty<CustomAttributeClonePlan>();
            }

            internal bool Succeeded { get; }

            internal IReadOnlyList<CustomAttributeClonePlan> ClonedAttributes { get; }

            internal string? FailureStatus { get; }

            internal string? FailureDiagnosticCode { get; }

            internal string? FailureDetail { get; }
        }

        private sealed class CustomAttributeClonePlan
        {
            internal CustomAttributeClonePlan(ICustomAttributeType constructor, IReadOnlyList<CAArgument> constructorArguments)
            {
                Constructor = constructor;
                ConstructorArguments = constructorArguments;
            }

            internal ICustomAttributeType Constructor { get; }

            internal IReadOnlyList<CAArgument> ConstructorArguments { get; }
        }

        private sealed partial class EmitterExecutionContext
        {
            private readonly Dictionary<string, ForwardBindingPlanCacheEntry> _forwardBindingsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, StructCopyFieldBindingPlanCacheEntry> _structCopyBindingsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<TypeDef, ReverseCustomAttributePlan> _reverseCustomAttributePlansByType = new();

            internal bool TryGetForwardBindingPlan(string key, out ForwardBindingPlanCacheEntry plan)
                => _forwardBindingsByKey.TryGetValue(key, out plan!);

            internal void CacheForwardBindingPlan(string key, ForwardBindingPlanCacheEntry plan)
                => _forwardBindingsByKey[key] = plan;

            internal bool TryGetStructCopyBindingPlan(string key, out StructCopyFieldBindingPlanCacheEntry plan)
                => _structCopyBindingsByKey.TryGetValue(key, out plan!);

            internal void CacheStructCopyBindingPlan(string key, StructCopyFieldBindingPlanCacheEntry plan)
                => _structCopyBindingsByKey[key] = plan;

            internal bool TryGetReverseCustomAttributePlan(TypeDef targetType, out ReverseCustomAttributePlan plan)
                => _reverseCustomAttributePlansByType.TryGetValue(targetType, out plan!);

            internal void CacheReverseCustomAttributePlan(TypeDef targetType, ReverseCustomAttributePlan plan)
                => _reverseCustomAttributePlansByType[targetType] = plan;
        }
    }
}
