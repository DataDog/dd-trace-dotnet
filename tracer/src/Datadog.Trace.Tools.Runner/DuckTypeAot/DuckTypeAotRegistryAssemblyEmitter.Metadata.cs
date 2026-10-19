// <copyright file="DuckTypeAotRegistryAssemblyEmitter.Metadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.DuckTyping.Aot;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Metadata foundations of the registry emitter: loading the input modules, importing types and members into the registry
    /// module, resolving the runtime types of metadata types, comparing types and reading the duck typing attributes. The other
    /// parts of the emitter build on these.
    /// </summary>
    internal static partial class DuckTypeAotRegistryAssemblyEmitter
    {
        /// <summary>
        /// Defines the bootstrap namespace constant.
        /// </summary>
        private const string BootstrapNamespace = "Datadog.Trace.DuckTyping.Generated";

        /// <summary>
        /// Defines the bootstrap type name constant.
        /// </summary>
        private const string BootstrapTypeName = "DuckTypeAotRegistryBootstrap";

        /// <summary>
        /// Defines the bootstrap initialize method name constant.
        /// </summary>
        private const string BootstrapInitializeMethodName = "Initialize";

        /// <summary>
        /// Defines the generated proxy namespace constant.
        /// </summary>
        private const string GeneratedProxyNamespace = "Datadog.Trace.DuckTyping.Generated.Proxies";

        /// <summary>
        /// Splits bootstrap registration IL into smaller methods to avoid runtime/JIT stack issues on older TFMs.
        /// </summary>
        private const int BootstrapMappingsPerMethod = 128;

        /// <summary>
        /// The most emission passes: each one leaves out what the previous one generated but the runtime can't load.
        /// </summary>
        private const int MaxEmissionPasses = 3;

        /// <summary>
        /// The HRESULT of the FileLoadException an invalid assembly name throws (FUSION_E_INVALID_NAME).
        /// </summary>
        private const int InvalidAssemblyNameHResult = unchecked((int)0x80131047);

        /// <summary>
        /// Defines datadog trace assembly name constant.
        /// </summary>
        private const string DatadogTraceAssemblyName = "Datadog.Trace";

        /// <summary>
        /// Defines the aot contract schema version constant.
        /// </summary>
        private const string AotContractSchemaVersion = "1";

        /// <summary>
        /// Defines the status code unsupported proxy kind constant.
        /// </summary>
        private const string StatusCodeUnsupportedProxyKind = "DTAOT0202";

        /// <summary>
        /// Defines the status code missing proxy type constant.
        /// </summary>
        private const string StatusCodeMissingProxyType = "DTAOT0204";

        /// <summary>
        /// Defines the status code missing target type constant.
        /// </summary>
        private const string StatusCodeMissingTargetType = "DTAOT0205";

        /// <summary>
        /// Defines the status code missing method constant.
        /// </summary>
        private const string StatusCodeMissingMethod = "DTAOT0207";

        /// <summary>
        /// Defines the status code incompatible signature constant.
        /// </summary>
        private const string StatusCodeIncompatibleSignature = "DTAOT0209";

        /// <summary>
        /// Defines the status code for proxy types Reflection.Emit can't create (sealed base type, abstract member left without
        /// implementation by [DuckIgnore]).
        /// </summary>
        private const string StatusCodeUnloadableProxyType = "DTAOT0215";

        /// <summary>
        /// Defines the duck attribute type name constant.
        /// </summary>
        private const string DuckAttributeTypeName = "Datadog.Trace.DuckTyping.DuckAttribute";

        /// <summary>
        /// Defines the duck field attribute type name constant.
        /// </summary>
        private const string DuckFieldAttributeTypeName = "Datadog.Trace.DuckTyping.DuckFieldAttribute";

        /// <summary>
        /// Defines the duck property or field attribute type name constant.
        /// </summary>
        private const string DuckPropertyOrFieldAttributeTypeName = "Datadog.Trace.DuckTyping.DuckPropertyOrFieldAttribute";

        /// <summary>
        /// Defines the duck copy attribute type name constant.
        /// </summary>
        private const string DuckCopyAttributeTypeName = "Datadog.Trace.DuckTyping.DuckCopyAttribute";

        /// <summary>
        /// Defines the duck reverse method attribute type name constant.
        /// </summary>
        private const string DuckReverseMethodAttributeTypeName = "Datadog.Trace.DuckTyping.DuckReverseMethodAttribute";

        /// <summary>
        /// Defines the duck ignore attribute type name constant.
        /// </summary>
        private const string DuckIgnoreAttributeTypeName = "Datadog.Trace.DuckTyping.DuckIgnoreAttribute";

        /// <summary>
        /// Defines the duck include attribute type name constant.
        /// </summary>
        private const string DuckIncludeAttributeTypeName = "Datadog.Trace.DuckTyping.DuckIncludeAttribute";

        /// <summary>
        /// Defines the default duck binding flags used when the proxy does not override them.
        /// </summary>
        private const BindingFlags DefaultDuckBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        /// <summary>
        /// Defines the duck as class attribute type name constant.
        /// </summary>
        private const string DuckAsClassAttributeTypeName = "Datadog.Trace.DuckTyping.DuckAsClassAttribute";

        /// <summary>
        /// The start of the message of the DuckTypeException dynamic duck typing throws when Reflection.Emit can't create a
        /// proxy type (see DuckType.CreateProxyType).
        /// </summary>
        private const string CreatingDuckTypeFailurePrefix = "Error creating duck type for type: ";

        /// <summary>
        /// Defines the duck kind property constant.
        /// </summary>
        private const int DuckKindProperty = 0;

        /// <summary>
        /// Defines the duck kind field constant.
        /// </summary>
        private const int DuckKindField = 1;

        /// <summary>
        /// Defines the duck kind property or field constant.
        /// </summary>
        private const int DuckKindPropertyOrField = 2;

        /// <summary>
        /// The directory of the generator's runtime framework assemblies.
        /// </summary>
        private static readonly string? RuntimeFrameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);

        // The registry layer (DuckTypeAotRegistryAssemblyEmitter.cs) sets the per-emission state at the start of each pass.
#pragma warning disable CS0169, CS0649
        /// <summary>
        /// Stores runtime assembly paths used by metadata-to-runtime type resolution during emission.
        /// Thread-static like the other per-emission state, so concurrent Emit calls don't clear each other's map.
        /// </summary>
        [ThreadStatic]
        private static IReadOnlyDictionary<string, string>? runtimeTypeResolutionAssemblyPathsByName;

        /// <summary>
        /// Caches UsesMetadataOnlyDuckAttributes per type definition during emission.
        /// </summary>
        [ThreadStatic]
        private static Dictionary<TypeDef, MetadataOnlyDuckAttributes>? metadataOnlyDuckAttributesByType;

        /// <summary>
        /// Imports runtime types and members into the registry being emitted (see DatadogTraceImportMapper).
        /// </summary>
        [ThreadStatic]
        private static Importer? runtimeImporter;

        /// <summary>
        /// The registrations whose generated proxy type a previous emission pass produced but the runtime can't load, by
        /// registration key, with the load failure: this pass registers their failure instead.
        /// </summary>
        [ThreadStatic]
        private static IReadOnlyDictionary<string, string>? unloadableGeneratedProxyTypes;

        [ThreadStatic]
        private static EmitterProfile? _currentProfile;

        [ThreadStatic]
        private static EmitterExecutionContext? _currentExecutionContext;
#pragma warning restore CS0169, CS0649

        /// <summary>
        /// Where a type hierarchy uses duck attributes dynamic duck typing doesn't read (see GetMetadataOnlyDuckAttributes).
        /// </summary>
        private enum MetadataOnlyDuckAttributes
        {
            /// <summary>
            /// Nowhere.
            /// </summary>
            None,

            /// <summary>
            /// On the types or their members.
            /// </summary>
            Own,

            /// <summary>
            /// Only on the types of their members.
            /// </summary>
            MemberTypes,
        }

        /// <summary>
        /// What dynamic duck typing does with a mapping.
        /// </summary>
        private enum DynamicCreationOutcome
        {
            /// <summary>
            /// The generator can't evaluate the mapping with dynamic duck typing (types it can't load...).
            /// </summary>
            Unavailable,

            /// <summary>
            /// Dynamic duck typing creates the proxy type.
            /// </summary>
            Created,

            /// <summary>
            /// Dynamic duck typing fails to create the proxy type.
            /// </summary>
            Failed,
        }

        /// <summary>
        /// Defines named constants for field resolution mode.
        /// </summary>
        private enum FieldResolutionMode
        {
            /// <summary>
            /// Represents disabled.
            /// </summary>
            Disabled,

            /// <summary>
            /// Represents allow fallback.
            /// </summary>
            AllowFallback,

            /// <summary>
            /// Represents field only.
            /// </summary>
            FieldOnly
        }

        private static Stopwatch? StartProfilePhase() => _currentProfile is null ? null : Stopwatch.StartNew();

        private static void StopProfilePhase(Stopwatch? stopwatch, Action<double> record)
        {
            if (stopwatch is null || _currentProfile is null)
            {
                return;
            }

            stopwatch.Stop();
            record(stopwatch.Elapsed.TotalSeconds);
        }

        private static void WriteProfileSummary(DuckTypeAotMappingResolutionResult mappingResolutionResult, int runtimeRegistrationCount)
        {
            var profile = _currentProfile;
            if (profile is null)
            {
                return;
            }

            profile.Total.Stop();

            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: total={profile.Total.Elapsed.TotalSeconds:F3}s canonicalMappings={mappingResolutionResult.Mappings.Count} runtimeRegistrations={runtimeRegistrationCount}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: loadProxyModules={profile.LoadProxyModulesSeconds:F3}s loadTargetModules={profile.LoadTargetModulesSeconds:F3}s runtimeTypeMap={profile.BuildRuntimeTypeResolutionMapSeconds:F3}s buildTargetTypeIndex={profile.BuildTargetTypeIndexSeconds:F3}s buildRuntimeRegistrations={profile.BuildRuntimeRegistrationsSeconds:F3}s emitLoop={profile.EmitLoopSeconds:F3}s");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: emitMapping={profile.EmitMappingSeconds:F3}s count={profile.EmitMappingCount} knownFailureRegistration={profile.KnownFailureRegistrationSeconds:F3}s count={profile.KnownFailureRegistrationCount} dynamicFailureProbe={profile.DynamicFailureProbeSeconds:F3}s count={profile.DynamicFailureProbeCount}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: runtimeTypeHits={profile.RuntimeTypeCacheHits} runtimeTypeMisses={profile.RuntimeTypeCacheMisses} runtimeTypeFallbackHits={profile.RuntimeTypeFallbackHits} runtimeTypeUnresolved={profile.RuntimeTypeUnresolved}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: failureClassifierFastPath={profile.FailureClassifierFastPathCount} failureClassifierFallback={profile.FailureClassifierFallbackCount} importCacheHits={profile.ImportCacheHits} importCacheMisses={profile.ImportCacheMisses}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: registrationPlanning={profile.RegistrationPlanningSeconds:F3}s bindingPlanHits={profile.ForwardBindingPlanCacheHits} bindingPlanMisses={profile.ForwardBindingPlanCacheMisses} conversionPlanHits={profile.ConversionPlanCacheHits} conversionPlanMisses={profile.ConversionPlanCacheMisses}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: methodCallTargetHits={profile.MethodCallTargetCacheHits} methodCallTargetMisses={profile.MethodCallTargetCacheMisses} propertySigHits={profile.PropertySignatureCacheHits} propertySigMisses={profile.PropertySignatureCacheMisses} reverseAttributePlanHits={profile.ReverseCustomAttributePlanCacheHits} reverseAttributePlanMisses={profile.ReverseCustomAttributePlanCacheMisses}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: forwardCollect={profile.ForwardBindingCollectionSeconds:F3}s structCopyCollect={profile.StructCopyBindingCollectionSeconds:F3}s duckIncludeCollect={profile.DuckIncludeCollectionSeconds:F3}s");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: forwardResolve={profile.ForwardBindingResolutionSeconds:F3}s forwardMethodBind={profile.ForwardMethodBindingSeconds:F3}s forwardParameterBind={profile.ForwardParameterBindingSeconds:F3}s");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: forwardCandidatesEnumerated={profile.ForwardCandidateEnumeratedCount} dedupRejects={profile.ForwardCandidateDedupRejectCount} nameRejects={profile.ForwardCandidateNameRejectCount} parameterTypeRejects={profile.ForwardCandidateParameterTypeRejectCount} privateRejects={profile.ForwardCandidatePrivateRejectCount} accepted={profile.ForwardCandidateAcceptedCount}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: forwardOutcomes methodSuccess={profile.ForwardResolutionMethodSuccessCount} fieldSuccess={profile.ForwardResolutionFieldSuccessCount} firstFailure={profile.ForwardResolutionFirstFailureCount} propertyCantBeWritten={profile.ForwardResolutionPropertyCantBeWrittenCount} missingTarget={profile.ForwardResolutionMissingTargetCount} ambiguous={profile.ForwardResolutionAmbiguousCount}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: closedGenericArgs={profile.ForwardClosedGenericMethodArgumentResolutionSeconds:F3}s count={profile.ForwardClosedGenericMethodArgumentResolutionCount} fieldLookup={profile.ForwardFieldResolutionSeconds:F3}s fieldCandidates={profile.ForwardFieldCandidateEnumeratedCount} fieldSignature={profile.ForwardFieldSignatureCompatibilitySeconds:F3}s propertyCantWriteLookup={profile.PropertyCantBeWrittenResolutionSeconds:F3}s propertyCandidates={profile.PropertyCantBeWrittenCandidateCount}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: forwardCandidateList={profile.ForwardCandidateListBuildSeconds:F3}s cacheHits={profile.ForwardCandidateListCacheHits} cacheMisses={profile.ForwardCandidateListCacheMisses}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: substitution={profile.TypeSubstitutionSeconds:F3}s substitutionHits={profile.TypeSubstitutionCacheHits} substitutionMisses={profile.TypeSubstitutionCacheMisses} runtimeTypeFromTypeSig={profile.RuntimeTypeFromTypeSigSeconds:F3}s");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: methodCallTarget={profile.MethodCallTargetSeconds:F3}s emitArgConversion={profile.EmitMethodArgumentConversionSeconds:F3}s emitReturnConversion={profile.EmitMethodReturnConversionSeconds:F3}s");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: emitMethodBody={profile.EmitForwardMethodBodySeconds:F3}s count={profile.EmitForwardMethodBodyCount} emitFieldGetBody={profile.EmitForwardFieldGetBodySeconds:F3}s count={profile.EmitForwardFieldGetBodyCount} emitFieldSetBody={profile.EmitForwardFieldSetBodySeconds:F3}s count={profile.EmitForwardFieldSetBodyCount}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: emitArgKinds none={profile.EmitArgumentConversionNoneCount} unwrap={profile.EmitArgumentConversionUnwrapCount} extractDuck={profile.EmitArgumentConversionExtractDuckTypeCount} duckChain={profile.EmitArgumentConversionDuckChainCount} typeConv={profile.EmitArgumentConversionTypeConversionCount}");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: cacheKeyBuild forwardBinding={profile.ForwardBindingPlanCacheKeyBuildSeconds:F3}s forwardMethod={profile.ForwardMethodBindingPlanCacheKeyBuildSeconds:F3}s argConv={profile.MethodArgumentConversionCacheKeyBuildSeconds:F3}s returnConv={profile.MethodReturnConversionCacheKeyBuildSeconds:F3}s");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: ensureInterfaceProperty={profile.EnsureInterfacePropertyMetadataSeconds:F3}s copyMethodGenerics={profile.CopyMethodGenericParametersSeconds:F3}s");
            Console.Error.WriteLine(
                $"ducktype-aot emitter profile: importTypeDefOrRef={profile.ImportTypeDefOrRefSeconds:F3}s importTypeSig={profile.ImportTypeSigSeconds:F3}s importMethod={profile.ImportMethodSeconds:F3}s importField={profile.ImportFieldSeconds:F3}s");
        }

        /// <summary>
        /// Resolves assembly mvid.
        /// </summary>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ResolveAssemblyMvid(string assemblyPath)
        {
            using var module = ModuleDefMD.Load(assemblyPath);
            return module.Mvid?.ToString("D") ?? string.Empty;
        }

        /// <summary>
        /// Determines whether two method signatures match after substituting closed generic target type arguments.
        /// </summary>
        /// <param name="leftMethodSig">The left method signature.</param>
        /// <param name="leftClosedGenericTargetTypeArguments">The left closed generic target type arguments.</param>
        /// <param name="rightMethodSig">The right method signature.</param>
        /// <param name="rightClosedGenericTargetTypeArguments">The right closed generic target type arguments.</param>
        /// <returns>true when the effective signatures are equivalent; otherwise, false.</returns>
        private static bool AreEffectiveMethodSignaturesEquivalent(
            MethodSig? leftMethodSig,
            IReadOnlyList<TypeSig>? leftClosedGenericTargetTypeArguments,
            MethodSig? rightMethodSig,
            IReadOnlyList<TypeSig>? rightClosedGenericTargetTypeArguments)
        {
            if (leftMethodSig is null || rightMethodSig is null)
            {
                return leftMethodSig is null && rightMethodSig is null;
            }

            if (leftMethodSig.HasThis != rightMethodSig.HasThis ||
                leftMethodSig.ExplicitThis != rightMethodSig.ExplicitThis ||
                leftMethodSig.Generic != rightMethodSig.Generic ||
                leftMethodSig.GenParamCount != rightMethodSig.GenParamCount ||
                leftMethodSig.Params.Count != rightMethodSig.Params.Count)
            {
                return false;
            }

            var leftReturnType = SubstituteTypeAndMethodGenericTypeArguments(
                leftMethodSig.RetType,
                leftClosedGenericTargetTypeArguments,
                closedGenericMethodArguments: null);
            var rightReturnType = SubstituteTypeAndMethodGenericTypeArguments(
                rightMethodSig.RetType,
                rightClosedGenericTargetTypeArguments,
                closedGenericMethodArguments: null);
            if (!AreTypesEquivalent(leftReturnType, rightReturnType))
            {
                return false;
            }

            for (var parameterIndex = 0; parameterIndex < leftMethodSig.Params.Count; parameterIndex++)
            {
                var leftParameterType = SubstituteTypeAndMethodGenericTypeArguments(
                    leftMethodSig.Params[parameterIndex],
                    leftClosedGenericTargetTypeArguments,
                    closedGenericMethodArguments: null);
                var rightParameterType = SubstituteTypeAndMethodGenericTypeArguments(
                    rightMethodSig.Params[parameterIndex],
                    rightClosedGenericTargetTypeArguments,
                    closedGenericMethodArguments: null);
                if (!AreTypesEquivalent(leftParameterType, rightParameterType))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds a method signature key after substituting closed generic target type arguments.
        /// </summary>
        /// <param name="methodSig">The method signature.</param>
        /// <param name="closedGenericTargetTypeArguments">The closed generic target type arguments.</param>
        /// <returns>The effective method signature key.</returns>
        private static string BuildEffectiveMethodSignatureKey(MethodSig? methodSig, IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            if (methodSig is null)
            {
                return string.Empty;
            }

            var returnType = SubstituteTypeAndMethodGenericTypeArguments(
                methodSig.RetType,
                closedGenericTargetTypeArguments,
                closedGenericMethodArguments: null);

            var parameterTypes = methodSig.Params.Select(
                parameterType => SubstituteTypeAndMethodGenericTypeArguments(
                    parameterType,
                    closedGenericTargetTypeArguments,
                    closedGenericMethodArguments: null));

            return string.Concat(
                methodSig.HasThis ? "instance" : "static",
                "|",
                methodSig.ExplicitThis ? "explicit" : "implicit",
                "|",
                methodSig.Generic ? "generic" : "non-generic",
                "|",
                methodSig.GenParamCount.ToString(CultureInfo.InvariantCulture),
                "|",
                BuildTypeSigCacheKey(returnType),
                "|",
                BuildTypeSigSequenceCacheKey(parameterTypes));
        }

        /// <summary>
        /// Builds a stable assembly-type lookup key.
        /// </summary>
        /// <param name="assemblyName">The assembly name value.</param>
        /// <param name="typeName">The type name value.</param>
        /// <returns>The assembly-type cache key.</returns>
        private static string BuildAssemblyTypeCacheKey(string assemblyName, string typeName)
        {
            return string.Concat(
                DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName).ToUpperInvariant(),
                "|",
                typeName);
        }

        /// <summary>
        /// Builds the normalized assembly-type lookup prefix used for repeated index inserts.
        /// </summary>
        /// <param name="assemblyName">The normalized assembly name value.</param>
        /// <returns>The resulting lookup-key prefix.</returns>
        private static string BuildNormalizedAssemblyTypeCacheKeyPrefix(string assemblyName)
        {
            return string.Concat(
                assemblyName.ToUpperInvariant(),
                "|");
        }

        /// <summary>
        /// Resolves the normalized assembly name for a dnlib type definition.
        /// </summary>
        /// <param name="type">The type definition value.</param>
        /// <returns>The normalized assembly name, or an empty string when it cannot be resolved.</returns>
        private static string ResolveTypeDefAssemblyName(TypeDef type)
        {
            return DuckTypeAotNameHelpers.NormalizeAssemblyName(type.Module?.Assembly?.Name?.String ?? string.Empty);
        }

        private static string GetRuntimeTypeName(TypeDef type)
        {
            var reflectionFullName = type.ReflectionFullName;
            if (!StringUtil.IsNullOrWhiteSpace(reflectionFullName))
            {
                return reflectionFullName!.Replace('/', '+');
            }

            return (type.FullName ?? string.Empty).Replace('/', '+');
        }

        /// <summary>
        /// Determines whether an indexed alias target resolves to the canonical target type itself.
        /// </summary>
        /// <param name="canonicalTargetType">The canonical target type value.</param>
        /// <param name="candidateAssemblyName">The candidate assembly name value.</param>
        /// <param name="candidateTypeName">The candidate type name value.</param>
        /// <returns>true when the candidate is the canonical target; otherwise, false.</returns>
        private static bool IsCanonicalTargetAliasTarget(TypeDef canonicalTargetType, string candidateAssemblyName, string candidateTypeName)
        {
            return string.Equals(candidateAssemblyName, ResolveTypeDefAssemblyName(canonicalTargetType), StringComparison.OrdinalIgnoreCase) &&
                   (string.Equals(candidateTypeName, canonicalTargetType.FullName, StringComparison.Ordinal) ||
                    string.Equals(candidateTypeName, canonicalTargetType.ReflectionFullName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Determines whether a forward mapping's target is an array type or System.Array, whose proxy binds the members of
        /// System.Array (the proxy of an array type, not the one of System.Array, also serves the other array types).
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <returns>true if the target is an array type or System.Array; otherwise, false.</returns>
        private static bool IsArrayTargetMapping(DuckTypeAotMapping mapping)
            => mapping.Mode == DuckTypeAotMappingMode.Forward &&
               (DuckTypeAotNameHelpers.IsArrayTypeName(mapping.TargetTypeName) || string.Equals(mapping.TargetTypeName, "System.Array", StringComparison.Ordinal));

        /// <summary>
        /// Determines whether a forward mapping's target is System.Array itself: unlike the proxy of an array type, its proxy binds
        /// what reflection finds on System.Array (e.g. the explicit implementations of its interfaces), not on an array type, so
        /// it only serves System.Array. The proxy for the array types is the one of the representative array alias (see
        /// GetArrayViewAliasTarget).
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <returns>true if the target is System.Array; otherwise, false.</returns>
        private static bool IsSystemArrayTargetMapping(DuckTypeAotMapping mapping)
            => string.Equals(mapping.TargetTypeName, "System.Array", StringComparison.Ordinal);

        /// <summary>
        /// Determines whether instances of a target type can have a non-public type of the runtime's core library, which the
        /// registry can't name and which differ between runtimes: an interface or a class that isn't sealed, defined in the core
        /// library. The proxy of such a target also serves them (see DuckTypeAotEngine.TryGetFallbackResult).
        /// </summary>
        /// <param name="targetType">The target type definition.</param>
        /// <returns>true if the target type can have such derived types; otherwise, false.</returns>
        private static bool HasRuntimeInternalSubtypes(TypeDef targetType)
            => !targetType.IsValueType &&
               (targetType.IsInterface || !targetType.IsSealed) &&
               IsCoreLibraryAssemblyName(targetType.Module?.Assembly?.Name?.String);

        /// <summary>
        /// Determines whether an assembly is the runtime's core library.
        /// </summary>
        /// <param name="assemblyName">The assembly name.</param>
        /// <returns>true if the assembly is the core library; otherwise, false.</returns>
        private static bool IsCoreLibraryAssemblyName(string? assemblyName)
            => string.Equals(assemblyName, "System.Private.CoreLib", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(assemblyName, "mscorlib", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Imports runtime type as type sig.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static TypeSig ImportRuntimeTypeSig(ModuleDef moduleDef, Type runtimeType)
        {
            var importedTypeSig = RuntimeImporter(moduleDef).ImportAsTypeSig(runtimeType);
            if (importedTypeSig is null)
            {
                throw new InvalidOperationException($"Unable to import runtime type signature for '{runtimeType.FullName ?? runtimeType.Name}'.");
            }

            return importedTypeSig;
        }

        /// <summary>
        /// Attempts to resolve runtime type.
        /// </summary>
        /// <param name="assemblyName">The assembly name value.</param>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <param name="typeName">The type name value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner executes as a build-time tool and intentionally resolves runtime metadata from discovered assemblies.")]
        [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Type names come from explicit mapping metadata and assembly inspection performed by the tool at build time.")]
#endif
        private static bool TryResolveRuntimeType(string assemblyName, string assemblyPath, string typeName, out Type? runtimeType)
        {
            var normalizedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(assemblyPath);
            var normalizedTypeName = typeName ?? string.Empty;
            var cacheKey = string.Concat(
                DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName).ToUpperInvariant(),
                "|",
                normalizedAssemblyPath,
                "|",
                normalizedTypeName);
            if (_currentExecutionContext?.TryGetRuntimeType(cacheKey, out runtimeType) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.RuntimeTypeCacheHits++;
                }

                return runtimeType is not null;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.RuntimeTypeCacheMisses++;
            }

            runtimeType = null;
            try
            {
                var normalizedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName);
                var candidateAssembly = TryResolvePreferredRuntimeAssembly(normalizedAssemblyName, normalizedAssemblyPath);
                runtimeType = candidateAssembly?.GetType(normalizedTypeName, throwOnError: false, ignoreCase: false);
                if (runtimeType is not null)
                {
                    _currentExecutionContext?.CacheRuntimeType(cacheKey, runtimeType);
                    return true;
                }

                foreach (var assemblyQualifiedTypeName in EnumerateAssemblyQualifiedTypeNames(normalizedTypeName, normalizedAssemblyName, candidateAssembly))
                {
                    runtimeType = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
                    if (runtimeType is not null &&
                        RuntimeTypeMatchesRequestedAssembly(runtimeType, normalizedAssemblyName, normalizedAssemblyPath))
                    {
                        if (_currentProfile is not null)
                        {
                            _currentProfile.RuntimeTypeFallbackHits++;
                        }

                        _currentExecutionContext?.CacheRuntimeType(cacheKey, runtimeType);
                        return true;
                    }
                }

                foreach (var assemblyQualifiedTypeName in EnumerateAssemblyQualifiedTypeNames(normalizedTypeName, normalizedAssemblyName, candidateAssembly))
                {
                    runtimeType = Type.GetType(
                        assemblyQualifiedTypeName,
                        requestedAssemblyName => ResolveRuntimeTypeAssembly(requestedAssemblyName, normalizedAssemblyName, normalizedAssemblyPath, candidateAssembly),
                        (requestedAssembly, requestedTypeName, ignoreCase) =>
                        {
                            return requestedAssembly?.GetType(requestedTypeName, throwOnError: false, ignoreCase: ignoreCase);
                        },
                        throwOnError: false);
                    if (runtimeType is not null &&
                        RuntimeTypeMatchesRequestedAssembly(runtimeType, normalizedAssemblyName, normalizedAssemblyPath))
                    {
                        if (_currentProfile is not null)
                        {
                            _currentProfile.RuntimeTypeFallbackHits++;
                        }

                        _currentExecutionContext?.CacheRuntimeType(cacheKey, runtimeType);
                        return true;
                    }
                }

                if (TryResolveClosedGenericRuntimeType(assemblyName, normalizedAssemblyPath, normalizedTypeName, out runtimeType))
                {
                    _currentExecutionContext?.CacheRuntimeType(cacheKey, runtimeType);
                    return true;
                }

                if (TryResolveRuntimeTypeByName(normalizedTypeName, normalizedAssemblyName, normalizedAssemblyPath, out runtimeType))
                {
                    if (runtimeType is not null)
                    {
                        _currentExecutionContext?.CacheRuntimeType(cacheKey, runtimeType);
                        return true;
                    }

                    runtimeType = null;
                }
            }
            catch
            {
                // Type probing is best-effort; failures are handled by returning false to the caller.
            }

            _currentExecutionContext?.CacheRuntimeType(cacheKey, runtimeType: null);
            if (_currentProfile is not null)
            {
                _currentProfile.RuntimeTypeUnresolved++;
            }

            return false;
        }

        /// <summary>
        /// Attempts to resolve a closed generic runtime type by recursively resolving its generic definition and arguments.
        /// </summary>
        /// <param name="assemblyName">The root assembly name value.</param>
        /// <param name="assemblyPath">The preferred root assembly path value.</param>
        /// <param name="typeName">The closed generic type name value.</param>
        /// <param name="runtimeType">The resolved runtime type value.</param>
        /// <returns>true when the closed generic type was resolved; otherwise, false.</returns>
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "The ducktype AOT runner closes generic types from build-time metadata while emitting registry assemblies; this path does not execute inside the trimmed customer application.")]
#endif
        private static bool TryResolveClosedGenericRuntimeType(string assemblyName, string assemblyPath, string typeName, out Type? runtimeType)
        {
            runtimeType = null;
            if (!DuckTypeAotNameHelpers.IsClosedGenericTypeName(typeName) ||
                !TrySplitClosedGenericTypeName(typeName, out var genericTypeDefinitionName, out var genericArgumentTypeNames, out var typeNameSuffix))
            {
                return false;
            }

            if (!TryResolveRuntimeType(assemblyName, assemblyPath, genericTypeDefinitionName, out var openGenericType) ||
                openGenericType is null)
            {
                return false;
            }

            if (openGenericType.IsGenericType && openGenericType.ContainsGenericParameters && !openGenericType.IsGenericTypeDefinition)
            {
                openGenericType = openGenericType.GetGenericTypeDefinition();
            }

            if (!openGenericType.IsGenericTypeDefinition)
            {
                return false;
            }

            var resolvedGenericArguments = new Type[genericArgumentTypeNames.Count];
            var normalizedRootAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName);

            for (var i = 0; i < genericArgumentTypeNames.Count; i++)
            {
                var (genericArgumentTypeName, genericArgumentAssemblyName) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(genericArgumentTypeNames[i]);
                var normalizedGenericArgumentAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(genericArgumentAssemblyName ?? string.Empty);
                var genericArgumentAssemblyPath = string.Empty;

                if (StringUtil.IsNullOrWhiteSpace(normalizedGenericArgumentAssemblyName))
                {
                    // Unqualified arguments resolve like Type.GetType does: in the assembly of the root type, then in the core
                    // library; then in the other input assemblies (the proxy of an expanded open generic mapping gets the
                    // arguments of a closed type of a target assembly).
                    if (TryResolveUnqualifiedGenericArgument(normalizedRootAssemblyName, assemblyPath, genericArgumentTypeName) is { } unqualifiedGenericArgumentType)
                    {
                        resolvedGenericArguments[i] = unqualifiedGenericArgumentType;
                        continue;
                    }

                    return false;
                }
                else if (string.Equals(normalizedGenericArgumentAssemblyName, normalizedRootAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    genericArgumentAssemblyPath = assemblyPath;
                }
                else if (runtimeTypeResolutionAssemblyPathsByName is not null &&
                         runtimeTypeResolutionAssemblyPathsByName.TryGetValue(normalizedGenericArgumentAssemblyName, out var resolvedAssemblyPath))
                {
                    genericArgumentAssemblyPath = resolvedAssemblyPath;
                }

                if (!TryResolveRuntimeType(normalizedGenericArgumentAssemblyName, genericArgumentAssemblyPath, genericArgumentTypeName, out var resolvedGenericArgumentType) ||
                    resolvedGenericArgumentType is null)
                {
                    return false;
                }

                resolvedGenericArguments[i] = resolvedGenericArgumentType;
            }

            runtimeType = ApplyTypeNameSuffix(openGenericType.MakeGenericType(resolvedGenericArguments), typeNameSuffix);
            return runtimeType is not null;

            static Type? TryResolveUnqualifiedGenericArgument(string rootAssemblyName, string rootAssemblyPath, string typeName)
            {
                if (TryResolveRuntimeType(rootAssemblyName, rootAssemblyPath, typeName, out var rootType) && rootType is not null)
                {
                    return rootType;
                }

                var coreLibraryName = typeof(object).Assembly.GetName().Name ?? string.Empty;
                if (TryResolveRuntimeType(coreLibraryName, string.Empty, typeName, out var coreLibraryType) && coreLibraryType is not null)
                {
                    return coreLibraryType;
                }

                Type? resolved = null;
                foreach (var inputAssembly in (runtimeTypeResolutionAssemblyPathsByName ?? new Dictionary<string, string>()).OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.Equals(inputAssembly.Key, rootAssemblyName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(inputAssembly.Key, coreLibraryName, StringComparison.OrdinalIgnoreCase) ||
                        !TryResolveRuntimeType(inputAssembly.Key, inputAssembly.Value, typeName, out var inputType) ||
                        inputType is null)
                    {
                        continue;
                    }

                    if (resolved is not null && resolved != inputType)
                    {
                        // Ambiguous: the name has to be qualified.
                        return null;
                    }

                    resolved = inputType;
                }

                return resolved;
            }
        }

        /// <summary>
        /// Applies the suffix following a type name in a reflection name ("[]", "[,]", "[*]", "*", "&amp;") to a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="suffix">The suffix.</param>
        /// <returns>The resulting type, or null when the suffix isn't one.</returns>
        private static Type? ApplyTypeNameSuffix(Type type, string suffix)
        {
            var index = 0;
            while (index < suffix.Length)
            {
                switch (suffix[index])
                {
                    case ' ':
                        index++;
                        break;
                    case '*':
                        type = type.MakePointerType();
                        index++;
                        break;
                    case '&':
                        type = type.MakeByRefType();
                        index++;
                        break;
                    case '[':
                        var end = suffix.IndexOf(']', index);
                        if (end < 0)
                        {
                            return null;
                        }

                        var ranks = suffix.Substring(index + 1, end - index - 1).Replace(" ", string.Empty);
                        if (ranks.Length == 0)
                        {
                            type = type.MakeArrayType();
                        }
                        else if (ranks == "*")
                        {
                            type = type.MakeArrayType(1);
                        }
                        else if (ranks.Trim(',').Length == 0)
                        {
                            type = type.MakeArrayType(ranks.Length + 1);
                        }
                        else
                        {
                            return null;
                        }

                        index = end + 1;
                        break;
                    default:
                        return null;
                }
            }

            return type;
        }

        /// <summary>
        /// Attempts to split a closed generic reflection name into its generic definition and top-level arguments.
        /// </summary>
        /// <param name="typeName">The closed generic type name value.</param>
        /// <param name="genericTypeDefinitionName">The generic type definition name value.</param>
        /// <param name="genericArgumentTypeNames">The top-level generic argument type names value.</param>
        /// <param name="suffix">What follows the generic argument list (e.g. "[]" for an array of the closed generic type).</param>
        /// <returns>true when the split succeeded; otherwise, false.</returns>
        private static bool TrySplitClosedGenericTypeName(
            string typeName,
            out string genericTypeDefinitionName,
            out IReadOnlyList<string> genericArgumentTypeNames,
            out string suffix)
        {
            genericTypeDefinitionName = string.Empty;
            genericArgumentTypeNames = [];
            suffix = string.Empty;
            if (StringUtil.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            var genericArgumentsStart = DuckTypeAotNameHelpers.FindGenericArgumentsStart(typeName);
            if (genericArgumentsStart < 0 ||
                !DuckTypeAotNameHelpers.TrySplitGenericArguments(typeName, genericArgumentsStart, out genericArgumentTypeNames, out var genericArgumentsEnd))
            {
                genericArgumentTypeNames = [];
                return false;
            }

            genericTypeDefinitionName = typeName.Substring(0, genericArgumentsStart);
            suffix = typeName.Substring(genericArgumentsEnd);
            return true;
        }

        /// <summary>
        /// Attempts to resolve preferred runtime assembly for metadata-to-runtime bridging.
        /// </summary>
        /// <param name="normalizedAssemblyName">The normalized assembly name value.</param>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>The resolved assembly when available; otherwise, null.</returns>
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner probes discovered assemblies on disk as a build-time tool; the standalone tool build is trimmed, but the emitted customer payload is not using this reflection path.")]
#endif
        private static Assembly? TryResolvePreferredRuntimeAssembly(string normalizedAssemblyName, string assemblyPath)
        {
            var normalizedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(assemblyPath);
            var cacheKey = string.Concat(normalizedAssemblyName.ToUpperInvariant(), "|", normalizedAssemblyPath);
            if (_currentExecutionContext?.TryGetPreferredRuntimeAssembly(cacheKey, out var cachedAssembly) == true)
            {
                return cachedAssembly;
            }

            // Prefer the explicit path from mapping resolution to avoid cross-TFM collisions with already-loaded assemblies.
            if (!StringUtil.IsNullOrWhiteSpace(normalizedAssemblyPath) && File.Exists(normalizedAssemblyPath))
            {
                foreach (var loadedAssembly in GetLoadedRuntimeAssemblies())
                {
                    if (AssemblyLocationOrIdentityMatchesPath(loadedAssembly, normalizedAssemblyPath))
                    {
                        _currentExecutionContext?.CachePreferredRuntimeAssembly(cacheKey, loadedAssembly);
                        return loadedAssembly;
                    }
                }

                try
                {
                    var assembly = Assembly.LoadFrom(normalizedAssemblyPath);
                    if (AssemblyLocationOrIdentityMatchesPath(assembly, normalizedAssemblyPath))
                    {
                        _currentExecutionContext?.CachePreferredRuntimeAssembly(cacheKey, assembly);
                        return assembly;
                    }
                }
                catch
                {
                    // Continue with the safe fallback below.
                }

                var frameworkAssembly = TryLoadRuntimeFrameworkAssembly(normalizedAssemblyPath);
                _currentExecutionContext?.CachePreferredRuntimeAssembly(cacheKey, frameworkAssembly);
                return frameworkAssembly;
            }

            foreach (var loadedAssembly in GetLoadedRuntimeAssemblies())
            {
                var loadedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(loadedAssembly.GetName().Name ?? string.Empty);
                if (string.Equals(loadedAssemblyName, normalizedAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    _currentExecutionContext?.CachePreferredRuntimeAssembly(cacheKey, loadedAssembly);
                    return loadedAssembly;
                }
            }

            _currentExecutionContext?.CachePreferredRuntimeAssembly(cacheKey, assembly: null);
            return null;
        }

        /// <summary>
        /// Returns the cached snapshot of currently loaded runtime assemblies for this emit pass.
        /// </summary>
        /// <returns>The loaded runtime assemblies.</returns>
        private static IReadOnlyList<Assembly> GetLoadedRuntimeAssemblies()
        {
            return _currentExecutionContext?.LoadedRuntimeAssemblies ?? AppDomain.CurrentDomain.GetAssemblies();
        }

        /// <summary>
        /// Normalizes an assembly path for runtime assembly resolution cache keys.
        /// </summary>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>The normalized path value.</returns>
        private static string NormalizeRuntimeAssemblyPathForCache(string? assemblyPath)
        {
            if (StringUtil.IsNullOrWhiteSpace(assemblyPath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(assemblyPath!);
            }
            catch
            {
                return assemblyPath!;
            }
        }

        /// <summary>
        /// Gets the path comparison used for runtime assembly locations.
        /// </summary>
        /// <returns>The path comparison for the current platform.</returns>
        private static StringComparison GetRuntimeAssemblyPathComparison()
        {
            return Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        /// <summary>
        /// Determines whether an assembly was loaded from a specific path.
        /// </summary>
        /// <param name="assembly">The assembly value.</param>
        /// <param name="assemblyPath">The expected assembly path.</param>
        /// <returns>true when the assembly location matches the expected path; otherwise, false.</returns>
        private static bool AssemblyLocationMatchesPath(Assembly assembly, string assemblyPath)
        {
            if (StringUtil.IsNullOrWhiteSpace(assemblyPath))
            {
                return false;
            }

            try
            {
                var location = assembly.Location;
                return !StringUtil.IsNullOrWhiteSpace(location) &&
                       string.Equals(
                           NormalizeRuntimeAssemblyPathForCache(location),
                           NormalizeRuntimeAssemblyPathForCache(assemblyPath),
                           GetRuntimeAssemblyPathComparison());
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether an assembly was loaded from a specific path or represents the same file identity.
        /// </summary>
        /// <param name="assembly">The assembly value.</param>
        /// <param name="assemblyPath">The expected assembly path.</param>
        /// <returns>true when the assembly location or MVID-backed identity matches the expected path; otherwise, false.</returns>
        private static bool AssemblyLocationOrIdentityMatchesPath(Assembly assembly, string assemblyPath)
        {
            return AssemblyLocationMatchesPath(assembly, assemblyPath) || AssemblyIdentityMatchesPath(assembly, assemblyPath) || RuntimeFrameworkAssemblyStandsFor(assembly, assemblyPath);
        }

        /// <summary>
        /// Determines whether an assembly is the generator's runtime framework assembly of the same file name as a path. The
        /// runtime can't load other versions of its framework assemblies (the application's runtime pack, the NativeAOT
        /// System.Private.CoreLib, a newer framework package): its own stands for them.
        /// </summary>
        /// <param name="assembly">The assembly value.</param>
        /// <param name="assemblyPath">The expected assembly path.</param>
        /// <returns>true when the assembly is the runtime's framework assembly for that file name; otherwise, false.</returns>
        private static bool RuntimeFrameworkAssemblyStandsFor(Assembly assembly, string assemblyPath)
        {
            if (assembly.IsDynamic || StringUtil.IsNullOrWhiteSpace(assemblyPath))
            {
                return false;
            }

            try
            {
                var location = assembly.Location;
                return !StringUtil.IsNullOrWhiteSpace(location) &&
                       string.Equals(Path.GetDirectoryName(location), RuntimeFrameworkDirectory, GetRuntimeAssemblyPathComparison()) &&
                       string.Equals(Path.GetFileName(location), Path.GetFileName(assemblyPath), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Loads the generator's runtime framework assembly of the same file name as a path, if there is one.
        /// </summary>
        /// <param name="assemblyPath">The requested assembly path.</param>
        /// <returns>The runtime framework assembly; otherwise, null.</returns>
        private static Assembly? TryLoadRuntimeFrameworkAssembly(string assemblyPath)
        {
            try
            {
                var frameworkPath = RuntimeFrameworkDirectory is null ? null : Path.Combine(RuntimeFrameworkDirectory, Path.GetFileName(assemblyPath));
                return frameworkPath is not null && File.Exists(frameworkPath) ? Assembly.Load(AssemblyName.GetAssemblyName(frameworkPath)) : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Determines whether an already-loaded assembly has the same assembly identity and module version id as a file path.
        /// </summary>
        /// <param name="assembly">The assembly value.</param>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>true when the assembly identity and MVID match; otherwise, false.</returns>
        private static bool AssemblyIdentityMatchesPath(Assembly assembly, string assemblyPath)
        {
            // Reading the identity of the file loads its metadata: the type resolution of the aliases checks the same few
            // assemblies for thousands of types (the framework assemblies of NativeAOT applications never match by location).
            var context = _currentExecutionContext;
            if (context is not null && context.TryGetAssemblyIdentityMatch(assembly, assemblyPath, out var cachedMatch))
            {
                return cachedMatch;
            }

            var match = ReadAssemblyIdentityMatchesPath(assembly, assemblyPath);
            context?.CacheAssemblyIdentityMatch(assembly, assemblyPath, match);
            return match;
        }

        private static bool ReadAssemblyIdentityMatchesPath(Assembly assembly, string assemblyPath)
        {
            if (StringUtil.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
            {
                return false;
            }

            try
            {
                var requestedAssemblyName = AssemblyName.GetAssemblyName(assemblyPath).FullName;
                if (!string.Equals(requestedAssemblyName, assembly.FullName, StringComparison.Ordinal))
                {
                    return false;
                }

                var requestedMvid = ResolveAssemblyMvidValue(assemblyPath);
                return requestedMvid is not null && requestedMvid.Value == assembly.ManifestModule.ModuleVersionId;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resolves the module version id for an assembly path.
        /// </summary>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>The module version id when available; otherwise, null.</returns>
        private static Guid? ResolveAssemblyMvidValue(string assemblyPath)
        {
            try
            {
                using var module = ModuleDefMD.Load(assemblyPath);
                return module.Mvid;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Determines whether a resolved runtime type belongs to the requested assembly identity.
        /// </summary>
        /// <param name="runtimeType">The resolved runtime type.</param>
        /// <param name="normalizedAssemblyName">The normalized requested assembly name.</param>
        /// <param name="assemblyPath">The preferred requested assembly path.</param>
        /// <returns>true when the type belongs to the requested assembly; otherwise, false.</returns>
        private static bool RuntimeTypeMatchesRequestedAssembly(Type runtimeType, string normalizedAssemblyName, string assemblyPath)
        {
            var typeAssembly = GetRuntimeTypeDefinitionAssembly(runtimeType);
            if (!StringUtil.IsNullOrWhiteSpace(assemblyPath))
            {
                return AssemblyLocationOrIdentityMatchesPath(typeAssembly, assemblyPath) ||
                       AssemblyPathForwardsRuntimeType(assemblyPath, runtimeType, typeAssembly);
            }

            var typeAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(typeAssembly.GetName().Name ?? string.Empty);
            if (string.Equals(typeAssemblyName, normalizedAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return TryResolveRuntimeResolutionAssemblyPath(normalizedAssemblyName, normalizedAssemblyName, assemblyPath: string.Empty, out var requestedAssemblyPath) &&
                   AssemblyPathForwardsRuntimeType(requestedAssemblyPath, runtimeType, typeAssembly);
        }

        /// <summary>
        /// Determines whether the requested assembly path forwards the runtime type to the resolved runtime assembly.
        /// </summary>
        /// <param name="assemblyPath">The requested assembly path.</param>
        /// <param name="runtimeType">The resolved runtime type.</param>
        /// <param name="runtimeTypeAssembly">The assembly defining the resolved runtime type.</param>
        /// <returns>true when the requested assembly forwards the type to the resolved runtime assembly; otherwise, false.</returns>
        private static bool AssemblyPathForwardsRuntimeType(string assemblyPath, Type runtimeType, Assembly runtimeTypeAssembly)
        {
            // Each check loads the module of the path (see AssemblyIdentityMatchesPath).
            var context = _currentExecutionContext;
            if (context is not null && context.TryGetForwardedRuntimeType(assemblyPath, runtimeType, out var cachedForwards))
            {
                return cachedForwards;
            }

            var forwards = AssemblyPathForwardsRuntimeType(
                NormalizeRuntimeAssemblyPathForCache(assemblyPath),
                runtimeType,
                runtimeTypeAssembly,
                new HashSet<string>(StringComparer.Ordinal));
            context?.CacheForwardedRuntimeType(assemblyPath, runtimeType, forwards);
            return forwards;
        }

        /// <summary>
        /// Determines whether the requested assembly path transitively forwards the runtime type to the resolved runtime assembly.
        /// </summary>
        /// <param name="assemblyPath">The requested assembly path.</param>
        /// <param name="runtimeType">The resolved runtime type.</param>
        /// <param name="runtimeTypeAssembly">The assembly defining the resolved runtime type.</param>
        /// <param name="visitedAssemblyPaths">The visited assembly paths.</param>
        /// <returns>true when the requested assembly transitively forwards the type to the resolved runtime assembly; otherwise, false.</returns>
        private static bool AssemblyPathForwardsRuntimeType(
            string assemblyPath,
            Type runtimeType,
            Assembly runtimeTypeAssembly,
            HashSet<string> visitedAssemblyPaths)
        {
            if (StringUtil.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
            {
                return false;
            }

            if (!visitedAssemblyPaths.Add(assemblyPath))
            {
                return false;
            }

            try
            {
                using var module = ModuleDefMD.Load(assemblyPath);
                var runtimeTypeDefinition = GetRuntimeTypeDefinition(runtimeType);
                var runtimeTypeName = runtimeTypeDefinition.FullName;
                var runtimeTypeNameWithNestedSeparator = runtimeTypeName?.Replace('+', '/');
                var runtimeTypeAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(runtimeTypeAssembly.GetName().Name ?? string.Empty);
                var runtimeTypeAssemblyFullName = runtimeTypeAssembly.GetName().FullName;

                foreach (var exportedType in module.ExportedTypes)
                {
                    if (!string.Equals(exportedType.FullName, runtimeTypeName, StringComparison.Ordinal) &&
                        !string.Equals(exportedType.FullName, runtimeTypeNameWithNestedSeparator, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (exportedType.Implementation is not AssemblyRef forwardedAssembly)
                    {
                        continue;
                    }

                    var forwardedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(forwardedAssembly.Name.String);
                    if (string.Equals(forwardedAssemblyName, runtimeTypeAssemblyName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(forwardedAssembly.FullName, runtimeTypeAssemblyFullName, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (TryResolveForwardedAssemblyPath(forwardedAssemblyName, assemblyPath, out var forwardedAssemblyPath) &&
                        AssemblyPathForwardsRuntimeType(forwardedAssemblyPath, runtimeType, runtimeTypeAssembly, visitedAssemblyPaths))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Type-forwarder probing is best-effort and must not weaken assembly identity checks on failure.
            }

            return false;
        }

        /// <summary>
        /// Attempts to resolve the next assembly path in a type-forwarder chain.
        /// </summary>
        /// <param name="forwardedAssemblyName">The forwarded assembly simple name.</param>
        /// <param name="currentAssemblyPath">The current assembly path.</param>
        /// <param name="forwardedAssemblyPath">The resolved forwarded assembly path.</param>
        /// <returns>true when a forwarded assembly path was found; otherwise, false.</returns>
        private static bool TryResolveForwardedAssemblyPath(string forwardedAssemblyName, string currentAssemblyPath, out string forwardedAssemblyPath)
        {
            forwardedAssemblyPath = string.Empty;
            if (StringUtil.IsNullOrWhiteSpace(forwardedAssemblyName))
            {
                return false;
            }

            if (runtimeTypeResolutionAssemblyPathsByName is not null &&
                runtimeTypeResolutionAssemblyPathsByName.TryGetValue(forwardedAssemblyName, out var mappedAssemblyPath) &&
                !StringUtil.IsNullOrWhiteSpace(mappedAssemblyPath) &&
                File.Exists(mappedAssemblyPath))
            {
                forwardedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(mappedAssemblyPath);
                return true;
            }

            var currentDirectory = Path.GetDirectoryName(currentAssemblyPath);
            if (!StringUtil.IsNullOrWhiteSpace(currentDirectory))
            {
                var siblingAssemblyPath = Path.Combine(currentDirectory!, forwardedAssemblyName + ".dll");
                if (File.Exists(siblingAssemblyPath))
                {
                    forwardedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(siblingAssemblyPath);
                    return true;
                }
            }

            if (TryResolveCurrentRuntimeDirectoryAssemblyPath(forwardedAssemblyName, out forwardedAssemblyPath))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves the assembly that defines the underlying runtime type.
        /// </summary>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>The assembly defining the type.</returns>
        private static Assembly GetRuntimeTypeDefinitionAssembly(Type runtimeType)
            => GetRuntimeTypeDefinition(runtimeType).Assembly;

        /// <summary>
        /// Resolves the underlying runtime type definition.
        /// </summary>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>The underlying type definition.</returns>
        private static Type GetRuntimeTypeDefinition(Type runtimeType)
        {
            while (runtimeType.HasElementType && runtimeType.GetElementType() is { } elementType)
            {
                runtimeType = elementType;
            }

            if (runtimeType.IsGenericType)
            {
                runtimeType = runtimeType.GetGenericTypeDefinition();
            }

            return runtimeType;
        }

        /// <summary>
        /// Enumerates assembly-qualified type-name candidates used for runtime resolution probes.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <param name="normalizedAssemblyName">The normalized assembly name value.</param>
        /// <param name="candidateAssembly">The candidate assembly value.</param>
        /// <returns>The resulting type-name sequence.</returns>
        private static IEnumerable<string> EnumerateAssemblyQualifiedTypeNames(
            string typeName,
            string normalizedAssemblyName,
            Assembly? candidateAssembly)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (!StringUtil.IsNullOrWhiteSpace(normalizedAssemblyName))
            {
                var candidate = $"{typeName}, {normalizedAssemblyName}";
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            var candidateAssemblySimpleName = candidateAssembly?.GetName().Name;
            if (!StringUtil.IsNullOrWhiteSpace(candidateAssemblySimpleName))
            {
                var candidate = $"{typeName}, {candidateAssemblySimpleName}";
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            if (!StringUtil.IsNullOrWhiteSpace(candidateAssembly?.FullName))
            {
                var candidate = $"{typeName}, {candidateAssembly!.FullName}";
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        /// <summary>
        /// Resolves runtime assemblies requested by Type.GetType generic-name parsing.
        /// </summary>
        /// <param name="requestedAssemblyName">The requested assembly name value.</param>
        /// <param name="normalizedAssemblyName">The normalized assembly name value.</param>
        /// <param name="assemblyPath">The preferred assembly path value.</param>
        /// <param name="candidateAssembly">The candidate assembly value.</param>
        /// <returns>The resolved assembly when available; otherwise, null.</returns>
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner resolves referenced assemblies from build outputs while generating metadata. This reflective probing is intentional and confined to the build-time tool.")]
#endif
        private static Assembly? ResolveRuntimeTypeAssembly(
            AssemblyName requestedAssemblyName,
            string normalizedAssemblyName,
            string assemblyPath,
            Assembly? candidateAssembly)
        {
            var requestedSimpleName = DuckTypeAotNameHelpers.NormalizeAssemblyName(requestedAssemblyName.Name ?? string.Empty);
            var normalizedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(assemblyPath);
            if (StringUtil.IsNullOrWhiteSpace(requestedSimpleName))
            {
                return null;
            }

            var cacheKey = string.Concat(
                requestedSimpleName.ToUpperInvariant(),
                "|",
                normalizedAssemblyName.ToUpperInvariant(),
                "|",
                normalizedAssemblyPath);
            if (_currentExecutionContext?.TryGetResolvedRuntimeAssembly(cacheKey, out var cachedAssembly) == true)
            {
                return cachedAssembly;
            }

            if (candidateAssembly is not null)
            {
                var candidateSimpleName = DuckTypeAotNameHelpers.NormalizeAssemblyName(candidateAssembly.GetName().Name ?? string.Empty);
                if (string.Equals(candidateSimpleName, requestedSimpleName, StringComparison.OrdinalIgnoreCase) &&
                    (StringUtil.IsNullOrWhiteSpace(normalizedAssemblyPath) || AssemblyLocationOrIdentityMatchesPath(candidateAssembly, normalizedAssemblyPath)))
                {
                    _currentExecutionContext?.CacheResolvedRuntimeAssembly(cacheKey, candidateAssembly);
                    return candidateAssembly;
                }
            }

            if (TryResolveRuntimeResolutionAssemblyPath(requestedSimpleName, normalizedAssemblyName, normalizedAssemblyPath, out var requestedAssemblyPath))
            {
                var resolvedAssembly = TryResolvePreferredRuntimeAssembly(requestedSimpleName, requestedAssemblyPath);
                if (resolvedAssembly is not null)
                {
                    _currentExecutionContext?.CacheResolvedRuntimeAssembly(cacheKey, resolvedAssembly);
                    return resolvedAssembly;
                }

                if (runtimeTypeResolutionAssemblyPathsByName is not null &&
                    runtimeTypeResolutionAssemblyPathsByName.ContainsKey(requestedSimpleName))
                {
                    _currentExecutionContext?.CacheResolvedRuntimeAssembly(cacheKey, assembly: null);
                    return null;
                }
            }

            try
            {
                var assembly = Assembly.Load(requestedAssemblyName);
                _currentExecutionContext?.CacheResolvedRuntimeAssembly(cacheKey, assembly);
                return assembly;
            }
            catch
            {
                // Best-effort probe only.
            }

            foreach (var loadedAssembly in GetLoadedRuntimeAssemblies())
            {
                var loadedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(loadedAssembly.GetName().Name ?? string.Empty);
                if (string.Equals(loadedAssemblyName, requestedSimpleName, StringComparison.OrdinalIgnoreCase))
                {
                    _currentExecutionContext?.CacheResolvedRuntimeAssembly(cacheKey, loadedAssembly);
                    return loadedAssembly;
                }
            }

            _currentExecutionContext?.CacheResolvedRuntimeAssembly(cacheKey, assembly: null);
            return null;
        }

        /// <summary>
        /// Attempts to resolve the preferred assembly path for a runtime assembly probe.
        /// </summary>
        /// <param name="requestedSimpleName">The requested assembly simple name.</param>
        /// <param name="normalizedAssemblyName">The normalized root assembly name.</param>
        /// <param name="assemblyPath">The normalized root assembly path.</param>
        /// <param name="requestedAssemblyPath">The resolved assembly path.</param>
        /// <returns>true when a concrete assembly path was found; otherwise, false.</returns>
        private static bool TryResolveRuntimeResolutionAssemblyPath(
            string requestedSimpleName,
            string normalizedAssemblyName,
            string assemblyPath,
            out string requestedAssemblyPath)
        {
            requestedAssemblyPath = string.Empty;
            if (string.Equals(requestedSimpleName, normalizedAssemblyName, StringComparison.OrdinalIgnoreCase) &&
                !StringUtil.IsNullOrWhiteSpace(assemblyPath) &&
                File.Exists(assemblyPath))
            {
                requestedAssemblyPath = assemblyPath;
                return true;
            }

            if (runtimeTypeResolutionAssemblyPathsByName is not null &&
                runtimeTypeResolutionAssemblyPathsByName.TryGetValue(requestedSimpleName, out var mappedAssemblyPath) &&
                !StringUtil.IsNullOrWhiteSpace(mappedAssemblyPath) &&
                File.Exists(mappedAssemblyPath))
            {
                requestedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(mappedAssemblyPath);
                return true;
            }

            var preferredDirectory = Path.GetDirectoryName(assemblyPath);
            if (!StringUtil.IsNullOrWhiteSpace(preferredDirectory))
            {
                var dependencyAssemblyPath = Path.Combine(preferredDirectory!, requestedSimpleName + ".dll");
                if (File.Exists(dependencyAssemblyPath))
                {
                    requestedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(dependencyAssemblyPath);
                    return true;
                }
            }

            if (TryResolveCurrentRuntimeDirectoryAssemblyPath(requestedSimpleName, out requestedAssemblyPath))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to resolve a framework assembly from the runtime directory of the executing tool.
        /// </summary>
        /// <param name="assemblyName">The assembly simple name.</param>
        /// <param name="assemblyPath">The resolved assembly path.</param>
        /// <returns>true when the assembly exists in the runtime directory; otherwise, false.</returns>
        private static bool TryResolveCurrentRuntimeDirectoryAssemblyPath(string assemblyName, out string assemblyPath)
        {
            assemblyPath = string.Empty;
            if (StringUtil.IsNullOrWhiteSpace(assemblyName))
            {
                return false;
            }

            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (StringUtil.IsNullOrWhiteSpace(runtimeDirectory))
            {
                return false;
            }

            var candidatePath = Path.Combine(runtimeDirectory!, assemblyName + ".dll");
            if (!File.Exists(candidatePath))
            {
                return false;
            }

            assemblyPath = NormalizeRuntimeAssemblyPathForCache(candidatePath);
            return true;
        }

        /// <summary>
        /// Attempts to resolve runtime type.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static Type? TryResolveRuntimeType(TypeSig typeSig)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                if (typeSig.IsGenericParameter)
                {
                    return null;
                }

                return typeSig.ElementType switch
                {
                    ElementType.Void => typeof(void),
                    ElementType.Boolean => typeof(bool),
                    ElementType.Char => typeof(char),
                    ElementType.I1 => typeof(sbyte),
                    ElementType.U1 => typeof(byte),
                    ElementType.I2 => typeof(short),
                    ElementType.U2 => typeof(ushort),
                    ElementType.I4 => typeof(int),
                    ElementType.U4 => typeof(uint),
                    ElementType.I8 => typeof(long),
                    ElementType.U8 => typeof(ulong),
                    ElementType.R4 => typeof(float),
                    ElementType.R8 => typeof(double),
                    ElementType.String => typeof(string),
                    ElementType.Object => typeof(object),
                    ElementType.I => typeof(IntPtr),
                    ElementType.U => typeof(UIntPtr),
                    _ => TryResolveRuntimeTypeFromTypeDefOrRef(typeSig)
                };
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.RuntimeTypeFromTypeSigSeconds += seconds);
            }
        }

        /// <summary>
        /// Attempts to resolve runtime type from type def or ref.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "The tool resolves type names from dnlib metadata to validate and emit mappings; this is not a trimmed app execution path.")]
#endif
        private static Type? TryResolveRuntimeTypeFromTypeDefOrRef(TypeSig typeSig)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            if (typeDefOrRef is null)
            {
                return null;
            }

            var reflectionName = typeDefOrRef.ReflectionFullName;
            if (StringUtil.IsNullOrWhiteSpace(reflectionName))
            {
                reflectionName = typeDefOrRef.FullName;
            }

            if (StringUtil.IsNullOrWhiteSpace(reflectionName))
            {
                return null;
            }

            reflectionName = reflectionName.Replace('/', '+');
            var assemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(typeDefOrRef.DefinitionAssembly?.Name.String ?? string.Empty);
            if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
            {
                var assemblyPath = string.Empty;
                if (runtimeTypeResolutionAssemblyPathsByName is not null &&
                    runtimeTypeResolutionAssemblyPathsByName.TryGetValue(assemblyName, out assemblyPath) &&
                    TryResolveRuntimeType(assemblyName, assemblyPath, reflectionName, out var resolvedFromKnownAssemblyPath) &&
                    resolvedFromKnownAssemblyPath is not null)
                {
                    return resolvedFromKnownAssemblyPath;
                }

                var normalizedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(assemblyPath);
                var assemblyQualifiedName = $"{reflectionName}, {assemblyName}";
                var resolvedFromAssembly = Type.GetType(assemblyQualifiedName, throwOnError: false);
                if (resolvedFromAssembly is not null &&
                    RuntimeTypeMatchesRequestedAssembly(resolvedFromAssembly, assemblyName, normalizedAssemblyPath))
                {
                    return resolvedFromAssembly;
                }

                if (TryResolveRuntimeTypeByName(reflectionName, assemblyName, assemblyPath ?? string.Empty, out var resolvedByScopedName) &&
                    resolvedByScopedName is not null)
                {
                    return resolvedByScopedName;
                }

                return null;
            }

            if (TryResolveRuntimeTypeByName(reflectionName, out var resolvedByName) &&
                resolvedByName is not null)
            {
                return resolvedByName;
            }

            return Type.GetType(reflectionName, throwOnError: false);
        }

        /// <summary>
        /// Determines whether object type sig.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsObjectTypeSig(TypeSig typeSig)
        {
            if (typeSig.ElementType == ElementType.Object)
            {
                return true;
            }

            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            if (typeDefOrRef is null)
            {
                return false;
            }

            return string.Equals(typeDefOrRef.FullName, "System.Object", StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether type assignable from.
        /// </summary>
        /// <param name="candidateBaseTypeSig">The candidate base type sig value.</param>
        /// <param name="derivedTypeSig">The derived type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsTypeAssignableFrom(TypeSig candidateBaseTypeSig, TypeSig derivedTypeSig)
        {
            var candidateBaseType = candidateBaseTypeSig.ToTypeDefOrRef();
            var derivedType = derivedTypeSig.ToTypeDefOrRef();
            if (candidateBaseType is null || derivedType is null)
            {
                return false;
            }

            return IsAssignableFrom(candidateBaseType, derivedType);
        }

        /// <summary>
        /// Determines whether a candidate accessor matches a generated accessor source method.
        /// </summary>
        /// <param name="candidate">The candidate accessor method.</param>
        /// <param name="accessorMethod">The source accessor method.</param>
        /// <returns>true when accessors match; otherwise false.</returns>
        private static bool AccessorMatches(MethodDef? candidate, MethodDef accessorMethod)
        {
            if (candidate is null)
            {
                return false;
            }

            // Fast-path when dnlib method identity matches exactly.
            if (MethodsMatch(candidate, accessorMethod))
            {
                return true;
            }

            // Fallback for cross-module imports where method object identity differs.
            if (!string.Equals(candidate.Name, accessorMethod.Name, StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(candidate.MethodSig.ToString(), accessorMethod.MethodSig.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether the two method definitions represent the same accessor.
        /// </summary>
        /// <param name="left">The left method definition.</param>
        /// <param name="right">The right method definition.</param>
        /// <returns>true when both method definitions match; otherwise false.</returns>
        private static bool MethodsMatch(MethodDef? left, MethodDef right)
        {
            if (left is null)
            {
                return false;
            }

            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left.MDToken.Raw != 0 && left.MDToken.Raw == right.MDToken.Raw)
            {
                return true;
            }

            return string.Equals(BuildMethodIdentityKey(left), BuildMethodIdentityKey(right), StringComparison.Ordinal);
        }

        private static ITypeDefOrRef ImportInterfaceContractTypeDefOrRef(
            ModuleDef moduleDef,
            InterfaceMethodContract interfaceMethodContract,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            string context)
        {
            if (closedGenericProxyTypeArguments is null || closedGenericProxyTypeArguments.Count == 0)
            {
                return ImportTypeDefOrRefCached(moduleDef, interfaceMethodContract.InterfaceReference, context);
            }

            var substitutedInterfaceTypeSig = SubstituteTypeAndMethodGenericTypeArguments(
                interfaceMethodContract.InterfaceTypeSig,
                closedGenericProxyTypeArguments,
                closedGenericMethodArguments: null);
            var importedInterfaceTypeSig = ImportInterfaceContractTypeSig(moduleDef, substitutedInterfaceTypeSig, context);
            return importedInterfaceTypeSig.ToTypeDefOrRef()
                ?? throw new InvalidOperationException($"Unable to import interface contract type for {context}.");
        }

        private static IReadOnlyList<TypeSig>? GetClassMemberGenericTypeArguments(
            TypeDef rootType,
            TypeDef declaringType,
            IReadOnlyList<TypeSig>? closedRootTypeArguments)
        {
            var current = rootType;
            var typeArguments = closedRootTypeArguments;
            while (current is not null)
            {
                if (ReferenceEquals(current, declaringType))
                {
                    return typeArguments;
                }

                if (current.BaseType is null)
                {
                    break;
                }

                var baseTypeSig = SubstituteTypeAndMethodGenericTypeArguments(current.BaseType.ToTypeSig(), typeArguments, closedGenericMethodArguments: null);
                typeArguments = (baseTypeSig as GenericInstSig)?.GenericArguments.ToArray();
                current = current.BaseType.ResolveTypeDef();
            }

            return closedRootTypeArguments;
        }

        /// <summary>
        /// Gets the method that introduces the vtable slot a virtual method of a class hierarchy uses: the method itself for a
        /// new slot, otherwise the base method with the same name and signature it overrides (with the generic arguments seen
        /// from the root type). An explicit override doesn't change the slot a method uses (see GetImplementedVtableSlotMethods).
        /// </summary>
        /// <param name="rootType">The most derived type of the hierarchy.</param>
        /// <param name="method">The virtual method.</param>
        /// <returns>The method introducing the slot.</returns>
        private static MethodDef GetVtableSlotMethod(TypeDef rootType, MethodDef method)
        {
            var slotMethod = method;
            var visitedMethods = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
            while (visitedMethods.Add(slotMethod) && FindOverriddenBaseMethod(rootType, slotMethod) is { } overriddenMethod)
            {
                slotMethod = overriddenMethod;
            }

            return slotMethod;
        }

        private static bool IsMemberAllowedByBindingFlags(bool isStatic, bool isPublic, bool isInherited, BindingFlags bindingFlags)
        {
            if (isInherited && (bindingFlags & BindingFlags.DeclaredOnly) != 0)
            {
                return false;
            }

            if (isStatic)
            {
                if ((bindingFlags & BindingFlags.Static) == 0)
                {
                    return false;
                }

                if (isInherited && (bindingFlags & BindingFlags.FlattenHierarchy) == 0)
                {
                    return false;
                }
            }
            else if ((bindingFlags & BindingFlags.Instance) == 0)
            {
                return false;
            }

            if (isPublic)
            {
                return (bindingFlags & BindingFlags.Public) != 0;
            }

            return (bindingFlags & BindingFlags.NonPublic) != 0;
        }

        private static string BuildTypeIdentityKey(TypeDef type)
        {
            var assemblyName = type.DefinitionAssembly?.Name?.String ?? string.Empty;
            return $"{assemblyName}::{type.FullName}";
        }

        private static string BuildTypeDefOrRefIdentityKey(ITypeDefOrRef typeDefOrRef)
        {
            var assemblyName = typeDefOrRef.DefinitionAssembly?.FullName ?? typeDefOrRef.DefinitionAssembly?.Name?.String ?? string.Empty;
            return $"{assemblyName}::{typeDefOrRef.FullName}";
        }

        private static string BuildTypeSigDefinitionIdentityKey(TypeSig typeSig)
        {
            var assemblyName = typeSig.DefinitionAssembly?.FullName ?? typeSig.DefinitionAssembly?.Name?.String ?? string.Empty;
            return $"{assemblyName}::{typeSig.FullName ?? typeSig.ToString()}";
        }

        private static string BuildMethodDefOrRefIdentityKey(IMethodDefOrRef method)
        {
            return string.Concat(
                method.DeclaringType is null ? string.Empty : BuildTypeDefOrRefIdentityKey(method.DeclaringType),
                "::",
                method.Name.String ?? method.Name.ToString(),
                "::",
                method.MethodSig?.ToString() ?? string.Empty);
        }

        private static string BuildMethodIdentityKey(MethodDef method)
        {
            var assemblyName = method.DeclaringType?.DefinitionAssembly?.Name?.String ?? string.Empty;
            return $"{assemblyName}::{method.DeclaringType?.FullName ?? "<synthetic>"}::{method.Name}::{method.MethodSig}";
        }

        private static string BuildFieldIdentityKey(FieldDef field)
        {
            var assemblyName = field.DeclaringType?.DefinitionAssembly?.Name?.String ?? string.Empty;
            return $"{assemblyName}::{field.FullName}";
        }

        private static string BuildPropertyIdentityKey(PropertyDef property)
        {
            var assemblyName = property.DeclaringType?.DefinitionAssembly?.Name?.String ?? string.Empty;
            return $"{assemblyName}::{property.DeclaringType?.FullName ?? "<synthetic>"}::{property.Name}::{property.PropertySig}";
        }

        private static string BuildTypeSigCacheKey(TypeSig typeSig)
        {
            switch (typeSig)
            {
                case GenericVar genericVar:
                    return string.Concat("!", genericVar.Number.ToString(CultureInfo.InvariantCulture));
                case GenericMVar genericMVar:
                    return string.Concat("!!", genericMVar.Number.ToString(CultureInfo.InvariantCulture));
                case PtrSig ptrSig:
                    return string.Concat("ptr(", BuildTypeSigCacheKey(ptrSig.Next), ")");
                case ByRefSig byRefSig:
                    return string.Concat("byref(", BuildTypeSigCacheKey(byRefSig.Next), ")");
                case SZArraySig szArraySig:
                    return string.Concat("szarray(", BuildTypeSigCacheKey(szArraySig.Next), ")");
                case ArraySig arraySig:
                    return string.Concat(
                        "array(",
                        arraySig.Rank.ToString(CultureInfo.InvariantCulture),
                        ":",
                        string.Join(",", arraySig.Sizes.Select(size => size.ToString(CultureInfo.InvariantCulture))),
                        ":",
                        string.Join(",", arraySig.LowerBounds.Select(lowerBound => lowerBound.ToString(CultureInfo.InvariantCulture))),
                        ":",
                        BuildTypeSigCacheKey(arraySig.Next),
                        ")");
                case GenericInstSig genericInstSig:
                    return string.Concat(
                        "generic(",
                        genericInstSig.GenericType.TypeDefOrRef is not null ? BuildTypeDefOrRefIdentityKey(genericInstSig.GenericType.TypeDefOrRef) : BuildTypeSigDefinitionIdentityKey(genericInstSig.GenericType),
                        "<",
                        string.Join(",", genericInstSig.GenericArguments.Select(BuildTypeSigCacheKey)),
                        ">)");
                case CModReqdSig requiredModifierSig:
                    return string.Concat("modreq(", BuildTypeDefOrRefIdentityKey(requiredModifierSig.Modifier), ":", BuildTypeSigCacheKey(requiredModifierSig.Next), ")");
                case CModOptSig optionalModifierSig:
                    return string.Concat("modopt(", BuildTypeDefOrRefIdentityKey(optionalModifierSig.Modifier), ":", BuildTypeSigCacheKey(optionalModifierSig.Next), ")");
                case PinnedSig pinnedSig:
                    return string.Concat("pinned(", BuildTypeSigCacheKey(pinnedSig.Next), ")");
                case ValueArraySig valueArraySig:
                    return string.Concat(
                        "valuearray(",
                        valueArraySig.Size.ToString(CultureInfo.InvariantCulture),
                        ":",
                        BuildTypeSigCacheKey(valueArraySig.Next),
                        ")");
                case ModuleSig moduleSig:
                    return string.Concat(
                        "module(",
                        moduleSig.Index.ToString(CultureInfo.InvariantCulture),
                        ":",
                        BuildTypeSigCacheKey(moduleSig.Next),
                        ")");
                case FnPtrSig fnPtrSig:
                    return string.Concat("fnptr(", fnPtrSig.Signature?.ToString() ?? string.Empty, ")");
                default:
                    return string.Concat(typeSig.ElementType.ToString(), ":", BuildTypeSigDefinitionIdentityKey(typeSig));
            }
        }

        private static string BuildTypeSigSequenceCacheKey(IEnumerable<TypeSig>? typeSigs)
        {
            if (typeSigs is null)
            {
                return string.Empty;
            }

            return string.Join(",", typeSigs.Select(BuildTypeSigCacheKey));
        }

        private static string BuildTypeSubstitutionCacheKey(
            TypeSig typeSig,
            IReadOnlyList<TypeSig>? closedGenericTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments)
        {
            return string.Concat(
                BuildTypeSigCacheKey(typeSig),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericTypeArguments),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericMethodArguments));
        }

        private static string BuildMethodCallTargetCacheKey(
            IMethodDefOrRef importedTargetMethod,
            ITypeDefOrRef importedTargetType,
            int generatedMethodGenericParameterCount,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments)
        {
            return string.Concat(
                BuildMethodDefOrRefIdentityKey(importedTargetMethod),
                "|",
                BuildTypeDefOrRefIdentityKey(importedTargetType),
                "|",
                generatedMethodGenericParameterCount.ToString(CultureInfo.InvariantCulture),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericMethodArguments));
        }

        private static string BuildPropertySignatureCacheKey(PropertySig propertySig)
        {
            return string.Concat(
                propertySig.HasThis ? "instance" : "static",
                "|",
                BuildTypeSigCacheKey(propertySig.RetType),
                "|",
                BuildTypeSigSequenceCacheKey(propertySig.Params));
        }

        private static IEnumerable<Type> EnumerateRuntimeTypeHierarchy(Type runtimeType)
        {
            for (var current = runtimeType; current is not null; current = current.BaseType)
            {
                yield return current;
            }

            foreach (var implementedInterface in runtimeType.GetInterfaces())
            {
                yield return implementedInterface;
            }
        }

        private static TMember? FindMemberDefInHierarchy<TMember>(TypeDef type, MemberInfo runtimeMember, Func<TypeDef, IEnumerable<TMember>> getMembers)
            where TMember : class, IMDTokenProvider
        {
            // A member of a reverse proxy type dynamic duck typing creates: the member of the reverse proxy type the registry
            // generates for the same pair, which has the same name and signature.
            if (runtimeMember.DeclaringType is { } declaringRuntimeType &&
                _currentExecutionContext?.RuntimeTypeGeneratedTypes.TryGetValue(declaringRuntimeType, out var generatedType) == true)
            {
                var generatedMember = getMembers(generatedType).FirstOrDefault(member => IsSameGeneratedMember(member, runtimeMember));

                // A non-public override of a member of the contract (e.g. a protected one) can't be called by the forward proxy,
                // another type: the proxy calls the member it overrides, which the runtime dispatches to the override (the
                // registry can access the contract's members).
                return generatedMember switch
                {
                    MethodDef method when IsNonPublicOverride(method) => GetVtableSlotMethod(generatedType, method) as TMember ?? generatedMember,
                    PropertyDef property when (property.GetMethod ?? property.SetMethod) is { } accessor && IsNonPublicOverride(accessor) =>
                        FindOverriddenProperty(generatedType, GetVtableSlotMethod(generatedType, accessor)) as TMember ?? generatedMember,
                    _ => generatedMember,
                };
            }

            var metadataToken = unchecked((uint)runtimeMember.MetadataToken);
            var moduleVersionId = runtimeMember.Module.ModuleVersionId;
            for (var current = type; current is not null; current = current.BaseType?.ResolveTypeDef())
            {
                if (current.Module?.Mvid != moduleVersionId)
                {
                    continue;
                }

                foreach (var member in getMembers(current))
                {
                    if (member.MDToken.Raw == metadataToken)
                    {
                        return member;
                    }
                }
            }

            return null;
        }

        private static bool IsNonPublicOverride(MethodDef method) => method.IsVirtual && !method.IsPublic && !method.IsNewSlot;

        /// <summary>
        /// Finds the property of a base type of a generated reverse proxy type whose accessor a generated accessor overrides.
        /// </summary>
        /// <param name="generatedType">The generated type.</param>
        /// <param name="overriddenAccessor">The accessor the generated property's accessor overrides.</param>
        /// <returns>The overridden property, or null when it isn't found.</returns>
        private static PropertyDef? FindOverriddenProperty(TypeDef generatedType, MethodDef overriddenAccessor)
        {
            for (var current = generatedType.BaseType?.ResolveTypeDef(); current is not null; current = current.BaseType?.ResolveTypeDef())
            {
                foreach (var candidate in current.Properties)
                {
                    if (candidate.GetMethod == overriddenAccessor || candidate.SetMethod == overriddenAccessor)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether a member of a generated type matches a member of the type dynamic duck typing creates for the same
        /// pair: same name, and for methods and properties the same parameter types.
        /// </summary>
        /// <param name="member">The member of the generated type.</param>
        /// <param name="runtimeMember">The runtime member.</param>
        /// <returns>true if the members match; otherwise, false.</returns>
        private static bool IsSameGeneratedMember(IMDTokenProvider member, MemberInfo runtimeMember)
        {
            switch (member, runtimeMember)
            {
                case (MethodDef method, MethodInfo runtimeMethod):
                    return string.Equals(method.Name, runtimeMethod.Name, StringComparison.Ordinal) &&
                           method.MethodSig.GenParamCount == (runtimeMethod.IsGenericMethodDefinition ? runtimeMethod.GetGenericArguments().Length : 0) &&
                           AreSameParameterTypes(method.MethodSig.Params, runtimeMethod.GetParameters());
                case (PropertyDef property, PropertyInfo runtimeProperty):
                    return string.Equals(property.Name, runtimeProperty.Name, StringComparison.Ordinal) &&
                           AreSameParameterTypes(property.PropertySig.Params, runtimeProperty.GetIndexParameters());
                case (FieldDef field, FieldInfo runtimeField):
                    return string.Equals(field.Name, runtimeField.Name, StringComparison.Ordinal);
                default:
                    return false;
            }

            static bool AreSameParameterTypes(IList<TypeSig> parameterTypes, ParameterInfo[] runtimeParameters)
            {
                if (parameterTypes.Count != runtimeParameters.Length)
                {
                    return false;
                }

                for (var i = 0; i < parameterTypes.Count; i++)
                {
                    if (TryResolveRuntimeType(parameterTypes[i]) is not { } parameterType || parameterType != runtimeParameters[i].ParameterType)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Attempts to get by ref element type.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="elementType">The element type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetByRefElementType(TypeSig typeSig, out TypeSig? elementType)
        {
            if (typeSig is ByRefSig byRefSig)
            {
                elementType = byRefSig.Next;
                return true;
            }

            elementType = null;
            return false;
        }

        /// <summary>
        /// Substitutes method generic parameters with closed generic arguments when provided.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="closedGenericTypeArguments">The closed generic type arguments value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static TypeSig SubstituteTypeAndMethodGenericTypeArguments(
            TypeSig typeSig,
            IReadOnlyList<TypeSig>? closedGenericTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments)
        {
            if ((closedGenericTypeArguments is null || closedGenericTypeArguments.Count == 0) &&
                (closedGenericMethodArguments is null || closedGenericMethodArguments.Count == 0))
            {
                return typeSig;
            }

            var phaseStopwatch = StartProfilePhase();
            try
            {
                var cacheKey = BuildTypeSubstitutionCacheKey(typeSig, closedGenericTypeArguments, closedGenericMethodArguments);
                if (_currentExecutionContext?.TryGetSubstitutedTypeSig(cacheKey, out var cachedTypeSig) == true)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.TypeSubstitutionCacheHits++;
                    }

                    return cachedTypeSig!;
                }

                if (_currentProfile is not null)
                {
                    _currentProfile.TypeSubstitutionCacheMisses++;
                }

                TypeSig substitutedTypeSig;
                if (typeSig is GenericVar typeGenericParameter &&
                    closedGenericTypeArguments is not null &&
                    typeGenericParameter.Number < closedGenericTypeArguments.Count)
                {
                    substitutedTypeSig = closedGenericTypeArguments[(int)typeGenericParameter.Number];
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                // Replace method generic parameter with the corresponding closed generic argument when available.
                if (typeSig is GenericMVar methodGenericParameter &&
                    closedGenericMethodArguments is not null &&
                    methodGenericParameter.Number < closedGenericMethodArguments.Count)
                {
                    substitutedTypeSig = closedGenericMethodArguments[(int)methodGenericParameter.Number];
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is PtrSig ptrSig)
                {
                    substitutedTypeSig = new PtrSig(SubstituteTypeAndMethodGenericTypeArguments(ptrSig.Next, closedGenericTypeArguments, closedGenericMethodArguments));
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is ByRefSig byRefSig)
                {
                    substitutedTypeSig = new ByRefSig(SubstituteTypeAndMethodGenericTypeArguments(byRefSig.Next, closedGenericTypeArguments, closedGenericMethodArguments));
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is SZArraySig szArraySig)
                {
                    substitutedTypeSig = new SZArraySig(SubstituteTypeAndMethodGenericTypeArguments(szArraySig.Next, closedGenericTypeArguments, closedGenericMethodArguments));
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is ArraySig arraySig)
                {
                    substitutedTypeSig = new ArraySig(
                        SubstituteTypeAndMethodGenericTypeArguments(arraySig.Next, closedGenericTypeArguments, closedGenericMethodArguments),
                        arraySig.Rank,
                        arraySig.Sizes,
                        arraySig.LowerBounds);
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is GenericInstSig genericInstSig)
                {
                    var genericArguments = new List<TypeSig>(genericInstSig.GenericArguments.Count);
                    for (var i = 0; i < genericInstSig.GenericArguments.Count; i++)
                    {
                        genericArguments.Add(SubstituteTypeAndMethodGenericTypeArguments(genericInstSig.GenericArguments[i], closedGenericTypeArguments, closedGenericMethodArguments));
                    }

                    substitutedTypeSig = new GenericInstSig(genericInstSig.GenericType, genericArguments);
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is CModReqdSig requiredModifierSig)
                {
                    substitutedTypeSig = new CModReqdSig(
                        requiredModifierSig.Modifier,
                        SubstituteTypeAndMethodGenericTypeArguments(requiredModifierSig.Next, closedGenericTypeArguments, closedGenericMethodArguments));
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is CModOptSig optionalModifierSig)
                {
                    substitutedTypeSig = new CModOptSig(
                        optionalModifierSig.Modifier,
                        SubstituteTypeAndMethodGenericTypeArguments(optionalModifierSig.Next, closedGenericTypeArguments, closedGenericMethodArguments));
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is PinnedSig pinnedSig)
                {
                    substitutedTypeSig = new PinnedSig(SubstituteTypeAndMethodGenericTypeArguments(pinnedSig.Next, closedGenericTypeArguments, closedGenericMethodArguments));
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is ValueArraySig valueArraySig)
                {
                    substitutedTypeSig = new ValueArraySig(
                        SubstituteTypeAndMethodGenericTypeArguments(valueArraySig.Next, closedGenericTypeArguments, closedGenericMethodArguments),
                        valueArraySig.Size);
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                if (typeSig is ModuleSig moduleSig)
                {
                    substitutedTypeSig = new ModuleSig(
                        moduleSig.Index,
                        SubstituteTypeAndMethodGenericTypeArguments(moduleSig.Next, closedGenericTypeArguments, closedGenericMethodArguments));
                    _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, substitutedTypeSig);
                    return substitutedTypeSig;
                }

                _currentExecutionContext?.CacheSubstitutedTypeSig(cacheKey, typeSig);
                return typeSig;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.TypeSubstitutionSeconds += seconds);
            }
        }

        /// <summary>
        /// Resolves imported type for type token.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="context">The context value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static ITypeDefOrRef ResolveImportedTypeForTypeToken(ModuleDef moduleDef, TypeSig typeSig, string context)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef()
                            ?? throw new InvalidOperationException($"Unable to resolve type token for {context}.");
            return ImportTypeDefOrRefCached(moduleDef, typeDefOrRef, context);
        }

        private static ITypeDefOrRef ImportTypeDefOrRefCached(ModuleDef moduleDef, ITypeDefOrRef typeDefOrRef, string context)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                if (_currentExecutionContext?.TryGetImportedTypeDefOrRef(typeDefOrRef, out var cachedType) == true)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.ImportCacheHits++;
                    }

                    return cachedType!;
                }

                var importedType = moduleDef.Import(typeDefOrRef) as ITypeDefOrRef
                                   ?? throw new InvalidOperationException($"Unable to import type token for {context}.");
                _currentExecutionContext?.CacheImportedTypeDefOrRef(typeDefOrRef, importedType);
                if (_currentProfile is not null)
                {
                    _currentProfile.ImportCacheMisses++;
                }

                return importedType;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ImportTypeDefOrRefSeconds += seconds);
            }
        }

        /// <summary>
        /// Gets the importer of runtime types and members into the registry being emitted.
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <returns>The importer.</returns>
        private static Importer RuntimeImporter(ModuleDef moduleDef) => runtimeImporter ?? new Importer(moduleDef);

        private static ITypeDefOrRef ImportRuntimeTypeCached(ModuleDef moduleDef, Type runtimeType, string context)
        {
            if (_currentExecutionContext?.TryGetImportedRuntimeType(runtimeType, out var cachedType) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ImportCacheHits++;
                }

                return cachedType!;
            }

            var importedType = RuntimeImporter(moduleDef).Import(runtimeType)
                               ?? throw new InvalidOperationException($"Unable to import runtime type for {context}.");
            _currentExecutionContext?.CacheImportedRuntimeType(runtimeType, importedType);
            if (_currentProfile is not null)
            {
                _currentProfile.ImportCacheMisses++;
            }

            return importedType;
        }

        private static TypeSig ImportTypeSigCached(ModuleDef moduleDef, TypeSig typeSig, string context)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var cacheKey = BuildTypeSigCacheKey(typeSig);
                if (_currentExecutionContext?.TryGetImportedTypeSig(cacheKey, out var cachedTypeSig) == true)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.ImportCacheHits++;
                    }

                    return cachedTypeSig!;
                }

                var importedTypeSig = moduleDef.Import(typeSig);
                _currentExecutionContext?.CacheImportedTypeSig(cacheKey, importedTypeSig);
                if (_currentProfile is not null)
                {
                    _currentProfile.ImportCacheMisses++;
                }

                return importedTypeSig;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ImportTypeSigSeconds += seconds);
            }
        }

        /// <summary>
        /// Imports a proxy interface contract signature using the generated module's framework assembly identity.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="typeSig">The source type signature.</param>
        /// <param name="context">The operation context.</param>
        /// <returns>The imported type signature.</returns>
        private static TypeSig ImportInterfaceContractTypeSig(ModuleDef moduleDef, TypeSig typeSig, string context)
        {
            if (TryCreateGeneratedCorLibTypeSig(moduleDef, typeSig, out var generatedCorLibTypeSig))
            {
                return generatedCorLibTypeSig!;
            }

            if (typeSig is GenericInstSig genericInstSig)
            {
                var importedGenericType = ImportInterfaceContractClassOrValueTypeSig(moduleDef, genericInstSig.GenericType, context);
                var importedGenericArguments = new TypeSig[genericInstSig.GenericArguments.Count];
                for (var i = 0; i < importedGenericArguments.Length; i++)
                {
                    importedGenericArguments[i] = ImportInterfaceContractTypeSig(moduleDef, genericInstSig.GenericArguments[i], context);
                }

                return new GenericInstSig(importedGenericType, importedGenericArguments);
            }

            if (typeSig is PtrSig ptrSig)
            {
                return new PtrSig(ImportInterfaceContractTypeSig(moduleDef, ptrSig.Next, context));
            }

            if (typeSig is ByRefSig byRefSig)
            {
                return new ByRefSig(ImportInterfaceContractTypeSig(moduleDef, byRefSig.Next, context));
            }

            if (typeSig is SZArraySig szArraySig)
            {
                return new SZArraySig(ImportInterfaceContractTypeSig(moduleDef, szArraySig.Next, context));
            }

            if (typeSig is ArraySig arraySig)
            {
                return new ArraySig(
                    ImportInterfaceContractTypeSig(moduleDef, arraySig.Next, context),
                    arraySig.Rank,
                    arraySig.Sizes,
                    arraySig.LowerBounds);
            }

            if (typeSig is CModReqdSig requiredModifierSig)
            {
                var importedModifier = ImportTypeDefOrRefCached(moduleDef, requiredModifierSig.Modifier, $"required modifier for {context}");
                return new CModReqdSig(importedModifier, ImportInterfaceContractTypeSig(moduleDef, requiredModifierSig.Next, context));
            }

            if (typeSig is CModOptSig optionalModifierSig)
            {
                var importedModifier = ImportTypeDefOrRefCached(moduleDef, optionalModifierSig.Modifier, $"optional modifier for {context}");
                return new CModOptSig(importedModifier, ImportInterfaceContractTypeSig(moduleDef, optionalModifierSig.Next, context));
            }

            if (typeSig is PinnedSig pinnedSig)
            {
                return new PinnedSig(ImportInterfaceContractTypeSig(moduleDef, pinnedSig.Next, context));
            }

            if (typeSig is ValueArraySig valueArraySig)
            {
                return new ValueArraySig(ImportInterfaceContractTypeSig(moduleDef, valueArraySig.Next, context), valueArraySig.Size);
            }

            if (typeSig is ModuleSig moduleSig)
            {
                return new ModuleSig(moduleSig.Index, ImportInterfaceContractTypeSig(moduleDef, moduleSig.Next, context));
            }

            return ImportTypeSigCached(moduleDef, typeSig, context);
        }

        private static ClassOrValueTypeSig ImportInterfaceContractClassOrValueTypeSig(ModuleDef moduleDef, ClassOrValueTypeSig typeSig, string context)
        {
            if (TryCreateGeneratedCorLibTypeSig(moduleDef, typeSig, out var generatedCorLibTypeSig) &&
                generatedCorLibTypeSig is ClassOrValueTypeSig generatedClassOrValueTypeSig)
            {
                return generatedClassOrValueTypeSig;
            }

            var importedTypeDefOrRef = ImportTypeDefOrRefCached(moduleDef, typeSig.TypeDefOrRef, context);
            return typeSig.ElementType == ElementType.ValueType ? new ValueTypeSig(importedTypeDefOrRef) : new ClassSig(importedTypeDefOrRef);
        }

        private static bool TryCreateGeneratedCorLibTypeSig(ModuleDef moduleDef, TypeSig typeSig, out TypeSig? generatedCorLibTypeSig)
        {
            generatedCorLibTypeSig = null;
            if (typeSig is not TypeDefOrRefSig typeDefOrRefSig ||
                !IsFrameworkImplementationType(typeDefOrRefSig.TypeDefOrRef))
            {
                return false;
            }

            var namespaceName = typeDefOrRefSig.TypeDefOrRef.Namespace ?? string.Empty;
            var typeName = typeDefOrRefSig.TypeDefOrRef.Name ?? string.Empty;
            if (StringUtil.IsNullOrEmpty(namespaceName) || StringUtil.IsNullOrEmpty(typeName))
            {
                return false;
            }

            var generatedTypeRef = moduleDef.CorLibTypes.GetTypeRef(namespaceName, typeName);
            generatedCorLibTypeSig = typeSig.ElementType == ElementType.ValueType
                                         ? new ValueTypeSig(generatedTypeRef)
                                         : typeSig.ElementType == ElementType.Class
                                             ? new ClassSig(generatedTypeRef)
                                             : generatedTypeRef.ToTypeSig();
            return true;
        }

        private static bool IsFrameworkImplementationType(ITypeDefOrRef typeDefOrRef)
        {
            var assemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(typeDefOrRef.DefinitionAssembly?.Name?.String ?? string.Empty);
            return string.Equals(assemblyName, "System.Private.CoreLib", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(assemblyName, "System.Runtime", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(assemblyName, "mscorlib", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(assemblyName, "netstandard", StringComparison.OrdinalIgnoreCase);
        }

        private static IMethod ImportMethodCached(ModuleDef moduleDef, IMethod method, string context)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                AddRequiredAccessCheckAssemblyName(method.DeclaringType);
                if (_currentExecutionContext?.TryGetImportedMethod(method, out var cachedMethod) == true)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.ImportCacheHits++;
                    }

                    return cachedMethod!;
                }

                var importedMethod = moduleDef.Import(method)
                                   ?? throw new InvalidOperationException($"Unable to import method for {context}.");
                _currentExecutionContext?.CacheImportedMethod(method, importedMethod);
                if (_currentProfile is not null)
                {
                    _currentProfile.ImportCacheMisses++;
                }

                return importedMethod;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ImportMethodSeconds += seconds);
            }
        }

        private static IMethodDefOrRef ImportMethodDefOrRefCached(ModuleDef moduleDef, IMethodDefOrRef method, string context)
        {
            var importedMethod = ImportMethodCached(moduleDef, method, context) as IMethodDefOrRef;
            return importedMethod
                ?? throw new InvalidOperationException($"Unable to import method definition/reference for {context}.");
        }

        private static IField ImportFieldCached(ModuleDef moduleDef, IField field, string context)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                AddRequiredAccessCheckAssemblyName(field.DeclaringType);
                if (_currentExecutionContext?.TryGetImportedField(field, out var cachedField) == true)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.ImportCacheHits++;
                    }

                    return cachedField!;
                }

                var importedField = moduleDef.Import(field)
                                  ?? throw new InvalidOperationException($"Unable to import field for {context}.");
                _currentExecutionContext?.CacheImportedField(field, importedField);
                if (_currentProfile is not null)
                {
                    _currentProfile.ImportCacheMisses++;
                }

                return importedField;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ImportFieldSeconds += seconds);
            }
        }

        private static void AddRequiredAccessCheckAssemblyName(ITypeDefOrRef? declaringType)
        {
            var assemblyName = declaringType?.DefinitionAssembly?.Name?.String;
            _currentExecutionContext?.AddRequiredAccessCheckAssemblyName(assemblyName);
        }

        private static IField ImportTypeMissingField(ModuleDef moduleDef)
        {
            if (_currentExecutionContext?.TypeMissingField is { } cachedField)
            {
                return cachedField;
            }

            var typeMissingField = RuntimeImporter(moduleDef).Import(typeof(Type).GetField(nameof(Type.Missing))
                                                    ?? throw new InvalidOperationException("Unable to resolve System.Type.Missing."));
            if (_currentExecutionContext is not null)
            {
                _currentExecutionContext.TypeMissingField = typeMissingField;
            }

            return typeMissingField;
        }

        private static bool IsTypeSigNamed(TypeSig typeSig, string fullName)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            return typeDefOrRef is not null &&
                   string.Equals(typeDefOrRef.FullName, fullName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets field resolution mode.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static FieldResolutionMode GetFieldResolutionMode(MethodDef proxyMethod)
        {
            var mode = FieldResolutionMode.Disabled;
            foreach (var duckAttribute in EnumerateDuckAttributes(proxyMethod))
            {
                var duckKind = ResolveDuckKind(duckAttribute);
                switch (duckKind)
                {
                    case DuckKindField:
                        return FieldResolutionMode.FieldOnly;
                    case DuckKindPropertyOrField:
                        mode = FieldResolutionMode.AllowFallback;
                        break;
                }
            }

            return mode;
        }

        /// <summary>
        /// Determines whether fallback to base types is enabled for a method mapping.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>true if fallback to base types is enabled; otherwise, false.</returns>
        private static bool IsFallbackToBaseTypesEnabled(MethodDef proxyMethod)
        {
            foreach (var duckAttribute in EnumerateDuckAttributes(proxyMethod))
            {
                if (IsFallbackToBaseTypesEnabled(duckAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether fallback to base types is enabled for a custom-attribute set.
        /// </summary>
        /// <param name="customAttributes">The custom attributes value.</param>
        /// <returns>true if fallback to base types is enabled; otherwise, false.</returns>
        private static bool IsFallbackToBaseTypesEnabled(IList<CustomAttribute> customAttributes)
        {
            foreach (var customAttribute in customAttributes)
            {
                if (!IsDuckAttribute(customAttribute))
                {
                    continue;
                }

                if (IsFallbackToBaseTypesEnabled(customAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether fallback to base types is enabled for a Duck attribute.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>true if fallback to base types is enabled; otherwise, false.</returns>
        private static bool IsFallbackToBaseTypesEnabled(CustomAttribute customAttribute)
        {
            foreach (var namedArgument in customAttribute.NamedArguments)
            {
                if (!string.Equals(namedArgument.Name.String, "FallbackToBaseTypes", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryGetBoolArgument(namedArgument.Argument.Value, out var fallbackToBaseTypes))
                {
                    return fallbackToBaseTypes;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the effective Duck binding flags for a proxy method.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The effective binding flags.</returns>
        private static BindingFlags GetDuckBindingFlags(MethodDef proxyMethod)
        {
            foreach (var duckAttribute in EnumerateDuckAttributes(proxyMethod))
            {
                return GetDuckBindingFlags(duckAttribute);
            }

            return DefaultDuckBindingFlags;
        }

        /// <summary>
        /// Gets the effective Duck binding flags for a custom-attribute set.
        /// </summary>
        /// <param name="customAttributes">The custom attributes value.</param>
        /// <returns>The effective binding flags.</returns>
        private static BindingFlags GetDuckBindingFlags(IList<CustomAttribute> customAttributes)
        {
            foreach (var customAttribute in customAttributes)
            {
                if (IsDuckAttribute(customAttribute))
                {
                    return GetDuckBindingFlags(customAttribute);
                }
            }

            return DefaultDuckBindingFlags;
        }

        /// <summary>
        /// Gets the effective binding flags for a Duck attribute.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>The effective binding flags.</returns>
        private static BindingFlags GetDuckBindingFlags(CustomAttribute customAttribute)
        {
            foreach (var namedArgument in customAttribute.NamedArguments)
            {
                if (!string.Equals(namedArgument.Name.String, "BindingFlags", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryGetIntArgument(namedArgument.Argument.Value, out var bindingFlags))
                {
                    return (BindingFlags)bindingFlags;
                }
            }

            return DefaultDuckBindingFlags;
        }

        /// <summary>
        /// Enumerates Duck attributes from the method and its declaring property.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<CustomAttribute> EnumerateDuckAttributes(MethodDef proxyMethod)
        {
            foreach (var attribute in proxyMethod.CustomAttributes)
            {
                if (IsDuckAttribute(attribute))
                {
                    yield return attribute;
                }
            }

            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty))
            {
                foreach (var attribute in declaringProperty!.CustomAttributes)
                {
                    if (IsDuckAttribute(attribute))
                    {
                        yield return attribute;
                    }
                }
            }
        }

        /// <summary>
        /// Resolves duck kind.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>The computed numeric value.</returns>
        private static int ResolveDuckKind(CustomAttribute customAttribute)
        {
            var fullName = customAttribute.TypeFullName;
            if (string.Equals(fullName, DuckFieldAttributeTypeName, StringComparison.Ordinal))
            {
                return DuckKindField;
            }

            if (string.Equals(fullName, DuckPropertyOrFieldAttributeTypeName, StringComparison.Ordinal))
            {
                return DuckKindPropertyOrField;
            }

            foreach (var namedArgument in customAttribute.NamedArguments)
            {
                if (!string.Equals(namedArgument.Name.String, "Kind", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryGetIntArgument(namedArgument.Argument.Value, out var kind))
                {
                    return kind;
                }
            }

            return DuckKindProperty;
        }

        /// <summary>
        /// Attempts to get declaring property.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="propertyDef">The property def value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetDeclaringProperty(MethodDef proxyMethod, out PropertyDef? propertyDef)
        {
            propertyDef = null;
            var declaringType = proxyMethod.DeclaringType;
            if (declaringType is null)
            {
                return false;
            }

            foreach (var property in declaringType.Properties)
            {
                if (property.GetMethod == proxyMethod || property.SetMethod == proxyMethod || property.OtherMethods.Contains(proxyMethod))
                {
                    propertyDef = property;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to resolve runtime type by name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner reflects over loaded assemblies as part of build-time compatibility analysis.")]
        [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Type names are supplied by mapping metadata and validated by discovery before emission.")]
#endif
        private static bool TryResolveRuntimeTypeByName(string typeName, out Type? runtimeType)
        {
            if (_currentExecutionContext?.TryGetTypeByName(typeName, out runtimeType) == true)
            {
                return runtimeType is not null;
            }

            runtimeType = Type.GetType(typeName, throwOnError: false);
            if (runtimeType is not null)
            {
                _currentExecutionContext?.CacheTypeByName(typeName, runtimeType);
                return true;
            }

            var (parsedTypeName, parsedAssemblyName) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(typeName);
            if (!StringUtil.IsNullOrWhiteSpace(parsedTypeName) &&
                !StringUtil.IsNullOrWhiteSpace(parsedAssemblyName) &&
                runtimeTypeResolutionAssemblyPathsByName is not null &&
                runtimeTypeResolutionAssemblyPathsByName.TryGetValue(parsedAssemblyName!, out var assemblyPath) &&
                TryResolveRuntimeType(parsedAssemblyName!, assemblyPath, parsedTypeName, out runtimeType) &&
                runtimeType is not null)
            {
                _currentExecutionContext?.CacheTypeByName(typeName, runtimeType);
                return true;
            }

            foreach (var loadedAssembly in GetLoadedRuntimeAssemblies())
            {
                runtimeType = loadedAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (runtimeType is not null)
                {
                    _currentExecutionContext?.CacheTypeByName(typeName, runtimeType);
                    return true;
                }
            }

            _currentExecutionContext?.CacheTypeByName(typeName, runtimeType: null);
            return false;
        }

#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner reflects over loaded assemblies as part of build-time compatibility analysis.")]
        [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Type names are supplied by mapping metadata and validated by discovery before emission.")]
#endif
        private static bool TryResolveRuntimeTypeByName(string typeName, string normalizedAssemblyName, string assemblyPath, out Type? runtimeType)
        {
            var normalizedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(assemblyPath);
            var cacheKey = string.Concat(
                typeName,
                "|",
                normalizedAssemblyName.ToUpperInvariant(),
                "|",
                normalizedAssemblyPath);
            if (_currentExecutionContext?.TryGetTypeByName(cacheKey, out runtimeType) == true)
            {
                return runtimeType is not null;
            }

            runtimeType = Type.GetType(typeName, throwOnError: false);
            if (runtimeType is not null &&
                RuntimeTypeMatchesRequestedAssembly(runtimeType, normalizedAssemblyName, normalizedAssemblyPath))
            {
                _currentExecutionContext?.CacheTypeByName(cacheKey, runtimeType);
                return true;
            }

            var (parsedTypeName, parsedAssemblyName) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(typeName);
            if (!StringUtil.IsNullOrWhiteSpace(parsedTypeName) &&
                !StringUtil.IsNullOrWhiteSpace(parsedAssemblyName) &&
                runtimeTypeResolutionAssemblyPathsByName is not null &&
                runtimeTypeResolutionAssemblyPathsByName.TryGetValue(parsedAssemblyName!, out var parsedAssemblyPath) &&
                TryResolveRuntimeType(parsedAssemblyName!, parsedAssemblyPath, parsedTypeName, out runtimeType) &&
                runtimeType is not null &&
                RuntimeTypeMatchesRequestedAssembly(runtimeType, normalizedAssemblyName, normalizedAssemblyPath))
            {
                _currentExecutionContext?.CacheTypeByName(cacheKey, runtimeType);
                return true;
            }

            foreach (var loadedAssembly in GetLoadedRuntimeAssemblies())
            {
                runtimeType = loadedAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (runtimeType is not null &&
                    RuntimeTypeMatchesRequestedAssembly(runtimeType, normalizedAssemblyName, normalizedAssemblyPath))
                {
                    _currentExecutionContext?.CacheTypeByName(cacheKey, runtimeType);
                    return true;
                }
            }

            runtimeType = null;
            _currentExecutionContext?.CacheTypeByName(cacheKey, runtimeType: null);
            return false;
        }

        /// <summary>
        /// Attempts to get duck attribute name.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <param name="configuredName">The configured name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetDuckAttributeName(CustomAttribute customAttribute, out string? configuredName)
        {
            foreach (var namedArgument in customAttribute.NamedArguments)
            {
                if (!string.Equals(namedArgument.Name.String, "Name", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryGetStringArgument(namedArgument.Argument.Value, out configuredName))
                {
                    return true;
                }
            }

            configuredName = null;
            return false;
        }

        /// <summary>
        /// Attempts to get duck attribute parameter type names.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <param name="parameterTypeNames">The parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetDuckAttributeParameterTypeNames(CustomAttribute customAttribute, out IReadOnlyList<string> parameterTypeNames)
        {
            var names = new List<string>();
            parameterTypeNames = names;
            foreach (var namedArgument in customAttribute.NamedArguments)
            {
                if (!string.Equals(namedArgument.Name.String, "ParameterTypeNames", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryGetStringArrayArgument(namedArgument.Argument.Value, out var configuredNames))
                {
                    names.AddRange(configuredNames);
                }
            }

            return names.Count > 0;
        }

        /// <summary>
        /// Gets type comparison names.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static HashSet<string> GetTypeComparisonNames(TypeSig typeSig)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var normalizedType = typeSig;
            if (normalizedType.ElementType == ElementType.ByRef && normalizedType is ByRefSig byRefSig)
            {
                normalizedType = byRefSig.Next;
            }

            var fullName = normalizedType.FullName;
            if (!StringUtil.IsNullOrWhiteSpace(fullName))
            {
                names.Add(fullName);
            }

            var typeName = normalizedType.TypeName;
            if (!StringUtil.IsNullOrWhiteSpace(typeName))
            {
                names.Add(typeName);
            }

            var typeDefOrRef = normalizedType.ToTypeDefOrRef();
            if (typeDefOrRef is not null)
            {
                var definitionFullName = typeDefOrRef.FullName;
                if (!StringUtil.IsNullOrWhiteSpace(definitionFullName))
                {
                    names.Add(definitionFullName);
                }

                var reflectionFullName = typeDefOrRef.ReflectionFullName?.Replace('/', '+');
                if (!StringUtil.IsNullOrWhiteSpace(reflectionFullName))
                {
                    names.Add(reflectionFullName!);
                }

                var simpleTypeName = typeDefOrRef.Name.String ?? typeDefOrRef.Name.ToString();
                if (!StringUtil.IsNullOrWhiteSpace(simpleTypeName))
                {
                    names.Add(simpleTypeName);
                }

                var assemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(typeDefOrRef.DefinitionAssembly?.Name.String ?? string.Empty);
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    if (!StringUtil.IsNullOrWhiteSpace(definitionFullName))
                    {
                        names.Add($"{definitionFullName}, {assemblyName}");
                    }

                    if (!StringUtil.IsNullOrWhiteSpace(reflectionFullName))
                    {
                        names.Add($"{reflectionFullName}, {assemblyName}");
                    }
                }
            }

            var runtimeType = TryResolveRuntimeType(normalizedType);
            if (runtimeType is not null)
            {
                names.Add(runtimeType.Name);
                if (!StringUtil.IsNullOrWhiteSpace(runtimeType.FullName))
                {
                    names.Add(runtimeType.FullName);
                    var assemblyName = runtimeType.Assembly.GetName().Name;
                    if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                    {
                        names.Add($"{runtimeType.FullName}, {assemblyName}");
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// Attempts to get duck attribute names.
        /// </summary>
        /// <param name="customAttributes">The custom attributes value.</param>
        /// <param name="names">The names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetDuckAttributeNames(IList<CustomAttribute> customAttributes, out IReadOnlyList<string> names)
        {
            var parsedNames = new List<string>();
            names = parsedNames;

            foreach (var customAttribute in customAttributes)
            {
                if (!IsDuckAttribute(customAttribute))
                {
                    continue;
                }

                foreach (var namedArgument in customAttribute.NamedArguments)
                {
                    if (!string.Equals(namedArgument.Name.String, "Name", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (TryGetStringArgument(namedArgument.Argument.Value, out var configuredName))
                    {
                        foreach (var name in SplitDuckNames(configuredName!))
                        {
                            if (!StringUtil.IsNullOrWhiteSpace(name))
                            {
                                parsedNames.Add(name);
                            }
                        }
                    }
                }
            }

            return parsedNames.Count > 0;
        }

        /// <summary>
        /// Determines whether duck attribute.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsDuckAttribute(CustomAttribute customAttribute)
        {
            var fullName = customAttribute.TypeFullName;
            return string.Equals(fullName, DuckAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckFieldAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckPropertyOrFieldAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckReverseMethodAttributeTypeName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether a method carries DuckIgnore semantics: its own [DuckIgnore], or for a property accessor the
        /// [DuckIgnore] of its property (dynamic duck typing reads the attributes of the property, not of the accessor).
        /// </summary>
        /// <param name="method">The method value.</param>
        /// <returns>true if the method is marked with DuckIgnore semantics; otherwise, false.</returns>
        private static bool IsDuckIgnoreMethod(MethodDef method)
        {
            // Like the attribute inheritance dynamic duck typing reads [DuckIgnore] with: an override inherits it.
            for (MethodDef? current = method; current is not null; current = FindOverriddenBaseMethod(method.DeclaringType, current))
            {
                if (TryGetDeclaringProperty(current, out var declaringProperty) && declaringProperty is not null)
                {
                    if (declaringProperty.CustomAttributes.Any(IsDuckIgnoreAttribute))
                    {
                        return true;
                    }
                }
                else if (current.CustomAttributes.Any(IsDuckIgnoreAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the base class method a virtual method overrides.
        /// </summary>
        /// <param name="rootType">The type the method is read from (for the generic arguments of its base types).</param>
        /// <param name="method">The method.</param>
        /// <returns>The overridden method, or null when the method doesn't override one.</returns>
        private static MethodDef? FindOverriddenBaseMethod(TypeDef rootType, MethodDef method)
        {
            if (!method.IsVirtual || method.IsNewSlot || method.DeclaringType is not { IsInterface: false } declaringType)
            {
                return null;
            }

            var methodArguments = GetClassMemberGenericTypeArguments(rootType, declaringType, closedRootTypeArguments: null);
            for (var baseType = declaringType.BaseType?.ResolveTypeDef(); baseType is not null; baseType = baseType.BaseType?.ResolveTypeDef())
            {
                var baseArguments = GetClassMemberGenericTypeArguments(rootType, baseType, closedRootTypeArguments: null);
                var baseMethod = baseType.Methods.FirstOrDefault(candidate =>
                    candidate.IsVirtual &&
                    !candidate.IsStatic &&
                    string.Equals(candidate.Name, method.Name, StringComparison.Ordinal) &&
                    AreEffectiveMethodSignaturesEquivalent(candidate.MethodSig, baseArguments, method.MethodSig, methodArguments));
                if (baseMethod is not null)
                {
                    return baseMethod;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether a custom attribute is DuckIgnore.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>true if the attribute is DuckIgnore; otherwise, false.</returns>
        private static bool IsDuckIgnoreAttribute(CustomAttribute customAttribute)
        {
            return string.Equals(customAttribute.TypeFullName, DuckIgnoreAttributeTypeName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Attempts to get string argument.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="text">The text value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetStringArgument(object? value, out string? text)
        {
            switch (value)
            {
                case UTF8String utf8:
                    text = utf8.String;
                    return !StringUtil.IsNullOrWhiteSpace(text);
                case string stringValue:
                    text = stringValue;
                    return !StringUtil.IsNullOrWhiteSpace(text);
                default:
                    text = null;
                    return false;
            }
        }

        /// <summary>
        /// Attempts to get string array argument.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="values">The values value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetStringArrayArgument(object? value, out IReadOnlyList<string> values)
        {
            var parsedValues = new List<string>();
            values = parsedValues;
            switch (value)
            {
                case IList<CAArgument> caArguments:
                    foreach (var caArgument in caArguments)
                    {
                        if (TryGetStringArgument(caArgument.Value, out var text))
                        {
                            parsedValues.Add(text!.Trim());
                        }
                    }

                    break;
                case string[] stringArray:
                    for (var i = 0; i < stringArray.Length; i++)
                    {
                        var valueText = stringArray[i]?.Trim();
                        if (!StringUtil.IsNullOrWhiteSpace(valueText))
                        {
                            parsedValues.Add(valueText!);
                        }
                    }

                    break;
                case object[] objectArray:
                    for (var i = 0; i < objectArray.Length; i++)
                    {
                        if (TryGetStringArgument(objectArray[i], out var text))
                        {
                            parsedValues.Add(text!.Trim());
                        }
                    }

                    break;
            }

            return parsedValues.Count > 0;
        }

        /// <summary>
        /// Attempts to get int argument.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="boolValue">The bool value value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetBoolArgument(object? value, out bool boolValue)
        {
            switch (value)
            {
                case bool typedBool:
                    boolValue = typedBool;
                    return true;
                case byte byteValue when byteValue is 0 or 1:
                    boolValue = byteValue != 0;
                    return true;
                case sbyte signedByteValue when signedByteValue is 0 or 1:
                    boolValue = signedByteValue != 0;
                    return true;
                case short int16Value when int16Value is 0 or 1:
                    boolValue = int16Value != 0;
                    return true;
                case int int32Value when int32Value is 0 or 1:
                    boolValue = int32Value != 0;
                    return true;
                default:
                    boolValue = default;
                    return false;
            }
        }

        /// <summary>
        /// Attempts to get int argument.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="intValue">The int value value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetIntArgument(object? value, out int intValue)
        {
            switch (value)
            {
                case int int32Value:
                    intValue = int32Value;
                    return true;
                case short int16Value:
                    intValue = int16Value;
                    return true;
                case byte byteValue:
                    intValue = byteValue;
                    return true;
                case sbyte sbyteValue:
                    intValue = sbyteValue;
                    return true;
                default:
                    intValue = default;
                    return false;
            }
        }

        /// <summary>
        /// Splits Duck attribute names into normalized candidate member names.
        /// </summary>
        /// <param name="configuredName">The configured name value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<string> SplitDuckNames(string configuredName)
        {
            // Same split as dynamic duck typing (commas inside generic arguments are part of the name).
            return DuckType.GetDuckAttributeCandidateNames(configuredName);
        }

        /// <summary>
        /// Attempts to get accessor prefix.
        /// </summary>
        /// <param name="methodName">The method name value.</param>
        /// <param name="prefix">The prefix value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetAccessorPrefix(string methodName, out string prefix)
        {
            var separatorIndex = methodName.IndexOf('_');
            if (separatorIndex <= 0)
            {
                prefix = string.Empty;
                return false;
            }

            prefix = methodName.Substring(0, separatorIndex + 1);
            return true;
        }

        /// <summary>
        /// Determines whether two type signatures should be treated as equivalent for mapping.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool AreTypesEquivalent(TypeSig proxyType, TypeSig targetType)
        {
            if (proxyType is GenericVar proxyTypeVar && targetType is GenericVar targetTypeVar)
            {
                return proxyTypeVar.Number == targetTypeVar.Number;
            }

            if (proxyType is GenericMVar proxyMethodVar && targetType is GenericMVar targetMethodVar)
            {
                return proxyMethodVar.Number == targetMethodVar.Number;
            }

            var proxyRuntimeType = TryResolveRuntimeType(proxyType);
            var targetRuntimeType = TryResolveRuntimeType(targetType);
            if (proxyRuntimeType is not null && targetRuntimeType is not null)
            {
                return proxyRuntimeType == targetRuntimeType;
            }

            if (proxyType is ByRefSig proxyByRef && targetType is ByRefSig targetByRef)
            {
                return AreTypesEquivalent(proxyByRef.Next, targetByRef.Next);
            }

            if (proxyType is SZArraySig proxySzArray && targetType is SZArraySig targetSzArray)
            {
                return AreTypesEquivalent(proxySzArray.Next, targetSzArray.Next);
            }

            if (proxyType is ArraySig proxyArray && targetType is ArraySig targetArray)
            {
                if (proxyArray.Rank != targetArray.Rank ||
                    !proxyArray.Sizes.SequenceEqual(targetArray.Sizes) ||
                    !proxyArray.LowerBounds.SequenceEqual(targetArray.LowerBounds))
                {
                    return false;
                }

                return AreTypesEquivalent(proxyArray.Next, targetArray.Next);
            }

            if (proxyType is GenericInstSig proxyGenericInst && targetType is GenericInstSig targetGenericInst)
            {
                var proxyGenericTypeKey = proxyGenericInst.GenericType.TypeDefOrRef is not null ? BuildTypeDefOrRefIdentityKey(proxyGenericInst.GenericType.TypeDefOrRef) : BuildTypeSigDefinitionIdentityKey(proxyGenericInst.GenericType);
                var targetGenericTypeKey = targetGenericInst.GenericType.TypeDefOrRef is not null ? BuildTypeDefOrRefIdentityKey(targetGenericInst.GenericType.TypeDefOrRef) : BuildTypeSigDefinitionIdentityKey(targetGenericInst.GenericType);
                if (!string.Equals(proxyGenericTypeKey, targetGenericTypeKey, StringComparison.Ordinal))
                {
                    return false;
                }

                if (proxyGenericInst.GenericArguments.Count != targetGenericInst.GenericArguments.Count)
                {
                    return false;
                }

                for (var i = 0; i < proxyGenericInst.GenericArguments.Count; i++)
                {
                    if (!AreTypesEquivalent(proxyGenericInst.GenericArguments[i], targetGenericInst.GenericArguments[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (proxyType is PtrSig proxyPtr && targetType is PtrSig targetPtr)
            {
                return AreTypesEquivalent(proxyPtr.Next, targetPtr.Next);
            }

            if (proxyType is CModReqdSig proxyRequiredModifier && targetType is CModReqdSig targetRequiredModifier)
            {
                return string.Equals(BuildTypeDefOrRefIdentityKey(proxyRequiredModifier.Modifier), BuildTypeDefOrRefIdentityKey(targetRequiredModifier.Modifier), StringComparison.Ordinal) &&
                    AreTypesEquivalent(proxyRequiredModifier.Next, targetRequiredModifier.Next);
            }

            if (proxyType is CModOptSig proxyOptionalModifier && targetType is CModOptSig targetOptionalModifier)
            {
                return string.Equals(BuildTypeDefOrRefIdentityKey(proxyOptionalModifier.Modifier), BuildTypeDefOrRefIdentityKey(targetOptionalModifier.Modifier), StringComparison.Ordinal) &&
                    AreTypesEquivalent(proxyOptionalModifier.Next, targetOptionalModifier.Next);
            }

            if (proxyType is PinnedSig proxyPinned && targetType is PinnedSig targetPinned)
            {
                return AreTypesEquivalent(proxyPinned.Next, targetPinned.Next);
            }

            if (proxyType is ValueArraySig proxyValueArray && targetType is ValueArraySig targetValueArray)
            {
                return proxyValueArray.Size == targetValueArray.Size &&
                    AreTypesEquivalent(proxyValueArray.Next, targetValueArray.Next);
            }

            if (proxyType is ModuleSig proxyModule && targetType is ModuleSig targetModule)
            {
                return proxyModule.Index == targetModule.Index &&
                    AreTypesEquivalent(proxyModule.Next, targetModule.Next);
            }

            return string.Equals(BuildTypeSigCacheKey(proxyType), BuildTypeSigCacheKey(targetType), StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets a method signature without the custom modifiers (modreq, modopt) of its return and parameter types, at any depth
        /// (e.g. of an array element or a generic argument), like the signatures of Reflection.Emit's methods.
        /// </summary>
        /// <param name="methodSig">The method signature.</param>
        /// <returns>The signature without custom modifiers.</returns>
        private static MethodSig WithoutCustomModifiers(MethodSig methodSig)
        {
            if (!HasCustomModifiers(methodSig.RetType) && !methodSig.Params.Any(HasCustomModifiers))
            {
                return methodSig;
            }

            var strippedSig = methodSig.Clone();
            strippedSig.RetType = StripModifiers(methodSig.RetType);
            for (var i = 0; i < strippedSig.Params.Count; i++)
            {
                strippedSig.Params[i] = StripModifiers(strippedSig.Params[i]);
            }

            return strippedSig;

            static bool HasCustomModifiers(TypeSig? type)
                => type switch
                {
                    null => false,
                    ModifierSig => true,
                    GenericInstSig genericInstance => genericInstance.GenericArguments.Any(HasCustomModifiers),
                    FnPtrSig functionPointer => functionPointer.MethodSig is { } signature && (HasCustomModifiers(signature.RetType) || signature.Params.Any(HasCustomModifiers)),
                    _ => HasCustomModifiers(type.Next),
                };

            static TypeSig StripModifiers(TypeSig type)
            {
                type = type.RemoveModifiers();
                return type switch
                {
                    ByRefSig byRef => new ByRefSig(StripModifiers(byRef.Next)),
                    PtrSig pointer => new PtrSig(StripModifiers(pointer.Next)),
                    SZArraySig vector => new SZArraySig(StripModifiers(vector.Next)),
                    ArraySig array => new ArraySig(StripModifiers(array.Next), array.Rank, array.Sizes, array.LowerBounds),
                    GenericInstSig genericInstance => new GenericInstSig(genericInstance.GenericType, genericInstance.GenericArguments.Select(StripModifiers).ToList()),
                    FnPtrSig { MethodSig: { } signature } => new FnPtrSig(WithoutCustomModifiers(signature)),
                    _ => type,
                };
            }
        }

        /// <summary>
        /// Determines whether a type definition is public, and nested in public types.
        /// </summary>
        /// <param name="type">The type definition.</param>
        /// <returns>true if the type definition is visible outside its assembly; otherwise, false.</returns>
        private static bool IsVisibleTypeDefinition(TypeDef type)
            => type.DeclaringType is { } declaringType ? type.IsNestedPublic && IsVisibleTypeDefinition(declaringType) : type.IsPublic;

        /// <summary>
        /// Attempts to get nullable element type.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="elementType">The element type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetNullableElementType(TypeSig typeSig, out TypeSig? elementType)
        {
            elementType = null;
            if (typeSig is not GenericInstSig genericInstSig || genericInstSig.GenericArguments.Count != 1)
            {
                return false;
            }

            var genericType = genericInstSig.GenericType?.TypeDefOrRef;
            if (genericType is null || !string.Equals(genericType.FullName, "System.Nullable`1", StringComparison.Ordinal))
            {
                return false;
            }

            elementType = genericInstSig.GenericArguments[0];
            return true;
        }

        /// <summary>
        /// Determines whether assignable from.
        /// </summary>
        /// <param name="candidateBaseType">The candidate base type value.</param>
        /// <param name="derivedType">The derived type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsAssignableFrom(ITypeDefOrRef candidateBaseType, ITypeDefOrRef derivedType)
        {
            if (IsSameMetadataType(candidateBaseType, derivedType))
            {
                return true;
            }

            var derivedTypeDef = derivedType.ResolveTypeDef();
            if (derivedTypeDef is null)
            {
                return false;
            }

            var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
            var typesToInspect = new Stack<TypeDef>();
            typesToInspect.Push(derivedTypeDef);
            while (typesToInspect.Count > 0)
            {
                var current = typesToInspect.Pop();
                if (!visitedTypes.Add(current.FullName))
                {
                    continue;
                }

                if (IsSameMetadataType(current, candidateBaseType))
                {
                    return true;
                }

                var baseType = current.BaseType?.ResolveTypeDef();
                if (baseType is not null)
                {
                    typesToInspect.Push(baseType);
                }

                foreach (var interfaceImpl in current.Interfaces)
                {
                    if (IsSameMetadataType(interfaceImpl.Interface, candidateBaseType))
                    {
                        return true;
                    }

                    var resolvedInterface = interfaceImpl.Interface.ResolveTypeDef();
                    if (resolvedInterface is not null)
                    {
                        typesToInspect.Push(resolvedInterface);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether two type references are the same type: types of different assemblies with the same name aren't (e.g.
        /// the Datadog.Trace.ISpan of Datadog.Trace.Manual), except the framework's, which assemblies reference through facades.
        /// Generic instances are compared argument by argument, and a forwarded type by the assembly that defines it.
        /// </summary>
        /// <param name="left">The first type.</param>
        /// <param name="right">The second type.</param>
        /// <returns>true if they are the same type; otherwise, false.</returns>
        private static bool IsSameMetadataType(ITypeDefOrRef left, ITypeDefOrRef right)
        {
            if (left is TypeSpec || right is TypeSpec)
            {
                return IsSameMetadataTypeSig(left.ToTypeSig(), right.ToTypeSig());
            }

            if (!string.Equals(left.FullName, right.FullName, StringComparison.Ordinal))
            {
                return false;
            }

            var leftAssemblyName = GetDefiningAssemblyName(left);
            var rightAssemblyName = GetDefiningAssemblyName(right);
            return leftAssemblyName.Length == 0 ||
                   rightAssemblyName.Length == 0 ||
                   string.Equals(leftAssemblyName, rightAssemblyName, StringComparison.OrdinalIgnoreCase) ||
                   IsFrameworkAssemblyName(leftAssemblyName) ||
                   IsFrameworkAssemblyName(rightAssemblyName);

            static string GetDefiningAssemblyName(ITypeDefOrRef type)
                => DuckTypeAotNameHelpers.NormalizeAssemblyName((type.ResolveTypeDef()?.DefinitionAssembly ?? type.DefinitionAssembly)?.Name?.String ?? string.Empty);

            static bool IsFrameworkAssemblyName(string assemblyName)
                => assemblyName.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether two signature types are the same type (see <see cref="IsSameMetadataType(ITypeDefOrRef, ITypeDefOrRef)"/>).
        /// </summary>
        /// <param name="left">The first type.</param>
        /// <param name="right">The second type.</param>
        /// <returns>true if they are the same type; otherwise, false.</returns>
        private static bool IsSameMetadataTypeSig(TypeSig? left, TypeSig? right)
        {
            switch (left, right)
            {
                case (null, null):
                    return true;
                case (GenericInstSig leftInstance, GenericInstSig rightInstance):
                    return leftInstance.GenericType?.TypeDefOrRef is { } leftGenericType &&
                           rightInstance.GenericType?.TypeDefOrRef is { } rightGenericType &&
                           IsSameMetadataType(leftGenericType, rightGenericType) &&
                           leftInstance.GenericArguments.Count == rightInstance.GenericArguments.Count &&
                           leftInstance.GenericArguments.Zip(rightInstance.GenericArguments, IsSameMetadataTypeSig).All(isSame => isSame);
                case (GenericSig leftGeneric, GenericSig rightGeneric):
                    return leftGeneric.IsMethodVar == rightGeneric.IsMethodVar && leftGeneric.Number == rightGeneric.Number;
                case (TypeDefOrRefSig leftType, TypeDefOrRefSig rightType):
                    return IsSameMetadataType(leftType.TypeDefOrRef, rightType.TypeDefOrRef);
                case (NonLeafSig leftNonLeaf, NonLeafSig rightNonLeaf):
                    return leftNonLeaf.ElementType == rightNonLeaf.ElementType &&
                           (leftNonLeaf is not ArraySig leftArray || (rightNonLeaf is ArraySig rightArray && leftArray.Rank == rightArray.Rank)) &&
                           IsSameMetadataTypeSig(leftNonLeaf.Next, rightNonLeaf.Next);
                default:
                    return left is not null && right is not null && left.ElementType == right.ElementType && string.Equals(left.FullName, right.FullName, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Attempts to resolve type.
        /// </summary>
        /// <param name="module">The module value.</param>
        /// <param name="typeName">The type name value.</param>
        /// <param name="type">The type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryResolveType(ModuleDef module, string typeName, out TypeDef type)
        {
            var typeLookup = _currentExecutionContext?.GetOrCreateTypeLookup(module);
            if (typeLookup is not null && typeLookup.TryGetValue(typeName, out type!))
            {
                return true;
            }

            type = module.Find(typeName, isReflectionName: true)
                ?? module.Find(typeName, isReflectionName: false)
                ?? module.GetTypes().FirstOrDefault(candidate =>
                    string.Equals(candidate.ReflectionFullName, typeName, StringComparison.Ordinal) ||
                    string.Equals(candidate.FullName, typeName, StringComparison.Ordinal))!;

            return type is not null;
        }

        /// <summary>
        /// Creates the dnlib module context used to resolve proxy and target dependencies.
        /// </summary>
        /// <param name="assemblyPathMaps">The assembly path maps value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static ModuleContext CreateModuleLoadContext(params IReadOnlyDictionary<string, string>[] assemblyPathMaps)
        {
            var assemblyResolver = new AssemblyResolver();
            var moduleContext = new ModuleContext(assemblyResolver);
            assemblyResolver.DefaultModuleContext = moduleContext;
            assemblyResolver.EnableTypeDefCache = true;

            foreach (var runtimeAssemblySearchPath in EnumerateRuntimeAssemblySearchPaths())
            {
                AddAssemblyResolverSearchPath(assemblyResolver, runtimeAssemblySearchPath);
            }

            foreach (var assemblyPathMap in assemblyPathMaps)
            {
                foreach (var assemblyPath in assemblyPathMap.Values)
                {
                    var directory = Path.GetDirectoryName(assemblyPath);
                    AddAssemblyResolverSearchPath(assemblyResolver, directory);
                }
            }

            return moduleContext;
        }

        private static void AddAssemblyResolverSearchPath(AssemblyResolver assemblyResolver, string? directory)
        {
            if (StringUtil.IsNullOrWhiteSpace(directory) ||
                assemblyResolver.PreSearchPaths.Contains(directory))
            {
                return;
            }

            assemblyResolver.PreSearchPaths.Add(directory);
        }

        private static IEnumerable<string> EnumerateRuntimeAssemblySearchPaths()
        {
            // Proxy contracts may inherit BCL interfaces that are not explicit target/proxy inputs.
            // Add the current runtime assembly directories so dnlib can resolve those inherited contracts.
            if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies)
            {
                foreach (var trustedPlatformAssembly in trustedPlatformAssemblies.Split(Path.PathSeparator))
                {
                    var directory = Path.GetDirectoryName(trustedPlatformAssembly);
                    if (!StringUtil.IsNullOrWhiteSpace(directory))
                    {
                        yield return directory!;
                    }
                }
            }

            var coreLibraryDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (!StringUtil.IsNullOrWhiteSpace(coreLibraryDirectory))
            {
                yield return coreLibraryDirectory!;
            }
        }

        /// <summary>
        /// Loads dnlib modules for all supplied assembly paths.
        /// </summary>
        /// <param name="assemblyPathsByName">The assembly paths by name value.</param>
        /// <param name="moduleContext">The module context value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyDictionary<string, ModuleDefMD> LoadModules(IReadOnlyDictionary<string, string> assemblyPathsByName, ModuleContext moduleContext)
        {
            var modulesByAssemblyName = new Dictionary<string, ModuleDefMD>(StringComparer.OrdinalIgnoreCase);
            foreach (var (assemblyName, assemblyPath) in assemblyPathsByName)
            {
                var module = ModuleDefMD.Load(assemblyPath, moduleContext);
                module.EnableTypeDefFindCache = true;
                if (moduleContext.AssemblyResolver is AssemblyResolver assemblyResolver)
                {
                    assemblyResolver.AddToCache(module);
                }

                modulesByAssemblyName[assemblyName] = module;
            }

            return modulesByAssemblyName;
        }

        /// <summary>
        /// Adds add assembly reference.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="assemblyReferences">The assembly references value.</param>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static AssemblyRef AddAssemblyReference(ModuleDef moduleDef, IDictionary<string, AssemblyRef> assemblyReferences, string assemblyPath)
        {
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            if (assemblyReferences.TryGetValue(assemblyName.Name ?? string.Empty, out var assemblyRef))
            {
                return assemblyRef;
            }

            assemblyRef = moduleDef.UpdateRowId(new AssemblyRefUser(assemblyName));
            assemblyReferences[assemblyName.Name ?? string.Empty] = assemblyRef;
            return assemblyRef;
        }

        /// <summary>
        /// Computes the MVID of the generated registry from everything its content depends on: the same inputs give the same
        /// MVID, and a registry generated from other builds of Datadog.Trace, of the proxies or of the targets gets another one.
        /// </summary>
        /// <param name="generatedAssemblyName">The generated assembly name value.</param>
        /// <param name="moduleFileName">The file name of the generated module.</param>
        /// <param name="strongNameKeyFile">The strong-name key file the registry is signed with, if any.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="datadogTraceAssemblyPath">The Datadog.Trace assembly the registry is bound to.</param>
        /// <returns>The result produced by this operation.</returns>
        private static Guid ComputeDeterministicMvid(string generatedAssemblyName, string moduleFileName, string? strongNameKeyFile, DuckTypeAotMappingResolutionResult mappingResolutionResult, string datadogTraceAssemblyPath)
        {
            using var sha256 = SHA256.Create();
            var deterministicInput = new StringBuilder()
                .Append(generatedAssemblyName)
                .Append('\n')
                .Append(moduleFileName)
                .Append('\n')
                .Append("key:")
                .Append(StringUtil.IsNullOrWhiteSpace(strongNameKeyFile) ? string.Empty : Convert.ToBase64String(sha256.ComputeHash(File.ReadAllBytes(strongNameKeyFile!))))
                .Append('\n')
                .Append("generator:")
                .Append(typeof(DuckTypeAotRegistryAssemblyEmitter).Assembly.ManifestModule.ModuleVersionId.ToString("D"))
                .Append('\n')
                .Append("Datadog.Trace:")
                .Append(ResolveAssemblyMvid(datadogTraceAssemblyPath))
                .Append('\n');

            foreach (var mapping in mappingResolutionResult.Mappings.OrderBy(m => m.Key, StringComparer.Ordinal))
            {
                _ = deterministicInput
                    .Append(mapping.Key)
                    .Append('\n');
            }

            foreach (var genericTypeRoot in mappingResolutionResult.GenericTypeRoots.Select(root => root.Key).OrderBy(key => key, StringComparer.Ordinal))
            {
                _ = deterministicInput
                    .Append("root:")
                    .Append(genericTypeRoot)
                    .Append('\n');
            }

            foreach (var assemblyPath in mappingResolutionResult.ProxyAssemblyPathsByName.Concat(mappingResolutionResult.TargetAssemblyPathsByName)
                                                                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                                                                .Select(entry => entry.Value))
            {
                _ = deterministicInput
                    .Append(Path.GetFileName(assemblyPath))
                    .Append(':')
                    .Append(ResolveAssemblyMvid(assemblyPath))
                    .Append('\n');
            }

            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(deterministicInput.ToString()));
            var guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, guidBytes.Length);
            return new Guid(guidBytes);
        }

        /// <summary>
        /// Computes stable short hash.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ComputeStableShortHash(string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return string.Concat(bytes.Take(4).Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// Represents parameter direction.
        /// </summary>
        private readonly struct ParameterDirection
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ParameterDirection"/> struct.
            /// </summary>
            /// <param name="isOut">The is out value.</param>
            /// <param name="isIn">The is in value.</param>
            internal ParameterDirection(bool isOut, bool isIn)
            {
                IsOut = isOut;
                IsIn = isIn;
            }

            /// <summary>
            /// Gets a value indicating whether is out.
            /// </summary>
            /// <value>The is out value.</value>
            internal bool IsOut { get; }

            /// <summary>
            /// Gets a value indicating whether is in.
            /// </summary>
            /// <value>The is in value.</value>
            internal bool IsIn { get; }
        }

        /// <summary>
        /// Represents method compatibility failure.
        /// </summary>
        private readonly struct MethodCompatibilityFailure
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MethodCompatibilityFailure"/> struct.
            /// </summary>
            /// <param name="detail">The detail value.</param>
            internal MethodCompatibilityFailure(string detail)
            {
                Detail = detail;
            }

            /// <summary>
            /// Gets detail.
            /// </summary>
            /// <value>The detail value.</value>
            internal string Detail { get; }
        }

        /// <summary>
        /// Imports the runtime types of the generator's own Datadog.Trace (DuckType, IDuckType...) as types of the Datadog.Trace
        /// the registry is bound to: the registry references a single Datadog.Trace, the application's, even when the generator
        /// runs another version.
        /// </summary>
        private sealed class DatadogTraceImportMapper : ImportMapper
        {
            private readonly ModuleDef _moduleDef;
            private readonly AssemblyRef _datadogTraceAssemblyRef;
            private readonly Dictionary<Type, TypeRef> _typeRefs = new();

            /// <summary>
            /// Initializes a new instance of the <see cref="DatadogTraceImportMapper"/> class.
            /// </summary>
            /// <param name="moduleDef">The registry module.</param>
            /// <param name="datadogTraceAssemblyRef">The reference to the Datadog.Trace the registry is bound to.</param>
            internal DatadogTraceImportMapper(ModuleDef moduleDef, AssemblyRef datadogTraceAssemblyRef)
            {
                _moduleDef = moduleDef;
                _datadogTraceAssemblyRef = datadogTraceAssemblyRef;
            }

            /// <inheritdoc />
            public override ITypeDefOrRef? Map(Type source) => MapTypeRef(source);

            private static string UnescapeReflectionName(string name)
            {
                if (name.IndexOf('\\') < 0)
                {
                    return name;
                }

                var unescaped = new StringBuilder(name.Length);
                for (var i = 0; i < name.Length; i++)
                {
                    if (name[i] == '\\' && i + 1 < name.Length)
                    {
                        i++;
                    }

                    unescaped.Append(name[i]);
                }

                return unescaped.ToString();
            }

            private TypeRef? MapTypeRef(Type source)
            {
                if (source.Assembly != typeof(DuckType).Assembly || source.HasElementType || source.IsGenericParameter || (source.IsGenericType && !source.IsGenericTypeDefinition))
                {
                    return null;
                }

                if (!_typeRefs.TryGetValue(source, out var typeRef))
                {
                    // Like dnlib's importer: reflection escapes the metadata name (e.g. the ',' of a compiler-generated name).
                    var name = UnescapeReflectionName(source.Name);
                    typeRef = source.DeclaringType is { } declaringType
                                  ? new TypeRefUser(_moduleDef, string.Empty, name, MapTypeRef(declaringType))
                                  : new TypeRefUser(_moduleDef, source.Namespace ?? string.Empty, name, _datadogTraceAssemblyRef);
                    typeRef = _moduleDef.UpdateRowId(typeRef);
                    _typeRefs[source] = typeRef;
                }

                return typeRef;
            }
        }

        /// <summary>
        /// Represents imported members.
        /// </summary>
        private sealed class ImportedMembers
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ImportedMembers"/> class.
            /// </summary>
            /// <param name="moduleDef">The module def value.</param>
            internal ImportedMembers(ModuleDef moduleDef)
            {
                var getTypeFromHandleMethod = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new[] { typeof(RuntimeTypeHandle) });
                if (getTypeFromHandleMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve Type.GetTypeFromHandle(RuntimeTypeHandle).");
                }

                var funcObjectObjectCtor = typeof(Func<object?, object?>).GetConstructor([typeof(object), typeof(IntPtr)]);
                if (funcObjectObjectCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve Func<object?, object?> constructor.");
                }

                var funcExceptionCtor = typeof(Func<Exception>).GetConstructor([typeof(object), typeof(IntPtr)]);
                if (funcExceptionCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve Func<Exception> constructor.");
                }

                var funcObjectTypeObjectCtor = typeof(Func<object?, Type, object?>).GetConstructor([typeof(object), typeof(IntPtr)]);
                if (funcObjectTypeObjectCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve Func<object?, Type, object?> constructor.");
                }

                var registerAotProxyMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotProxy),
                    new[] { typeof(Type), typeof(Type), typeof(Type), typeof(Func<object?, object?>) });
                if (registerAotProxyMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotProxy(Type, Type, Type, Func<object?, object?>).");
                }

                var registerAotReverseProxyMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotReverseProxy),
                    new[] { typeof(Type), typeof(Type), typeof(Type), typeof(Func<object?, object?>) });
                if (registerAotReverseProxyMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotReverseProxy(Type, Type, Type, Func<object?, object?>).");
                }

                var enableAotModeMethod = typeof(DuckType).GetMethod(nameof(DuckType.EnableAotMode), Type.EmptyTypes);
                if (enableAotModeMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.EnableAotMode().");
                }

                var validateAotRegistryContractMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.ValidateAotRegistryContract),
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(string)
                    });
                if (validateAotRegistryContractMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.ValidateAotRegistryContract(string, string, string, string, string).");
                }

                var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes);
                if (objectCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve object constructor.");
                }

                var objectToStringMethod = typeof(object).GetMethod(nameof(object.ToString), Type.EmptyTypes);
                if (objectToStringMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve object.ToString().");
                }

                var importer = RuntimeImporter(moduleDef);
                var iDuckTypeType = importer.Import(typeof(IDuckType));
                if (iDuckTypeType is null)
                {
                    throw new InvalidOperationException("Unable to import IDuckType.");
                }

                var iDuckTypeInstanceGetter = typeof(IDuckType).GetProperty(nameof(IDuckType.Instance), BindingFlags.Instance | BindingFlags.Public)?.GetMethod;
                if (iDuckTypeInstanceGetter is null)
                {
                    throw new InvalidOperationException("Unable to resolve IDuckType.Instance getter.");
                }

                var ignoresAccessChecksToCtor = typeof(IgnoresAccessChecksToAttribute).GetConstructor([typeof(string)]);
                if (ignoresAccessChecksToCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve IgnoresAccessChecksToAttribute(string).");
                }

                var duckTypeAotRegisteredFailureCreateMethod = typeof(DuckTypeAotRegisteredFailureException).GetMethod(
                    nameof(DuckTypeAotRegisteredFailureException.Create),
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    binder: null,
                    types: new[] { typeof(string), typeof(string) },
                    modifiers: null);
                if (duckTypeAotRegisteredFailureCreateMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckTypeAotRegisteredFailureException.Create(string, string).");
                }

                var duckTypeAotRegisteredFailureCreateWithInnerExceptionMethod = typeof(DuckTypeAotRegisteredFailureException).GetMethod(
                    nameof(DuckTypeAotRegisteredFailureException.Create),
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    binder: null,
                    types: [typeof(string), typeof(string), typeof(string), typeof(string)],
                    modifiers: null)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotRegisteredFailureException.Create(string, string, string, string).");
                var duckTypeAotRegisteredFailureCreateWithInnerExceptionInstanceMethod = typeof(DuckTypeAotRegisteredFailureException).GetMethod(
                    nameof(DuckTypeAotRegisteredFailureException.Create),
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    binder: null,
                    types: [typeof(string), typeof(string), typeof(Exception)],
                    modifiers: null)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotRegisteredFailureException.Create(string, string, Exception).");

                GetTypeFromHandleMethod = importer.Import(getTypeFromHandleMethod);
                FuncObjectObjectCtor = importer.Import(funcObjectObjectCtor);
                FuncExceptionCtor = importer.Import(funcExceptionCtor);
                FuncObjectTypeObjectCtor = importer.Import(funcObjectTypeObjectCtor);
                RegisterAotProxyMethod = importer.Import(registerAotProxyMethod);
                RegisterTypedProxyMethod = importer.Import(
                    typeof(DuckTypeAotEngine).GetMethod(nameof(DuckTypeAotEngine.RegisterTypedProxy), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotEngine.RegisterTypedProxy(Type, Type, Type, Delegate)."));
                RegisterTypedReverseProxyMethod = importer.Import(
                    typeof(DuckTypeAotEngine).GetMethod(nameof(DuckTypeAotEngine.RegisterTypedReverseProxy), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotEngine.RegisterTypedReverseProxy(Type, Type, Type, Delegate)."));
                CreateProxyInstanceType = importer.Import(typeof(CreateProxyInstance<>));
                RecordRegistrationFailureMethod = importer.Import(
                    typeof(DuckTypeAotEngine).GetMethod(nameof(DuckTypeAotEngine.RecordRegistrationFailure), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotEngine.RecordRegistrationFailure(Exception)."));
                IsDynamicCodeSupportedMethod = importer.Import(
                    typeof(DuckTypeAotEngine).GetProperty(nameof(DuckTypeAotEngine.IsDynamicCodeSupported), BindingFlags.NonPublic | BindingFlags.Static)?.GetMethod
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotEngine.IsDynamicCodeSupported."));
                FuncObjectObjectTypeSig = importer.ImportAsTypeSig(typeof(Func<object?, object?>));
                // The bootstrap validates the registry contract (and identity) first: these entry points don't inspect its
                // delegates (Delegate.Method, slow under NativeAOT), like RegisterTypedProxy.
                RegisterAotFallbackProxyMethod = importer.Import(
                    typeof(DuckTypeAotEngine).GetMethod(nameof(DuckTypeAotEngine.RegisterValidatedFallbackProxy), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotEngine.RegisterValidatedFallbackProxy(Type, Type, Type, Func<object?, Type, object?>)."));
                RegisterAotDerivedTypesProxyMethod = importer.Import(
                    typeof(DuckTypeAotEngine).GetMethod(nameof(DuckTypeAotEngine.RegisterValidatedDerivedTypesProxy), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotEngine.RegisterValidatedDerivedTypesProxy(Type, Type, Type, Func<object?, Type, object?>)."));
                SystemTypeSig = importer.ImportAsTypeSig(typeof(Type));
                RegisterAotReverseProxyMethod = importer.Import(registerAotReverseProxyMethod);
                RegisterAotProxyFailureMethod = importer.Import(
                    typeof(DuckTypeAotEngine).GetMethod(nameof(DuckTypeAotEngine.RegisterValidatedProxyFailureFactory), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotEngine.RegisterValidatedProxyFailureFactory(Type, Type, Func<Exception>)."));
                RegisterAotReverseProxyFailureMethod = importer.Import(
                    typeof(DuckTypeAotEngine).GetMethod(nameof(DuckTypeAotEngine.RegisterValidatedReverseProxyFailureFactory), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotEngine.RegisterValidatedReverseProxyFailureFactory(Type, Type, Func<Exception>)."));
                EnableAotModeMethod = importer.Import(enableAotModeMethod);
                ValidateAotRegistryContractMethod = importer.Import(validateAotRegistryContractMethod);
                DuckTypeAotRegisteredFailureCreateMethod = importer.Import(duckTypeAotRegisteredFailureCreateMethod);
                DuckTypeAotRegisteredFailureCreateWithInnerExceptionMethod = importer.Import(duckTypeAotRegisteredFailureCreateWithInnerExceptionMethod);
                DuckTypeAotRegisteredFailureCreateWithInnerExceptionInstanceMethod = importer.Import(duckTypeAotRegisteredFailureCreateWithInnerExceptionInstanceMethod);
                ThrowActivatorInvalidCastMethod = importer.Import(
                    typeof(DuckTypeAotEngine).GetMethod(nameof(DuckTypeAotEngine.ThrowActivatorInvalidCast), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("Unable to resolve DuckTypeAotEngine.ThrowActivatorInvalidCast(object, Type)."));
                ObjectCtor = importer.Import(objectCtor);
                ObjectToStringMethod = importer.Import(objectToStringMethod);
                IDuckTypeType = iDuckTypeType;
                IDuckTypeInstanceGetter = importer.Import(iDuckTypeInstanceGetter);
                var importedIgnoresAccessChecksToCtor = importer.Import(ignoresAccessChecksToCtor) as ICustomAttributeType;
                if (importedIgnoresAccessChecksToCtor is null)
                {
                    throw new InvalidOperationException("Unable to import IgnoresAccessChecksToAttribute(string).");
                }

                IgnoresAccessChecksToAttributeCtor = importedIgnoresAccessChecksToCtor;
            }

            /// <summary>
            /// Gets type from handle method.
            /// </summary>
            /// <value>The get type from handle method value.</value>
            internal IMethod GetTypeFromHandleMethod { get; }

            /// <summary>
            /// Gets Func&lt;object?, object?&gt; constructor.
            /// </summary>
            internal IMethod FuncObjectObjectCtor { get; }

            /// <summary>
            /// Gets the nonthrowing exception factory delegate constructor.
            /// </summary>
            internal IMethod FuncExceptionCtor { get; }

            /// <summary>
            /// Gets the Func&lt;object?, Type, object?&gt; constructor of the activators of array proxies.
            /// </summary>
            internal IMethod FuncObjectTypeObjectCtor { get; }

            /// <summary>
            /// Gets the method registering the activator of an array proxy for the array types assignable to its target.
            /// </summary>
            internal IMethod RegisterAotFallbackProxyMethod { get; }

            /// <summary>
            /// Gets the method registering the activator of a proxy for the classes deriving from its target.
            /// </summary>
            internal IMethod RegisterAotDerivedTypesProxyMethod { get; }

            /// <summary>
            /// Gets the System.Type signature.
            /// </summary>
            internal TypeSig SystemTypeSig { get; }

            /// <summary>
            /// Gets register aot proxy method.
            /// </summary>
            /// <value>The register aot proxy method value.</value>
            internal IMethod RegisterAotProxyMethod { get; }

            internal IMethod RegisterTypedProxyMethod { get; }

            internal IMethod RegisterTypedReverseProxyMethod { get; }

            internal ITypeDefOrRef CreateProxyInstanceType { get; }

            internal IMethod RecordRegistrationFailureMethod { get; }

            internal IMethod IsDynamicCodeSupportedMethod { get; }

            internal TypeSig FuncObjectObjectTypeSig { get; }

            /// <summary>
            /// Gets register aot reverse proxy method.
            /// </summary>
            /// <value>The register aot reverse proxy method value.</value>
            internal IMethod RegisterAotReverseProxyMethod { get; }

            /// <summary>
            /// Gets register aot proxy failure method.
            /// </summary>
            /// <value>The register aot proxy failure method value.</value>
            internal IMethod RegisterAotProxyFailureMethod { get; }

            /// <summary>
            /// Gets register aot reverse proxy failure method.
            /// </summary>
            /// <value>The register aot reverse proxy failure method value.</value>
            internal IMethod RegisterAotReverseProxyFailureMethod { get; }

            /// <summary>
            /// Gets enable aot mode method.
            /// </summary>
            /// <value>The enable aot mode method value.</value>
            internal IMethod EnableAotModeMethod { get; }

            /// <summary>
            /// Gets deterministic AOT registered failure throw method.
            /// </summary>
            internal IMethod DuckTypeAotRegisteredFailureCreateMethod { get; }

            internal IMethod DuckTypeAotRegisteredFailureCreateWithInnerExceptionMethod { get; }

            internal IMethod DuckTypeAotRegisteredFailureCreateWithInnerExceptionInstanceMethod { get; }

            internal IMethod ThrowActivatorInvalidCastMethod { get; }

            /// <summary>
            /// Gets validate aot registry contract method.
            /// </summary>
            /// <value>The validate aot registry contract method value.</value>
            internal IMethod ValidateAotRegistryContractMethod { get; }

            /// <summary>
            /// Gets object ctor.
            /// </summary>
            /// <value>The object ctor value.</value>
            internal IMethod ObjectCtor { get; }

            /// <summary>
            /// Gets object to string method.
            /// </summary>
            /// <value>The object to string method value.</value>
            internal IMethod ObjectToStringMethod { get; }

            /// <summary>
            /// Gets i duck type type.
            /// </summary>
            /// <value>The i duck type type value.</value>
            internal ITypeDefOrRef IDuckTypeType { get; }

            /// <summary>
            /// Gets i duck type instance getter.
            /// </summary>
            /// <value>The i duck type instance getter value.</value>
            internal IMethod IDuckTypeInstanceGetter { get; }

            /// <summary>
            /// Gets ignores access checks to attribute ctor.
            /// </summary>
            /// <value>The ignores access checks to attribute ctor value.</value>
            internal ICustomAttributeType IgnoresAccessChecksToAttributeCtor { get; }
        }

        private sealed partial class EmitterExecutionContext
        {
            private readonly Dictionary<ModuleDef, IReadOnlyDictionary<string, TypeDef>> _typeLookupsByModule = new();
            private readonly Dictionary<string, RuntimeTypeResolutionCacheEntry> _runtimeTypesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, AssemblyResolutionCacheEntry> _preferredRuntimeAssembliesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, AssemblyResolutionCacheEntry> _resolvedRuntimeAssembliesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, RuntimeTypeResolutionCacheEntry> _typesByName = new(StringComparer.Ordinal);
            private readonly Dictionary<(Assembly Assembly, string Path), bool> _assemblyIdentityMatches = new();
            private readonly Dictionary<(string Path, Type RuntimeType), bool> _forwardedRuntimeTypes = new();
            private readonly HashSet<string> _dynamicFailureReplayKeys = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IReadOnlyList<KeyValuePair<MethodInfo, MethodInfo>>?> _dynamicReverseSelections = new(StringComparer.Ordinal);
            private readonly DynamicOracleCache _dynamicOracleCache;
            private readonly HashSet<string> _appliedFailureProbeKeys = new(StringComparer.Ordinal);
            private readonly Dictionary<ITypeDefOrRef, ITypeDefOrRef> _importedTypeDefOrRefs = new(ReferenceIdentityComparer<ITypeDefOrRef>.Instance);
            private readonly Dictionary<Type, ITypeDefOrRef> _importedRuntimeTypes = new();
            private readonly Dictionary<string, TypeSig> _importedTypeSigs = new(StringComparer.Ordinal);
            private readonly Dictionary<string, TypeSig> _substitutedTypeSigs = new(StringComparer.Ordinal);
            private readonly Dictionary<IMethod, IMethod> _importedMethods = new(ReferenceIdentityComparer<IMethod>.Instance);
            private readonly Dictionary<IField, IField> _importedFields = new(ReferenceIdentityComparer<IField>.Instance);
            private readonly Dictionary<string, IMethod> _methodCallTargetsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, PropertySig> _propertySignaturesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IField> _valueWithTypeValueFieldRefsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IMethodDefOrRef> _valueWithTypeCreateMethodRefsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IMethodDefOrRef> _duckTypeCreateCacheCreateMethodRefsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IMethod> _duckTypeCreateCacheCreateFromMethodRefsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IMethodDefOrRef> _nullableCtorRefsByKey = new(StringComparer.Ordinal);
            private readonly HashSet<string> _requiredAccessCheckAssemblyNames = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, MethodDef> _failureThrowerMethodsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<TypeDef, IReadOnlyList<MethodDef>> _duckIncludeMethodsByTargetType = new();
            private readonly Dictionary<ICustomAttributeType, ICustomAttributeType> _importedCustomAttributeTypes = new(ReferenceIdentityComparer<ICustomAttributeType>.Instance);

            internal EmitterExecutionContext(TargetTypeIndex targetTypeIndex, DynamicOracleCache dynamicOracleCache)
            {
                TargetTypeIndex = targetTypeIndex;
                LoadedRuntimeAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                _dynamicOracleCache = dynamicOracleCache;
            }

            internal List<string> Warnings { get; } = new();

            internal HashSet<Tuple<Type, object>> CreatableForwardProxyShapes => _dynamicOracleCache.CreatableForwardProxyShapes;

            internal Dictionary<string, bool> MetadataOnlyMappings { get; } = new(StringComparer.Ordinal);

            /// <summary>
            /// Gets the keys of the mappings the generator couldn't evaluate with dynamic duck typing.
            /// </summary>
            internal HashSet<string> MetadataOnlyEvaluations { get; } = new(StringComparer.Ordinal);

            /// <summary>
            /// Gets why mappings are specific to the generator's runtime, by mapping key (see RecordRuntimeSpecificBindings).
            /// </summary>
            internal Dictionary<string, string> RuntimeSpecificMappings { get; } = new(StringComparer.Ordinal);

            /// <summary>
            /// Gets the reverse proxy types dynamic duck typing created in the generator, by reverse mapping key.
            /// </summary>
            internal Dictionary<string, Type> DynamicReverseProxyTypes { get; } = new(StringComparer.Ordinal);

            /// <summary>
            /// Gets the reverse proxy types the registry generates, with the ones dynamic duck typing creates for the same pairs:
            /// the generator binds the forward proxies of a generated reverse proxy type like dynamic duck typing binds them for its own.
            /// </summary>
            internal Dictionary<TypeDef, Type> GeneratedTypeRuntimeTypes { get; } = new();

            /// <summary>
            /// Gets the generated reverse proxy types by the reverse proxy type dynamic duck typing creates for the same pair.
            /// </summary>
            internal Dictionary<Type, TypeDef> RuntimeTypeGeneratedTypes { get; } = new();

            /// <summary>
            /// Gets the type and message of the exception wrapped by the failure of dynamic duck typing, by mapping key.
            /// </summary>
            internal Dictionary<string, IReadOnlyList<KeyValuePair<string, string>>> DynamicFailureInnerExceptions { get; } = new(StringComparer.Ordinal);

            /// <summary>
            /// Gets whether the runtime types used as generic arguments use duck attributes dynamic duck typing doesn't read.
            /// </summary>
            internal Dictionary<Type, bool> MetadataOnlyRuntimeTypes { get; } = new();

            /// <summary>
            /// Gets the keys of the mappings whose dynamic duck typing failure the generator replays without vouching for it (see
            /// TryGetMissingTypeAssemblyName).
            /// </summary>
            internal HashSet<string> UnverifiedDynamicFailures { get; } = new(StringComparer.Ordinal);

            internal List<KeyValuePair<string, string>> GeneratedProxyTypes { get; } = new();

            internal IField? TypeMissingField { get; set; }

            internal TargetTypeIndex TargetTypeIndex { get; }

            internal IReadOnlyList<Assembly> LoadedRuntimeAssemblies { get; }

            internal IReadOnlyCollection<string> RequiredAccessCheckAssemblyNames => _requiredAccessCheckAssemblyNames;

            internal void AddRequiredAccessCheckAssemblyName(string? assemblyName)
            {
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = _requiredAccessCheckAssemblyNames.Add(assemblyName!);
                }
            }

            internal IReadOnlyDictionary<string, TypeDef> GetOrCreateTypeLookup(ModuleDef module)
            {
                if (_typeLookupsByModule.TryGetValue(module, out var cachedLookup))
                {
                    return cachedLookup;
                }

                var typeLookup = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
                foreach (var type in module.GetTypes())
                {
                    if (!StringUtil.IsNullOrWhiteSpace(type.FullName))
                    {
                        typeLookup[type.FullName] = type;
                    }

                    if (!StringUtil.IsNullOrWhiteSpace(type.ReflectionFullName))
                    {
                        typeLookup[type.ReflectionFullName] = type;
                    }
                }

                _typeLookupsByModule[module] = typeLookup;
                return typeLookup;
            }

            internal bool TryGetRuntimeType(string key, out Type? runtimeType)
            {
                if (_runtimeTypesByKey.TryGetValue(key, out var cachedEntry))
                {
                    runtimeType = cachedEntry.Type;
                    return true;
                }

                runtimeType = null;
                return false;
            }

            internal void CacheRuntimeType(string key, Type? runtimeType)
            {
                _runtimeTypesByKey[key] = new RuntimeTypeResolutionCacheEntry(runtimeType);
            }

            internal bool TryGetAssemblyIdentityMatch(Assembly assembly, string path, out bool match)
                => _assemblyIdentityMatches.TryGetValue((assembly, path), out match);

            internal void CacheAssemblyIdentityMatch(Assembly assembly, string path, bool match)
                => _assemblyIdentityMatches[(assembly, path)] = match;

            internal bool TryGetForwardedRuntimeType(string path, Type runtimeType, out bool forwards)
                => _forwardedRuntimeTypes.TryGetValue((path, runtimeType), out forwards);

            internal void CacheForwardedRuntimeType(string path, Type runtimeType, bool forwards)
                => _forwardedRuntimeTypes[(path, runtimeType)] = forwards;

            internal bool TryGetPreferredRuntimeAssembly(string key, out Assembly? assembly)
            {
                if (_preferredRuntimeAssembliesByKey.TryGetValue(key, out var cachedEntry))
                {
                    assembly = cachedEntry.Assembly;
                    return true;
                }

                assembly = null;
                return false;
            }

            internal void CachePreferredRuntimeAssembly(string key, Assembly? assembly)
            {
                _preferredRuntimeAssembliesByKey[key] = new AssemblyResolutionCacheEntry(assembly);
            }

            internal bool TryGetResolvedRuntimeAssembly(string key, out Assembly? assembly)
            {
                if (_resolvedRuntimeAssembliesByKey.TryGetValue(key, out var cachedEntry))
                {
                    assembly = cachedEntry.Assembly;
                    return true;
                }

                assembly = null;
                return false;
            }

            internal void CacheResolvedRuntimeAssembly(string key, Assembly? assembly)
            {
                _resolvedRuntimeAssembliesByKey[key] = new AssemblyResolutionCacheEntry(assembly);
            }

            internal bool TryGetTypeByName(string typeName, out Type? runtimeType)
            {
                if (_typesByName.TryGetValue(typeName, out var cachedEntry))
                {
                    runtimeType = cachedEntry.Type;
                    return true;
                }

                runtimeType = null;
                return false;
            }

            internal void CacheTypeByName(string typeName, Type? runtimeType)
            {
                _typesByName[typeName] = new RuntimeTypeResolutionCacheEntry(runtimeType);
            }

            internal IReadOnlyList<KeyValuePair<MethodInfo, MethodInfo>>? GetOrCreateDynamicReverseSelection(string mappingKey, Func<IReadOnlyList<KeyValuePair<MethodInfo, MethodInfo>>?> factory)
            {
                if (!_dynamicReverseSelections.TryGetValue(mappingKey, out var selection))
                {
                    selection = factory();
                    _dynamicReverseSelections[mappingKey] = selection;
                }

                return selection;
            }

            internal void MarkDynamicFailureReplayed(string mappingKey)
            {
                _dynamicFailureReplayKeys.Add(mappingKey);
            }

            internal bool ReplaysDynamicFailure(string mappingKey)
            {
                return _dynamicFailureReplayKeys.Contains(mappingKey);
            }

            internal bool TryGetFailureProbe(string key, out FailureProbeCacheEntry failureProbe)
            {
                if (!_dynamicOracleCache.FailureProbes.TryGetValue(key, out failureProbe!))
                {
                    return false;
                }

                // Asked in a previous pass: what the probe recorded applies to this pass too.
                if (_appliedFailureProbeKeys.Add(key))
                {
                    Warnings.AddRange(failureProbe.Warnings);
                    if (failureProbe.MetadataOnly)
                    {
                        MetadataOnlyEvaluations.Add(key);
                    }

                    if (failureProbe.Unverified)
                    {
                        UnverifiedDynamicFailures.Add(key);
                    }

                    if (failureProbe.InnerException is { } innerException)
                    {
                        DynamicFailureInnerExceptions[key] = innerException;
                    }

                    if (failureProbe.DynamicReverseProxyType is { } dynamicReverseProxyType)
                    {
                        DynamicReverseProxyTypes[key] = dynamicReverseProxyType;
                    }
                }

                return true;
            }

            internal void CacheFailureProbe(string key, DynamicCreationOutcome outcome, Type? exceptionType, string? exceptionMessage, int firstProbeWarning)
            {
                _appliedFailureProbeKeys.Add(key);
                _dynamicOracleCache.FailureProbes[key] = new FailureProbeCacheEntry(
                    outcome,
                    exceptionType,
                    exceptionMessage,
                    Warnings.Skip(firstProbeWarning).ToArray(),
                    MetadataOnlyEvaluations.Contains(key),
                    UnverifiedDynamicFailures.Contains(key),
                    DynamicFailureInnerExceptions.TryGetValue(key, out var innerException) ? innerException : null,
                    DynamicReverseProxyTypes.TryGetValue(key, out var dynamicReverseProxyType) ? dynamicReverseProxyType : null);
            }

            internal bool TryGetImportedTypeDefOrRef(ITypeDefOrRef source, out ITypeDefOrRef importedType)
                => _importedTypeDefOrRefs.TryGetValue(source, out importedType!);

            internal void CacheImportedTypeDefOrRef(ITypeDefOrRef source, ITypeDefOrRef importedType)
                => _importedTypeDefOrRefs[source] = importedType;

            internal bool TryGetImportedRuntimeType(Type runtimeType, out ITypeDefOrRef importedType)
                => _importedRuntimeTypes.TryGetValue(runtimeType, out importedType!);

            internal void CacheImportedRuntimeType(Type runtimeType, ITypeDefOrRef importedType)
                => _importedRuntimeTypes[runtimeType] = importedType;

            internal bool TryGetImportedTypeSig(string key, out TypeSig importedTypeSig)
                => _importedTypeSigs.TryGetValue(key, out importedTypeSig!);

            internal void CacheImportedTypeSig(string key, TypeSig importedTypeSig)
                => _importedTypeSigs[key] = importedTypeSig;

            internal bool TryGetSubstitutedTypeSig(string key, out TypeSig substitutedTypeSig)
                => _substitutedTypeSigs.TryGetValue(key, out substitutedTypeSig!);

            internal void CacheSubstitutedTypeSig(string key, TypeSig substitutedTypeSig)
                => _substitutedTypeSigs[key] = substitutedTypeSig;

            internal bool TryGetImportedMethod(IMethod source, out IMethod importedMethod)
                => _importedMethods.TryGetValue(source, out importedMethod!);

            internal void CacheImportedMethod(IMethod source, IMethod importedMethod)
                => _importedMethods[source] = importedMethod;

            internal bool TryGetImportedField(IField source, out IField importedField)
                => _importedFields.TryGetValue(source, out importedField!);

            internal void CacheImportedField(IField source, IField importedField)
                => _importedFields[source] = importedField;

            internal bool TryGetMethodCallTarget(string key, out IMethod method)
                => _methodCallTargetsByKey.TryGetValue(key, out method!);

            internal void CacheMethodCallTarget(string key, IMethod method)
                => _methodCallTargetsByKey[key] = method;

            internal bool TryGetPropertySignature(string key, out PropertySig propertySig)
                => _propertySignaturesByKey.TryGetValue(key, out propertySig!);

            internal void CachePropertySignature(string key, PropertySig propertySig)
                => _propertySignaturesByKey[key] = propertySig;

            internal bool TryGetValueWithTypeValueFieldRef(string key, out IField field)
                => _valueWithTypeValueFieldRefsByKey.TryGetValue(key, out field!);

            internal void CacheValueWithTypeValueFieldRef(string key, IField field)
                => _valueWithTypeValueFieldRefsByKey[key] = field;

            internal bool TryGetValueWithTypeCreateMethodRef(string key, out IMethodDefOrRef method)
                => _valueWithTypeCreateMethodRefsByKey.TryGetValue(key, out method!);

            internal void CacheValueWithTypeCreateMethodRef(string key, IMethodDefOrRef method)
                => _valueWithTypeCreateMethodRefsByKey[key] = method;

            internal bool TryGetDuckTypeCreateCacheCreateMethodRef(string key, out IMethodDefOrRef method)
                => _duckTypeCreateCacheCreateMethodRefsByKey.TryGetValue(key, out method!);

            internal void CacheDuckTypeCreateCacheCreateMethodRef(string key, IMethodDefOrRef method)
                => _duckTypeCreateCacheCreateMethodRefsByKey[key] = method;

            internal bool TryGetDuckTypeCreateCacheCreateFromMethodRef(string key, out IMethod method)
                => _duckTypeCreateCacheCreateFromMethodRefsByKey.TryGetValue(key, out method!);

            internal void CacheDuckTypeCreateCacheCreateFromMethodRef(string key, IMethod method)
                => _duckTypeCreateCacheCreateFromMethodRefsByKey[key] = method;

            internal bool TryGetNullableCtorRef(string key, out IMethodDefOrRef method)
                => _nullableCtorRefsByKey.TryGetValue(key, out method!);

            internal void CacheNullableCtorRef(string key, IMethodDefOrRef method)
                => _nullableCtorRefsByKey[key] = method;

            internal bool TryGetFailureThrowerMethod(string key, out MethodDef method)
                => _failureThrowerMethodsByKey.TryGetValue(key, out method!);

            internal void CacheFailureThrowerMethod(string key, MethodDef method)
                => _failureThrowerMethodsByKey[key] = method;

            internal bool TryGetImportedCustomAttributeType(ICustomAttributeType source, out ICustomAttributeType importedType)
                => _importedCustomAttributeTypes.TryGetValue(source, out importedType!);

            internal void CacheImportedCustomAttributeType(ICustomAttributeType source, ICustomAttributeType importedType)
                => _importedCustomAttributeTypes[source] = importedType;

            internal IReadOnlyList<MethodDef> GetOrCreateDuckIncludeMethods(TypeDef targetType)
            {
                if (_duckIncludeMethodsByTargetType.TryGetValue(targetType, out var cachedMethods))
                {
                    return cachedMethods;
                }

                // Like dynamic duck typing (GetMethods(targetType), then GetCustomAttribute<DuckIncludeAttribute>(true)): the most
                // derived method of each vtable slot, when it's virtual (or a member of an interface target), not final, private
                // or an accessor, and it has [DuckInclude] or overrides a method that has it. A sealed override hides it.
                var methods = new List<MethodDef>();
                var visitedSlots = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                for (var current = targetType; current is not null; current = current.BaseType?.ResolveTypeDef())
                {
                    foreach (var method in current.Methods)
                    {
                        if (method.IsConstructor || method.IsStatic ||
                            (method.IsVirtual && !visitedSlots.Add(GetVtableSlotMethod(targetType, method))) ||
                            method.IsFinal || method.IsPrivate || method.IsSpecialName || !(targetType.IsInterface || method.IsVirtual || method.IsAbstract))
                        {
                            continue;
                        }

                        var visitedMethods = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                        for (var declaration = method; declaration is not null && visitedMethods.Add(declaration); declaration = FindOverriddenBaseMethod(targetType, declaration))
                        {
                            if (declaration.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, DuckIncludeAttributeTypeName, StringComparison.Ordinal)))
                            {
                                methods.Add(method);
                                break;
                            }
                        }
                    }
                }

                _duckIncludeMethodsByTargetType[targetType] = methods;
                return methods;
            }
        }

        private sealed class TargetTypeIndex
        {
            internal TargetTypeIndex(
                IReadOnlyDictionary<string, TypeDef> typeByAssemblyAndName,
                IReadOnlyList<TargetTypeIndexEntry> aliasCandidateTargets,
                IReadOnlyDictionary<string, IReadOnlyList<TargetAliasTargetInfo>> assignableForwardTypesByAncestor,
                IReadOnlyDictionary<string, IReadOnlyList<TargetAliasTargetInfo>> assignableReverseTypesByAncestor)
            {
                TypeByAssemblyAndName = typeByAssemblyAndName;
                AliasCandidateTargets = aliasCandidateTargets;
                AssignableForwardTypesByAncestor = assignableForwardTypesByAncestor;
                AssignableReverseTypesByAncestor = assignableReverseTypesByAncestor;
            }

            internal IReadOnlyDictionary<string, TypeDef> TypeByAssemblyAndName { get; }

            internal IReadOnlyList<TargetTypeIndexEntry> AliasCandidateTargets { get; }

            internal IReadOnlyDictionary<string, IReadOnlyList<TargetAliasTargetInfo>> AssignableForwardTypesByAncestor { get; }

            internal IReadOnlyDictionary<string, IReadOnlyList<TargetAliasTargetInfo>> AssignableReverseTypesByAncestor { get; }

            internal bool TryGetAssignableTargets(DuckTypeAotMappingMode mode, string targetAssemblyName, string targetTypeName, out IReadOnlyList<TargetAliasTargetInfo> aliasTargets)
            {
                aliasTargets = Array.Empty<TargetAliasTargetInfo>();
                if (!TypeByAssemblyAndName.TryGetValue(BuildAssemblyTypeCacheKey(targetAssemblyName, targetTypeName), out var canonicalTargetType))
                {
                    return false;
                }

                var sourceIndex = mode == DuckTypeAotMappingMode.Reverse ? AssignableReverseTypesByAncestor : AssignableForwardTypesByAncestor;
                if (!sourceIndex.TryGetValue(BuildAssemblyTypeCacheKey(targetAssemblyName, targetTypeName), out var rawTargets))
                {
                    return false;
                }

                aliasTargets = rawTargets
                              .Where(target => !IsCanonicalTargetAliasTarget(canonicalTargetType, target.AssemblyName, target.TypeName))
                              .ToList();
                return aliasTargets.Count > 0;
            }
        }

        private sealed class TargetTypeIndexEntry
        {
            internal TargetTypeIndexEntry(string assemblyName, TypeDef type)
            {
                AssemblyName = assemblyName;
                Type = type;
            }

            internal string AssemblyName { get; }

            internal TypeDef Type { get; }
        }

        private sealed class TargetAliasTargetInfo
        {
            internal TargetAliasTargetInfo(string assemblyName, string typeName)
            {
                AssemblyName = assemblyName;
                TypeName = typeName;
            }

            internal string AssemblyName { get; }

            internal string TypeName { get; }
        }

        private sealed class RuntimeTypeResolutionCacheEntry
        {
            internal RuntimeTypeResolutionCacheEntry(Type? type)
            {
                Type = type;
            }

            internal Type? Type { get; }
        }

        private sealed class AssemblyResolutionCacheEntry
        {
            internal AssemblyResolutionCacheEntry(Assembly? assembly)
            {
                Assembly = assembly;
            }

            internal Assembly? Assembly { get; }
        }

        /// <summary>
        /// What dynamic duck typing in the generator does with a mapping, and what the probe recorded about it.
        /// </summary>
        private sealed class FailureProbeCacheEntry
        {
            internal FailureProbeCacheEntry(
                DynamicCreationOutcome outcome,
                Type? exceptionType,
                string? exceptionMessage,
                IReadOnlyList<string> warnings,
                bool metadataOnly,
                bool unverified,
                IReadOnlyList<KeyValuePair<string, string>>? innerException,
                Type? dynamicReverseProxyType)
            {
                Outcome = outcome;
                ExceptionType = exceptionType;
                ExceptionMessage = exceptionMessage;
                Warnings = warnings;
                MetadataOnly = metadataOnly;
                Unverified = unverified;
                InnerException = innerException;
                DynamicReverseProxyType = dynamicReverseProxyType;
            }

            internal DynamicCreationOutcome Outcome { get; }

            internal Type? ExceptionType { get; }

            internal string? ExceptionMessage { get; }

            internal IReadOnlyList<string> Warnings { get; }

            internal bool MetadataOnly { get; }

            internal bool Unverified { get; }

            internal IReadOnlyList<KeyValuePair<string, string>>? InnerException { get; }

            internal Type? DynamicReverseProxyType { get; }
        }

        /// <summary>
        /// The answers of dynamic duck typing in the generator, kept across the emission passes of a generation (see Emit): they
        /// don't depend on the pass, and the probes create Reflection.Emit types, which every pass would create again.
        /// </summary>
        /// <summary>
        /// Finds the types of the registry being emitted by full name. ModuleDef.Find enumerates every type on each call
        /// (its cache doesn't see the types added afterwards), which is quadratic with the thousands of registrations of a
        /// target type that isn't sealed (System.Object, System.Threading.Tasks.Task): the index only reads the types
        /// added since the previous lookup.
        /// </summary>
        private sealed class GeneratedTypeIndex
        {
            private readonly ModuleDef _module;
            private readonly Dictionary<string, TypeDef> _types = new(StringComparer.Ordinal);
            private int _indexedTopLevelTypes;

            public GeneratedTypeIndex(ModuleDef module)
            {
                _module = module;
            }

            public TypeDef? Find(string? fullName)
            {
                if (fullName is null)
                {
                    return null;
                }

                if (_types.TryGetValue(fullName, out var type))
                {
                    return type;
                }

                var topLevelTypes = _module.Types;
                for (; _indexedTopLevelTypes < topLevelTypes.Count; _indexedTopLevelTypes++)
                {
                    foreach (var added in AllTypes(topLevelTypes[_indexedTopLevelTypes]))
                    {
                        _types[added.FullName] = added;
                    }
                }

                // Types can also be added to the types read before (nested): those are still found, the slow way.
                return _types.TryGetValue(fullName, out type) ? type : _module.Find(fullName, isReflectionName: false);
            }

            private static IEnumerable<TypeDef> AllTypes(TypeDef type)
            {
                yield return type;
                foreach (var nested in type.NestedTypes)
                {
                    foreach (var nestedType in AllTypes(nested))
                    {
                        yield return nestedType;
                    }
                }
            }
        }

        private sealed class DynamicOracleCache
        {
            internal Dictionary<string, FailureProbeCacheEntry> FailureProbes { get; } = new(StringComparer.Ordinal);

            internal HashSet<Tuple<Type, object>> CreatableForwardProxyShapes { get; } = new();
        }

        private sealed class ReferenceIdentityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            internal static readonly ReferenceIdentityComparer<T> Instance = new();

            public bool Equals(T? x, T? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        private sealed class InterfaceMethodContract
        {
            internal InterfaceMethodContract(TypeDef interfaceType, ITypeDefOrRef interfaceReference, TypeSig interfaceTypeSig, bool isDeclaredOnProxyType, MethodSig methodSig)
            {
                InterfaceType = interfaceType;
                InterfaceReference = interfaceReference;
                InterfaceTypeSig = interfaceTypeSig;
                IsDeclaredOnProxyType = isDeclaredOnProxyType;
                MethodSig = methodSig;
            }

            internal TypeDef InterfaceType { get; }

            internal ITypeDefOrRef InterfaceReference { get; }

            internal TypeSig InterfaceTypeSig { get; }

            internal bool IsDeclaredOnProxyType { get; }

            internal bool RequiresExplicitInterfaceImplementation => !IsDeclaredOnProxyType &&
                                                                     InterfaceType.GenericParameters.Count == 0 &&
                                                                     InterfaceReference is not TypeSpec;

            internal MethodSig MethodSig { get; }
        }

        private sealed class EmitterProfile
        {
            internal Stopwatch Total { get; } = Stopwatch.StartNew();

            internal double LoadProxyModulesSeconds { get; set; }

            internal double LoadTargetModulesSeconds { get; set; }

            internal double BuildRuntimeTypeResolutionMapSeconds { get; set; }

            internal double BuildTargetTypeIndexSeconds { get; set; }

            internal double BuildRuntimeRegistrationsSeconds { get; set; }

            internal double EmitLoopSeconds { get; set; }

            internal double EmitMappingSeconds { get; set; }

            internal int EmitMappingCount { get; set; }

            internal double DynamicFailureProbeSeconds { get; set; }

            internal int DynamicFailureProbeCount { get; set; }

            internal double KnownFailureRegistrationSeconds { get; set; }

            internal int KnownFailureRegistrationCount { get; set; }

            internal int RuntimeTypeCacheHits { get; set; }

            internal int RuntimeTypeCacheMisses { get; set; }

            internal int RuntimeTypeFallbackHits { get; set; }

            internal int RuntimeTypeUnresolved { get; set; }

            internal int FailureClassifierFastPathCount { get; set; }

            internal int FailureClassifierFallbackCount { get; set; }

            internal int ImportCacheHits { get; set; }

            internal int ImportCacheMisses { get; set; }

            internal double RegistrationPlanningSeconds { get; set; }

            internal int ForwardBindingPlanCacheHits { get; set; }

            internal int ForwardBindingPlanCacheMisses { get; set; }

            internal int ConversionPlanCacheHits { get; set; }

            internal int ConversionPlanCacheMisses { get; set; }

            internal int MethodCallTargetCacheHits { get; set; }

            internal int MethodCallTargetCacheMisses { get; set; }

            internal int PropertySignatureCacheHits { get; set; }

            internal int PropertySignatureCacheMisses { get; set; }

            internal int ReverseCustomAttributePlanCacheHits { get; set; }

            internal int ReverseCustomAttributePlanCacheMisses { get; set; }

            internal double ForwardBindingCollectionSeconds { get; set; }

            internal double StructCopyBindingCollectionSeconds { get; set; }

            internal double DuckIncludeCollectionSeconds { get; set; }

            internal double ForwardBindingResolutionSeconds { get; set; }

            internal double ForwardMethodBindingSeconds { get; set; }

            internal double ForwardParameterBindingSeconds { get; set; }

            internal int ForwardCandidateEnumeratedCount { get; set; }

            internal int ForwardCandidateDedupRejectCount { get; set; }

            internal int ForwardCandidateNameRejectCount { get; set; }

            internal int ForwardCandidateParameterTypeRejectCount { get; set; }

            internal int ForwardCandidatePrivateRejectCount { get; set; }

            internal int ForwardCandidateAcceptedCount { get; set; }

            internal int ForwardResolutionMethodSuccessCount { get; set; }

            internal int ForwardResolutionFieldSuccessCount { get; set; }

            internal int ForwardResolutionFirstFailureCount { get; set; }

            internal int ForwardResolutionPropertyCantBeWrittenCount { get; set; }

            internal int ForwardResolutionMissingTargetCount { get; set; }

            internal int ForwardResolutionAmbiguousCount { get; set; }

            internal double ForwardClosedGenericMethodArgumentResolutionSeconds { get; set; }

            internal int ForwardClosedGenericMethodArgumentResolutionCount { get; set; }

            internal double ForwardFieldResolutionSeconds { get; set; }

            internal int ForwardFieldResolutionCount { get; set; }

            internal int ForwardFieldCandidateEnumeratedCount { get; set; }

            internal double ForwardFieldSignatureCompatibilitySeconds { get; set; }

            internal double PropertyCantBeWrittenResolutionSeconds { get; set; }

            internal int PropertyCantBeWrittenResolutionCount { get; set; }

            internal int PropertyCantBeWrittenCandidateCount { get; set; }

            internal double ForwardCandidateListBuildSeconds { get; set; }

            internal int ForwardCandidateListCacheHits { get; set; }

            internal int ForwardCandidateListCacheMisses { get; set; }

            internal double TypeSubstitutionSeconds { get; set; }

            internal int TypeSubstitutionCacheHits { get; set; }

            internal int TypeSubstitutionCacheMisses { get; set; }

            internal double RuntimeTypeFromTypeSigSeconds { get; set; }

            internal double MethodCallTargetSeconds { get; set; }

            internal double EmitMethodArgumentConversionSeconds { get; set; }

            internal double EmitMethodReturnConversionSeconds { get; set; }

            internal double EmitForwardMethodBodySeconds { get; set; }

            internal int EmitForwardMethodBodyCount { get; set; }

            internal double EmitForwardFieldGetBodySeconds { get; set; }

            internal int EmitForwardFieldGetBodyCount { get; set; }

            internal double EmitForwardFieldSetBodySeconds { get; set; }

            internal int EmitForwardFieldSetBodyCount { get; set; }

            internal int EmitArgumentConversionNoneCount { get; set; }

            internal int EmitArgumentConversionUnwrapCount { get; set; }

            internal int EmitArgumentConversionExtractDuckTypeCount { get; set; }

            internal int EmitArgumentConversionDuckChainCount { get; set; }

            internal int EmitArgumentConversionTypeConversionCount { get; set; }

            internal double ForwardBindingPlanCacheKeyBuildSeconds { get; set; }

            internal double ForwardMethodBindingPlanCacheKeyBuildSeconds { get; set; }

            internal double MethodArgumentConversionCacheKeyBuildSeconds { get; set; }

            internal double MethodReturnConversionCacheKeyBuildSeconds { get; set; }

            internal double EnsureInterfacePropertyMetadataSeconds { get; set; }

            internal double CopyMethodGenericParametersSeconds { get; set; }

            internal double ImportTypeDefOrRefSeconds { get; set; }

            internal double ImportTypeSigSeconds { get; set; }

            internal double ImportMethodSeconds { get; set; }

            internal double ImportFieldSeconds { get; set; }
        }
    }
}
