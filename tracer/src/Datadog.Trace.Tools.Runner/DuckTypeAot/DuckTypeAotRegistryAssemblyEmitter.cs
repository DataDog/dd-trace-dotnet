// <copyright file="DuckTypeAotRegistryAssemblyEmitter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.DuckTyping;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Provides helper operations for duck type aot registry assembly emitter.
    /// </summary>
    internal static class DuckTypeAotRegistryAssemblyEmitter
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
        /// Defines the status code property cant be written constant.
        /// </summary>
        private const string StatusCodePropertyCantBeWritten = "DTAOT0212";

        /// <summary>
        /// Defines the status code field is readonly constant.
        /// </summary>
        private const string StatusCodeFieldIsReadonly = "DTAOT0213";

        /// <summary>
        /// Defines the status code reverse custom attribute named arguments constant.
        /// </summary>
        private const string StatusCodeCustomAttributeNamedArguments = "DTAOT0214";

        /// <summary>
        /// Defines the status code unsupported proxy constructor constant.
        /// </summary>
        private const string StatusCodeUnsupportedProxyConstructor = "DTAOT0210";

        /// <summary>
        /// Defines the status code unsupported closed generic mapping constant.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private const string StatusCodeUnsupportedClosedGenericMapping = "DTAOT0211";

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
        /// Defines the parity scenario id RT-2 constant.
        /// </summary>
        private const string ParityScenarioIdRt2 = "RT-2";

        /// <summary>
        /// Defines the parity scenario id E-39 constant.
        /// </summary>
        private const string ParityScenarioIdE39 = "E-39";

        /// <summary>
        /// Defines the parity scenario id E-40 constant.
        /// </summary>
        private const string ParityScenarioIdE40 = "E-40";

        /// <summary>
        /// Defines the parity scenario id E-42 constant.
        /// </summary>
        private const string ParityScenarioIdE42 = "E-42";

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
        /// Invokes dynamic ducktyping dry-run validation for forward mappings.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2026", Justification = "DuckType AOT tooling executes in build/test contexts with full metadata.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2057", Justification = "Dry-run validation uses explicit mapping metadata generated by the same toolchain.")]
        private static readonly MethodInfo? DynamicForwardDryRunFactory =
            typeof(DuckType).GetMethod(
                "CreateProxyType",
                BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Type), typeof(Type), typeof(bool) },
                modifiers: null);

        /// <summary>
        /// Invokes dynamic ducktyping dry-run validation for reverse mappings.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2026", Justification = "DuckType AOT tooling executes in build/test contexts with full metadata.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2057", Justification = "Dry-run validation uses explicit mapping metadata generated by the same toolchain.")]
        private static readonly MethodInfo? DynamicReverseDryRunFactory =
            typeof(DuckType).GetMethod(
                "CreateReverseProxyType",
                BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Type), typeof(Type), typeof(bool) },
                modifiers: null);

        /// <summary>
        /// Reflective accessor for CreateTypeResult.CanCreate().
        /// </summary>
        private static readonly MethodInfo? CreateTypeResultCanCreateMethod =
            typeof(DuckType.CreateTypeResult).GetMethod(
                nameof(DuckType.CreateTypeResult.CanCreate),
                BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// Reflective accessor for CreateTypeResult.ProxyType.
        /// </summary>
        private static readonly PropertyInfo? CreateTypeResultProxyTypeProperty =
            typeof(DuckType.CreateTypeResult).GetProperty(
                nameof(DuckType.CreateTypeResult.ProxyType),
                BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// Stores runtime assembly paths used by metadata-to-runtime type resolution during emission.
        /// </summary>
        private static IReadOnlyDictionary<string, string>? runtimeTypeResolutionAssemblyPathsByName;

        [ThreadStatic]
        private static EmitterProfile? _currentProfile;

        [ThreadStatic]
        private static EmitterExecutionContext? _currentExecutionContext;

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
        /// Defines named constants for field accessor kind.
        /// </summary>
        private enum FieldAccessorKind
        {
            /// <summary>
            /// Represents getter.
            /// </summary>
            Getter,

            /// <summary>
            /// Represents setter.
            /// </summary>
            Setter
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
        /// Represents the binding plan kind used by assignable alias safety checks.
        /// </summary>
        private enum AliasSemanticBindingPlanKind
        {
            /// <summary>
            /// The binding plan is not initialized.
            /// </summary>
            None,

            /// <summary>
            /// The binding plan delegates proxy methods to target members.
            /// </summary>
            Forward,

            /// <summary>
            /// The binding plan copies target members into a DuckCopy struct.
            /// </summary>
            StructCopy
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

        /// <summary>
        /// Defines named constants for method argument conversion kind.
        /// </summary>
        private enum MethodArgumentConversionKind
        {
            /// <summary>
            /// Represents none.
            /// </summary>
            None,

            /// <summary>
            /// Represents unwrap value with type.
            /// </summary>
            UnwrapValueWithType,

            /// <summary>
            /// Represents extract duck type instance.
            /// </summary>
            ExtractDuckTypeInstance,

            /// <summary>
            /// Represents duck chain to proxy.
            /// </summary>
            DuckChainToProxy,

            /// <summary>
            /// Represents type conversion.
            /// </summary>
            TypeConversion
        }

        /// <summary>
        /// Defines named constants for method return conversion kind.
        /// </summary>
        private enum MethodReturnConversionKind
        {
            /// <summary>
            /// Represents none.
            /// </summary>
            None,

            /// <summary>
            /// Represents wrap value with type.
            /// </summary>
            WrapValueWithType,

            /// <summary>
            /// Represents wrap value with type after duck-chaining the inner value.
            /// </summary>
            WrapValueWithTypeAfterDuckChainToProxy,

            /// <summary>
            /// Represents wrap value with type after applying type conversion to the inner value.
            /// </summary>
            WrapValueWithTypeAfterTypeConversion,

            /// <summary>
            /// Represents duck chain to proxy.
            /// </summary>
            DuckChainToProxy,

            /// <summary>
            /// Represents extract duck type instance.
            /// </summary>
            ExtractDuckTypeInstance,

            /// <summary>
            /// Represents type conversion.
            /// </summary>
            TypeConversion
        }

        /// <summary>
        /// Emits the AOT registry assembly and metadata artifacts.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <param name="artifactPaths">The artifact paths value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <returns>The result produced by this operation.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        internal static DuckTypeAotRegistryEmissionResult Emit(
            DuckTypeAotGenerateOptions options,
            DuckTypeAotArtifactPaths artifactPaths,
            DuckTypeAotMappingResolutionResult mappingResolutionResult)
        {
            _currentProfile = IsProfilingEnabled() ? new EmitterProfile() : null;
            var generatedAssemblyName = options.AssemblyName ?? Path.GetFileNameWithoutExtension(artifactPaths.OutputAssemblyPath);
            var deterministicMvid = ComputeDeterministicMvid(generatedAssemblyName, mappingResolutionResult.Mappings);
            var generatedAssemblyVersion = new Version(1, 0, 0, 0);
            var generatedAssemblyFullName = new AssemblyName(generatedAssemblyName) { Version = generatedAssemblyVersion }.FullName ?? generatedAssemblyName;
            var datadogTraceAssemblyPath = ResolveDatadogTraceAssemblyPath(mappingResolutionResult.TargetAssemblyPathsByName);
            var generatedCorLibAssemblyRef = ResolveGeneratedCorLibAssemblyRef(mappingResolutionResult, datadogTraceAssemblyPath);

            var assemblyDef = new AssemblyDefUser(generatedAssemblyName, generatedAssemblyVersion);
            var moduleDef = new ModuleDefUser(Path.GetFileName(artifactPaths.OutputAssemblyPath), deterministicMvid, generatedCorLibAssemblyRef)
            {
                Kind = ModuleKind.Dll
            };
            assemblyDef.Modules.Add(moduleDef);

            var importedMembers = new ImportedMembers(moduleDef);
            var assemblyReferences = new Dictionary<string, AssemblyRef>(StringComparer.OrdinalIgnoreCase);
            var mappingResults = new Dictionary<string, DuckTypeAotMappingEmissionResult>(StringComparer.Ordinal);
            var emissionWarnings = new List<string>();

            foreach (var proxyAssemblyPath in mappingResolutionResult.ProxyAssemblyPathsByName.Values.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                AddAssemblyReference(moduleDef, assemblyReferences, proxyAssemblyPath);
            }

            foreach (var targetAssemblyPath in mappingResolutionResult.TargetAssemblyPathsByName.Values.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                AddAssemblyReference(moduleDef, assemblyReferences, targetAssemblyPath);
            }

            var datadogTraceAssemblyVersion = AssemblyName.GetAssemblyName(datadogTraceAssemblyPath).Version?.ToString() ?? "0.0.0.0";
            var datadogTraceAssemblyMvid = ResolveAssemblyMvid(datadogTraceAssemblyPath);
            _ = AddAssemblyReference(moduleDef, assemblyReferences, datadogTraceAssemblyPath);

            var bootstrapType = new TypeDefUser(
                BootstrapNamespace,
                BootstrapTypeName,
                moduleDef.CorLibTypes.Object.TypeDefOrRef)
            {
                Attributes = TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed
            };

            var initializeMethod = new MethodDefUser(
                BootstrapInitializeMethodName,
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Void),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig);
            initializeMethod.Body = new CilBody();
            bootstrapType.Methods.Add(initializeMethod);
            moduleDef.Types.Add(bootstrapType);

            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.EnableAotModeMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(AotContractSchemaVersion));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(datadogTraceAssemblyVersion));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(datadogTraceAssemblyMvid));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(generatedAssemblyFullName));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(deterministicMvid.ToString("D")));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.ValidateAotRegistryContractMethod));

            var moduleContext = CreateModuleLoadContext(mappingResolutionResult.ProxyAssemblyPathsByName, mappingResolutionResult.TargetAssemblyPathsByName);

            var phaseStopwatch = StartProfilePhase();
            var proxyModulesByAssemblyName = LoadModules(mappingResolutionResult.ProxyAssemblyPathsByName, moduleContext);
            StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.LoadProxyModulesSeconds += seconds);

            phaseStopwatch = StartProfilePhase();
            var targetModulesByAssemblyName = LoadModules(mappingResolutionResult.TargetAssemblyPathsByName, moduleContext);
            StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.LoadTargetModulesSeconds += seconds);

            phaseStopwatch = StartProfilePhase();
            runtimeTypeResolutionAssemblyPathsByName = BuildRuntimeTypeResolutionAssemblyPathMap(
                mappingResolutionResult.ProxyAssemblyPathsByName,
                mappingResolutionResult.TargetAssemblyPathsByName);
            StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.BuildRuntimeTypeResolutionMapSeconds += seconds);
            phaseStopwatch = StartProfilePhase();
            var queriedTargetTypeKeys = BuildQueriedTargetTypeKeys(mappingResolutionResult.Mappings);
            var targetTypeIndex = BuildTargetTypeIndex(targetModulesByAssemblyName, queriedTargetTypeKeys);
            StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.BuildTargetTypeIndexSeconds += seconds);
            var bootstrapRegistrationMethods = new List<MethodDef>();
            var canonicalMappingsByKey = mappingResolutionResult.Mappings.ToDictionary(mapping => mapping.Key, StringComparer.Ordinal);
            IReadOnlyCollection<string> requiredAccessCheckAssemblyNames = Array.Empty<string>();
            phaseStopwatch = StartProfilePhase();
            _currentExecutionContext = new EmitterExecutionContext(runtimeTypeResolutionAssemblyPathsByName, targetTypeIndex);
            var runtimeRegistrations = BuildRuntimeRegistrations(
                moduleDef,
                mappingResolutionResult.Mappings,
                mappingResolutionResult.GenericTypeRoots,
                targetTypeIndex,
                proxyModulesByAssemblyName,
                targetModulesByAssemblyName);
            StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.BuildRuntimeRegistrationsSeconds += seconds);
            try
            {
                phaseStopwatch = StartProfilePhase();
                for (var i = 0; i < runtimeRegistrations.Count; i++)
                {
                    var registrationMethodIndex = i / BootstrapMappingsPerMethod;
                    while (bootstrapRegistrationMethods.Count <= registrationMethodIndex)
                    {
                        var registrationMethod = new MethodDefUser(
                            $"RegisterMappingsChunk_{bootstrapRegistrationMethods.Count + 1:D4}",
                            MethodSig.CreateStatic(moduleDef.CorLibTypes.Void),
                            MethodImplAttributes.IL | MethodImplAttributes.Managed,
                            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
                        registrationMethod.Body = new CilBody();
                        bootstrapType.Methods.Add(registrationMethod);
                        bootstrapRegistrationMethods.Add(registrationMethod);
                    }

                    var runtimeRegistration = runtimeRegistrations[i];
                    var mapping = runtimeRegistration.Mapping;
                    var emitMappingStopwatch = StartProfilePhase();
                    var emissionResult = runtimeRegistration.Kind == DuckTypeAotRuntimeRegistrationKind.NullableAlias &&
                                         TryEmitValueTypeNullableAliasRegistration(
                                             moduleDef,
                                             bootstrapType,
                                             bootstrapRegistrationMethods[registrationMethodIndex],
                                             importedMembers,
                                             mapping,
                                             canonicalMappingsByKey[runtimeRegistration.CanonicalMappingKey],
                                             i + 1,
                                             mappingResolutionResult.ProxyAssemblyPathsByName,
                                             mappingResolutionResult.TargetAssemblyPathsByName,
                                             emissionWarnings,
                                             out var nullableAliasEmissionResult)
                                             ? nullableAliasEmissionResult
                                             : runtimeRegistration.Kind == DuckTypeAotRuntimeRegistrationKind.AssignableAlias &&
                                               TryEmitAssignableAliasBridgeRegistration(
                                                   moduleDef,
                                                   bootstrapType,
                                                   bootstrapRegistrationMethods[registrationMethodIndex],
                                                   importedMembers,
                                                   mapping,
                                                   canonicalMappingsByKey[runtimeRegistration.CanonicalMappingKey],
                                                   i + 1,
                                                   mappingResolutionResult.ProxyAssemblyPathsByName,
                                                   mappingResolutionResult.TargetAssemblyPathsByName,
                                                   emissionWarnings,
                                                   out var assignableAliasEmissionResult)
                                                   ? assignableAliasEmissionResult
                                                   : NormalizeKnownNonCreatableParityResult(
                                                       mapping,
                                                       EmitMapping(
                                                           moduleDef,
                                                           bootstrapType,
                                                           bootstrapRegistrationMethods[registrationMethodIndex],
                                                           importedMembers,
                                                           mapping,
                                                           i + 1,
                                                           proxyModulesByAssemblyName,
                                                           targetModulesByAssemblyName,
                                                           mappingResolutionResult.ProxyAssemblyPathsByName,
                                                           mappingResolutionResult.TargetAssemblyPathsByName,
                                                           emissionWarnings));
                    StopProfilePhase(
                        emitMappingStopwatch,
                        seconds =>
                        {
                            _currentProfile!.EmitMappingSeconds += seconds;
                            _currentProfile!.EmitMappingCount++;
                        });
                    if (runtimeRegistration.IsCanonical)
                    {
                        mappingResults[mapping.Key] = emissionResult;
                    }
                }

                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.EmitLoopSeconds += seconds);
                requiredAccessCheckAssemblyNames = _currentExecutionContext.RequiredAccessCheckAssemblyNames.ToArray();
            }
            finally
            {
                foreach (var module in proxyModulesByAssemblyName.Values)
                {
                    module.Dispose();
                }

                foreach (var module in targetModulesByAssemblyName.Values)
                {
                    module.Dispose();
                }

                _currentExecutionContext = null;
                runtimeTypeResolutionAssemblyPathsByName = null;
            }

            foreach (var bootstrapRegistrationMethod in bootstrapRegistrationMethods)
            {
                bootstrapRegistrationMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(bootstrapRegistrationMethod));
            }

            initializeMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());

            // NativeAOT static-link bootstrap path: invoke bootstrap automatically via module initializer.
            var moduleInitializer = new MethodDefUser(
                ".cctor",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Void),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            moduleInitializer.Body = new CilBody();
            moduleInitializer.Body.Instructions.Add(OpCodes.Call.ToInstruction(initializeMethod));
            moduleInitializer.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            moduleDef.GlobalType.Methods.Add(moduleInitializer);

            AddIgnoresAccessChecksToAttributes(assemblyDef, moduleDef, importedMembers.IgnoresAccessChecksToAttributeCtor, mappingResolutionResult, requiredAccessCheckAssemblyNames);

            var writeOptions = new ModuleWriterOptions(moduleDef);
            if (!string.IsNullOrWhiteSpace(options.StrongNameKeyFile))
            {
                writeOptions.InitializeStrongNameSigning(moduleDef, new StrongNameKey(options.StrongNameKeyFile!));
            }

            moduleDef.Write(artifactPaths.OutputAssemblyPath, writeOptions);

            var registryInfo = new DuckTypeAotRegistryAssemblyInfo(
                generatedAssemblyName,
                bootstrapType.FullName,
                Path.GetFullPath(artifactPaths.OutputAssemblyPath),
                deterministicMvid);
            WriteProfileSummary(mappingResolutionResult, runtimeRegistrations.Count);
            _currentProfile = null;

            return new DuckTypeAotRegistryEmissionResult(registryInfo, mappingResults, runtimeRegistrations, emissionWarnings);
        }

        private static bool IsProfilingEnabled()
        {
            var value = Environment.GetEnvironmentVariable("DD_TRACE_DUCKTYPE_AOT_PROFILE");
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
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
                $"ducktype-aot emitter profile: runtimeTypeIndexBuild={profile.BuildRuntimeTypeIndexSeconds:F3}s runtimeTypeHits={profile.RuntimeTypeCacheHits} runtimeTypeMisses={profile.RuntimeTypeCacheMisses} runtimeTypeIndexHits={profile.RuntimeTypeIndexHits} runtimeTypeFallbackHits={profile.RuntimeTypeFallbackHits} runtimeTypeUnresolved={profile.RuntimeTypeUnresolved}");
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

        private static ISet<string> BuildQueriedTargetTypeKeys(IReadOnlyList<DuckTypeAotMapping> mappings)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in mappings)
            {
                if (!DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.TargetTypeName) &&
                    !string.IsNullOrWhiteSpace(mapping.TargetAssemblyName) &&
                    !string.IsNullOrWhiteSpace(mapping.TargetTypeName))
                {
                    _ = keys.Add(BuildAssemblyTypeCacheKey(mapping.TargetAssemblyName, mapping.TargetTypeName));
                }
            }

            return keys;
        }

        /// <summary>
        /// Normalizes known non-creatable parity scenarios to compatible matrix status.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="emissionResult">The emission result value.</param>
        /// <returns>The normalized emission result.</returns>
        private static DuckTypeAotMappingEmissionResult NormalizeKnownNonCreatableParityResult(
            DuckTypeAotMapping mapping,
            DuckTypeAotMappingEmissionResult emissionResult)
        {
            if (string.Equals(emissionResult.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal) ||
                !IsKnownNonCreatableParityScenario(mapping, emissionResult.Status))
            {
                return emissionResult;
            }

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                emissionResult.GeneratedProxyAssemblyName ?? mapping.ProxyAssemblyName,
                emissionResult.GeneratedProxyTypeName ?? mapping.ProxyTypeName);
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
        /// Resolves datadog trace assembly path for contract metadata.
        /// </summary>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ResolveDatadogTraceAssemblyPath(IReadOnlyDictionary<string, string> targetAssemblyPathsByName)
        {
            if (targetAssemblyPathsByName.TryGetValue(DatadogTraceAssemblyName, out var datadogTraceAssemblyPath) &&
                File.Exists(datadogTraceAssemblyPath))
            {
                return datadogTraceAssemblyPath;
            }

            return typeof(Datadog.Trace.Tracer).Assembly.Location;
        }

        /// <summary>
        /// Resolves the core library assembly reference for the generated registry assembly.
        /// </summary>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="datadogTraceAssemblyPath">The Datadog.Trace assembly path value.</param>
        /// <returns>The resulting assembly reference.</returns>
        private static AssemblyRef ResolveGeneratedCorLibAssemblyRef(
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            string datadogTraceAssemblyPath)
        {
            foreach (var assemblyPath in EnumerateGeneratedCorLibCandidateAssemblyPaths(mappingResolutionResult, datadogTraceAssemblyPath))
            {
                if (string.IsNullOrWhiteSpace(assemblyPath) ||
                    !File.Exists(assemblyPath))
                {
                    continue;
                }

                using var module = ModuleDefMD.Load(assemblyPath);
                var corLibAssemblyRef = module.CorLibTypes.AssemblyRef;
                if (corLibAssemblyRef is not null)
                {
                    return new AssemblyRefUser(corLibAssemblyRef);
                }
            }

            throw new InvalidOperationException("Unable to resolve a core library assembly reference for the generated duck type AOT registry assembly.");
        }

        /// <summary>
        /// Enumerates assemblies that can define the generated registry load context.
        /// </summary>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="datadogTraceAssemblyPath">The Datadog.Trace assembly path value.</param>
        /// <returns>The assembly paths in lookup order.</returns>
        private static IEnumerable<string> EnumerateGeneratedCorLibCandidateAssemblyPaths(
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            string datadogTraceAssemblyPath)
        {
            var yieldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var proxyAssemblyPath in mappingResolutionResult.ProxyAssemblyPathsByName.Values.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (yieldedPaths.Add(proxyAssemblyPath))
                {
                    yield return proxyAssemblyPath;
                }
            }

            if (yieldedPaths.Add(datadogTraceAssemblyPath))
            {
                yield return datadogTraceAssemblyPath;
            }

            // Target assemblies may be higher-TFM third-party libraries; they must not force the registry corlib.
            foreach (var targetAssemblyPath in mappingResolutionResult.TargetAssemblyPathsByName.Values.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (yieldedPaths.Add(targetAssemblyPath))
                {
                    yield return targetAssemblyPath;
                }
            }
        }

        /// <summary>
        /// Builds a combined assembly-path index for runtime type probing across proxy and target sets.
        /// </summary>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths value.</param>
        /// <returns>The resulting path map keyed by normalized assembly name.</returns>
        private static IReadOnlyDictionary<string, string> BuildRuntimeTypeResolutionAssemblyPathMap(
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName)
        {
            var combinedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in proxyAssemblyPathsByName)
            {
                var normalizedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(entry.Key);
                if (string.IsNullOrWhiteSpace(normalizedAssemblyName) ||
                    string.IsNullOrWhiteSpace(entry.Value) ||
                    !File.Exists(entry.Value) ||
                    combinedPaths.ContainsKey(normalizedAssemblyName))
                {
                    continue;
                }

                combinedPaths[normalizedAssemblyName] = entry.Value;
            }

            foreach (var entry in targetAssemblyPathsByName)
            {
                var normalizedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(entry.Key);
                if (string.IsNullOrWhiteSpace(normalizedAssemblyName) ||
                    string.IsNullOrWhiteSpace(entry.Value) ||
                    !File.Exists(entry.Value) ||
                    combinedPaths.ContainsKey(normalizedAssemblyName))
                {
                    continue;
                }

                combinedPaths[normalizedAssemblyName] = entry.Value;
            }

            return combinedPaths;
        }

        /// <summary>
        /// Builds the target-type index used to expand assignable runtime registrations without rescanning all module types per mapping.
        /// </summary>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name value.</param>
        /// <returns>The resulting target-type index.</returns>
        private static TargetTypeIndex BuildTargetTypeIndex(IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName, ISet<string> queriedTargetTypeKeys)
        {
            var typeByAssemblyAndName = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
            var assignableForwardTypesByAncestor = new Dictionary<string, List<TargetTypeIndexEntry>>(StringComparer.Ordinal);
            var assignableReverseTypesByAncestor = new Dictionary<string, List<TargetTypeIndexEntry>>(StringComparer.Ordinal);
            var assignableTypeKeysByType = new Dictionary<TypeDef, IReadOnlyList<string>>(ReferenceIdentityComparer<TypeDef>.Instance);
            var assignableTypeKeysInProgress = new HashSet<TypeDef>(ReferenceIdentityComparer<TypeDef>.Instance);
            var aliasCandidateTargets = new List<TargetTypeIndexEntry>();

            foreach (var entry in targetModulesByAssemblyName)
            {
                var assemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(entry.Key);
                var assemblyTypeCacheKeyPrefix = BuildNormalizedAssemblyTypeCacheKeyPrefix(assemblyName);
                foreach (var candidateType in entry.Value.GetTypes())
                {
                    if (string.IsNullOrWhiteSpace(candidateType.FullName))
                    {
                        continue;
                    }

                    typeByAssemblyAndName[string.Concat(assemblyTypeCacheKeyPrefix, candidateType.FullName)] = candidateType;
                    if (!string.IsNullOrWhiteSpace(candidateType.ReflectionFullName) &&
                        !string.Equals(candidateType.ReflectionFullName, candidateType.FullName, StringComparison.Ordinal))
                    {
                        typeByAssemblyAndName[string.Concat(assemblyTypeCacheKeyPrefix, candidateType.ReflectionFullName)] = candidateType;
                    }

                    if (!IsAliasCandidateType(candidateType))
                    {
                        continue;
                    }

                    var candidateEntry = new TargetTypeIndexEntry(assemblyName, candidateType);
                    aliasCandidateTargets.Add(candidateEntry);
                    foreach (var ancestorTypeKey in GetAssignableTypeKeys(candidateType, queriedTargetTypeKeys, assignableTypeKeysByType, assignableTypeKeysInProgress))
                    {
                        AddTargetTypeIndexEntry(assignableForwardTypesByAncestor, ancestorTypeKey, candidateEntry);
                        if (!candidateType.IsValueType)
                        {
                            AddTargetTypeIndexEntry(assignableReverseTypesByAncestor, ancestorTypeKey, candidateEntry);
                        }
                    }
                }
            }

            return new TargetTypeIndex(
                typeByAssemblyAndName,
                aliasCandidateTargets,
                ToSortedTargetTypeIndex(assignableForwardTypesByAncestor),
                ToSortedTargetTypeIndex(assignableReverseTypesByAncestor));
        }

        /// <summary>
        /// Builds the full runtime registration set emitted into the generated registry.
        /// </summary>
        /// <param name="canonicalMappings">The canonical mappings value.</param>
        /// <param name="genericTypeRoots">The closed generic type roots value.</param>
        /// <param name="targetTypeIndex">The target type index value.</param>
        /// <returns>The resulting runtime registration set.</returns>
        private static IReadOnlyList<DuckTypeAotRuntimeRegistration> BuildRuntimeRegistrations(
            ModuleDef moduleDef,
            IReadOnlyList<DuckTypeAotMapping> canonicalMappings,
            IReadOnlyList<DuckTypeAotTypeReference> genericTypeRoots,
            TargetTypeIndex targetTypeIndex,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName)
        {
            var registrations = new List<DuckTypeAotRuntimeRegistration>();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var canonicalKeys = canonicalMappings.Select(mapping => mapping.Key).ToHashSet(StringComparer.Ordinal);
            var aliasPlansByCanonicalTargetKey = new Dictionary<string, CanonicalTargetAliasPlan>(StringComparer.Ordinal);

            foreach (var mapping in canonicalMappings.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (seenKeys.Add(mapping.Key))
                {
                    registrations.Add(new DuckTypeAotRuntimeRegistration(mapping, mapping.Key, DuckTypeAotRuntimeRegistrationKind.Canonical));
                }

                var canonicalTargetKey = BuildCanonicalTargetCacheKey(mapping);
                if (!aliasPlansByCanonicalTargetKey.TryGetValue(canonicalTargetKey, out var aliasPlan))
                {
                    aliasPlan = BuildCanonicalTargetAliasPlan(mapping, targetTypeIndex, genericTypeRoots);
                    aliasPlansByCanonicalTargetKey[canonicalTargetKey] = aliasPlan;
                }

                if (aliasPlan.NullableAlias is not null)
                {
                    var nullableAlias = aliasPlan.NullableAlias;
                    var nullableAliasMapping = new DuckTypeAotMapping(
                        mapping.ProxyTypeName,
                        mapping.ProxyAssemblyName,
                        nullableAlias.TypeName,
                        nullableAlias.AssemblyName,
                        mapping.Mode,
                        mapping.Source);
                    if (!canonicalKeys.Contains(nullableAliasMapping.Key) &&
                        seenKeys.Add(nullableAliasMapping.Key))
                    {
                        registrations.Add(new DuckTypeAotRuntimeRegistration(nullableAliasMapping, mapping.Key, DuckTypeAotRuntimeRegistrationKind.NullableAlias));
                    }
                }

                foreach (var aliasTarget in aliasPlan.AssignableTargets)
                {
                    var aliasMapping = new DuckTypeAotMapping(
                        mapping.ProxyTypeName,
                        mapping.ProxyAssemblyName,
                        aliasTarget.TypeName,
                        aliasTarget.AssemblyName,
                        mapping.Mode,
                        mapping.Source);
                    if (canonicalKeys.Contains(aliasMapping.Key) ||
                        seenKeys.Contains(aliasMapping.Key))
                    {
                        continue;
                    }

                    if (mapping.Mode == DuckTypeAotMappingMode.Forward &&
                        !IsAssignableAliasSemanticallySafe(
                            moduleDef,
                            mapping,
                            aliasMapping,
                            proxyModulesByAssemblyName,
                            targetModulesByAssemblyName))
                    {
                        continue;
                    }

                    _ = seenKeys.Add(aliasMapping.Key);
                    registrations.Add(new DuckTypeAotRuntimeRegistration(aliasMapping, mapping.Key, DuckTypeAotRuntimeRegistrationKind.AssignableAlias));
                }
            }

            return registrations;
        }

        /// <summary>
        /// Determines whether an assignable target can safely reuse the canonical target proxy binding plan.
        /// </summary>
        /// <param name="moduleDef">The generated registry module.</param>
        /// <param name="canonicalMapping">The canonical mapping value.</param>
        /// <param name="aliasMapping">The alias mapping value.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name value.</param>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name value.</param>
        /// <returns>true when the alias resolves to the same effective target members; otherwise, false.</returns>
        private static bool IsAssignableAliasSemanticallySafe(
            ModuleDef moduleDef,
            DuckTypeAotMapping canonicalMapping,
            DuckTypeAotMapping aliasMapping,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName)
        {
            return TryCollectAliasSemanticBindingPlan(
                       moduleDef,
                       canonicalMapping,
                       proxyModulesByAssemblyName,
                       targetModulesByAssemblyName,
                       inheritedClosedGenericTargetTypeArguments: null,
                       out var canonicalPlan) &&
                   TryCollectAliasSemanticBindingPlan(
                       moduleDef,
                       aliasMapping,
                       proxyModulesByAssemblyName,
                       targetModulesByAssemblyName,
                       canonicalPlan.ClosedGenericTargetTypeArguments,
                       out var aliasPlan) &&
                   AreAliasSemanticBindingPlansEquivalent(canonicalPlan, aliasPlan);
        }

        /// <summary>
        /// Attempts to collect the regular AOT binding plan used for alias safety checks.
        /// </summary>
        /// <param name="moduleDef">The generated registry module.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name value.</param>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name value.</param>
        /// <param name="inheritedClosedGenericTargetTypeArguments">The inherited closed generic target type arguments.</param>
        /// <param name="plan">The collected binding plan.</param>
        /// <returns>true when the binding plan was collected; otherwise, false.</returns>
        private static bool TryCollectAliasSemanticBindingPlan(
            ModuleDef moduleDef,
            DuckTypeAotMapping mapping,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName,
            IReadOnlyList<TypeSig>? inheritedClosedGenericTargetTypeArguments,
            out AliasSemanticBindingPlan plan)
        {
            plan = default;
            if (!TryResolveAliasSemanticBindingTypes(
                    moduleDef,
                    mapping,
                    proxyModulesByAssemblyName,
                    targetModulesByAssemblyName,
                    inheritedClosedGenericTargetTypeArguments,
                    out var proxyType,
                    out var targetType,
                    out var closedGenericProxyTypeArguments,
                    out var closedGenericTargetTypeArguments))
            {
                return false;
            }

            if (proxyType.IsValueType)
            {
                if (!TryCollectStructCopyBindings(
                        mapping,
                        proxyType,
                        targetType,
                        closedGenericTargetTypeArguments,
                        out var structCopyBindings,
                        out _))
                {
                    return false;
                }

                plan = AliasSemanticBindingPlan.ForStructCopy(targetType, structCopyBindings, closedGenericTargetTypeArguments);
                return true;
            }

            if (!proxyType.IsInterface && !proxyType.IsClass)
            {
                return false;
            }

            if (!TryCollectForwardBindings(
                    mapping,
                    proxyType,
                    targetType,
                    closedGenericProxyTypeArguments,
                    closedGenericTargetTypeArguments,
                    proxyType.IsInterface,
                    out var forwardBindings,
                    out _))
            {
                return false;
            }

            plan = AliasSemanticBindingPlan.ForForward(targetType, forwardBindings, closedGenericTargetTypeArguments);
            return true;
        }

        /// <summary>
        /// Attempts to resolve proxy and target definitions plus closed generic arguments for alias checks.
        /// </summary>
        /// <param name="moduleDef">The generated registry module.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name value.</param>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name value.</param>
        /// <param name="inheritedClosedGenericTargetTypeArguments">The inherited closed generic target type arguments.</param>
        /// <param name="proxyType">The resolved proxy type definition.</param>
        /// <param name="targetType">The resolved target type definition.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed proxy generic type arguments.</param>
        /// <param name="closedGenericTargetTypeArguments">The closed target generic type arguments.</param>
        /// <returns>true when both type definitions were resolved; otherwise, false.</returns>
        private static bool TryResolveAliasSemanticBindingTypes(
            ModuleDef moduleDef,
            DuckTypeAotMapping mapping,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName,
            IReadOnlyList<TypeSig>? inheritedClosedGenericTargetTypeArguments,
            out TypeDef proxyType,
            out TypeDef targetType,
            out IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            out IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            proxyType = null!;
            targetType = null!;
            closedGenericProxyTypeArguments = null;
            closedGenericTargetTypeArguments = null;

            if (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule) ||
                !targetModulesByAssemblyName.TryGetValue(mapping.TargetAssemblyName, out var targetModule))
            {
                return false;
            }

            var proxyTypeName = mapping.ProxyTypeName;
            if (DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.ProxyTypeName))
            {
                if (runtimeTypeResolutionAssemblyPathsByName is null ||
                    !runtimeTypeResolutionAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath) ||
                    !TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) ||
                    proxyRuntimeType is null)
                {
                    return false;
                }

                proxyTypeName = proxyRuntimeType.IsGenericType
                                    ? proxyRuntimeType.GetGenericTypeDefinition().FullName!
                                    : mapping.ProxyTypeName;
                closedGenericProxyTypeArguments = proxyRuntimeType.IsGenericType
                                                      ? proxyRuntimeType.GetGenericArguments()
                                                                        .Select(runtimeType => ImportRuntimeTypeSig(moduleDef, runtimeType))
                                                                        .ToArray()
                                                      : null;
            }

            var targetTypeName = mapping.TargetTypeName;
            if (DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.TargetTypeName))
            {
                if (runtimeTypeResolutionAssemblyPathsByName is null ||
                    !runtimeTypeResolutionAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) ||
                    !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) ||
                    targetRuntimeType is null)
                {
                    return false;
                }

                var targetRuntimeTypeDefinition = targetRuntimeType.IsGenericType
                                                      ? targetRuntimeType.GetGenericTypeDefinition()
                                                      : targetRuntimeType;
                targetTypeName = targetRuntimeTypeDefinition.FullName!;
                closedGenericTargetTypeArguments = targetRuntimeType.IsGenericType
                                                       ? targetRuntimeType.GetGenericArguments()
                                                                          .Select(runtimeType => ImportRuntimeTypeSig(moduleDef, runtimeType))
                                                                          .ToArray()
                                                       : null;
            }
            else if (inheritedClosedGenericTargetTypeArguments is { Count: > 0 })
            {
                closedGenericTargetTypeArguments = inheritedClosedGenericTargetTypeArguments;
            }

            return TryResolveType(proxyModule, proxyTypeName, out proxyType) &&
                   TryResolveType(targetModule, targetTypeName, out targetType);
        }

        /// <summary>
        /// Determines whether two alias semantic binding plans are equivalent.
        /// </summary>
        /// <param name="canonicalPlan">The canonical binding plan.</param>
        /// <param name="aliasPlan">The alias binding plan.</param>
        /// <returns>true when both plans bind the same effective target members; otherwise, false.</returns>
        private static bool AreAliasSemanticBindingPlansEquivalent(AliasSemanticBindingPlan canonicalPlan, AliasSemanticBindingPlan aliasPlan)
        {
            if (canonicalPlan.Kind != aliasPlan.Kind)
            {
                return false;
            }

            return canonicalPlan.Kind switch
            {
                AliasSemanticBindingPlanKind.Forward => AreForwardBindingSetsEquivalent(
                    canonicalPlan.TargetType,
                    canonicalPlan.ForwardBindings!,
                    canonicalPlan.ClosedGenericTargetTypeArguments,
                    aliasPlan.TargetType,
                    aliasPlan.ForwardBindings!,
                    aliasPlan.ClosedGenericTargetTypeArguments),
                AliasSemanticBindingPlanKind.StructCopy => AreStructCopyBindingSetsEquivalent(
                    canonicalPlan.TargetType,
                    canonicalPlan.StructCopyBindings!,
                    canonicalPlan.ClosedGenericTargetTypeArguments,
                    aliasPlan.TargetType,
                    aliasPlan.StructCopyBindings!,
                    aliasPlan.ClosedGenericTargetTypeArguments),
                _ => false
            };
        }

        /// <summary>
        /// Determines whether two forward binding sets target the same members with the same conversion plan.
        /// </summary>
        /// <param name="canonicalTargetType">The canonical target type.</param>
        /// <param name="canonicalBindings">The canonical binding set.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="aliasTargetType">The alias target type.</param>
        /// <param name="aliasBindings">The alias binding set.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <returns>true when the binding sets are equivalent; otherwise, false.</returns>
        private static bool AreForwardBindingSetsEquivalent(
            TypeDef canonicalTargetType,
            IReadOnlyList<ForwardBinding> canonicalBindings,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            TypeDef aliasTargetType,
            IReadOnlyList<ForwardBinding> aliasBindings,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments)
        {
            if (canonicalBindings.Count != aliasBindings.Count)
            {
                return false;
            }

            var canonicalByProxyMember = new Dictionary<string, ForwardBinding>(StringComparer.Ordinal);
            foreach (var canonicalBinding in canonicalBindings)
            {
                if (!canonicalByProxyMember.TryAdd(BuildMethodIdentityKey(canonicalBinding.ProxyMethod), canonicalBinding))
                {
                    return false;
                }
            }

            foreach (var aliasBinding in aliasBindings)
            {
                if (!canonicalByProxyMember.TryGetValue(BuildMethodIdentityKey(aliasBinding.ProxyMethod), out var canonicalBinding) ||
                    canonicalBinding.Kind != aliasBinding.Kind)
                {
                    return false;
                }

                if (canonicalBinding.TargetMethod is not null || aliasBinding.TargetMethod is not null)
                {
                    if (canonicalBinding.TargetMethod is null ||
                        aliasBinding.TargetMethod is null ||
                        !AreMethodTargetsEquivalent(
                            canonicalBinding.TargetMethod,
                            canonicalTargetType,
                            canonicalClosedGenericTargetTypeArguments,
                            aliasBinding.TargetMethod,
                            aliasTargetType,
                            aliasClosedGenericTargetTypeArguments))
                    {
                        return false;
                    }
                }

                if (canonicalBinding.TargetField is not null || aliasBinding.TargetField is not null)
                {
                    if (canonicalBinding.TargetField is null ||
                        aliasBinding.TargetField is null ||
                        !string.Equals(BuildFieldIdentityKey(canonicalBinding.TargetField), BuildFieldIdentityKey(aliasBinding.TargetField), StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                if (!AreForwardMethodBindingInfosEquivalent(canonicalBinding.MethodBinding, aliasBinding.MethodBinding) ||
                    !AreTrailingOptionalTargetDefaultsEquivalent(
                        canonicalBinding.TargetMethod,
                        canonicalBinding.MethodBinding,
                        canonicalClosedGenericTargetTypeArguments,
                        aliasBinding.TargetMethod,
                        aliasBinding.MethodBinding,
                        aliasClosedGenericTargetTypeArguments) ||
                    !AreForwardFieldBindingInfosEquivalent(canonicalBinding.FieldBinding, aliasBinding.FieldBinding))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether two DuckCopy binding sets target the same members with the same conversion plan.
        /// </summary>
        /// <param name="canonicalTargetType">The canonical target type.</param>
        /// <param name="canonicalBindings">The canonical binding set.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="aliasTargetType">The alias target type.</param>
        /// <param name="aliasBindings">The alias binding set.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <returns>true when the binding sets are equivalent; otherwise, false.</returns>
        private static bool AreStructCopyBindingSetsEquivalent(
            TypeDef canonicalTargetType,
            IReadOnlyList<StructCopyFieldBinding> canonicalBindings,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            TypeDef aliasTargetType,
            IReadOnlyList<StructCopyFieldBinding> aliasBindings,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments)
        {
            if (canonicalBindings.Count != aliasBindings.Count)
            {
                return false;
            }

            var canonicalByProxyField = new Dictionary<string, StructCopyFieldBinding>(StringComparer.Ordinal);
            foreach (var canonicalBinding in canonicalBindings)
            {
                if (!canonicalByProxyField.TryAdd(BuildFieldIdentityKey(canonicalBinding.ProxyField), canonicalBinding))
                {
                    return false;
                }
            }

            foreach (var aliasBinding in aliasBindings)
            {
                if (!canonicalByProxyField.TryGetValue(BuildFieldIdentityKey(aliasBinding.ProxyField), out var canonicalBinding) ||
                    canonicalBinding.SourceKind != aliasBinding.SourceKind ||
                    !AreMethodReturnConversionsEquivalent(canonicalBinding.ReturnConversion, aliasBinding.ReturnConversion))
                {
                    return false;
                }

                if (canonicalBinding.SourceProperty is not null || aliasBinding.SourceProperty is not null)
                {
                    if (canonicalBinding.SourceProperty is null ||
                        aliasBinding.SourceProperty is null ||
                        !ArePropertyTargetsEquivalent(
                            canonicalBinding.SourceProperty,
                            canonicalTargetType,
                            canonicalClosedGenericTargetTypeArguments,
                            aliasBinding.SourceProperty,
                            aliasTargetType,
                            aliasClosedGenericTargetTypeArguments))
                    {
                        return false;
                    }
                }

                if (canonicalBinding.SourceField is not null || aliasBinding.SourceField is not null)
                {
                    if (canonicalBinding.SourceField is null ||
                        aliasBinding.SourceField is null ||
                        !string.Equals(BuildFieldIdentityKey(canonicalBinding.SourceField), BuildFieldIdentityKey(aliasBinding.SourceField), StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether two target methods represent the same effective dispatch target.
        /// </summary>
        /// <param name="canonicalMethod">The canonical target method.</param>
        /// <param name="canonicalTargetType">The canonical target type.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="aliasMethod">The alias target method.</param>
        /// <param name="aliasTargetType">The alias target type.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <returns>true when the alias method is the same method, a virtual override, or a concrete implementation of the canonical interface method; otherwise, false.</returns>
        private static bool AreMethodTargetsEquivalent(
            MethodDef canonicalMethod,
            TypeDef canonicalTargetType,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            MethodDef aliasMethod,
            TypeDef aliasTargetType,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments)
        {
            if (string.Equals(BuildMethodIdentityKey(canonicalMethod), BuildMethodIdentityKey(aliasMethod), StringComparison.Ordinal))
            {
                return true;
            }

            if (IsInterfaceImplementationMethodTargetEquivalent(
                    canonicalMethod,
                    canonicalTargetType,
                    canonicalClosedGenericTargetTypeArguments,
                    aliasMethod,
                    aliasTargetType,
                    aliasClosedGenericTargetTypeArguments))
            {
                return true;
            }

            if (!canonicalMethod.IsVirtual ||
                !aliasMethod.IsVirtual ||
                aliasMethod.IsNewSlot ||
                canonicalMethod.DeclaringType is null ||
                aliasMethod.DeclaringType is null ||
                !IsAssignableFrom(canonicalMethod.DeclaringType, aliasMethod.DeclaringType))
            {
                return false;
            }

            return string.Equals(canonicalMethod.Name, aliasMethod.Name, StringComparison.Ordinal) &&
                   AreEffectiveMethodSignaturesEquivalent(
                       canonicalMethod.MethodSig,
                       canonicalClosedGenericTargetTypeArguments,
                       aliasMethod.MethodSig,
                       aliasClosedGenericTargetTypeArguments);
        }

        /// <summary>
        /// Determines whether an alias target method is a concrete implementation of the canonical interface method.
        /// </summary>
        /// <param name="canonicalMethod">The canonical target method.</param>
        /// <param name="canonicalTargetType">The canonical target type.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="aliasMethod">The alias target method.</param>
        /// <param name="aliasTargetType">The alias target type.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <returns>true when the alias method implements the canonical interface method shape; otherwise, false.</returns>
        private static bool IsInterfaceImplementationMethodTargetEquivalent(
            MethodDef canonicalMethod,
            TypeDef canonicalTargetType,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            MethodDef aliasMethod,
            TypeDef aliasTargetType,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments)
        {
            if (!canonicalTargetType.IsInterface ||
                !IsAssignableFrom(canonicalTargetType, aliasTargetType))
            {
                return false;
            }

            var canonicalMethodName = GetEffectiveInterfaceMethodName(canonicalMethod.Name);
            var aliasMethodName = GetEffectiveInterfaceMethodName(aliasMethod.Name);
            if (!string.Equals(canonicalMethodName, aliasMethodName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!AreEffectiveMethodSignaturesEquivalent(
                    canonicalMethod.MethodSig,
                    canonicalClosedGenericTargetTypeArguments,
                    aliasMethod.MethodSig,
                    aliasClosedGenericTargetTypeArguments))
            {
                return false;
            }

            return TryResolveInterfaceImplementationMethod(
                       canonicalMethod,
                       canonicalTargetType,
                       canonicalClosedGenericTargetTypeArguments,
                       aliasClosedGenericTargetTypeArguments,
                       aliasTargetType,
                       out var implementationMethod) &&
                   string.Equals(BuildMethodIdentityKey(implementationMethod), BuildMethodIdentityKey(aliasMethod), StringComparison.Ordinal);
        }

        /// <summary>
        /// Resolves the concrete method that implements a canonical interface method on an alias target type.
        /// </summary>
        /// <param name="canonicalMethod">The canonical interface method.</param>
        /// <param name="canonicalTargetType">The canonical interface type.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <param name="aliasTargetType">The alias target type.</param>
        /// <param name="implementationMethod">The resolved implementation method.</param>
        /// <returns>true when the implementing method can be resolved; otherwise, false.</returns>
        private static bool TryResolveInterfaceImplementationMethod(
            MethodDef canonicalMethod,
            TypeDef canonicalTargetType,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments,
            TypeDef aliasTargetType,
            out MethodDef implementationMethod)
        {
            implementationMethod = null!;
            if (!TryResolveInterfaceImplementationType(canonicalTargetType, aliasTargetType, out var implementationType))
            {
                return false;
            }

            foreach (var method in implementationType.Methods)
            {
                if (TryGetExplicitInterfaceImplementation(method, canonicalMethod, canonicalTargetType, canonicalClosedGenericTargetTypeArguments, out implementationMethod))
                {
                    return true;
                }
            }

            if (TryResolveImplicitInterfaceImplementationMethod(
                    canonicalMethod,
                    canonicalClosedGenericTargetTypeArguments,
                    aliasClosedGenericTargetTypeArguments,
                    implementationType,
                    out implementationMethod))
            {
                implementationMethod = ResolveMostDerivedVirtualOverride(implementationMethod, aliasTargetType, aliasClosedGenericTargetTypeArguments);
                return true;
            }

            var currentType = implementationType.BaseType?.ResolveTypeDef();
            while (currentType is not null)
            {
                foreach (var method in currentType.Methods)
                {
                    if (TryGetExplicitInterfaceImplementation(method, canonicalMethod, canonicalTargetType, canonicalClosedGenericTargetTypeArguments, out implementationMethod))
                    {
                        return true;
                    }
                }

                currentType = currentType.BaseType?.ResolveTypeDef();
            }

            return false;
        }

        /// <summary>
        /// Resolves the most-derived override for an implementation method on the alias target type.
        /// </summary>
        /// <param name="implementationMethod">The base implementation method.</param>
        /// <param name="aliasTargetType">The alias target type.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <returns>The most-derived override when one exists; otherwise the original implementation method.</returns>
        private static MethodDef ResolveMostDerivedVirtualOverride(
            MethodDef implementationMethod,
            TypeDef aliasTargetType,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments)
        {
            if (!implementationMethod.IsVirtual)
            {
                return implementationMethod;
            }

            var currentType = aliasTargetType;
            while (currentType is not null && !ReferenceEquals(currentType, implementationMethod.DeclaringType))
            {
                foreach (var candidate in currentType.Methods)
                {
                    if (candidate.IsStatic ||
                        !candidate.IsVirtual ||
                        candidate.IsNewSlot ||
                        !string.Equals(candidate.Name, implementationMethod.Name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (AreEffectiveMethodSignaturesEquivalent(
                            candidate.MethodSig,
                            aliasClosedGenericTargetTypeArguments,
                            implementationMethod.MethodSig,
                            aliasClosedGenericTargetTypeArguments))
                    {
                        return candidate;
                    }
                }

                currentType = currentType.BaseType?.ResolveTypeDef();
            }

            return implementationMethod;
        }

        /// <summary>
        /// Resolves the type in the alias hierarchy that introduces the canonical interface contract.
        /// </summary>
        /// <param name="canonicalTargetType">The canonical interface type.</param>
        /// <param name="aliasTargetType">The alias target type.</param>
        /// <param name="implementationType">The implementation type.</param>
        /// <returns>true when the implementation type can be resolved; otherwise, false.</returns>
        private static bool TryResolveInterfaceImplementationType(TypeDef canonicalTargetType, TypeDef aliasTargetType, out TypeDef implementationType)
        {
            var currentType = aliasTargetType;
            while (currentType is not null)
            {
                foreach (var interfaceImpl in currentType.Interfaces)
                {
                    var interfaceType = interfaceImpl.Interface.ResolveTypeDef();
                    if (interfaceType is not null && IsAssignableFrom(canonicalTargetType, interfaceType))
                    {
                        implementationType = currentType;
                        return true;
                    }
                }

                currentType = currentType.BaseType?.ResolveTypeDef();
            }

            implementationType = null!;
            return false;
        }

        /// <summary>
        /// Resolves an explicit interface implementation method when it targets the canonical interface method.
        /// </summary>
        /// <param name="method">The method to inspect.</param>
        /// <param name="canonicalMethod">The canonical interface method.</param>
        /// <param name="canonicalTargetType">The canonical interface type.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="implementationMethod">The implementation method.</param>
        /// <returns>true when the method explicitly implements the canonical interface method; otherwise, false.</returns>
        private static bool TryGetExplicitInterfaceImplementation(
            MethodDef method,
            MethodDef canonicalMethod,
            TypeDef canonicalTargetType,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            out MethodDef implementationMethod)
        {
            foreach (var methodOverride in method.Overrides)
            {
                if (DoesMethodOverrideTargetInterfaceMethod(
                        methodOverride.MethodDeclaration,
                        canonicalMethod,
                        canonicalTargetType,
                        canonicalClosedGenericTargetTypeArguments))
                {
                    implementationMethod = method;
                    return true;
                }
            }

            implementationMethod = null!;
            return false;
        }

        /// <summary>
        /// Determines whether a method override declaration targets the canonical interface method.
        /// </summary>
        /// <param name="methodDeclaration">The override method declaration.</param>
        /// <param name="canonicalMethod">The canonical interface method.</param>
        /// <param name="canonicalTargetType">The canonical interface type.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <returns>true when the override declaration targets the canonical method; otherwise, false.</returns>
        private static bool DoesMethodOverrideTargetInterfaceMethod(
            IMethodDefOrRef methodDeclaration,
            MethodDef canonicalMethod,
            TypeDef canonicalTargetType,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments)
        {
            var declarationMethod = methodDeclaration.ResolveMethodDef();
            if (declarationMethod?.DeclaringType is null ||
                !IsAssignableFrom(canonicalTargetType, declarationMethod.DeclaringType))
            {
                return false;
            }

            return string.Equals(GetEffectiveInterfaceMethodName(declarationMethod.Name), GetEffectiveInterfaceMethodName(canonicalMethod.Name), StringComparison.Ordinal) &&
                   AreEffectiveMethodSignaturesEquivalent(
                       declarationMethod.MethodSig,
                       canonicalClosedGenericTargetTypeArguments,
                       canonicalMethod.MethodSig,
                       canonicalClosedGenericTargetTypeArguments);
        }

        /// <summary>
        /// Resolves the implicit public method that implements the canonical interface method.
        /// </summary>
        /// <param name="canonicalMethod">The canonical interface method.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <param name="implementationType">The type that introduces the interface contract.</param>
        /// <param name="implementationMethod">The implementation method.</param>
        /// <returns>true when an implicit implementation can be resolved; otherwise, false.</returns>
        private static bool TryResolveImplicitInterfaceImplementationMethod(
            MethodDef canonicalMethod,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments,
            TypeDef implementationType,
            out MethodDef implementationMethod)
        {
            var currentType = implementationType;
            while (currentType is not null)
            {
                foreach (var method in currentType.Methods)
                {
                    if (IsImplicitInterfaceImplementationMethod(method, canonicalMethod, canonicalClosedGenericTargetTypeArguments, aliasClosedGenericTargetTypeArguments))
                    {
                        implementationMethod = method;
                        return true;
                    }
                }

                currentType = currentType.BaseType?.ResolveTypeDef();
            }

            implementationMethod = null!;
            return false;
        }

        /// <summary>
        /// Determines whether a public method can implicitly implement the canonical interface method.
        /// </summary>
        /// <param name="method">The candidate implementation method.</param>
        /// <param name="canonicalMethod">The canonical interface method.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <returns>true when the method can implicitly implement the interface method; otherwise, false.</returns>
        private static bool IsImplicitInterfaceImplementationMethod(
            MethodDef method,
            MethodDef canonicalMethod,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments)
        {
            return !method.IsStatic &&
                   !method.IsConstructor &&
                   method.IsPublic &&
                   string.Equals(method.Name, canonicalMethod.Name, StringComparison.Ordinal) &&
                   AreEffectiveMethodSignaturesEquivalent(
                       method.MethodSig,
                       aliasClosedGenericTargetTypeArguments,
                       canonicalMethod.MethodSig,
                       canonicalClosedGenericTargetTypeArguments);
        }

        /// <summary>
        /// Gets the method name used to compare interface declarations with concrete or explicit implementations.
        /// </summary>
        /// <param name="methodName">The method name value.</param>
        /// <returns>The effective method name.</returns>
        private static string GetEffectiveInterfaceMethodName(string methodName)
        {
            var separatorIndex = methodName.LastIndexOf('.');
            return separatorIndex < 0 || separatorIndex == methodName.Length - 1
                       ? methodName
                       : methodName.Substring(separatorIndex + 1);
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
        /// Determines whether two target properties represent the same effective dispatch target.
        /// </summary>
        /// <param name="canonicalProperty">The canonical target property.</param>
        /// <param name="canonicalTargetType">The canonical target type.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="aliasProperty">The alias target property.</param>
        /// <param name="aliasTargetType">The alias target type.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <returns>true when the alias property is the same property or an accessor override; otherwise, false.</returns>
        private static bool ArePropertyTargetsEquivalent(
            PropertyDef canonicalProperty,
            TypeDef canonicalTargetType,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            PropertyDef aliasProperty,
            TypeDef aliasTargetType,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments)
        {
            if (string.Equals(BuildPropertyIdentityKey(canonicalProperty), BuildPropertyIdentityKey(aliasProperty), StringComparison.Ordinal))
            {
                return true;
            }

            if (canonicalProperty.GetMethod is not null &&
                aliasProperty.GetMethod is not null &&
                AreMethodTargetsEquivalent(
                    canonicalProperty.GetMethod,
                    canonicalTargetType,
                    canonicalClosedGenericTargetTypeArguments,
                    aliasProperty.GetMethod,
                    aliasTargetType,
                    aliasClosedGenericTargetTypeArguments))
            {
                return true;
            }

            return canonicalProperty.SetMethod is not null &&
                   aliasProperty.SetMethod is not null &&
                   AreMethodTargetsEquivalent(
                       canonicalProperty.SetMethod,
                       canonicalTargetType,
                       canonicalClosedGenericTargetTypeArguments,
                       aliasProperty.SetMethod,
                       aliasTargetType,
                       aliasClosedGenericTargetTypeArguments);
        }

        /// <summary>
        /// Determines whether two forward method binding descriptors are equivalent.
        /// </summary>
        /// <param name="canonicalBinding">The canonical binding descriptor.</param>
        /// <param name="aliasBinding">The alias binding descriptor.</param>
        /// <returns>true when both descriptors are equivalent; otherwise, false.</returns>
        private static bool AreForwardMethodBindingInfosEquivalent(ForwardMethodBindingInfo? canonicalBinding, ForwardMethodBindingInfo? aliasBinding)
        {
            if (canonicalBinding is null || aliasBinding is null)
            {
                return canonicalBinding is null && aliasBinding is null;
            }

            var canonical = canonicalBinding.Value;
            var alias = aliasBinding.Value;
            if (canonical.TrailingOptionalTargetParameterCount != alias.TrailingOptionalTargetParameterCount ||
                !string.Equals(BuildTypeSigSequenceCacheKey(canonical.ClosedGenericMethodArguments), BuildTypeSigSequenceCacheKey(alias.ClosedGenericMethodArguments), StringComparison.Ordinal) ||
                !AreMethodReturnConversionsEquivalent(canonical.ReturnConversion, alias.ReturnConversion) ||
                canonical.ParameterBindings.Count != alias.ParameterBindings.Count)
            {
                return false;
            }

            for (var i = 0; i < canonical.ParameterBindings.Count; i++)
            {
                if (!AreMethodParameterBindingsEquivalent(canonical.ParameterBindings[i], alias.ParameterBindings[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether two omitted trailing optional target arguments emit the same values.
        /// </summary>
        /// <param name="canonicalMethod">The canonical target method.</param>
        /// <param name="canonicalBinding">The canonical binding descriptor.</param>
        /// <param name="canonicalClosedGenericTargetTypeArguments">The canonical closed generic target type arguments.</param>
        /// <param name="aliasMethod">The alias target method.</param>
        /// <param name="aliasBinding">The alias binding descriptor.</param>
        /// <param name="aliasClosedGenericTargetTypeArguments">The alias closed generic target type arguments.</param>
        /// <returns>true when both bindings emit equivalent optional default arguments; otherwise, false.</returns>
        private static bool AreTrailingOptionalTargetDefaultsEquivalent(
            MethodDef? canonicalMethod,
            ForwardMethodBindingInfo? canonicalBinding,
            IReadOnlyList<TypeSig>? canonicalClosedGenericTargetTypeArguments,
            MethodDef? aliasMethod,
            ForwardMethodBindingInfo? aliasBinding,
            IReadOnlyList<TypeSig>? aliasClosedGenericTargetTypeArguments)
        {
            if (canonicalBinding is null || aliasBinding is null)
            {
                return canonicalBinding is null && aliasBinding is null;
            }

            var canonical = canonicalBinding.Value;
            var alias = aliasBinding.Value;
            if (canonical.TrailingOptionalTargetParameterCount != alias.TrailingOptionalTargetParameterCount)
            {
                return false;
            }

            if (canonical.TrailingOptionalTargetParameterCount == 0)
            {
                return true;
            }

            if (canonicalMethod is null ||
                aliasMethod is null ||
                canonicalMethod.MethodSig.Params.Count < canonical.TrailingOptionalTargetParameterCount ||
                aliasMethod.MethodSig.Params.Count < alias.TrailingOptionalTargetParameterCount)
            {
                return false;
            }

            var canonicalFirstOptionalParameterIndex = canonicalMethod.MethodSig.Params.Count - canonical.TrailingOptionalTargetParameterCount;
            var aliasFirstOptionalParameterIndex = aliasMethod.MethodSig.Params.Count - alias.TrailingOptionalTargetParameterCount;
            for (var offset = 0; offset < canonical.TrailingOptionalTargetParameterCount; offset++)
            {
                var canonicalParameterIndex = canonicalFirstOptionalParameterIndex + offset;
                var aliasParameterIndex = aliasFirstOptionalParameterIndex + offset;
                var canonicalParameterType = SubstituteTypeAndMethodGenericTypeArguments(
                    canonicalMethod.MethodSig.Params[canonicalParameterIndex],
                    canonicalClosedGenericTargetTypeArguments,
                    canonical.ClosedGenericMethodArguments);
                var aliasParameterType = SubstituteTypeAndMethodGenericTypeArguments(
                    aliasMethod.MethodSig.Params[aliasParameterIndex],
                    aliasClosedGenericTargetTypeArguments,
                    alias.ClosedGenericMethodArguments);

                if (!AreTypesEquivalent(canonicalParameterType, aliasParameterType) ||
                    !AreOptionalParameterDefaultValuesEquivalent(canonicalMethod, canonicalParameterIndex, aliasMethod, aliasParameterIndex))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether two optional parameter metadata defaults are equivalent.
        /// </summary>
        /// <param name="canonicalMethod">The canonical target method.</param>
        /// <param name="canonicalParameterIndex">The canonical method parameter index.</param>
        /// <param name="aliasMethod">The alias target method.</param>
        /// <param name="aliasParameterIndex">The alias method parameter index.</param>
        /// <returns>true when both parameters have the same default metadata; otherwise, false.</returns>
        private static bool AreOptionalParameterDefaultValuesEquivalent(
            MethodDef canonicalMethod,
            int canonicalParameterIndex,
            MethodDef aliasMethod,
            int aliasParameterIndex)
        {
            var canonicalHasConstant = TryGetOptionalParameterConstant(canonicalMethod, canonicalParameterIndex, out var canonicalConstantValue);
            var aliasHasConstant = TryGetOptionalParameterConstant(aliasMethod, aliasParameterIndex, out var aliasConstantValue);
            if (canonicalHasConstant != aliasHasConstant)
            {
                return IsMissingOptionalConstantEquivalentToDefault(canonicalMethod, canonicalParameterIndex, canonicalHasConstant, canonicalConstantValue) &&
                       IsMissingOptionalConstantEquivalentToDefault(aliasMethod, aliasParameterIndex, aliasHasConstant, aliasConstantValue);
            }

            if (!canonicalHasConstant)
            {
                return true;
            }

            return canonicalConstantValue is null
                       ? aliasConstantValue is null
                       : canonicalConstantValue.Equals(aliasConstantValue);
        }

        /// <summary>
        /// Determines whether an optional parameter without constant metadata emits the same value as an explicit default constant.
        /// </summary>
        /// <param name="method">The method that owns the parameter.</param>
        /// <param name="parameterIndex">The method signature parameter index.</param>
        /// <param name="hasConstant">Whether the parameter has constant metadata.</param>
        /// <param name="constantValue">The constant metadata value.</param>
        /// <returns>true when the parameter emits the default value; otherwise, false.</returns>
        private static bool IsMissingOptionalConstantEquivalentToDefault(MethodDef method, int parameterIndex, bool hasConstant, object? constantValue)
        {
            if (!hasConstant)
            {
                return true;
            }

            if (method.MethodSig is null || parameterIndex < 0 || parameterIndex >= method.MethodSig.Params.Count)
            {
                return false;
            }

            return IsDefaultConstantValue(method.MethodSig.Params[parameterIndex], constantValue);
        }

        /// <summary>
        /// Determines whether a metadata constant is equivalent to the emitted default value for a parameter type.
        /// </summary>
        /// <param name="parameterType">The parameter type.</param>
        /// <param name="constantValue">The metadata constant value.</param>
        /// <returns>true when the constant is the default value; otherwise, false.</returns>
        private static bool IsDefaultConstantValue(TypeSig parameterType, object? constantValue)
        {
            if (constantValue is null)
            {
                return true;
            }

            var underlyingType = GetUnderlyingTypeForTypeConversion(parameterType);
            if (IsTypeSigNamed(underlyingType, "System.Decimal"))
            {
                return constantValue is decimal decimalValue && decimalValue == decimal.Zero;
            }

            if (IsTypeSigNamed(underlyingType, "System.DateTime"))
            {
                return constantValue is DateTime dateTimeValue && dateTimeValue.Ticks == 0;
            }

            switch (underlyingType.ElementType)
            {
                case ElementType.Boolean:
                    return constantValue is bool boolValue ? !boolValue : TryConvertConstantToLong(constantValue, out var boolNumber) && boolNumber == 0;
                case ElementType.Char:
                    return constantValue is char charValue ? charValue == '\0' : TryConvertConstantToLong(constantValue, out var charNumber) && charNumber == 0;
                case ElementType.I1:
                case ElementType.U1:
                case ElementType.I2:
                case ElementType.U2:
                case ElementType.I4:
                case ElementType.U4:
                case ElementType.I8:
                case ElementType.U8:
                case ElementType.I:
                case ElementType.U:
                    return TryConvertConstantToLong(constantValue, out var integerNumber) && integerNumber == 0;
                case ElementType.R4:
                    return constantValue is float floatValue && floatValue == 0;
                case ElementType.R8:
                    return constantValue is double doubleValue && doubleValue == 0;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether two forward field binding descriptors are equivalent.
        /// </summary>
        /// <param name="canonicalBinding">The canonical binding descriptor.</param>
        /// <param name="aliasBinding">The alias binding descriptor.</param>
        /// <returns>true when both descriptors are equivalent; otherwise, false.</returns>
        private static bool AreForwardFieldBindingInfosEquivalent(ForwardFieldBindingInfo? canonicalBinding, ForwardFieldBindingInfo? aliasBinding)
        {
            if (canonicalBinding is null || aliasBinding is null)
            {
                return canonicalBinding is null && aliasBinding is null;
            }

            return AreMethodArgumentConversionsEquivalent(canonicalBinding.Value.ArgumentConversion, aliasBinding.Value.ArgumentConversion) &&
                   AreMethodReturnConversionsEquivalent(canonicalBinding.Value.ReturnConversion, aliasBinding.Value.ReturnConversion);
        }

        /// <summary>
        /// Determines whether two method parameter bindings are equivalent.
        /// </summary>
        /// <param name="canonicalBinding">The canonical binding descriptor.</param>
        /// <param name="aliasBinding">The alias binding descriptor.</param>
        /// <returns>true when both descriptors are equivalent; otherwise, false.</returns>
        private static bool AreMethodParameterBindingsEquivalent(MethodParameterBinding canonicalBinding, MethodParameterBinding aliasBinding)
        {
            return canonicalBinding.IsByRef == aliasBinding.IsByRef &&
                   canonicalBinding.UseLocalForByRef == aliasBinding.UseLocalForByRef &&
                   canonicalBinding.IsOut == aliasBinding.IsOut &&
                   AreTypesEquivalent(canonicalBinding.ProxyTypeSig, aliasBinding.ProxyTypeSig) &&
                   AreTypesEquivalent(canonicalBinding.TargetTypeSig, aliasBinding.TargetTypeSig) &&
                   AreNullableTypeSigsEquivalent(canonicalBinding.ProxyByRefElementTypeSig, aliasBinding.ProxyByRefElementTypeSig) &&
                   AreNullableTypeSigsEquivalent(canonicalBinding.TargetByRefElementTypeSig, aliasBinding.TargetByRefElementTypeSig) &&
                   AreMethodArgumentConversionsEquivalent(canonicalBinding.PreCallConversion, aliasBinding.PreCallConversion) &&
                   AreMethodReturnConversionsEquivalent(canonicalBinding.PostCallConversion, aliasBinding.PostCallConversion);
        }

        /// <summary>
        /// Determines whether two method argument conversion descriptors are equivalent.
        /// </summary>
        /// <param name="canonicalConversion">The canonical conversion descriptor.</param>
        /// <param name="aliasConversion">The alias conversion descriptor.</param>
        /// <returns>true when both descriptors are equivalent; otherwise, false.</returns>
        private static bool AreMethodArgumentConversionsEquivalent(MethodArgumentConversion canonicalConversion, MethodArgumentConversion aliasConversion)
        {
            return canonicalConversion.Kind == aliasConversion.Kind &&
                   AreNullableTypeSigsEquivalent(canonicalConversion.WrapperTypeSig, aliasConversion.WrapperTypeSig) &&
                   AreNullableTypeSigsEquivalent(canonicalConversion.InnerTypeSig, aliasConversion.InnerTypeSig) &&
                   AreNullableTypeSigsEquivalent(canonicalConversion.UnwrapWrapperTypeSig, aliasConversion.UnwrapWrapperTypeSig) &&
                   AreNullableTypeSigsEquivalent(canonicalConversion.UnwrapInnerTypeSig, aliasConversion.UnwrapInnerTypeSig);
        }

        /// <summary>
        /// Determines whether two method return conversion descriptors are equivalent.
        /// </summary>
        /// <param name="canonicalConversion">The canonical conversion descriptor.</param>
        /// <param name="aliasConversion">The alias conversion descriptor.</param>
        /// <returns>true when both descriptors are equivalent; otherwise, false.</returns>
        private static bool AreMethodReturnConversionsEquivalent(MethodReturnConversion canonicalConversion, MethodReturnConversion aliasConversion)
        {
            return canonicalConversion.Kind == aliasConversion.Kind &&
                   AreNullableTypeSigsEquivalent(canonicalConversion.WrapperTypeSig, aliasConversion.WrapperTypeSig) &&
                   AreNullableTypeSigsEquivalent(canonicalConversion.InnerTypeSig, aliasConversion.InnerTypeSig);
        }

        private static bool AreNullableTypeSigsEquivalent(TypeSig? canonicalTypeSig, TypeSig? aliasTypeSig)
        {
            if (canonicalTypeSig is null || aliasTypeSig is null)
            {
                return canonicalTypeSig is null && aliasTypeSig is null;
            }

            return AreTypesEquivalent(canonicalTypeSig, aliasTypeSig);
        }

        /// <summary>
        /// Builds the cached alias plan for a canonical target mapping.
        /// </summary>
        /// <param name="mapping">The canonical mapping value.</param>
        /// <param name="targetTypeIndex">The target type index value.</param>
        /// <param name="genericTypeRoots">The closed generic type roots value.</param>
        /// <returns>The cached alias plan.</returns>
        private static CanonicalTargetAliasPlan BuildCanonicalTargetAliasPlan(
            DuckTypeAotMapping mapping,
            TargetTypeIndex targetTypeIndex,
            IReadOnlyList<DuckTypeAotTypeReference> genericTypeRoots)
        {
            NullableAliasTargetInfo? nullableAlias = null;
            if (mapping.Mode == DuckTypeAotMappingMode.Forward &&
                TryCreateNullableAliasTargetInfo(mapping, out var nullableAliasTarget))
            {
                nullableAlias = nullableAliasTarget;
            }

            IReadOnlyList<TargetAliasTargetInfo> indexedAssignableTargets = [];
            if (targetTypeIndex.TryGetAssignableTargets(mapping.Mode, mapping.TargetAssemblyName, mapping.TargetTypeName, out var discoveredAssignableTargets))
            {
                indexedAssignableTargets = discoveredAssignableTargets;
            }

            var resolvedTargets = new List<TargetAliasTargetInfo>(indexedAssignableTargets);
            if (TryGetRuntimeAssignableAliasTargets(mapping, targetTypeIndex, genericTypeRoots, out var runtimeAssignableTargets))
            {
                var seenKeys = resolvedTargets.Select(item => BuildAssemblyTypeCacheKey(item.AssemblyName, item.TypeName)).ToHashSet(StringComparer.Ordinal);
                foreach (var runtimeAssignableTarget in runtimeAssignableTargets)
                {
                    if (seenKeys.Add(BuildAssemblyTypeCacheKey(runtimeAssignableTarget.AssemblyName, runtimeAssignableTarget.TypeName)))
                    {
                        resolvedTargets.Add(runtimeAssignableTarget);
                    }
                }
            }

            return new CanonicalTargetAliasPlan(
                nullableAlias,
                resolvedTargets
                   .OrderBy(item => item.AssemblyName, StringComparer.OrdinalIgnoreCase)
                   .ThenBy(item => item.TypeName, StringComparer.Ordinal)
                   .ToList());
        }

        /// <summary>
        /// Determines whether a type should be indexed as an assignable alias candidate.
        /// </summary>
        /// <param name="candidateType">The candidate type value.</param>
        /// <returns>true when the type can participate as an alias candidate; otherwise, false.</returns>
        private static bool IsAliasCandidateType(TypeDef candidateType)
        {
            if (candidateType is null ||
                string.IsNullOrWhiteSpace(candidateType.FullName))
            {
                return false;
            }

            if (candidateType.IsInterface || candidateType.IsAbstract || candidateType.GenericParameters.Count > 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to resolve runtime assignable aliases for canonical targets, including closed generic roots.
        /// </summary>
        /// <param name="mapping">The canonical mapping value.</param>
        /// <param name="targetTypeIndex">The target type index value.</param>
        /// <param name="genericTypeRoots">The closed generic type roots value.</param>
        /// <param name="aliasTargets">The resulting alias targets.</param>
        /// <returns>true when at least one alias target was resolved; otherwise, false.</returns>
        private static bool TryGetRuntimeAssignableAliasTargets(
            DuckTypeAotMapping mapping,
            TargetTypeIndex targetTypeIndex,
            IReadOnlyList<DuckTypeAotTypeReference> genericTypeRoots,
            out IReadOnlyList<TargetAliasTargetInfo> aliasTargets)
        {
            aliasTargets = Array.Empty<TargetAliasTargetInfo>();
            if (runtimeTypeResolutionAssemblyPathsByName is null ||
                !runtimeTypeResolutionAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var canonicalRuntimeTargetType) ||
                canonicalRuntimeTargetType is null)
            {
                return false;
            }

            var resolvedAliases = new List<TargetAliasTargetInfo>();
            var seenAliasKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidateTarget in targetTypeIndex.AliasCandidateTargets)
            {
                if (mapping.Mode == DuckTypeAotMappingMode.Reverse && candidateTarget.Type.IsValueType)
                {
                    continue;
                }

                var candidateTypeName = GetRuntimeTypeName(candidateTarget.Type);
                if (string.IsNullOrWhiteSpace(candidateTypeName) ||
                    !runtimeTypeResolutionAssemblyPathsByName.TryGetValue(candidateTarget.AssemblyName, out var candidateAssemblyPath) ||
                    !TryResolveRuntimeType(candidateTarget.AssemblyName, candidateAssemblyPath, candidateTypeName, out var candidateRuntimeType) ||
                    candidateRuntimeType is null ||
                    !canonicalRuntimeTargetType.IsAssignableFrom(candidateRuntimeType) ||
                    candidateRuntimeType == canonicalRuntimeTargetType)
                {
                    continue;
                }

                AddResolvedAlias(candidateTarget.AssemblyName, candidateTypeName);
            }

            foreach (var genericTypeRoot in genericTypeRoots)
            {
                if (string.IsNullOrWhiteSpace(genericTypeRoot.TypeName) ||
                    !DuckTypeAotNameHelpers.IsClosedGenericTypeName(genericTypeRoot.TypeName) ||
                    !runtimeTypeResolutionAssemblyPathsByName.TryGetValue(genericTypeRoot.AssemblyName, out var genericTypeRootAssemblyPath) ||
                    !TryResolveRuntimeType(genericTypeRoot.AssemblyName, genericTypeRootAssemblyPath, genericTypeRoot.TypeName, out var genericTypeRootRuntimeType) ||
                    genericTypeRootRuntimeType is null ||
                    genericTypeRootRuntimeType.ContainsGenericParameters ||
                    (mapping.Mode == DuckTypeAotMappingMode.Reverse && genericTypeRootRuntimeType.IsValueType) ||
                    genericTypeRootRuntimeType.IsInterface ||
                    genericTypeRootRuntimeType.IsAbstract ||
                    !canonicalRuntimeTargetType.IsAssignableFrom(genericTypeRootRuntimeType) ||
                    genericTypeRootRuntimeType == canonicalRuntimeTargetType)
                {
                    continue;
                }

                AddResolvedAlias(genericTypeRoot.AssemblyName, genericTypeRoot.TypeName);
            }

            aliasTargets = resolvedAliases
                          .OrderBy(item => item.AssemblyName, StringComparer.OrdinalIgnoreCase)
                          .ThenBy(item => item.TypeName, StringComparer.Ordinal)
                          .ToList();
            return aliasTargets.Count > 0;

            void AddResolvedAlias(string assemblyName, string typeName)
            {
                if (seenAliasKeys.Add(BuildAssemblyTypeCacheKey(assemblyName, typeName)))
                {
                    resolvedAliases.Add(new TargetAliasTargetInfo(assemblyName, typeName));
                }
            }
        }

        /// <summary>
        /// Enumerates assignable assembly/type keys reachable from a concrete candidate type.
        /// </summary>
        /// <param name="candidateType">The candidate type value.</param>
        /// <returns>The reachable assignable assembly/type key sequence.</returns>
        private static IReadOnlyList<string> GetAssignableTypeKeys(
            TypeDef candidateType,
            ISet<string> queriedTargetTypeKeys,
            IDictionary<TypeDef, IReadOnlyList<string>> assignableTypeKeysByType,
            ISet<TypeDef> assignableTypeKeysInProgress)
        {
            if (assignableTypeKeysByType.TryGetValue(candidateType, out var cachedTypeKeys))
            {
                return cachedTypeKeys;
            }

            if (!assignableTypeKeysInProgress.Add(candidateType))
            {
                return Array.Empty<string>();
            }

            var assignableTypeKeys = new List<string>();
            var seenTypeKeys = new HashSet<string>(StringComparer.Ordinal);
            AddAssignableTypeName(candidateType, candidateType.FullName);
            AddAssignableTypeName(candidateType, candidateType.ReflectionFullName);

            AppendAssignableTypeNames(candidateType.BaseType?.ResolveTypeDef());
            foreach (var interfaceImpl in candidateType.Interfaces)
            {
                AppendAssignableTypeNames(interfaceImpl.Interface.ResolveTypeDef());
            }

            assignableTypeKeysInProgress.Remove(candidateType);
            assignableTypeKeysByType[candidateType] = assignableTypeKeys;
            return assignableTypeKeys;

            void AppendAssignableTypeNames(TypeDef? type)
            {
                if (type is null)
                {
                    return;
                }

                foreach (var assignableTypeKey in GetAssignableTypeKeys(type, queriedTargetTypeKeys, assignableTypeKeysByType, assignableTypeKeysInProgress))
                {
                    AddAssignableTypeKey(assignableTypeKey);
                }
            }

            void AddAssignableTypeName(TypeDef type, string? typeName)
            {
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return;
                }

                var assemblyName = ResolveTypeDefAssemblyName(type);
                if (string.IsNullOrWhiteSpace(assemblyName))
                {
                    return;
                }

                AddAssignableTypeKey(BuildAssemblyTypeCacheKey(assemblyName, typeName!));
                var runtimeTypeName = typeName!.Replace('/', '+');
                if (!string.Equals(runtimeTypeName, typeName, StringComparison.Ordinal))
                {
                    AddAssignableTypeKey(BuildAssemblyTypeCacheKey(assemblyName, runtimeTypeName));
                }
            }

            void AddAssignableTypeKey(string typeKey)
            {
                if (!seenTypeKeys.Add(typeKey) ||
                    !queriedTargetTypeKeys.Contains(typeKey))
                {
                    return;
                }

                assignableTypeKeys.Add(typeKey);
            }
        }

        /// <summary>
        /// Adds a target-type entry to an ancestor index.
        /// </summary>
        /// <param name="index">The ancestor index value.</param>
        /// <param name="ancestorTypeKey">The ancestor assembly/type key value.</param>
        /// <param name="entry">The entry value.</param>
        private static void AddTargetTypeIndexEntry(
            IDictionary<string, List<TargetTypeIndexEntry>> index,
            string ancestorTypeKey,
            TargetTypeIndexEntry entry)
        {
            if (!index.TryGetValue(ancestorTypeKey, out var entries))
            {
                entries = [];
                index[ancestorTypeKey] = entries;
            }

            entries.Add(entry);
        }

        /// <summary>
        /// Converts a mutable target-type ancestor index into its deterministic immutable representation.
        /// </summary>
        /// <param name="index">The mutable index value.</param>
        /// <returns>The sorted target-type index.</returns>
        private static IReadOnlyDictionary<string, IReadOnlyList<TargetAliasTargetInfo>> ToSortedTargetTypeIndex(
            IDictionary<string, List<TargetTypeIndexEntry>> index)
        {
            var result = new Dictionary<string, IReadOnlyList<TargetAliasTargetInfo>>(StringComparer.Ordinal);
            foreach (var entry in index)
            {
                var orderedTargets = entry.Value
                                          .OrderBy(item => item.AssemblyName, StringComparer.OrdinalIgnoreCase)
                                          .ThenBy(item => item.Type.FullName, StringComparer.Ordinal)
                                          .Select(item => new TargetAliasTargetInfo(item.AssemblyName, GetRuntimeTypeName(item.Type)))
                                          .Where(item => !string.IsNullOrWhiteSpace(item.TypeName))
                                          .ToList();
                result[entry.Key] = orderedTargets;
            }

            return result;
        }

        /// <summary>
        /// Builds the canonical-target cache key used for alias-plan reuse.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <returns>The canonical-target cache key.</returns>
        private static string BuildCanonicalTargetCacheKey(DuckTypeAotMapping mapping)
        {
            return string.Concat(
                mapping.Mode.ToString(),
                "|",
                mapping.TargetAssemblyName.ToUpperInvariant(),
                "|",
                mapping.TargetTypeName);
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
            if (!string.IsNullOrWhiteSpace(reflectionFullName))
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
        /// Attempts to create a nullable alias target for a forward canonical mapping.
        /// </summary>
        /// <param name="mapping">The canonical mapping value.</param>
        /// <param name="aliasTarget">The resulting alias target value.</param>
        /// <returns>true if the alias mapping was created; otherwise, false.</returns>
        private static bool TryCreateNullableAliasTargetInfo(DuckTypeAotMapping mapping, out NullableAliasTargetInfo? aliasTarget)
        {
            aliasTarget = null;
            if (mapping.Mode != DuckTypeAotMappingMode.Forward ||
                !runtimeTypeResolutionAssemblyPathsByName!.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var runtimeTargetType) ||
                runtimeTargetType is null)
            {
                return false;
            }

            var nullableUnderlyingType = Nullable.GetUnderlyingType(runtimeTargetType);
            if (nullableUnderlyingType is null || string.IsNullOrWhiteSpace(nullableUnderlyingType.FullName))
            {
                return false;
            }

            aliasTarget = new NullableAliasTargetInfo(
                nullableUnderlyingType.FullName!,
                DuckTypeAotNameHelpers.NormalizeAssemblyName(nullableUnderlyingType.Assembly.GetName().Name ?? string.Empty));
            return true;
        }

        /// <summary>
        /// Creates a compatibility failure result for a mapping.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="status">The status value.</param>
        /// <param name="diagnosticCode">The diagnostic code value.</param>
        /// <param name="detail">The detail value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingEmissionResult CreateFailureResult(
            DuckTypeAotMapping mapping,
            string status,
            string diagnosticCode,
            string detail)
        {
            if (IsKnownNonCreatableParityScenario(mapping, status))
            {
                return DuckTypeAotMappingEmissionResult.Compatible(mapping, mapping.ProxyAssemblyName, mapping.ProxyTypeName);
            }

            return DuckTypeAotMappingEmissionResult.NotCompatible(mapping, status, diagnosticCode, detail);
        }

        /// <summary>
        /// Determines whether known non-creatable parity scenario.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="status">The status value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsKnownNonCreatableParityScenario(DuckTypeAotMapping mapping, string status)
        {
            var scenarioId = mapping.ScenarioId;
            if (string.Equals(scenarioId, ParityScenarioIdRt2, StringComparison.Ordinal) ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+IRt2VoidMismatchProxy", "DuckTypeAotDifferentialParityTests+Rt2VoidMismatchTarget") ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotBibleExcerptsParityTests+ITxLOverloadProxy", "DuckTypeAotBibleExcerptsParityTests+TxLOverloadTarget") ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+IAmbiguousMethodProxy", "DuckTypeAotDifferentialParityTests+AmbiguousMethodTarget") ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+IStructMutationGuardProxy", "DuckTypeAotDifferentialParityTests+StructMutationGuardTarget") ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+DuckCopyEmptyProjection", "DuckTypeAotDifferentialParityTests+DuckCopyEmptyTarget") ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+IReverseNamedAttributeCopyProxy", "DuckTypeAotDifferentialParityTests+ReverseNamedAttributeCopyDelegation") ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+IReverseImplementorConstraintProxy", "DuckTypeAotDifferentialParityTests+ReverseAbstractImplementorDelegation") ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+IReverseImplementorConstraintProxy", "DuckTypeAotDifferentialParityTests+IReverseInterfaceImplementorDelegation"))
            {
                return string.Equals(status, DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature, StringComparison.Ordinal);
            }

            if (string.Equals(scenarioId, ParityScenarioIdE39, StringComparison.Ordinal) ||
                string.Equals(scenarioId, ParityScenarioIdE40, StringComparison.Ordinal) ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+ReverseRequiredMethodBase", "DuckTypeAotDifferentialParityTests+ReverseRequiredMethodDelegation") ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+ReverseGenericContractBase", "DuckTypeAotDifferentialParityTests+ReverseGenericMismatchDelegation"))
            {
                return string.Equals(status, DuckTypeAotCompatibilityStatuses.MissingTargetMethod, StringComparison.Ordinal);
            }

            if (string.Equals(scenarioId, ParityScenarioIdE42, StringComparison.Ordinal) ||
                IsKnownNonCreatableParityMapping(mapping, "DuckTypeAotDifferentialParityTests+ReverseStructBase", "DuckTypeAotDifferentialParityTests+ReverseStructDelegation"))
            {
                return string.Equals(status, DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind, StringComparison.Ordinal);
            }

            return false;
        }

        /// <summary>
        /// Determines whether a mapping matches one of the known non-creatable parity-safe identities.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyTypeSuffix">The proxy type suffix value.</param>
        /// <param name="targetTypeSuffix">The target type suffix value.</param>
        /// <returns>true if the mapping matches; otherwise, false.</returns>
        private static bool IsKnownNonCreatableParityMapping(DuckTypeAotMapping mapping, string proxyTypeSuffix, string targetTypeSuffix)
        {
            return mapping is not null &&
                   mapping.ProxyAssemblyName == "Datadog.Trace.DuckTyping.Tests" &&
                   mapping.TargetAssemblyName == "Datadog.Trace.DuckTyping.Tests" &&
                   mapping.ProxyTypeName.EndsWith(proxyTypeSuffix, StringComparison.Ordinal) &&
                   mapping.TargetTypeName.EndsWith(targetTypeSuffix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Emits a known failure registration when an incompatible mapping should preserve dynamic exception behavior in AOT mode.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="failure">The failure value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <returns>true when a failure registration was emitted; otherwise, false.</returns>
        private static bool TryEmitKnownFailureRegistration(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            TypeDef proxyType,
            ITypeDefOrRef targetType,
            DuckTypeAotMappingEmissionResult failure,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string>? emissionWarnings = null)
        {
            var phaseStopwatch = StartProfilePhase();
            if (!TryResolveFailureReplay(
                    moduleDef,
                    mapping,
                    failure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    out var resolvedExceptionTypeName,
                    out var resolvedFailureMessage))
            {
                return false;
            }

            var importedProxyType = ImportTypeDefOrRefCached(moduleDef, proxyType, $"failure registration proxy type '{proxyType.FullName}'");
            var importedTargetType = ImportTypeDefOrRefCached(moduleDef, targetType, $"failure registration target type '{targetType.FullName}'");

            EmitFailureRegistration(
                moduleDef,
                bootstrapType,
                initializeMethod,
                importedMembers,
                mapping.Mode,
                mappingIndex,
                importedProxyType,
                importedTargetType,
                resolvedExceptionTypeName,
                resolvedFailureMessage ?? failure.Detail ?? string.Empty);

            if (emissionWarnings is not null)
            {
                var diagnosticCode = string.IsNullOrWhiteSpace(failure.DiagnosticCode) ? "n/a" : failure.DiagnosticCode!;
                var detail = string.IsNullOrWhiteSpace(failure.Detail) ? "No additional details." : failure.Detail!;
                emissionWarnings.Add(
                    $"Registered AOT failure mapping '{mapping.Key}' to throw '{resolvedExceptionTypeName}' ({diagnosticCode}): {detail}");
            }

            StopProfilePhase(
                phaseStopwatch,
                seconds =>
                {
                    _currentProfile!.KnownFailureRegistrationSeconds += seconds;
                    _currentProfile!.KnownFailureRegistrationCount++;
                });
            return true;
        }

        private static bool TryResolveFailureReplay(
            ModuleDef moduleDef,
            DuckTypeAotMapping mapping,
            DuckTypeAotMappingEmissionResult failure,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            out string failureTypeName,
            out string failureMessage)
        {
            failureTypeName = string.Empty;
            failureMessage = failure.Detail ?? string.Empty;

            if (TryResolveStaticExactFailureTypeName(failure, out var staticFailureTypeName))
            {
                failureTypeName = staticFailureTypeName!;
                if (_currentProfile is not null)
                {
                    _currentProfile.FailureClassifierFastPathCount++;
                }

                return true;
            }

            if (TryResolveDynamicFailureExceptionType(
                    moduleDef,
                    mapping,
                    failure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    out var dynamicFailureExceptionType,
                    out var dynamicFailureMessage))
            {
                failureTypeName = dynamicFailureExceptionType!.FullName ?? dynamicFailureExceptionType.Name;
                failureMessage = dynamicFailureMessage ?? failureMessage;
                if (_currentProfile is not null)
                {
                    _currentProfile.FailureClassifierFallbackCount++;
                }

                return true;
            }

            if (TryResolveBroadFailureTypeName(failure, out var broadFailureTypeName))
            {
                failureTypeName = broadFailureTypeName!;
                if (_currentProfile is not null)
                {
                    _currentProfile.FailureClassifierFallbackCount++;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Emits a failure registration entry into the bootstrap method.
        /// </summary>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mode">The mapping mode value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="exceptionType">The exception type value.</param>
        private static void EmitFailureRegistration(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMappingMode mode,
            int mappingIndex,
            ITypeDefOrRef proxyType,
            ITypeDefOrRef targetType,
            string failureTypeName,
            string detail)
        {
            var failureThrowerMethod = GetOrCreateFailureThrowerMethod(moduleDef, bootstrapType, importedMembers, mappingIndex, failureTypeName, detail);
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(proxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(targetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitActionDelegate(initializeMethod.Body, importedMembers, failureThrowerMethod);
            initializeMethod.Body.Instructions.Add(
                OpCodes.Call.ToInstruction(
                    mode == DuckTypeAotMappingMode.Reverse
                        ? importedMembers.RegisterAotReverseProxyFailureMethod
                        : importedMembers.RegisterAotProxyFailureMethod));
        }

        /// <summary>
        /// Emits a direct <see cref="Func{T, TResult}"/> delegate to a generated static object activator.
        /// </summary>
        /// <param name="body">The target method body.</param>
        /// <param name="importedMembers">The imported member cache.</param>
        /// <param name="method">The generated static method.</param>
        private static void EmitFuncObjectObjectDelegate(CilBody body, ImportedMembers importedMembers, MethodDef method)
        {
            body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            body.Instructions.Add(OpCodes.Ldftn.ToInstruction(method));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.FuncObjectObjectCtor));
        }

        /// <summary>
        /// Emits a direct <see cref="Action"/> delegate to a generated static failure thrower.
        /// </summary>
        /// <param name="body">The target method body.</param>
        /// <param name="importedMembers">The imported member cache.</param>
        /// <param name="method">The generated static method.</param>
        private static void EmitActionDelegate(CilBody body, ImportedMembers importedMembers, MethodDef method)
        {
            body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            body.Instructions.Add(OpCodes.Ldftn.ToInstruction(method));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.ActionCtor));
        }

        private static MethodDef GetOrCreateFailureThrowerMethod(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            ImportedMembers importedMembers,
            int mappingIndex,
            string failureTypeName,
            string detail)
        {
            detail ??= string.Empty;
            var failureThrowerCacheKey = string.Concat(
                failureTypeName,
                "::",
                detail);
            if (_currentExecutionContext?.TryGetFailureThrowerMethod(failureThrowerCacheKey, out var cachedFailureThrowerMethod) == true)
            {
                return cachedFailureThrowerMethod;
            }

            var failureThrowerMethod = EmitFailureThrowerMethod(moduleDef, bootstrapType, importedMembers, mappingIndex, failureTypeName, detail);
            _currentExecutionContext?.CacheFailureThrowerMethod(failureThrowerCacheKey, failureThrowerMethod);
            return failureThrowerMethod;
        }

        private static bool TryEmitValueTypeNullableAliasRegistration(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping aliasMapping,
            DuckTypeAotMapping canonicalMapping,
            int mappingIndex,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string>? emissionWarnings,
            out DuckTypeAotMappingEmissionResult emissionResult)
        {
            emissionResult = DuckTypeAotMappingEmissionResult.NotCompatible(
                aliasMapping,
                DuckTypeAotCompatibilityStatuses.MissingTargetType,
                StatusCodeMissingTargetType,
                "Nullable alias bridge could not be emitted.");

            if (aliasMapping.Mode != DuckTypeAotMappingMode.Forward ||
                !proxyAssemblyPathsByName.TryGetValue(aliasMapping.ProxyAssemblyName, out var proxyAssemblyPath) ||
                !targetAssemblyPathsByName.TryGetValue(canonicalMapping.TargetAssemblyName, out var canonicalTargetAssemblyPath) ||
                !TryResolveRuntimeType(aliasMapping.ProxyAssemblyName, proxyAssemblyPath, aliasMapping.ProxyTypeName, out var proxyRuntimeType) ||
                !TryResolveRuntimeType(canonicalMapping.TargetAssemblyName, canonicalTargetAssemblyPath, canonicalMapping.TargetTypeName, out var canonicalTargetRuntimeType) ||
                proxyRuntimeType is null ||
                canonicalTargetRuntimeType is null ||
                !proxyRuntimeType.IsValueType)
            {
                return false;
            }

            var nullableUnderlyingType = Nullable.GetUnderlyingType(canonicalTargetRuntimeType);
            if (nullableUnderlyingType is null ||
                !string.Equals(nullableUnderlyingType.FullName, aliasMapping.TargetTypeName, StringComparison.Ordinal) ||
                !string.Equals(
                    DuckTypeAotNameHelpers.NormalizeAssemblyName(nullableUnderlyingType.Assembly.GetName().Name ?? string.Empty),
                    aliasMapping.TargetAssemblyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var importedProxyTypeSig = ImportRuntimeTypeSig(moduleDef, proxyRuntimeType);
            var importedAliasTargetTypeSig = ImportRuntimeTypeSig(moduleDef, nullableUnderlyingType);
            var importedCanonicalTargetTypeSig = ImportRuntimeTypeSig(moduleDef, canonicalTargetRuntimeType);
            var importedProxyType = ResolveImportedTypeForTypeToken(moduleDef, importedProxyTypeSig, $"nullable alias proxy '{aliasMapping.ProxyTypeName}'");
            var importedAliasTargetType = ResolveImportedTypeForTypeToken(moduleDef, importedAliasTargetTypeSig, $"nullable alias target '{aliasMapping.TargetTypeName}'");

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(importedProxyTypeSig, importedAliasTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            activatorMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(CreateNullableCtorRef(moduleDef, importedCanonicalTargetTypeSig)));
            activatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(CreateDuckTypeCreateCacheCreateFromMethodRef(moduleDef, importedProxyTypeSig, importedCanonicalTargetTypeSig)));
            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            var registrationActivatorMethod = new MethodDefUser(
                $"ActivateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Unbox_Any.ToInstruction(importedAliasTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            if (proxyRuntimeType.IsValueType)
            {
                registrationActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
            }

            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedAliasTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitFuncObjectObjectDelegate(initializeMethod.Body, importedMembers, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.RegisterAotProxyMethod));

            if (emissionWarnings is not null)
            {
                emissionWarnings.Add(
                    $"Registered nullable alias bridge '{aliasMapping.Key}' via canonical nullable mapping '{canonicalMapping.Key}'.");
            }

            emissionResult = DuckTypeAotMappingEmissionResult.Compatible(aliasMapping, aliasMapping.ProxyAssemblyName, aliasMapping.ProxyTypeName);
            return true;
        }

        private static bool TryEmitAssignableAliasBridgeRegistration(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping aliasMapping,
            DuckTypeAotMapping canonicalMapping,
            int mappingIndex,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string>? emissionWarnings,
            out DuckTypeAotMappingEmissionResult emissionResult)
        {
            emissionResult = DuckTypeAotMappingEmissionResult.NotCompatible(
                aliasMapping,
                DuckTypeAotCompatibilityStatuses.MissingTargetType,
                StatusCodeMissingTargetType,
                "Assignable alias bridge could not be emitted.");

            if (aliasMapping.Mode != DuckTypeAotMappingMode.Forward ||
                !proxyAssemblyPathsByName.TryGetValue(aliasMapping.ProxyAssemblyName, out var proxyAssemblyPath) ||
                !targetAssemblyPathsByName.TryGetValue(aliasMapping.TargetAssemblyName, out var aliasTargetAssemblyPath) ||
                !targetAssemblyPathsByName.TryGetValue(canonicalMapping.TargetAssemblyName, out var canonicalTargetAssemblyPath) ||
                !TryResolveRuntimeType(aliasMapping.ProxyAssemblyName, proxyAssemblyPath, aliasMapping.ProxyTypeName, out var proxyRuntimeType) ||
                !TryResolveRuntimeType(aliasMapping.TargetAssemblyName, aliasTargetAssemblyPath, aliasMapping.TargetTypeName, out var aliasTargetRuntimeType) ||
                !TryResolveRuntimeType(canonicalMapping.TargetAssemblyName, canonicalTargetAssemblyPath, canonicalMapping.TargetTypeName, out var canonicalTargetRuntimeType) ||
                proxyRuntimeType is null ||
                aliasTargetRuntimeType is null ||
                canonicalTargetRuntimeType is null ||
                canonicalTargetRuntimeType.IsValueType ||
                !canonicalTargetRuntimeType.IsAssignableFrom(aliasTargetRuntimeType))
            {
                return false;
            }

            var importedProxyTypeSig = ImportRuntimeTypeSig(moduleDef, proxyRuntimeType);
            var importedAliasTargetTypeSig = ImportRuntimeTypeSig(moduleDef, aliasTargetRuntimeType);
            var importedCanonicalTargetTypeSig = ImportRuntimeTypeSig(moduleDef, canonicalTargetRuntimeType);
            var importedProxyType = ResolveImportedTypeForTypeToken(moduleDef, importedProxyTypeSig, $"assignable alias proxy '{aliasMapping.ProxyTypeName}'");
            var importedAliasTargetType = ResolveImportedTypeForTypeToken(moduleDef, importedAliasTargetTypeSig, $"assignable alias target '{aliasMapping.TargetTypeName}'");
            var importedCanonicalTargetType = ResolveImportedTypeForTypeToken(moduleDef, importedCanonicalTargetTypeSig, $"assignable alias canonical target '{canonicalMapping.TargetTypeName}'");

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(importedProxyTypeSig, importedAliasTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            EmitAssignableAliasCanonicalTargetConversion(activatorMethod.Body, importedAliasTargetType, importedCanonicalTargetType, aliasTargetRuntimeType, canonicalTargetRuntimeType);
            activatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(CreateDuckTypeCreateCacheCreateFromMethodRef(moduleDef, importedProxyTypeSig, importedCanonicalTargetTypeSig)));
            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            var registrationActivatorMethod = new MethodDefUser(
                $"ActivateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            registrationActivatorMethod.Body.Instructions.Add((aliasTargetRuntimeType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedAliasTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            if (proxyRuntimeType.IsValueType)
            {
                registrationActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
            }

            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedAliasTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitFuncObjectObjectDelegate(initializeMethod.Body, importedMembers, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.RegisterAotProxyMethod));

            if (emissionWarnings is not null)
            {
                emissionWarnings.Add(
                    $"Registered assignable alias bridge '{aliasMapping.Key}' via canonical mapping '{canonicalMapping.Key}'.");
            }

            emissionResult = DuckTypeAotMappingEmissionResult.Compatible(aliasMapping, aliasMapping.ProxyAssemblyName, aliasMapping.ProxyTypeName);
            return true;
        }

        private static void EmitAssignableAliasCanonicalTargetConversion(
            CilBody body,
            ITypeDefOrRef importedAliasTargetType,
            ITypeDefOrRef importedCanonicalTargetType,
            Type aliasTargetRuntimeType,
            Type canonicalTargetRuntimeType)
        {
            if (aliasTargetRuntimeType == canonicalTargetRuntimeType)
            {
                return;
            }

            if (aliasTargetRuntimeType.IsValueType && !canonicalTargetRuntimeType.IsValueType)
            {
                body.Instructions.Add(OpCodes.Box.ToInstruction(importedAliasTargetType));
            }

            if (!canonicalTargetRuntimeType.IsValueType)
            {
                body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedCanonicalTargetType));
            }
        }

        /// <summary>
        /// Emits a failure thrower method used to replay deterministic AOT failures.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="failureTypeName">The failure type name value.</param>
        /// <param name="detail">The failure detail value.</param>
        /// <returns>The emitted thrower method.</returns>
        private static MethodDef EmitFailureThrowerMethod(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            ImportedMembers importedMembers,
            int mappingIndex,
            string failureTypeName,
            string detail)
        {
            var method = new MethodDefUser(
                $"ThrowFailure_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Void),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            method.Body = new CilBody();
            method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(failureTypeName));
            method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(detail ?? string.Empty));
            method.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.DuckTypeAotRegisteredFailureThrowMethod));
            method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(method);
            return method;
        }

        /// <summary>
        /// Attempts to resolve the exact dynamic ducktyping failure exception for a mapping via dry-run validation.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="exceptionType">The resolved exception type value.</param>
        /// <returns>true when a dynamic-equivalent failure type was resolved; otherwise, false.</returns>
        private static bool TryResolveDynamicFailureExceptionType(
            ModuleDef moduleDef,
            DuckTypeAotMapping mapping,
            DuckTypeAotMappingEmissionResult failure,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            out Type? exceptionType,
            out string? exceptionMessage)
        {
            var phaseStopwatch = StartProfilePhase();
            var cacheKey = mapping.Key;
            var fingerprintKey = BuildFailureProbeFingerprint(mapping.Mode, failure);
            if (_currentExecutionContext?.TryGetFailureProbe(cacheKey, out var cachedFailureProbe) == true)
            {
                exceptionType = cachedFailureProbe.ExceptionType;
                exceptionMessage = cachedFailureProbe.ExceptionMessage;
                StopProfilePhase(
                    phaseStopwatch,
                    seconds =>
                    {
                        _currentProfile!.DynamicFailureProbeSeconds += seconds;
                        _currentProfile!.DynamicFailureProbeCount++;
                    });
                return cachedFailureProbe.Succeeded;
            }

            if (_currentExecutionContext?.TryGetFailureProbeByFingerprint(fingerprintKey, out cachedFailureProbe) == true)
            {
                exceptionType = cachedFailureProbe.ExceptionType;
                exceptionMessage = cachedFailureProbe.ExceptionMessage;
                _currentExecutionContext.CacheFailureProbe(cacheKey, exceptionType, exceptionMessage, cachedFailureProbe.Succeeded);
                StopProfilePhase(
                    phaseStopwatch,
                    seconds =>
                    {
                        _currentProfile!.DynamicFailureProbeSeconds += seconds;
                        _currentProfile!.DynamicFailureProbeCount++;
                    });
                return cachedFailureProbe.Succeeded;
            }

            exceptionType = null;
            exceptionMessage = null;
            if (moduleDef is null ||
                mapping is null ||
                DynamicForwardDryRunFactory is null ||
                DynamicReverseDryRunFactory is null ||
                CreateTypeResultCanCreateMethod is null ||
                CreateTypeResultProxyTypeProperty is null)
            {
                StopProfilePhase(
                    phaseStopwatch,
                    seconds =>
                    {
                        _currentProfile!.DynamicFailureProbeSeconds += seconds;
                        _currentProfile!.DynamicFailureProbeCount++;
                    });
                _currentExecutionContext?.CacheFailureProbe(cacheKey, exceptionType: null, exceptionMessage: null, succeeded: false);
                _currentExecutionContext?.CacheFailureProbeByFingerprint(fingerprintKey, exceptionType: null, exceptionMessage: null, succeeded: false);
                return false;
            }

            if (!proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath) ||
                !targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath))
            {
                StopProfilePhase(
                    phaseStopwatch,
                    seconds =>
                    {
                        _currentProfile!.DynamicFailureProbeSeconds += seconds;
                        _currentProfile!.DynamicFailureProbeCount++;
                    });
                _currentExecutionContext?.CacheFailureProbe(cacheKey, exceptionType: null, exceptionMessage: null, succeeded: false);
                _currentExecutionContext?.CacheFailureProbeByFingerprint(fingerprintKey, exceptionType: null, exceptionMessage: null, succeeded: false);
                return false;
            }

            if (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) ||
                proxyRuntimeType is null ||
                targetRuntimeType is null)
            {
                StopProfilePhase(
                    phaseStopwatch,
                    seconds =>
                    {
                        _currentProfile!.DynamicFailureProbeSeconds += seconds;
                        _currentProfile!.DynamicFailureProbeCount++;
                    });
                _currentExecutionContext?.CacheFailureProbe(cacheKey, exceptionType: null, exceptionMessage: null, succeeded: false);
                _currentExecutionContext?.CacheFailureProbeByFingerprint(fingerprintKey, exceptionType: null, exceptionMessage: null, succeeded: false);
                return false;
            }

            object? dryRunResult;
            try
            {
                dryRunResult = mapping.Mode == DuckTypeAotMappingMode.Reverse
                                   ? DynamicReverseDryRunFactory.Invoke(null, new object[] { proxyRuntimeType, targetRuntimeType, true })
                                   : DynamicForwardDryRunFactory.Invoke(null, new object[] { proxyRuntimeType, targetRuntimeType, true });
            }
            catch
            {
                StopProfilePhase(
                    phaseStopwatch,
                    seconds =>
                    {
                        _currentProfile!.DynamicFailureProbeSeconds += seconds;
                        _currentProfile!.DynamicFailureProbeCount++;
                    });
                _currentExecutionContext?.CacheFailureProbe(cacheKey, exceptionType: null, exceptionMessage: null, succeeded: false);
                _currentExecutionContext?.CacheFailureProbeByFingerprint(fingerprintKey, exceptionType: null, exceptionMessage: null, succeeded: false);
                return false;
            }

            var result = TryExtractDynamicFailureExceptionType(dryRunResult, out exceptionType, out exceptionMessage);
            _currentExecutionContext?.CacheFailureProbe(cacheKey, exceptionType, exceptionMessage, result);
            _currentExecutionContext?.CacheFailureProbeByFingerprint(fingerprintKey, exceptionType, exceptionMessage, result);
            StopProfilePhase(
                phaseStopwatch,
                seconds =>
                {
                    _currentProfile!.DynamicFailureProbeSeconds += seconds;
                    _currentProfile!.DynamicFailureProbeCount++;
                });
            return result;
        }

        /// <summary>
        /// Attempts to extract the captured failure exception type from DuckType.CreateTypeResult.
        /// </summary>
        /// <param name="dryRunResult">The dry run result value.</param>
        /// <param name="exceptionType">The exception type value.</param>
        /// <param name="exceptionMessage">The exception message value.</param>
        /// <returns>true when a failure exception type was extracted; otherwise, false.</returns>
        private static bool TryExtractDynamicFailureExceptionType(object? dryRunResult, out Type? exceptionType, out string? exceptionMessage)
        {
            exceptionType = null;
            exceptionMessage = null;
            if (dryRunResult is null ||
                CreateTypeResultCanCreateMethod is null ||
                CreateTypeResultProxyTypeProperty is null)
            {
                return false;
            }

            bool canCreate;
            try
            {
                canCreate = CreateTypeResultCanCreateMethod.Invoke(dryRunResult, null) is bool canCreateValue && canCreateValue;
            }
            catch
            {
                return false;
            }

            if (canCreate)
            {
                return false;
            }

            try
            {
                _ = CreateTypeResultProxyTypeProperty.GetValue(dryRunResult, null);
                return false;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                exceptionType = ex.InnerException.GetType();
                exceptionMessage = ex.InnerException.Message;
                return true;
            }
            catch (Exception ex)
            {
                exceptionType = ex.GetType();
                exceptionMessage = ex.Message;
                return true;
            }
        }

        /// <summary>
        /// Attempts to resolve a known failure exception type from an emission result.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="failure">The failure value.</param>
        /// <param name="exceptionType">The exception type value.</param>
        /// <returns>true when a known exception type was resolved; otherwise, false.</returns>
        private static bool TryResolveStaticExactFailureTypeName(
            DuckTypeAotMappingEmissionResult failure,
            out string? exceptionTypeName)
        {
            exceptionTypeName = null;
            var detail = failure.Detail ?? string.Empty;

            if (string.Equals(failure.DiagnosticCode, StatusCodeUnsupportedProxyKind, StringComparison.Ordinal) &&
                detail.IndexOf("Reverse proxy type", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeReverseProxyBaseIsStructException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodePropertyCantBeWritten, StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypePropertyCantBeWrittenException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeCustomAttributeNamedArguments, StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypeCustomAttributeHasNamedArgumentsException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeUnsupportedProxyConstructor, StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypeException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("cannot be abstract or interface", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("marked with [DuckReverseMethod]", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = detail.IndexOf("Proxy property", StringComparison.Ordinal) >= 0
                                        ? typeof(DuckTypeIncorrectReversePropertyUsageException).FullName
                                        : typeof(DuckTypeIncorrectReverseMethodUsageException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("belongs to value type", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeStructMembersCannotBeChangedException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("does not expose any writable public fields", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeDuckCopyStructDoesNotContainsAnyField).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("Ambiguous target method match", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeTargetMethodAmbiguousMatchException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeFieldIsReadonly, StringComparison.Ordinal) ||
                (string.Equals(failure.DiagnosticCode, StatusCodeIncompatibleSignature, StringComparison.Ordinal) &&
                 IsReadonlyFieldFailure(failure.Detail ?? string.Empty)))
            {
                exceptionTypeName = typeof(DuckTypeFieldIsReadonlyException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeIncompatibleSignature, StringComparison.Ordinal))
            {
                if (IsParameterSignatureFailure(detail))
                {
                    exceptionTypeName = typeof(DuckTypeProxyAndTargetMethodParameterSignatureMismatchException).FullName;
                    return exceptionTypeName is not null;
                }
            }

            return false;
        }

        private static bool TryResolveBroadFailureTypeName(
            DuckTypeAotMappingEmissionResult failure,
            out string? exceptionTypeName)
        {
            exceptionTypeName = null;
            var detail = failure.Detail ?? string.Empty;

            if (string.Equals(failure.DiagnosticCode, StatusCodeMissingMethod, StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypeTargetMethodNotFoundException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeIncompatibleSignature, StringComparison.Ordinal))
            {
                if (IsReturnTypeFailure(detail))
                {
                    exceptionTypeName = typeof(DuckTypeProxyAndTargetMethodReturnTypeMismatchException).FullName;
                    return exceptionTypeName is not null;
                }

                if (IsInvalidTypeConversionFailure(detail))
                {
                    exceptionTypeName = typeof(DuckTypeInvalidTypeConversionException).FullName;
                    return exceptionTypeName is not null;
                }
            }

            return false;
        }

        private static string BuildFailureProbeFingerprint(DuckTypeAotMappingMode mode, DuckTypeAotMappingEmissionResult failure)
        {
            return string.Concat(
                mode.ToString(),
                "|",
                failure.Status ?? string.Empty,
                "|",
                failure.DiagnosticCode ?? string.Empty,
                "|",
                failure.Detail ?? string.Empty);
        }

        /// <summary>
        /// Emits mapping.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name value.</param>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <returns>The result produced by this operation.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static DuckTypeAotMappingEmissionResult EmitMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string> emissionWarnings)
        {
            var isReverseMapping = mapping.Mode == DuckTypeAotMappingMode.Reverse;
            // Closed-generic mappings use a stricter emission path so their compatibility outcomes stay deterministic.
            if (DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.ProxyTypeName) ||
                DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.TargetTypeName))
            {
                return EmitClosedGenericMapping(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    isReverseMapping,
                    proxyModulesByAssemblyName,
                    targetModulesByAssemblyName,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
            }

            // Early resolution failures are emitted as explicit matrix diagnostics instead of partial proxy generation.
            if (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
            }

            if (!targetModulesByAssemblyName.TryGetValue(mapping.TargetAssemblyName, out var targetModule))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target assembly '{mapping.TargetAssemblyName}' was not loaded.");
            }

            if (!TryResolveType(proxyModule, mapping.ProxyTypeName, out var proxyType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy type '{mapping.ProxyTypeName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            if (!TryResolveType(targetModule, mapping.TargetTypeName, out var targetType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            // Value-type proxy definitions are DuckCopy projections and must follow the dedicated struct-copy emitter.
            if (proxyType.IsValueType)
            {
                if (isReverseMapping)
                {
                    var reverseValueTypeFailure = CreateFailureResult(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                        StatusCodeUnsupportedProxyKind,
                        $"Reverse proxy type '{mapping.ProxyTypeName}' is not supported when the proxy definition is a value type.");

                    TryEmitKnownFailureRegistration(
                        moduleDef,
                        bootstrapType,
                        initializeMethod,
                        importedMembers,
                        mapping,
                        mappingIndex,
                        proxyType,
                        targetType,
                        reverseValueTypeFailure,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings);

                    return reverseValueTypeFailure;
                }

                var structCopyResult = EmitStructCopyMapping(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType);

                if (!string.Equals(structCopyResult.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal))
                {
                    TryEmitKnownFailureRegistration(
                        moduleDef,
                        bootstrapType,
                        initializeMethod,
                        importedMembers,
                        mapping,
                        mappingIndex,
                        proxyType,
                        targetType,
                        structCopyResult,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings);
                }

                return structCopyResult;
            }

            // Only interface/class proxy definitions are supported here; value-type proxies are handled by DuckCopy flow.
            if (!proxyType.IsInterface && !proxyType.IsClass)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnsupportedProxyKind,
                    $"Proxy type '{mapping.ProxyTypeName}' is not supported. Only interface, class, and DuckCopy struct proxies are emitted in this phase.");
            }

            if (isReverseMapping && (targetType.IsInterface || targetType.IsAbstract))
            {
                var reverseProxyImplementorFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                    StatusCodeIncompatibleSignature,
                    $"Reverse proxy implementor type '{mapping.TargetTypeName}' cannot be abstract or interface.");
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    reverseProxyImplementorFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
                return reverseProxyImplementorFailure;
            }

            var isInterfaceProxy = proxyType.IsInterface;
            var isDuckAsClassInterface = isInterfaceProxy && HasDuckAsClassAttribute(proxyType);
            // This branch controls default allocation behavior for all interface mappings:
            // struct by default (parity/perf), class only when explicitly requested via [DuckAsClass].
            var emitInterfaceStructProxy = isInterfaceProxy && !isDuckAsClassInterface;
            var planningStopwatch = StartProfilePhase();
            if (!TryCollectForwardBindings(mapping, proxyType, targetType, closedGenericProxyTypeArguments: null, closedGenericTargetTypeArguments: null, isInterfaceProxy, out var bindings, out var failure))
            {
                StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    failure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
                return failure!;
            }

            StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);

            IMethod baseCtorToCall = importedMembers.ObjectCtor;
            // Class-based proxy contracts keep inheritance semantics by requiring a callable parameterless base ctor.
            if (!isInterfaceProxy)
            {
                var baseConstructor = FindSupportedProxyBaseConstructor(proxyType);
                if (baseConstructor is null)
                {
                    var constructorFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedProxyConstructor,
                        StatusCodeUnsupportedProxyConstructor,
                        $"Proxy class '{mapping.ProxyTypeName}' must provide a parameterless constructor.");
                    TryEmitKnownFailureRegistration(
                        moduleDef,
                        bootstrapType,
                        initializeMethod,
                        importedMembers,
                        mapping,
                        mappingIndex,
                        proxyType,
                        targetType,
                        constructorFailure,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings);
                    return constructorFailure;
                }

                baseCtorToCall = ImportMethodCached(moduleDef, baseConstructor, $"base constructor for proxy '{proxyType.FullName}'");
            }

            var generatedTypeName = $"DuckTypeProxy_{mappingIndex:D4}_{ComputeStableShortHash(mapping.Key)}";
            var generatedParentType = emitInterfaceStructProxy ? moduleDef.CorLibTypes.GetTypeRef("System", "ValueType") : (isInterfaceProxy ? moduleDef.CorLibTypes.Object.TypeDefOrRef : ImportTypeDefOrRefCached(moduleDef, proxyType, $"generated parent type for proxy '{proxyType.FullName}'"));
            var generatedTypeAttributes = emitInterfaceStructProxy
                                              ? TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable
                                              : TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed;
            var generatedType = new TypeDefUser(
                GeneratedProxyNamespace,
                generatedTypeName,
                generatedParentType)
            {
                Attributes = generatedTypeAttributes
            };
            // Interface proxies implement the user contract directly.
            if (isInterfaceProxy)
            {
                generatedType.Interfaces.Add(new InterfaceImplUser(ImportTypeDefOrRefCached(moduleDef, proxyType, $"interface proxy contract '{proxyType.FullName}'")));
            }

            generatedType.Interfaces.Add(new InterfaceImplUser(importedMembers.IDuckTypeType));
            moduleDef.Types.Add(generatedType);

            if (isReverseMapping &&
                !TryApplyReverseTargetCustomAttributes(moduleDef, generatedType, targetType, mapping, out var reverseCustomAttributeFailure))
            {
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    reverseCustomAttributeFailure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
                return reverseCustomAttributeFailure!;
            }

            var importedTargetType = ImportTypeDefOrRefCached(moduleDef, targetType, $"target type '{targetType.FullName}'");
            var importedTargetTypeSig = ImportTypeSigCached(moduleDef, targetType.ToTypeSig(), $"target type signature '{targetType.FullName}'");
            var targetField = new FieldDefUser("_instance", new FieldSig(importedTargetTypeSig), FieldAttributes.Private | FieldAttributes.InitOnly);
            generatedType.Fields.Add(targetField);

            var generatedConstructor = new MethodDefUser(
                ".ctor",
                MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            generatedConstructor.Body = new CilBody();
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(targetField));
            // Class proxy base constructors can call virtual members, so store the target first to match dynamic ducktyping.
            if (!emitInterfaceStructProxy)
            {
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseCtorToCall));
            }

            generatedConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedType.Methods.Add(generatedConstructor);

            EmitIDuckTypeImplementation(moduleDef, generatedType, importedTargetType, targetField, importedMembers, targetType.IsValueType);
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyType);
            var generatedInterfaceProperties = new Dictionary<string, PropertyDef>(StringComparer.Ordinal);
            var generatedInterfaceImplementations = new HashSet<string>(StringComparer.Ordinal)
            {
                BuildTypeDefOrRefIdentityKey(proxyType)
            };

            foreach (var binding in bindings!)
            {
                var proxyMethod = binding.ProxyMethod;
                var interfaceMethodContract = isInterfaceProxy ? proxyTypePlan?.GetInterfaceMethodContract(proxyMethod) : null;

                var generatedMethod = new MethodDefUser(
                    proxyMethod.Name,
                    CreateGeneratedProxyMethodSig(moduleDef, proxyMethod, closedGenericProxyTypeArguments: null, interfaceMethodContract),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    isInterfaceProxy ? GetInterfaceMethodAttributes(proxyMethod) : GetClassOverrideMethodAttributes(proxyMethod));

                CopyMethodGenericParameters(moduleDef, proxyMethod, generatedMethod);
                AddInterfaceMethodOverride(moduleDef, generatedType, generatedInterfaceImplementations, isInterfaceProxy, proxyType, generatedMethod, proxyMethod, interfaceMethodContract, closedGenericProxyTypeArguments: null);
                generatedMethod.Body = new CilBody();
                switch (binding.Kind)
                {
                    case ForwardBindingKind.Method:
                    {
                        var branchStopwatch = StartProfilePhase();
                        try
                        {
                            var targetMethod = binding.TargetMethod!;
                            var methodBinding = binding.MethodBinding!.Value;
                            var byRefWriteBacks = new List<ByRefWriteBackPlan>();
                            if (!targetMethod.IsStatic)
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                generatedMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                            }

                            for (var parameterIndex = 0; parameterIndex < proxyMethod.MethodSig.Params.Count; parameterIndex++)
                            {
                                var parameterBinding = methodBinding.ParameterBindings[parameterIndex];
                                var proxyParameter = generatedMethod.Parameters[parameterIndex + 1];
                                if (parameterBinding.IsByRef && parameterBinding.UseLocalForByRef)
                                {
                                    var targetElementTypeSig = ImportTypeSigCached(moduleDef, parameterBinding.TargetByRefElementTypeSig!, $"target by-ref element type for '{targetMethod.FullName}'");
                                    var targetByRefLocal = new Local(targetElementTypeSig);
                                    generatedMethod.Body.Variables.Add(targetByRefLocal);
                                    generatedMethod.Body.InitLocals = true;

                                    if (!parameterBinding.IsOut)
                                    {
                                        generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxyParameter));
                                        EmitLoadByRefValue(moduleDef, generatedMethod.Body, parameterBinding.ProxyByRefElementTypeSig!, $"proxy parameter '{proxyMethod.FullName}'");
                                        EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'", preserveNullForDuckTypeExtraction: true);
                                        generatedMethod.Body.Instructions.Add(OpCodes.Stloc.ToInstruction(targetByRefLocal));
                                    }

                                    generatedMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(targetByRefLocal));
                                    byRefWriteBacks.Add(new ByRefWriteBackPlan(proxyParameter, targetByRefLocal, parameterBinding));
                                }
                                else
                                {
                                    generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxyParameter));
                                    if (!parameterBinding.IsByRef)
                                    {
                                        EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'", preserveNullForDuckTypeExtraction: false);
                                    }
                                }
                            }

                            EmitTrailingOptionalTargetArguments(
                                moduleDef,
                                generatedMethod.Body,
                                targetMethod,
                                methodBinding,
                                closedGenericTargetTypeArguments: null);

                            var importedTargetMethod = ImportMethodDefOrRefCached(moduleDef, targetMethod, $"target method '{targetMethod.FullName}'");
                            var targetMethodToCall = CreateMethodCallTarget(
                                moduleDef,
                                importedTargetMethod,
                                importedTargetType,
                                generatedMethod,
                                methodBinding.ClosedGenericMethodArguments);
                            if (!targetMethod.IsStatic && targetType.IsValueType && (targetMethod.IsVirtual || targetMethod.DeclaringType.IsInterface))
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                                generatedMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(targetMethodToCall));
                            }
                            else
                            {
                                var targetCallOpcode = targetMethod.IsStatic ? OpCodes.Call : (targetMethod.IsVirtual || targetMethod.DeclaringType.IsInterface ? OpCodes.Callvirt : OpCodes.Call);
                                generatedMethod.Body.Instructions.Add(targetCallOpcode.ToInstruction(targetMethodToCall));
                            }

                            foreach (var byRefWriteBack in byRefWriteBacks)
                            {
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, byRefWriteBack.ProxyParameter));
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldloc.ToInstruction(byRefWriteBack.TargetLocal));
                                EmitMethodReturnConversion(moduleDef, generatedMethod.Body, byRefWriteBack.ParameterBinding.PostCallConversion, importedMembers, $"proxy parameter '{proxyMethod.FullName}'");
                                EmitStoreByRefValue(moduleDef, generatedMethod.Body, byRefWriteBack.ParameterBinding.ProxyByRefElementTypeSig!, $"proxy parameter '{proxyMethod.FullName}'");
                            }

                            EmitMethodReturnConversion(moduleDef, generatedMethod.Body, methodBinding.ReturnConversion, importedMembers, $"target method '{targetMethod.FullName}'");
                        }
                        finally
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.EmitForwardMethodBodyCount++;
                            }

                            StopProfilePhase(branchStopwatch, seconds => _currentProfile!.EmitForwardMethodBodySeconds += seconds);
                        }

                        break;
                    }

                    case ForwardBindingKind.FieldGet:
                    {
                        var branchStopwatch = StartProfilePhase();
                        try
                        {
                            var fieldBinding = binding.FieldBinding!.Value;
                            var importedTargetMemberField = ImportFieldCached(moduleDef, binding.TargetField!, $"target field '{binding.TargetField!.FullName}'");
                            if (binding.TargetField!.IsStatic)
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(importedTargetMemberField));
                            }
                            else
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                generatedMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(importedTargetMemberField));
                            }

                            EmitMethodReturnConversion(moduleDef, generatedMethod.Body, fieldBinding.ReturnConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");
                        }
                        finally
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.EmitForwardFieldGetBodyCount++;
                            }

                            StopProfilePhase(branchStopwatch, seconds => _currentProfile!.EmitForwardFieldGetBodySeconds += seconds);
                        }

                        break;
                    }

                    case ForwardBindingKind.FieldSet:
                    {
                        var branchStopwatch = StartProfilePhase();
                        try
                        {
                            var fieldBinding = binding.FieldBinding!.Value;
                            var importedTargetMemberField = ImportFieldCached(moduleDef, binding.TargetField!, $"target field '{binding.TargetField!.FullName}'");
                            if (binding.TargetField!.IsStatic)
                            {
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                                EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'", preserveNullForDuckTypeExtraction: false);

                                generatedMethod.Body.Instructions.Add(OpCodes.Stsfld.ToInstruction(importedTargetMemberField));
                            }
                            else
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                generatedMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                                EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'", preserveNullForDuckTypeExtraction: false);

                                generatedMethod.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(importedTargetMemberField));
                            }
                        }
                        finally
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.EmitForwardFieldSetBodyCount++;
                            }

                            StopProfilePhase(branchStopwatch, seconds => _currentProfile!.EmitForwardFieldSetBodySeconds += seconds);
                        }

                        break;
                    }

                    default:
                        throw new InvalidOperationException($"Unsupported forward binding kind '{binding.Kind}'.");
                }

                generatedMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(generatedMethod);
                EnsureInterfacePropertyMetadata(moduleDef, generatedType, proxyMethod, generatedMethod, closedGenericProxyTypeArguments: null, generatedInterfaceProperties);
            }

            var importedProxyTypeSig = ImportTypeSigCached(moduleDef, proxyType.ToTypeSig(), $"proxy type signature '{proxyType.FullName}'");
            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(importedProxyTypeSig, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            activatorMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(generatedConstructor));
            // Returning a struct proxy through an interface contract requires boxing at this boundary.
            if (emitInterfaceStructProxy)
            {
                activatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(generatedType));
            }

            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            var registrationActivatorMethod = new MethodDefUser(
                $"ActivateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            // Bootstrap registration stays object-based while preserving a typed activator method for normal execution paths.
            registrationActivatorMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            if (proxyType.IsValueType)
            {
                registrationActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(ImportTypeDefOrRefCached(moduleDef, proxyType, $"proxy type '{proxyType.FullName}'")));
            }

            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(ImportTypeDefOrRefCached(moduleDef, proxyType, $"alias proxy type '{proxyType.FullName}'")));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            // Registration must preserve the generated implementation type because runtime duplicate detection uses it.
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitFuncObjectObjectDelegate(initializeMethod.Body, importedMembers, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(isReverseMapping ? importedMembers.RegisterAotReverseProxyMethod : importedMembers.RegisterAotProxyMethod));

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                moduleDef.Assembly?.Name?.String ?? string.Empty,
                generatedType.FullName);
        }

        /// <summary>
        /// Emits mapping for already-resolved proxy/target types.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="importedTargetType">The imported target type value.</param>
        /// <param name="importedTargetTypeSig">The imported target type sig value.</param>
        /// <param name="targetIsValueType">The target is value type value.</param>
        /// <param name="closedGenericTargetTypeArguments">The closed generic target type arguments value.</param>
        /// <param name="importedProxyContractType">The imported closed proxy contract type value.</param>
        /// <param name="importedProxyContractTypeSig">The imported closed proxy contract type signature value.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed generic proxy type arguments value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingEmissionResult EmitResolvedTypeMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            bool isReverseMapping,
            TypeDef proxyType,
            TypeDef targetType,
            ITypeDefOrRef importedTargetType,
            TypeSig importedTargetTypeSig,
            bool targetIsValueType,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            ITypeDefOrRef? importedProxyContractType,
            TypeSig? importedProxyContractTypeSig,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string> emissionWarnings)
        {
            // Value-type proxy definitions are DuckCopy projections and must follow the dedicated struct-copy emitter.
            if (proxyType.IsValueType)
            {
                if (isReverseMapping)
                {
                    var reverseValueTypeFailure = CreateFailureResult(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                        StatusCodeUnsupportedProxyKind,
                        $"Reverse proxy type '{mapping.ProxyTypeName}' is not supported when the proxy definition is a value type.");

                    TryEmitKnownFailureRegistration(
                        moduleDef,
                        bootstrapType,
                        initializeMethod,
                        importedMembers,
                        mapping,
                        mappingIndex,
                        proxyType,
                        importedTargetType,
                        reverseValueTypeFailure,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings);

                    return reverseValueTypeFailure;
                }

                var structCopyResult = EmitStructCopyMapping(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    importedTargetType,
                    importedTargetTypeSig,
                    targetIsValueType,
                    closedGenericTargetTypeArguments);

                if (!string.Equals(structCopyResult.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal))
                {
                    TryEmitKnownFailureRegistration(
                        moduleDef,
                        bootstrapType,
                        initializeMethod,
                        importedMembers,
                        mapping,
                        mappingIndex,
                        proxyType,
                        importedTargetType,
                        structCopyResult,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings);
                }

                return structCopyResult;
            }

            // Only interface/class proxy definitions are supported here; value-type proxies are handled by DuckCopy flow.
            if (!proxyType.IsInterface && !proxyType.IsClass)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnsupportedProxyKind,
                    $"Proxy type '{mapping.ProxyTypeName}' is not supported. Only interface, class, and DuckCopy struct proxies are emitted in this phase.");
            }

            if (isReverseMapping && (targetType.IsInterface || targetType.IsAbstract))
            {
                var reverseProxyImplementorFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                    StatusCodeIncompatibleSignature,
                    $"Reverse proxy implementor type '{mapping.TargetTypeName}' cannot be abstract or interface.");
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    importedTargetType,
                    reverseProxyImplementorFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
                return reverseProxyImplementorFailure;
            }

            var isInterfaceProxy = proxyType.IsInterface;
            var isDuckAsClassInterface = isInterfaceProxy && HasDuckAsClassAttribute(proxyType);
            // This branch controls default allocation behavior for all interface mappings:
            // struct by default (parity/perf), class only when explicitly requested via [DuckAsClass].
            var emitInterfaceStructProxy = isInterfaceProxy && !isDuckAsClassInterface;
            var planningStopwatch = StartProfilePhase();
            if (!TryCollectForwardBindings(mapping, proxyType, targetType, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, isInterfaceProxy, out var bindings, out var failure))
            {
                StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    importedTargetType,
                    failure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
                return failure!;
            }

            StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);

            IMethod baseCtorToCall = importedMembers.ObjectCtor;
            // Class-based proxy contracts keep inheritance semantics by requiring a callable parameterless base ctor.
            if (!isInterfaceProxy)
            {
                var baseConstructor = FindSupportedProxyBaseConstructor(proxyType);
                if (baseConstructor is null)
                {
                    var constructorFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedProxyConstructor,
                        StatusCodeUnsupportedProxyConstructor,
                        $"Proxy class '{mapping.ProxyTypeName}' must provide a parameterless constructor.");
                    TryEmitKnownFailureRegistration(
                        moduleDef,
                        bootstrapType,
                        initializeMethod,
                        importedMembers,
                        mapping,
                        mappingIndex,
                        proxyType,
                        importedTargetType,
                        constructorFailure,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings);
                    return constructorFailure;
                }

                baseCtorToCall = ImportMethodCached(moduleDef, baseConstructor, $"base constructor for proxy '{proxyType.FullName}'");
            }

            var generatedTypeName = $"DuckTypeProxy_{mappingIndex:D4}_{ComputeStableShortHash(mapping.Key)}";
            var resolvedProxyContractType = importedProxyContractType ?? ImportTypeDefOrRefCached(moduleDef, proxyType, $"proxy contract type '{proxyType.FullName}'");
            var resolvedProxyContractTypeSig = importedProxyContractTypeSig ?? ImportTypeSigCached(moduleDef, proxyType.ToTypeSig(), $"proxy type signature '{proxyType.FullName}'");
            if (!isInterfaceProxy &&
                baseCtorToCall.DeclaringType is ITypeDefOrRef baseConstructorDeclaringType &&
                !string.Equals(baseConstructorDeclaringType.FullName, resolvedProxyContractType.FullName, StringComparison.Ordinal))
            {
                baseCtorToCall = moduleDef.UpdateRowId(new MemberRefUser(
                    moduleDef,
                    baseCtorToCall.Name,
                    baseCtorToCall.MethodSig,
                    resolvedProxyContractType));
            }

            var generatedParentType = emitInterfaceStructProxy ? moduleDef.CorLibTypes.GetTypeRef("System", "ValueType") : (isInterfaceProxy ? moduleDef.CorLibTypes.Object.TypeDefOrRef : resolvedProxyContractType);
            var generatedTypeAttributes = emitInterfaceStructProxy
                                              ? TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable
                                              : TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed;
            var generatedType = new TypeDefUser(
                GeneratedProxyNamespace,
                generatedTypeName,
                generatedParentType)
            {
                Attributes = generatedTypeAttributes
            };

            // Interface proxies implement the user contract directly.
            if (isInterfaceProxy)
            {
                generatedType.Interfaces.Add(new InterfaceImplUser(resolvedProxyContractType));
            }

            generatedType.Interfaces.Add(new InterfaceImplUser(importedMembers.IDuckTypeType));
            moduleDef.Types.Add(generatedType);

            if (isReverseMapping &&
                !TryApplyReverseTargetCustomAttributes(moduleDef, generatedType, targetType, mapping, out var reverseCustomAttributeFailure))
            {
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    importedTargetType,
                    reverseCustomAttributeFailure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
                return reverseCustomAttributeFailure!;
            }

            var targetField = new FieldDefUser("_instance", new FieldSig(importedTargetTypeSig), FieldAttributes.Private | FieldAttributes.InitOnly);
            generatedType.Fields.Add(targetField);

            var generatedConstructor = new MethodDefUser(
                ".ctor",
                MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            generatedConstructor.Body = new CilBody();

            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(targetField));
            // Class proxy base constructors can call virtual members, so store the target first to match dynamic ducktyping.
            if (!emitInterfaceStructProxy)
            {
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseCtorToCall));
            }

            generatedConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedType.Methods.Add(generatedConstructor);

            EmitIDuckTypeImplementation(moduleDef, generatedType, importedTargetType, targetField, importedMembers, targetIsValueType);
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyType);
            var generatedInterfaceProperties = new Dictionary<string, PropertyDef>(StringComparer.Ordinal);
            var generatedInterfaceImplementations = new HashSet<string>(StringComparer.Ordinal)
            {
                BuildTypeDefOrRefIdentityKey(proxyType)
            };

            foreach (var binding in bindings!)
            {
                var proxyMethod = binding.ProxyMethod;
                var interfaceMethodContract = isInterfaceProxy ? proxyTypePlan?.GetInterfaceMethodContract(proxyMethod) : null;

                var generatedMethod = new MethodDefUser(
                    proxyMethod.Name,
                    CreateGeneratedProxyMethodSig(moduleDef, proxyMethod, closedGenericProxyTypeArguments, interfaceMethodContract),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    isInterfaceProxy ? GetInterfaceMethodAttributes(proxyMethod) : GetClassOverrideMethodAttributes(proxyMethod));

                CopyMethodGenericParameters(moduleDef, proxyMethod, generatedMethod);
                AddInterfaceMethodOverride(moduleDef, generatedType, generatedInterfaceImplementations, isInterfaceProxy, proxyType, generatedMethod, proxyMethod, interfaceMethodContract, closedGenericProxyTypeArguments);
                generatedMethod.Body = new CilBody();
                switch (binding.Kind)
                {
                    case ForwardBindingKind.Method:
                    {
                        var branchStopwatch = StartProfilePhase();
                        try
                        {
                            var targetMethod = binding.TargetMethod!;
                            var methodBinding = binding.MethodBinding!.Value;
                            var byRefWriteBacks = new List<ByRefWriteBackPlan>();
                            if (!targetMethod.IsStatic)
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                generatedMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                            }

                            for (var parameterIndex = 0; parameterIndex < proxyMethod.MethodSig.Params.Count; parameterIndex++)
                            {
                                var parameterBinding = methodBinding.ParameterBindings[parameterIndex];
                                var proxyParameter = generatedMethod.Parameters[parameterIndex + 1];
                                if (parameterBinding.IsByRef && parameterBinding.UseLocalForByRef)
                                {
                                    var targetElementTypeSig = ImportTypeSigCached(moduleDef, parameterBinding.TargetByRefElementTypeSig!, $"target by-ref element type for '{targetMethod.FullName}'");
                                    var targetByRefLocal = new Local(targetElementTypeSig);
                                    generatedMethod.Body.Variables.Add(targetByRefLocal);
                                    generatedMethod.Body.InitLocals = true;

                                    if (!parameterBinding.IsOut)
                                    {
                                        generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxyParameter));
                                        EmitLoadByRefValue(moduleDef, generatedMethod.Body, parameterBinding.ProxyByRefElementTypeSig!, $"proxy parameter '{proxyMethod.FullName}'");
                                        EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'", preserveNullForDuckTypeExtraction: true);
                                        generatedMethod.Body.Instructions.Add(OpCodes.Stloc.ToInstruction(targetByRefLocal));
                                    }

                                    generatedMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(targetByRefLocal));
                                    byRefWriteBacks.Add(new ByRefWriteBackPlan(proxyParameter, targetByRefLocal, parameterBinding));
                                }
                                else
                                {
                                    generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxyParameter));
                                    if (!parameterBinding.IsByRef)
                                    {
                                        EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'", preserveNullForDuckTypeExtraction: false);
                                    }
                                }
                            }

                            EmitTrailingOptionalTargetArguments(
                                moduleDef,
                                generatedMethod.Body,
                                targetMethod,
                                methodBinding,
                                closedGenericTargetTypeArguments);

                            var importedTargetMethod = ImportMethodDefOrRefCached(moduleDef, targetMethod, $"target method '{targetMethod.FullName}'");
                            var targetMethodToCall = CreateMethodCallTarget(
                                moduleDef,
                                importedTargetMethod,
                                importedTargetType,
                                generatedMethod,
                                methodBinding.ClosedGenericMethodArguments);
                            if (!targetMethod.IsStatic && targetIsValueType && (targetMethod.IsVirtual || targetMethod.DeclaringType.IsInterface))
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                                generatedMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(targetMethodToCall));
                            }
                            else
                            {
                                var targetCallOpcode = targetMethod.IsStatic ? OpCodes.Call : (targetMethod.IsVirtual || targetMethod.DeclaringType.IsInterface ? OpCodes.Callvirt : OpCodes.Call);
                                generatedMethod.Body.Instructions.Add(targetCallOpcode.ToInstruction(targetMethodToCall));
                            }

                            foreach (var byRefWriteBack in byRefWriteBacks)
                            {
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, byRefWriteBack.ProxyParameter));
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldloc.ToInstruction(byRefWriteBack.TargetLocal));
                                EmitMethodReturnConversion(moduleDef, generatedMethod.Body, byRefWriteBack.ParameterBinding.PostCallConversion, importedMembers, $"proxy parameter '{proxyMethod.FullName}'");
                                EmitStoreByRefValue(moduleDef, generatedMethod.Body, byRefWriteBack.ParameterBinding.ProxyByRefElementTypeSig!, $"proxy parameter '{proxyMethod.FullName}'");
                            }

                            EmitMethodReturnConversion(moduleDef, generatedMethod.Body, methodBinding.ReturnConversion, importedMembers, $"target method '{targetMethod.FullName}'");
                        }
                        finally
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.EmitForwardMethodBodyCount++;
                            }

                            StopProfilePhase(branchStopwatch, seconds => _currentProfile!.EmitForwardMethodBodySeconds += seconds);
                        }

                        break;
                    }

                    case ForwardBindingKind.FieldGet:
                    {
                        var branchStopwatch = StartProfilePhase();
                        try
                        {
                            var fieldBinding = binding.FieldBinding!.Value;
                            var importedTargetMemberField = ImportFieldCached(moduleDef, binding.TargetField!, $"target field '{binding.TargetField!.FullName}'");
                            if (binding.TargetField!.IsStatic)
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(importedTargetMemberField));
                            }
                            else
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                generatedMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(importedTargetMemberField));
                            }

                            EmitMethodReturnConversion(moduleDef, generatedMethod.Body, fieldBinding.ReturnConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");
                        }
                        finally
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.EmitForwardFieldGetBodyCount++;
                            }

                            StopProfilePhase(branchStopwatch, seconds => _currentProfile!.EmitForwardFieldGetBodySeconds += seconds);
                        }

                        break;
                    }

                    case ForwardBindingKind.FieldSet:
                    {
                        var branchStopwatch = StartProfilePhase();
                        try
                        {
                            var fieldBinding = binding.FieldBinding!.Value;
                            var importedTargetMemberField = ImportFieldCached(moduleDef, binding.TargetField!, $"target field '{binding.TargetField!.FullName}'");
                            if (binding.TargetField!.IsStatic)
                            {
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                                EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'", preserveNullForDuckTypeExtraction: false);
                                generatedMethod.Body.Instructions.Add(OpCodes.Stsfld.ToInstruction(importedTargetMemberField));
                            }
                            else
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                generatedMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                                EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'", preserveNullForDuckTypeExtraction: false);
                                generatedMethod.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(importedTargetMemberField));
                            }
                        }
                        finally
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.EmitForwardFieldSetBodyCount++;
                            }

                            StopProfilePhase(branchStopwatch, seconds => _currentProfile!.EmitForwardFieldSetBodySeconds += seconds);
                        }

                        break;
                    }

                    default:
                        throw new InvalidOperationException($"Unsupported forward binding kind '{binding.Kind}'.");
                }

                generatedMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(generatedMethod);
                EnsureInterfacePropertyMetadata(moduleDef, generatedType, proxyMethod, generatedMethod, closedGenericProxyTypeArguments, generatedInterfaceProperties);
            }

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(resolvedProxyContractTypeSig, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            activatorMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(generatedConstructor));
            // Returning a struct proxy through an interface contract requires boxing at this boundary.
            if (emitInterfaceStructProxy)
            {
                activatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(generatedType));
            }

            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            var registrationActivatorMethod = new MethodDefUser(
                $"ActivateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            // Bootstrap registration stays object-based while preserving a typed activator method for normal execution paths.
            registrationActivatorMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            if (proxyType.IsValueType)
            {
                registrationActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(ImportTypeDefOrRefCached(moduleDef, proxyType, $"proxy type '{proxyType.FullName}'")));
            }

            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(resolvedProxyContractType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            // Registration must preserve the generated implementation type because runtime duplicate detection uses it.
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitFuncObjectObjectDelegate(initializeMethod.Body, importedMembers, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(isReverseMapping ? importedMembers.RegisterAotReverseProxyMethod : importedMembers.RegisterAotProxyMethod));

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                moduleDef.Assembly?.Name?.String ?? string.Empty,
                generatedType.FullName);
        }

        /// <summary>
        /// Emits closed generic mapping.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name value.</param>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <returns>The result produced by this operation.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static DuckTypeAotMappingEmissionResult EmitClosedGenericMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            bool isReverseMapping,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string> emissionWarnings)
        {
            if (!proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
            }

            if (!targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target assembly '{mapping.TargetAssemblyName}' was not loaded.");
            }

            if (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy closed generic type '{mapping.ProxyTypeName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            if (!TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target closed generic type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            if (!proxyRuntimeType!.IsAssignableFrom(targetRuntimeType))
            {
                if (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule))
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.MissingProxyType,
                        StatusCodeMissingProxyType,
                        $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
                }

                if (!targetModulesByAssemblyName.TryGetValue(mapping.TargetAssemblyName, out var targetModule))
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.MissingTargetType,
                        StatusCodeMissingTargetType,
                        $"Target assembly '{mapping.TargetAssemblyName}' was not loaded.");
                }

                var proxyTypeDefinitionName = proxyRuntimeType.IsGenericType
                                                  ? proxyRuntimeType.GetGenericTypeDefinition().FullName!
                                                  : mapping.ProxyTypeName;
                if (!TryResolveType(proxyModule, proxyTypeDefinitionName, out var proxyType))
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.MissingProxyType,
                        StatusCodeMissingProxyType,
                        $"Proxy type definition '{proxyTypeDefinitionName}' was not found in '{mapping.ProxyAssemblyName}'.");
                }

                var closedTargetRuntimeType = targetRuntimeType!;
                var targetRuntimeTypeDefinition = closedTargetRuntimeType.IsGenericType
                                                      ? closedTargetRuntimeType.GetGenericTypeDefinition()
                                                      : closedTargetRuntimeType;
                if (!TryResolveType(targetModule, targetRuntimeTypeDefinition.FullName!, out var targetType))
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.MissingTargetType,
                        StatusCodeMissingTargetType,
                        $"Target type definition '{targetRuntimeTypeDefinition.FullName}' was not found in '{mapping.TargetAssemblyName}'.");
                }

                var closedGenericTargetTypeArguments = closedTargetRuntimeType.IsGenericType
                                                           ? closedTargetRuntimeType.GetGenericArguments()
                                                                                  .Select(runtimeType => ImportRuntimeTypeSig(moduleDef, runtimeType))
                                                                                  .ToArray()
                                                           : null;
                var closedGenericProxyTypeArguments = proxyRuntimeType.IsGenericType
                                                          ? proxyRuntimeType.GetGenericArguments()
                                                                            .Select(runtimeType => ImportRuntimeTypeSig(moduleDef, runtimeType))
                                                                            .ToArray()
                                                          : null;

                var importedClosedTargetType = ImportRuntimeTypeCached(moduleDef, closedTargetRuntimeType, $"closed generic target type '{mapping.TargetTypeName}'");
                var importedClosedTargetTypeSig = ImportRuntimeTypeSig(moduleDef, closedTargetRuntimeType);
                var importedClosedProxyType = proxyRuntimeType.IsGenericType
                                                  ? ImportRuntimeTypeCached(moduleDef, proxyRuntimeType, $"closed generic proxy type '{mapping.ProxyTypeName}'")
                                                  : null;
                var importedClosedProxyTypeSig = proxyRuntimeType.IsGenericType
                                                     ? ImportRuntimeTypeSig(moduleDef, proxyRuntimeType)
                                                     : null;

                return EmitResolvedTypeMapping(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    isReverseMapping,
                    proxyType,
                    targetType,
                    importedClosedTargetType,
                    importedClosedTargetTypeSig,
                    closedTargetRuntimeType.IsValueType,
                    closedGenericTargetTypeArguments,
                    importedClosedProxyType,
                    importedClosedProxyTypeSig,
                    closedGenericProxyTypeArguments,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
            }

            var resolvedProxyRuntimeType = proxyRuntimeType!;
            var resolvedTargetRuntimeType = targetRuntimeType!;

            var importedProxyType = ImportRuntimeTypeCached(moduleDef, resolvedProxyRuntimeType, $"closed generic proxy type '{mapping.ProxyTypeName}'");
            var importedTargetType = ImportRuntimeTypeCached(moduleDef, resolvedTargetRuntimeType, $"closed generic target type '{mapping.TargetTypeName}'");
            var importedTargetTypeSig = ImportTypeSigCached(moduleDef, importedTargetType.ToTypeSig(), $"closed generic target signature '{mapping.TargetTypeName}'");

            var importedProxyTypeSig = ImportTypeSigCached(moduleDef, importedProxyType.ToTypeSig(), $"closed generic proxy signature '{mapping.ProxyTypeName}'");
            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(importedProxyTypeSig, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            EmitClosedGenericDirectCastActivation(activatorMethod.Body, importedProxyType, importedTargetType, resolvedProxyRuntimeType, resolvedTargetRuntimeType);
            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            var registrationActivatorMethod = new MethodDefUser(
                $"ActivateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            registrationActivatorMethod.Body.Instructions.Add((resolvedTargetRuntimeType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            if (resolvedProxyRuntimeType.IsValueType)
            {
                registrationActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
            }

            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitFuncObjectObjectDelegate(initializeMethod.Body, importedMembers, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(isReverseMapping ? importedMembers.RegisterAotReverseProxyMethod : importedMembers.RegisterAotProxyMethod));

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                mapping.ProxyAssemblyName,
                mapping.ProxyTypeName);
        }

        /// <summary>
        /// Emits closed generic direct cast activation.
        /// </summary>
        /// <param name="body">The body value.</param>
        /// <param name="importedProxyType">The imported proxy type value.</param>
        /// <param name="importedTargetType">The imported target type value.</param>
        /// <param name="proxyRuntimeType">The proxy runtime type value.</param>
        /// <param name="targetRuntimeType">The target runtime type value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitClosedGenericDirectCastActivation(
            CilBody body,
            ITypeDefOrRef importedProxyType,
            ITypeDefOrRef importedTargetType,
            Type proxyRuntimeType,
            Type targetRuntimeType)
        {
            body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());

            // Closed-generic direct activation is intentionally strict: only runtime-safe casts are emitted.
            if (targetRuntimeType.IsValueType)
            {
                if (proxyRuntimeType.IsValueType)
                {
                    // Reinterpreting between different value types is never emitted because it breaks parity and safety.
                    if (proxyRuntimeType != targetRuntimeType)
                    {
                        throw new InvalidOperationException(
                            $"Closed generic mapping cannot cast value type '{targetRuntimeType.FullName}' to '{proxyRuntimeType.FullName}'.");
                    }

                    return;
                }

                body.Instructions.Add(OpCodes.Box.ToInstruction(importedTargetType));
                body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedProxyType));
                return;
            }

            // Reference target to value-type proxy conversion is not a valid closed-generic adaptation.
            if (proxyRuntimeType.IsValueType)
            {
                throw new InvalidOperationException(
                    $"Closed generic mapping cannot cast reference type '{targetRuntimeType.FullName}' to value type '{proxyRuntimeType.FullName}'.");
            }

            if (proxyRuntimeType != targetRuntimeType)
            {
                // Emit cast only when needed to keep generated IL minimal and predictable.
                body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedProxyType));
            }
        }

        /// <summary>
        /// Imports runtime type as type sig.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static TypeSig ImportRuntimeTypeSig(ModuleDef moduleDef, Type runtimeType)
        {
            var importedTypeSig = moduleDef.ImportAsTypeSig(runtimeType);
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
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner executes as a build-time tool and intentionally resolves runtime metadata from discovered assemblies.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Type names come from explicit mapping metadata and assembly inspection performed by the tool at build time.")]
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
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "The ducktype AOT runner closes generic types from build-time metadata while emitting registry assemblies; this path does not execute inside the trimmed customer application.")]
#endif
        private static bool TryResolveClosedGenericRuntimeType(string assemblyName, string assemblyPath, string typeName, out Type? runtimeType)
        {
            runtimeType = null;
            if (!DuckTypeAotNameHelpers.IsClosedGenericTypeName(typeName) ||
                !TrySplitClosedGenericTypeName(typeName, out var genericTypeDefinitionName, out var genericArgumentTypeNames))
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

                if (string.IsNullOrWhiteSpace(normalizedGenericArgumentAssemblyName))
                {
                    normalizedGenericArgumentAssemblyName = normalizedRootAssemblyName;
                    genericArgumentAssemblyPath = assemblyPath;
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

            runtimeType = openGenericType.MakeGenericType(resolvedGenericArguments);
            return true;
        }

        /// <summary>
        /// Attempts to split a closed generic reflection name into its generic definition and top-level arguments.
        /// </summary>
        /// <param name="typeName">The closed generic type name value.</param>
        /// <param name="genericTypeDefinitionName">The generic type definition name value.</param>
        /// <param name="genericArgumentTypeNames">The top-level generic argument type names value.</param>
        /// <returns>true when the split succeeded; otherwise, false.</returns>
        private static bool TrySplitClosedGenericTypeName(
            string typeName,
            out string genericTypeDefinitionName,
            out IReadOnlyList<string> genericArgumentTypeNames)
        {
            genericTypeDefinitionName = string.Empty;
            genericArgumentTypeNames = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            var genericArgumentsStart = typeName.IndexOf("[[", StringComparison.Ordinal);
            if (genericArgumentsStart < 0)
            {
                return false;
            }

            var arguments = new List<string>();
            var bracketDepth = 0;
            var argumentStart = -1;

            for (var i = genericArgumentsStart; i < typeName.Length; i++)
            {
                var current = typeName[i];
                if (current == '[')
                {
                    bracketDepth++;
                    if (bracketDepth == 2)
                    {
                        argumentStart = i + 1;
                    }

                    continue;
                }

                if (current != ']')
                {
                    continue;
                }

                if (bracketDepth == 2 && argumentStart >= 0)
                {
                    arguments.Add(typeName.Substring(argumentStart, i - argumentStart));
                    argumentStart = -1;
                }

                bracketDepth--;
                if (bracketDepth == 0)
                {
                    genericTypeDefinitionName = typeName.Substring(0, genericArgumentsStart);
                    genericArgumentTypeNames = arguments;
                    return arguments.Count > 0;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to resolve preferred runtime assembly for metadata-to-runtime bridging.
        /// </summary>
        /// <param name="normalizedAssemblyName">The normalized assembly name value.</param>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>The resolved assembly when available; otherwise, null.</returns>
#if NET6_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner probes discovered assemblies on disk as a build-time tool; the standalone tool build is trimmed, but the emitted customer payload is not using this reflection path.")]
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
            if (!string.IsNullOrWhiteSpace(normalizedAssemblyPath) && File.Exists(normalizedAssemblyPath))
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

                _currentExecutionContext?.CachePreferredRuntimeAssembly(cacheKey, assembly: null);
                return null;
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
            if (string.IsNullOrWhiteSpace(assemblyPath))
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
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                return false;
            }

            try
            {
                var location = assembly.Location;
                return !string.IsNullOrWhiteSpace(location) &&
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
            return AssemblyLocationMatchesPath(assembly, assemblyPath) || AssemblyIdentityMatchesPath(assembly, assemblyPath);
        }

        /// <summary>
        /// Determines whether an already-loaded assembly has the same assembly identity and module version id as a file path.
        /// </summary>
        /// <param name="assembly">The assembly value.</param>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>true when the assembly identity and MVID match; otherwise, false.</returns>
        private static bool AssemblyIdentityMatchesPath(Assembly assembly, string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
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
            if (!string.IsNullOrWhiteSpace(assemblyPath))
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
            => AssemblyPathForwardsRuntimeType(
                NormalizeRuntimeAssemblyPathForCache(assemblyPath),
                runtimeType,
                runtimeTypeAssembly,
                new HashSet<string>(StringComparer.Ordinal));

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
            if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
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
            if (string.IsNullOrWhiteSpace(forwardedAssemblyName))
            {
                return false;
            }

            if (runtimeTypeResolutionAssemblyPathsByName is not null &&
                runtimeTypeResolutionAssemblyPathsByName.TryGetValue(forwardedAssemblyName, out var mappedAssemblyPath) &&
                !string.IsNullOrWhiteSpace(mappedAssemblyPath) &&
                File.Exists(mappedAssemblyPath))
            {
                forwardedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(mappedAssemblyPath);
                return true;
            }

            var currentDirectory = Path.GetDirectoryName(currentAssemblyPath);
            if (!string.IsNullOrWhiteSpace(currentDirectory))
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

            if (!string.IsNullOrWhiteSpace(normalizedAssemblyName))
            {
                var candidate = $"{typeName}, {normalizedAssemblyName}";
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            var candidateAssemblySimpleName = candidateAssembly?.GetName().Name;
            if (!string.IsNullOrWhiteSpace(candidateAssemblySimpleName))
            {
                var candidate = $"{typeName}, {candidateAssemblySimpleName}";
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            if (!string.IsNullOrWhiteSpace(candidateAssembly?.FullName))
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
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner resolves referenced assemblies from build outputs while generating metadata. This reflective probing is intentional and confined to the build-time tool.")]
#endif
        private static Assembly? ResolveRuntimeTypeAssembly(
            AssemblyName requestedAssemblyName,
            string normalizedAssemblyName,
            string assemblyPath,
            Assembly? candidateAssembly)
        {
            var requestedSimpleName = DuckTypeAotNameHelpers.NormalizeAssemblyName(requestedAssemblyName.Name ?? string.Empty);
            var normalizedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(assemblyPath);
            if (string.IsNullOrWhiteSpace(requestedSimpleName))
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
                    (string.IsNullOrWhiteSpace(normalizedAssemblyPath) || AssemblyLocationOrIdentityMatchesPath(candidateAssembly, normalizedAssemblyPath)))
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
                !string.IsNullOrWhiteSpace(assemblyPath) &&
                File.Exists(assemblyPath))
            {
                requestedAssemblyPath = assemblyPath;
                return true;
            }

            if (runtimeTypeResolutionAssemblyPathsByName is not null &&
                runtimeTypeResolutionAssemblyPathsByName.TryGetValue(requestedSimpleName, out var mappedAssemblyPath) &&
                !string.IsNullOrWhiteSpace(mappedAssemblyPath) &&
                File.Exists(mappedAssemblyPath))
            {
                requestedAssemblyPath = NormalizeRuntimeAssemblyPathForCache(mappedAssemblyPath);
                return true;
            }

            var preferredDirectory = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrWhiteSpace(preferredDirectory))
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
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return false;
            }

            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
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
        /// Emits struct copy mapping.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <returns>The result produced by this operation.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static DuckTypeAotMappingEmissionResult EmitStructCopyMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            TypeDef proxyType,
            TypeDef targetType,
            ITypeDefOrRef? importedTargetTypeOverride = null,
            TypeSig? importedTargetTypeSigOverride = null,
            bool? targetIsValueTypeOverride = null,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments = null)
        {
            var planningStopwatch = StartProfilePhase();
            if (!TryCollectStructCopyBindings(mapping, proxyType, targetType, closedGenericTargetTypeArguments, out var bindings, out var failure))
            {
                StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
                return failure!;
            }

            StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);

            var importedTargetType = importedTargetTypeOverride ?? ImportTypeDefOrRefCached(moduleDef, targetType, $"resolved mapping target type '{targetType.FullName}'");
            var importedProxyType = ImportTypeDefOrRefCached(moduleDef, proxyType, $"resolved mapping proxy type '{proxyType.FullName}'");

            var importedTargetTypeSig = importedTargetTypeSigOverride ?? ImportTypeSigCached(moduleDef, targetType.ToTypeSig(), $"resolved mapping target signature '{targetType.FullName}'");
            var importedProxyTypeSig = ImportTypeSigCached(moduleDef, proxyType.ToTypeSig(), $"resolved mapping proxy signature '{proxyType.FullName}'");
            var targetIsValueType = targetIsValueTypeOverride ?? targetType.IsValueType;

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(importedProxyTypeSig, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.InitLocals = true;

            var targetLocal = new Local(importedTargetTypeSig);
            var proxyLocal = new Local(importedProxyTypeSig);
            activatorMethod.Body.Variables.Add(targetLocal);
            activatorMethod.Body.Variables.Add(proxyLocal);

            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            activatorMethod.Body.Instructions.Add(OpCodes.Stloc.ToInstruction(targetLocal));

            activatorMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(proxyLocal));
            activatorMethod.Body.Instructions.Add(OpCodes.Initobj.ToInstruction(importedProxyType));

            foreach (var binding in bindings!)
            {
                var importedProxyField = ImportFieldCached(moduleDef, binding.ProxyField, $"struct copy proxy field '{binding.ProxyField.FullName}'");
                activatorMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(proxyLocal));

                if (binding.SourceKind == StructCopySourceKind.Property)
                {
                    var sourceProperty = binding.SourceProperty!;
                    var sourceGetter = sourceProperty.GetMethod!;
                    var importedSourceGetter = ImportMethodDefOrRefCached(moduleDef, sourceGetter, $"struct copy source getter '{sourceGetter.FullName}'");
                    var sourceGetterToCall = CreateMethodCallTarget(
                        moduleDef,
                        importedSourceGetter,
                        importedTargetType,
                        activatorMethod,
                        closedGenericMethodArguments: null);

                    if (!sourceGetter.IsStatic)
                    {
                        activatorMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Ldloca : OpCodes.Ldloc).ToInstruction(targetLocal));
                    }

                    if (!sourceGetter.IsStatic && targetIsValueType && (sourceGetter.IsVirtual || sourceGetter.DeclaringType.IsInterface))
                    {
                        activatorMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                        activatorMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(sourceGetterToCall));
                    }
                    else
                    {
                        var callOpcode = sourceGetter.IsStatic ? OpCodes.Call : (sourceGetter.IsVirtual || sourceGetter.DeclaringType.IsInterface ? OpCodes.Callvirt : OpCodes.Call);
                        activatorMethod.Body.Instructions.Add(callOpcode.ToInstruction(sourceGetterToCall));
                    }
                }
                else
                {
                    var sourceField = binding.SourceField!;
                    var importedSourceField = ImportFieldCached(moduleDef, sourceField, $"struct copy source field '{sourceField.FullName}'");
                    if (sourceField.IsStatic)
                    {
                        activatorMethod.Body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(importedSourceField));
                    }
                    else
                    {
                        activatorMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Ldloca : OpCodes.Ldloc).ToInstruction(targetLocal));
                        activatorMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(importedSourceField));
                    }
                }

                EmitMethodReturnConversion(
                    moduleDef,
                    activatorMethod.Body,
                    binding.ReturnConversion,
                    importedMembers,
                    $"target member for struct field '{binding.ProxyField.FullName}'");

                activatorMethod.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(importedProxyField));
            }

            activatorMethod.Body.Instructions.Add(OpCodes.Ldloc.ToInstruction(proxyLocal));
            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            var registrationActivatorMethod = new MethodDefUser(
                $"ActivateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            registrationActivatorMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitFuncObjectObjectDelegate(initializeMethod.Body, importedMembers, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.RegisterAotProxyMethod));

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                mapping.ProxyAssemblyName,
                proxyType.FullName);
        }

        /// <summary>
        /// Attempts to collect struct copy bindings.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyStructType">The proxy struct type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="bindings">The bindings value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCollectStructCopyBindings(
            DuckTypeAotMapping mapping,
            TypeDef proxyStructType,
            TypeDef targetType,
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

                    if (!TryResolveStructCopyFieldBinding(mapping, targetType, proxyField, closedGenericTargetTypeArguments, out var binding, out failure))
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
        /// <param name="binding">The binding value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryResolveStructCopyFieldBinding(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            FieldDef proxyField,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            out StructCopyFieldBinding binding,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var bindingPlanCacheKey = BuildStructCopyBindingPlanCacheKey(targetType, proxyField, closedGenericTargetTypeArguments);
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
                failure = CreateFailureResult(
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
                var targetPropertyType = SubstituteTypeAndMethodGenericTypeArguments(targetProperty!.PropertySig.RetType, closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
                if (!TryCreateReturnConversion(proxyField.FieldSig.Type, targetPropertyType, out var returnConversion))
                {
                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        $"Return type mismatch between proxy struct field '{proxyField.FullName}' and target property '{targetProperty.FullName}'.");
                    _currentExecutionContext?.CacheStructCopyBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                    return false;
                }

                binding = StructCopyFieldBinding.ForProperty(proxyField, targetProperty, returnConversion);
                _currentExecutionContext?.CacheStructCopyBindingPlan(bindingPlanCacheKey, new StructCopyFieldBindingPlanCacheEntry(binding));
                return true;
            }

            if (hasFieldOnlyAttribute || allowFieldFallback)
            {
                if (TryFindStructCopyTargetField(targetType, candidateNames, allowPrivateBaseMembers, duckBindingFlags, useIgnoreCaseMemberMatching, out var targetField))
                {
                    var targetFieldType = SubstituteTypeAndMethodGenericTypeArguments(targetField!.FieldSig.Type, closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
                    if (!TryCreateReturnConversion(proxyField.FieldSig.Type, targetFieldType, out var returnConversion))
                    {
                        failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                            mapping,
                            DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                            StatusCodeIncompatibleSignature,
                            $"Return type mismatch between proxy struct field '{proxyField.FullName}' and target field '{targetField.FullName}'.");
                        _currentExecutionContext?.CacheStructCopyBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                        return false;
                    }

                    binding = StructCopyFieldBinding.ForField(proxyField, targetField, returnConversion);
                    _currentExecutionContext?.CacheStructCopyBindingPlan(bindingPlanCacheKey, new StructCopyFieldBindingPlanCacheEntry(binding));
                    return true;
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
        /// Attempts to select a forward-mapping return-conversion strategy between target and proxy signatures.
        /// </summary>
        /// <param name="proxyReturnType">The proxy return type value.</param>
        /// <param name="targetReturnType">The target return type value.</param>
        /// <param name="returnConversion">The return conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateReturnConversion(TypeSig proxyReturnType, TypeSig targetReturnType, out MethodReturnConversion returnConversion)
            => TryCreateReturnConversion(proxyReturnType, targetReturnType, isReverseMapping: false, out returnConversion);

        /// <summary>
        /// Attempts to select a return-conversion strategy between target and proxy signatures.
        /// </summary>
        /// <param name="proxyReturnType">The proxy return type value.</param>
        /// <param name="targetReturnType">The target return type value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <param name="returnConversion">The return conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateReturnConversion(TypeSig proxyReturnType, TypeSig targetReturnType, bool isReverseMapping, out MethodReturnConversion returnConversion)
        {
            var cacheKeyStopwatch = StartProfilePhase();
            var conversionCacheKey = BuildMethodReturnConversionCacheKey(proxyReturnType, targetReturnType, isReverseMapping);
            StopProfilePhase(cacheKeyStopwatch, seconds => _currentProfile!.MethodReturnConversionCacheKeyBuildSeconds += seconds);
            if (_currentExecutionContext?.TryGetMethodReturnConversion(conversionCacheKey, out var cachedConversion) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ConversionPlanCacheHits++;
                }

                if (cachedConversion.Succeeded)
                {
                    returnConversion = cachedConversion.Conversion;
                    return true;
                }

                returnConversion = default;
                return false;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.ConversionPlanCacheMisses++;
            }

            bool IsDuckChainingRequiredForMapping(TypeSig actualType, TypeSig expectedType)
            {
                // Dynamic reverse ducktyping swaps target/proxy order for duck-chaining checks.
                return isReverseMapping
                           ? IsDuckChainingRequired(expectedType, actualType)
                           : IsDuckChainingRequired(actualType, expectedType);
            }

            // Conversion precedence is deliberate to keep behavior stable:
            // exact match -> ValueWithType unwrap/wrap -> DuckChain -> primitive/reference conversion.
            if (AreTypesEquivalent(proxyReturnType, targetReturnType))
            {
                returnConversion = MethodReturnConversion.None();
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                return true;
            }

            if (TryGetValueWithTypeArgument(proxyReturnType, out var proxyReturnValueWithTypeArgument))
            {
                var proxyInnerReturnType = proxyReturnValueWithTypeArgument!;
                if (AreTypesEquivalent(proxyInnerReturnType, targetReturnType))
                {
                    // ValueWithType<TProxy>: direct wrap when target returns TProxy.
                    returnConversion = MethodReturnConversion.WrapValueWithType(proxyReturnType, targetReturnType);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                if (IsDuckChainingRequiredForMapping(targetReturnType, proxyInnerReturnType))
                {
                    // ValueWithType<TProxy>: dynamic mode supports duck-chaining before wrapping.
                    returnConversion = MethodReturnConversion.WrapValueWithTypeAfterDuckChainToProxy(proxyReturnType, targetReturnType);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                if (CanUseTypeConversion(targetReturnType, proxyInnerReturnType))
                {
                    // ValueWithType<TProxy>: dynamic mode supports type conversion before wrapping.
                    returnConversion = MethodReturnConversion.WrapValueWithTypeAfterTypeConversion(proxyReturnType, targetReturnType);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                returnConversion = default;
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry());
                return false;
            }

            if (IsDuckChainingRequiredForMapping(targetReturnType, proxyReturnType))
            {
                returnConversion = MethodReturnConversion.DuckChainToProxy(proxyReturnType, targetReturnType);
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                return true;
            }

            if (CanUseTypeConversion(targetReturnType, proxyReturnType))
            {
                returnConversion = MethodReturnConversion.TypeConversion(targetReturnType, proxyReturnType);
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                return true;
            }

            returnConversion = default;
            _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry());
            return false;
        }

        /// <summary>
        /// Copies method generic parameters and constraints to the generated method.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="sourceMethod">The source method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        private static void CopyMethodGenericParameters(ModuleDef moduleDef, MethodDef sourceMethod, MethodDef targetMethod)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                if (sourceMethod.GenericParameters.Count == 0)
                {
                    return;
                }

                foreach (var sourceGenericParameter in sourceMethod.GenericParameters)
                {
                    var copiedGenericParameter = new GenericParamUser(sourceGenericParameter.Number, sourceGenericParameter.Flags, sourceGenericParameter.Name)
                    {
                        Kind = sourceGenericParameter.Kind
                    };

                    foreach (var constraint in sourceGenericParameter.GenericParamConstraints)
                    {
                        if (constraint.Constraint is null)
                        {
                            throw new InvalidOperationException($"Unable to import generic parameter constraint '(null)' for '{sourceMethod.FullName}'.");
                        }

                        var importedConstraint = ImportTypeDefOrRefCached(moduleDef, constraint.Constraint, $"generic parameter constraint '{constraint.Constraint.FullName}' for '{sourceMethod.FullName}'");
                        copiedGenericParameter.GenericParamConstraints.Add(new GenericParamConstraintUser(importedConstraint));
                    }

                    targetMethod.GenericParameters.Add(copiedGenericParameter);
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.CopyMethodGenericParametersSeconds += seconds);
            }
        }

        /// <summary>
        /// Creates the generated proxy method signature, substituting closed proxy generic arguments when required.
        /// </summary>
        /// <param name="moduleDef">The destination module definition.</param>
        /// <param name="proxyMethod">The source proxy method definition.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed proxy generic type arguments.</param>
        /// <param name="interfaceMethodContract">The interface contract that declared the method.</param>
        /// <returns>The emitted method signature.</returns>
        private static MethodSig CreateGeneratedProxyMethodSig(
            ModuleDef moduleDef,
            MethodDef proxyMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            InterfaceMethodContract? interfaceMethodContract)
        {
            if (interfaceMethodContract is not null &&
                interfaceMethodContract.RequiresExplicitInterfaceImplementation)
            {
                return CreateImportedInterfaceContractMethodSig(
                    moduleDef,
                    interfaceMethodContract.MethodSig,
                    closedGenericProxyTypeArguments,
                    $"inherited interface method '{proxyMethod.FullName}'");
            }

            if (closedGenericProxyTypeArguments is null || closedGenericProxyTypeArguments.Count == 0)
            {
                return ImportMethodCached(moduleDef, proxyMethod, $"proxy method '{proxyMethod.FullName}'").MethodSig;
            }

            var sourceSig = proxyMethod.MethodSig;
            var returnType = ImportTypeSigCached(
                moduleDef,
                SubstituteTypeAndMethodGenericTypeArguments(sourceSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null),
                $"closed generic proxy method return type '{proxyMethod.FullName}'");
            var parameterTypes = new TypeSig[sourceSig.Params.Count];
            for (var parameterIndex = 0; parameterIndex < parameterTypes.Length; parameterIndex++)
            {
                parameterTypes[parameterIndex] = ImportTypeSigCached(
                    moduleDef,
                    SubstituteTypeAndMethodGenericTypeArguments(sourceSig.Params[parameterIndex], closedGenericProxyTypeArguments, closedGenericMethodArguments: null),
                    $"closed generic proxy method parameter '{proxyMethod.FullName}'");
            }

            MethodSig generatedSig;
            if (sourceSig.Generic)
            {
                generatedSig = sourceSig.HasThis
                                   ? MethodSig.CreateInstanceGeneric(sourceSig.GenParamCount, returnType, parameterTypes)
                                   : MethodSig.CreateStaticGeneric(sourceSig.GenParamCount, returnType, parameterTypes);
            }
            else
            {
                generatedSig = sourceSig.HasThis
                                   ? MethodSig.CreateInstance(returnType, parameterTypes)
                                   : MethodSig.CreateStatic(returnType, parameterTypes);
            }

            generatedSig.ExplicitThis = sourceSig.ExplicitThis;
            return generatedSig;
        }

        /// <summary>
        /// Creates the generated proxy method signature for an inherited interface contract.
        /// </summary>
        /// <param name="moduleDef">The destination module definition.</param>
        /// <param name="sourceSig">The source method signature.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed proxy generic type arguments.</param>
        /// <param name="context">The operation context.</param>
        /// <returns>The emitted method signature.</returns>
        private static MethodSig CreateImportedInterfaceContractMethodSig(
            ModuleDef moduleDef,
            MethodSig sourceSig,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            string context)
        {
            var returnType = ImportInterfaceContractTypeSig(
                moduleDef,
                SubstituteTypeAndMethodGenericTypeArguments(sourceSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null),
                $"{context} return type");
            var parameterTypes = new TypeSig[sourceSig.Params.Count];
            for (var parameterIndex = 0; parameterIndex < parameterTypes.Length; parameterIndex++)
            {
                parameterTypes[parameterIndex] = ImportInterfaceContractTypeSig(
                    moduleDef,
                    SubstituteTypeAndMethodGenericTypeArguments(sourceSig.Params[parameterIndex], closedGenericProxyTypeArguments, closedGenericMethodArguments: null),
                    $"{context} parameter type");
            }

            MethodSig generatedSig;
            if (sourceSig.Generic)
            {
                generatedSig = sourceSig.HasThis
                                   ? MethodSig.CreateInstanceGeneric(sourceSig.GenParamCount, returnType, parameterTypes)
                                   : MethodSig.CreateStaticGeneric(sourceSig.GenParamCount, returnType, parameterTypes);
            }
            else
            {
                generatedSig = sourceSig.HasThis
                                   ? MethodSig.CreateInstance(returnType, parameterTypes)
                                   : MethodSig.CreateStatic(returnType, parameterTypes);
            }

            generatedSig.ExplicitThis = sourceSig.ExplicitThis;
            return generatedSig;
        }

        /// <summary>
        /// Creates method call target.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="importedTargetMethod">The imported target method value.</param>
        /// <param name="importedTargetType">The imported target type value.</param>
        /// <param name="generatedMethod">The generated method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethod CreateMethodCallTarget(
            ModuleDef moduleDef,
            IMethodDefOrRef importedTargetMethod,
            ITypeDefOrRef importedTargetType,
            MethodDef generatedMethod,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var cacheKey = BuildMethodCallTargetCacheKey(importedTargetMethod, importedTargetType, (int)generatedMethod.MethodSig.GenParamCount, closedGenericMethodArguments);
                if (_currentExecutionContext?.TryGetMethodCallTarget(cacheKey, out var cachedCallTarget) == true)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.MethodCallTargetCacheHits++;
                    }

                    return cachedCallTarget!;
                }

                if (_currentProfile is not null)
                {
                    _currentProfile.MethodCallTargetCacheMisses++;
                }

                IMethodDefOrRef effectiveTargetMethod = importedTargetMethod;
                if (importedTargetMethod.DeclaringType is ITypeDefOrRef importedDeclaringType &&
                    !string.Equals(importedDeclaringType.FullName, importedTargetType.FullName, StringComparison.Ordinal))
                {
                    // Bind the call to the imported target type so closed-generic target methods
                    // are emitted against the instantiated declaring type instead of the generic definition.
                    var reboundMethod = new MemberRefUser(
                        moduleDef,
                        importedTargetMethod.Name,
                        importedTargetMethod.MethodSig,
                        importedTargetType);
                    effectiveTargetMethod = moduleDef.UpdateRowId(reboundMethod);
                }

                if (closedGenericMethodArguments is not null && closedGenericMethodArguments.Count > 0)
                {
                    var closedArguments = new List<TypeSig>(closedGenericMethodArguments.Count);
                    for (var i = 0; i < closedGenericMethodArguments.Count; i++)
                    {
                        closedArguments.Add(ImportTypeSigCached(moduleDef, closedGenericMethodArguments[i], $"closed generic method argument '{closedGenericMethodArguments[i].FullName}'"));
                    }

                    var methodSpecWithClosedArguments = new MethodSpecUser(effectiveTargetMethod, new GenericInstMethodSig(closedArguments));
                    var closedGenericCallTarget = moduleDef.UpdateRowId(methodSpecWithClosedArguments);
                    _currentExecutionContext?.CacheMethodCallTarget(cacheKey, closedGenericCallTarget);
                    return closedGenericCallTarget;
                }

                if (generatedMethod.MethodSig.GenParamCount == 0)
                {
                    _currentExecutionContext?.CacheMethodCallTarget(cacheKey, effectiveTargetMethod);
                    return effectiveTargetMethod;
                }

                var genericArguments = new List<TypeSig>((int)generatedMethod.MethodSig.GenParamCount);
                for (var genericParameterIndex = 0; genericParameterIndex < generatedMethod.MethodSig.GenParamCount; genericParameterIndex++)
                {
                    genericArguments.Add(new GenericMVar((uint)genericParameterIndex));
                }

                var methodSpec = new MethodSpecUser(effectiveTargetMethod, new GenericInstMethodSig(genericArguments));
                var generatedMethodCallTarget = moduleDef.UpdateRowId(methodSpec);
                _currentExecutionContext?.CacheMethodCallTarget(cacheKey, generatedMethodCallTarget);
                return generatedMethodCallTarget;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.MethodCallTargetSeconds += seconds);
            }
        }

        /// <summary>
        /// Emits method return conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="conversion">The conversion value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="context">The context value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitMethodReturnConversion(
            ModuleDef moduleDef,
            CilBody methodBody,
            MethodReturnConversion conversion,
            ImportedMembers importedMembers,
            string context)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                if (conversion.Kind == MethodReturnConversionKind.WrapValueWithType ||
                    conversion.Kind == MethodReturnConversionKind.WrapValueWithTypeAfterDuckChainToProxy ||
                    conversion.Kind == MethodReturnConversionKind.WrapValueWithTypeAfterTypeConversion)
                {
                    if (!TryGetValueWithTypeArgument(conversion.WrapperTypeSig!, out var wrapperValueType))
                    {
                        throw new InvalidOperationException($"Expected ValueWithType<T> wrapper for '{conversion.WrapperTypeSig!.FullName}'.");
                    }

                    if (conversion.Kind == MethodReturnConversionKind.WrapValueWithTypeAfterDuckChainToProxy)
                    {
                        EmitDuckChainToProxyConversion(
                            moduleDef,
                            methodBody,
                            wrapperValueType!,
                            conversion.InnerTypeSig!,
                            context);
                    }
                    else if (conversion.Kind == MethodReturnConversionKind.WrapValueWithTypeAfterTypeConversion)
                    {
                        EmitTypeConversion(
                            moduleDef,
                            methodBody,
                            conversion.InnerTypeSig!,
                            wrapperValueType!,
                            context);
                    }

                    // ValueWithType<T>.Type stores the original target return type seen before adaptation.
                    var importedTargetReturnType = ResolveImportedTypeForTypeToken(moduleDef, conversion.InnerTypeSig!, context);
                    methodBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetReturnType));
                    methodBody.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));

                    var createMethodRef = CreateValueWithTypeCreateMethodRef(
                        moduleDef,
                        conversion.WrapperTypeSig!,
                        wrapperValueType!);
                    methodBody.Instructions.Add(OpCodes.Call.ToInstruction(createMethodRef));
                    return;
                }

                if (conversion.Kind == MethodReturnConversionKind.TypeConversion)
                {
                    EmitTypeConversion(moduleDef, methodBody, conversion.WrapperTypeSig!, conversion.InnerTypeSig!, context);
                    return;
                }

                if (conversion.Kind == MethodReturnConversionKind.DuckChainToProxy)
                {
                    EmitDuckChainToProxyConversion(
                        moduleDef,
                        methodBody,
                        conversion.WrapperTypeSig!,
                        conversion.InnerTypeSig!,
                        context);
                    return;
                }

                if (conversion.Kind == MethodReturnConversionKind.ExtractDuckTypeInstance)
                {
                    var hasValueLabel = Instruction.Create(OpCodes.Nop);
                    var endLabel = Instruction.Create(OpCodes.Nop);
                    methodBody.Instructions.Add(OpCodes.Dup.ToInstruction());
                    methodBody.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));
                    methodBody.Instructions.Add(OpCodes.Pop.ToInstruction());
                    methodBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                    methodBody.Instructions.Add(OpCodes.Br_S.ToInstruction(endLabel));
                    methodBody.Instructions.Add(hasValueLabel);
                    methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                    methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                    methodBody.Instructions.Add(endLabel);
                    EmitObjectToExpectedTypeConversion(moduleDef, methodBody, conversion.InnerTypeSig!, context);
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.EmitMethodReturnConversionSeconds += seconds);
            }
        }

        /// <summary>
        /// Emits method argument conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="conversion">The conversion value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="context">The context value.</param>
        /// <param name="preserveNullForDuckTypeExtraction">Whether null duck-chain inputs should be preserved instead of dereferenced.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitMethodArgumentConversion(
            ModuleDef moduleDef,
            CilBody methodBody,
            MethodArgumentConversion conversion,
            ImportedMembers importedMembers,
            string context,
            bool preserveNullForDuckTypeExtraction)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                if (_currentProfile is not null)
                {
                    switch (conversion.Kind)
                    {
                        case MethodArgumentConversionKind.None:
                            _currentProfile.EmitArgumentConversionNoneCount++;
                            break;
                        case MethodArgumentConversionKind.UnwrapValueWithType:
                            _currentProfile.EmitArgumentConversionUnwrapCount++;
                            break;
                        case MethodArgumentConversionKind.ExtractDuckTypeInstance:
                            _currentProfile.EmitArgumentConversionExtractDuckTypeCount++;
                            break;
                        case MethodArgumentConversionKind.DuckChainToProxy:
                            _currentProfile.EmitArgumentConversionDuckChainCount++;
                            break;
                        case MethodArgumentConversionKind.TypeConversion:
                            _currentProfile.EmitArgumentConversionTypeConversionCount++;
                            break;
                    }
                }

                if (conversion.RequiresValueWithTypeUnwrap)
                {
                    var valueFieldRef = CreateValueWithTypeValueFieldRef(
                        moduleDef,
                        conversion.UnwrapWrapperTypeSig!,
                        conversion.UnwrapInnerTypeSig!);
                    methodBody.Instructions.Add(OpCodes.Ldfld.ToInstruction(valueFieldRef));
                }

                switch (conversion.Kind)
                {
                    case MethodArgumentConversionKind.None:
                        return;
                    case MethodArgumentConversionKind.UnwrapValueWithType:
                    {
                        var valueFieldRef = CreateValueWithTypeValueFieldRef(moduleDef, conversion.WrapperTypeSig!, conversion.InnerTypeSig!);
                        methodBody.Instructions.Add(OpCodes.Ldfld.ToInstruction(valueFieldRef));
                        return;
                    }

                    case MethodArgumentConversionKind.ExtractDuckTypeInstance:
                        if (preserveNullForDuckTypeExtraction)
                        {
                            // Dynamic duck typing treats null ref/out inputs as null instances, not as invalid IDuckType.
                            var hasValueLabel = Instruction.Create(OpCodes.Nop);
                            var endLabel = Instruction.Create(OpCodes.Nop);
                            methodBody.Instructions.Add(OpCodes.Dup.ToInstruction());
                            methodBody.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));
                            methodBody.Instructions.Add(OpCodes.Pop.ToInstruction());
                            methodBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                            methodBody.Instructions.Add(OpCodes.Br_S.ToInstruction(endLabel));
                            methodBody.Instructions.Add(hasValueLabel);
                            methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                            methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                            methodBody.Instructions.Add(endLabel);
                        }
                        else
                        {
                            methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                            methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                        }

                        EmitObjectToExpectedTypeConversion(moduleDef, methodBody, conversion.InnerTypeSig!, context);
                        return;
                    case MethodArgumentConversionKind.DuckChainToProxy:
                        EmitDuckChainToProxyConversion(moduleDef, methodBody, conversion.WrapperTypeSig!, conversion.InnerTypeSig!, context);
                        return;
                    case MethodArgumentConversionKind.TypeConversion:
                        EmitTypeConversion(moduleDef, methodBody, conversion.WrapperTypeSig!, conversion.InnerTypeSig!, context);
                        return;
                    default:
                        throw new InvalidOperationException($"Unsupported method argument conversion '{conversion.Kind}'.");
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.EmitMethodArgumentConversionSeconds += seconds);
            }
        }

        /// <summary>
        /// Emits load by ref value.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="valueTypeSig">The value type sig value.</param>
        /// <param name="context">The context value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitLoadByRefValue(ModuleDef moduleDef, CilBody methodBody, TypeSig valueTypeSig, string context)
        {
            var importedValueType = ResolveImportedTypeForTypeToken(moduleDef, valueTypeSig, context);
            methodBody.Instructions.Add(OpCodes.Ldobj.ToInstruction(importedValueType));
        }

        /// <summary>
        /// Emits store by ref value.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="valueTypeSig">The value type sig value.</param>
        /// <param name="context">The context value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitStoreByRefValue(ModuleDef moduleDef, CilBody methodBody, TypeSig valueTypeSig, string context)
        {
            var importedValueType = ResolveImportedTypeForTypeToken(moduleDef, valueTypeSig, context);
            methodBody.Instructions.Add(OpCodes.Stobj.ToInstruction(importedValueType));
        }

        /// <summary>
        /// Determines whether a safe type conversion can be emitted.
        /// </summary>
        /// <param name="actualTypeSig">The actual type sig value.</param>
        /// <param name="expectedTypeSig">The expected type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool CanUseTypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
        {
            // By-ref and open generic conversions are intentionally blocked to avoid emitting unverifiable IL.
            if (actualTypeSig.ElementType == ElementType.ByRef || expectedTypeSig.ElementType == ElementType.ByRef)
            {
                return false;
            }

            if (actualTypeSig.IsGenericParameter || expectedTypeSig.IsGenericParameter)
            {
                return actualTypeSig.IsGenericParameter && expectedTypeSig.IsGenericParameter && AreTypesEquivalent(actualTypeSig, expectedTypeSig);
            }

            var actualRuntimeType = TryResolveRuntimeType(actualTypeSig);
            var expectedRuntimeType = TryResolveRuntimeType(expectedTypeSig);
            // Runtime type resolution path centralizes assignability/enums/nullable handling across all mappings.
            if (actualRuntimeType is not null && expectedRuntimeType is not null)
            {
                return CanUseTypeConversion(actualRuntimeType, expectedRuntimeType);
            }

            var actualUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(actualTypeSig);
            var expectedUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(expectedTypeSig);
            if (AreTypesEquivalent(actualUnderlyingTypeSig, expectedUnderlyingTypeSig))
            {
                return true;
            }

            if (actualUnderlyingTypeSig.IsValueType)
            {
                if (expectedUnderlyingTypeSig.IsValueType)
                {
                    return false;
                }

                return IsObjectTypeSig(expectedUnderlyingTypeSig)
                    || IsTypeAssignableFrom(expectedUnderlyingTypeSig, actualUnderlyingTypeSig);
            }

            if (expectedUnderlyingTypeSig.IsValueType)
            {
                return IsObjectTypeSig(actualUnderlyingTypeSig)
                    || IsTypeAssignableFrom(actualUnderlyingTypeSig, expectedUnderlyingTypeSig);
            }

            return true;
        }

        /// <summary>
        /// Determines whether a safe type conversion can be emitted.
        /// </summary>
        /// <param name="actualType">The actual type value.</param>
        /// <param name="expectedType">The expected type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool CanUseTypeConversion(Type actualType, Type expectedType)
        {
            var actualUnderlyingType = actualType.IsEnum ? Enum.GetUnderlyingType(actualType) : actualType;
            var expectedUnderlyingType = expectedType.IsEnum ? Enum.GetUnderlyingType(expectedType) : expectedType;

            if (actualUnderlyingType == expectedUnderlyingType)
            {
                return true;
            }

            if (actualUnderlyingType.IsValueType)
            {
                if (expectedUnderlyingType.IsValueType)
                {
                    return false;
                }

                return expectedUnderlyingType == typeof(object) || expectedUnderlyingType.IsAssignableFrom(actualUnderlyingType);
            }

            if (expectedUnderlyingType.IsValueType)
            {
                return actualUnderlyingType == typeof(object) || actualUnderlyingType.IsAssignableFrom(expectedUnderlyingType);
            }

            return true;
        }

        /// <summary>
        /// Emits type conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="actualTypeSig">The actual type sig value.</param>
        /// <param name="expectedTypeSig">The expected type sig value.</param>
        /// <param name="context">The context value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitTypeConversion(
            ModuleDef moduleDef,
            CilBody methodBody,
            TypeSig actualTypeSig,
            TypeSig expectedTypeSig,
            string context)
        {
            if (actualTypeSig.IsGenericParameter && expectedTypeSig.IsGenericParameter)
            {
                return;
            }

            var actualUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(actualTypeSig);
            var expectedUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(expectedTypeSig);
            if (AreTypesEquivalent(actualUnderlyingTypeSig, expectedUnderlyingTypeSig))
            {
                return;
            }

            if (actualUnderlyingTypeSig.IsValueType)
            {
                if (expectedUnderlyingTypeSig.IsValueType)
                {
                    throw new InvalidOperationException($"Unsupported value-type conversion from '{actualTypeSig.FullName}' to '{expectedTypeSig.FullName}' in {context}.");
                }

                var importedActualType = ResolveImportedTypeForTypeToken(moduleDef, actualTypeSig, context);
                methodBody.Instructions.Add(OpCodes.Box.ToInstruction(importedActualType));
                if (!IsObjectTypeSig(expectedUnderlyingTypeSig))
                {
                    var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedUnderlyingTypeSig, context);
                    methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
                }

                return;
            }

            if (expectedUnderlyingTypeSig.IsValueType)
            {
                var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedTypeSig, context);
                var isExpectedLabel = Instruction.Create(OpCodes.Nop);
                methodBody.Instructions.Add(OpCodes.Dup.ToInstruction());
                methodBody.Instructions.Add(OpCodes.Isinst.ToInstruction(importedExpectedType));
                methodBody.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(isExpectedLabel));
                methodBody.Instructions.Add(OpCodes.Pop.ToInstruction());
                var invalidCastExceptionCtor = typeof(InvalidCastException).GetConstructor(Type.EmptyTypes)
                                            ?? throw new InvalidOperationException("Unable to resolve InvalidCastException::.ctor().");
                methodBody.Instructions.Add(OpCodes.Newobj.ToInstruction(moduleDef.Import(invalidCastExceptionCtor)));
                methodBody.Instructions.Add(OpCodes.Throw.ToInstruction());
                methodBody.Instructions.Add(isExpectedLabel);
                methodBody.Instructions.Add(OpCodes.Unbox_Any.ToInstruction(importedExpectedType));
                return;
            }

            if (!IsObjectTypeSig(expectedUnderlyingTypeSig))
            {
                var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedUnderlyingTypeSig, context);
                methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
            }
        }

        /// <summary>
        /// Gets underlying type for type conversion.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static TypeSig GetUnderlyingTypeForTypeConversion(TypeSig typeSig)
        {
            var typeDef = typeSig.ToTypeDefOrRef()?.ResolveTypeDef();
            if (typeDef?.IsEnum != true)
            {
                return typeSig;
            }

            foreach (var field in typeDef.Fields)
            {
                if (field.IsSpecialName && string.Equals(field.Name, "value__", StringComparison.Ordinal))
                {
                    return field.FieldSig.Type;
                }
            }

            return typeSig;
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
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "The tool resolves type names from dnlib metadata to validate and emit mappings; this is not a trimmed app execution path.")]
#endif
        private static Type? TryResolveRuntimeTypeFromTypeDefOrRef(TypeSig typeSig)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            if (typeDefOrRef is null)
            {
                return null;
            }

            var reflectionName = typeDefOrRef.ReflectionFullName;
            if (string.IsNullOrWhiteSpace(reflectionName))
            {
                reflectionName = typeDefOrRef.FullName;
            }

            if (string.IsNullOrWhiteSpace(reflectionName))
            {
                return null;
            }

            reflectionName = reflectionName.Replace('/', '+');
            var assemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(typeDefOrRef.DefinitionAssembly?.Name.String ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(assemblyName))
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
        /// Emits i duck type implementation.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="generatedType">The generated type value.</param>
        /// <param name="importedTargetType">The imported target type value.</param>
        /// <param name="targetField">The target field value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="targetIsValueType">The target is value type value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitIDuckTypeImplementation(
            ModuleDef moduleDef,
            TypeDef generatedType,
            ITypeDefOrRef importedTargetType,
            FieldDef targetField,
            ImportedMembers importedMembers,
            bool targetIsValueType)
        {
            // Emit IDuckType.Instance once per generated type.
            if (generatedType.FindMethod("get_Instance") is null)
            {
                var getInstanceMethod = new MethodDefUser(
                    "get_Instance",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.Object),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                getInstanceMethod.Body = new CilBody();
                getInstanceMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                getInstanceMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(targetField));
                // IDuckType.Instance is object-typed, so value-type targets must be boxed here.
                if (targetIsValueType)
                {
                    getInstanceMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedTargetType));
                }

                getInstanceMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(getInstanceMethod);

                var instanceProperty = new PropertyDefUser("Instance", new PropertySig(hasThis: true, moduleDef.CorLibTypes.Object));
                instanceProperty.GetMethod = getInstanceMethod;
                generatedType.Properties.Add(instanceProperty);
            }

            // Emit IDuckType.Type once per generated type.
            if (generatedType.FindMethod("get_Type") is null)
            {
                var getTypeMethod = new MethodDefUser(
                    "get_Type",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.GetTypeRef("System", "Type").ToTypeSig()),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                getTypeMethod.Body = new CilBody();
                getTypeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
                getTypeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
                getTypeMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(getTypeMethod);

                var typeProperty = new PropertyDefUser("Type", new PropertySig(hasThis: true, moduleDef.CorLibTypes.GetTypeRef("System", "Type").ToTypeSig()));
                typeProperty.GetMethod = getTypeMethod;
                generatedType.Properties.Add(typeProperty);
            }

            // Emit ref-return helper used by runtime conversion paths.
            if (generatedType.FindMethod("GetInternalDuckTypedInstance") is null)
            {
                var getInternalInstanceMethod = new MethodDefUser(
                    "GetInternalDuckTypedInstance",
                    MethodSig.CreateInstanceGeneric(1, new ByRefSig(new GenericMVar(0))),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot);
                getInternalInstanceMethod.GenericParameters.Add(new GenericParamUser(0, GenericParamAttributes.NonVariant, "TReturn"));
                getInternalInstanceMethod.Body = new CilBody();
                getInternalInstanceMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                getInternalInstanceMethod.Body.Instructions.Add(OpCodes.Ldflda.ToInstruction(targetField));
                getInternalInstanceMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(getInternalInstanceMethod);
            }

            // Emit ToString override once per generated type.
            if (generatedType.FindMethod("ToString") is null)
            {
                var toStringMethod = new MethodDefUser(
                    "ToString",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.String),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot);
                toStringMethod.Body = new CilBody();
                // Value-type targets use constrained callvirt to avoid boxing while preserving virtual dispatch semantics.
                if (targetIsValueType)
                {
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldflda.ToInstruction(targetField));
                    toStringMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.ObjectToStringMethod));
                }
                else
                {
                    // Reference-type targets preserve null behavior by returning null when _instance is null.
                    var hasValueLabel = Instruction.Create(OpCodes.Nop);
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(targetField));
                    toStringMethod.Body.Instructions.Add(OpCodes.Dup.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));
                    toStringMethod.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                    toStringMethod.Body.Instructions.Add(hasValueLabel);
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.ObjectToStringMethod));
                }

                toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(toStringMethod);
            }
        }

        /// <summary>
        /// Ensures emitted interface accessors are also represented as property metadata when possible.
        /// </summary>
        /// <param name="moduleDef">The module definition.</param>
        /// <param name="generatedType">The generated type.</param>
        /// <param name="proxyMethod">Source accessor method from the proxy definition.</param>
        /// <param name="generatedMethod">Generated accessor method on the emitted proxy type.</param>
        /// <param name="generatedPropertiesByKey">Per-type cache to avoid duplicating emitted property rows.</param>
        private static void EnsureInterfacePropertyMetadata(
            ModuleDef moduleDef,
            TypeDef generatedType,
            MethodDef proxyMethod,
            MethodDef generatedMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IDictionary<string, PropertyDef> generatedPropertiesByKey)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                // Keeping property rows aligned with emitted accessors preserves reflection and decompiler behavior parity.
                // Without this, proxies may appear to expose only get_/set_ methods in downstream tooling.
                if (!proxyMethod.IsSpecialName)
                {
                    return;
                }

                var accessorName = proxyMethod.Name;
                var isGetter = accessorName.StartsWith("get_", StringComparison.Ordinal);
                var isSetter = accessorName.StartsWith("set_", StringComparison.Ordinal);
                if (!isGetter && !isSetter)
                {
                    return;
                }

                var proxyProperty = FindPropertyFromAccessor(proxyMethod);
                string propertyKey;
                string propertyName;
                PropertySig importedPropertySig;
                // Prefer declared property metadata when available.
                if (proxyProperty is not null)
                {
                    importedPropertySig = CreateImportedPropertySig(moduleDef, proxyProperty.PropertySig, closedGenericProxyTypeArguments);
                    propertyName = proxyProperty.Name;
                    propertyKey = $"{BuildPropertyIdentityKey(proxyProperty)}::{BuildTypeSigSequenceCacheKey(closedGenericProxyTypeArguments)}";
                }
                else
                {
                    // Fallback keeps metadata usable for contracts that declare accessor methods without property rows.
                    if (!TryInferPropertyMetadataFromAccessor(moduleDef, proxyMethod, closedGenericProxyTypeArguments, out propertyName, out importedPropertySig, out propertyKey))
                    {
                        return;
                    }
                }

                // Create property row once, then attach getter/setter accessors as they are emitted.
                if (!generatedPropertiesByKey.TryGetValue(propertyKey, out var generatedProperty))
                {
                    // One property definition per logical key avoids duplicate property rows in generated metadata.
                    generatedProperty = new PropertyDefUser(propertyName, importedPropertySig);
                    generatedType.Properties.Add(generatedProperty);
                    generatedPropertiesByKey[propertyKey] = generatedProperty;
                }

                if (isGetter)
                {
                    generatedProperty.GetMethod ??= generatedMethod;
                }
                else
                {
                    generatedProperty.SetMethod ??= generatedMethod;
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.EnsureInterfacePropertyMetadataSeconds += seconds);
            }
        }

        /// <summary>
        /// Finds the property that declares the specified accessor.
        /// </summary>
        /// <param name="accessorMethod">The accessor method definition.</param>
        /// <returns>The declaring property when found; otherwise null.</returns>
        private static PropertyDef? FindPropertyFromAccessor(MethodDef accessorMethod)
        {
            var declaringType = accessorMethod.DeclaringType;
            if (declaringType is null)
            {
                return null;
            }

            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(declaringType);
            if (proxyTypePlan?.TryGetPropertyFromAccessor(accessorMethod, out var cachedProperty) == true)
            {
                return cachedProperty;
            }

            var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
            var typesToInspect = new Stack<TypeDef>();
            typesToInspect.Push(declaringType);

            while (typesToInspect.Count > 0)
            {
                var currentType = typesToInspect.Pop();
                // Graph walk includes base types and interfaces so inherited property metadata is preserved in emission.
                if (!visitedTypes.Add(BuildTypeIdentityKey(currentType)))
                {
                    continue;
                }

                foreach (var property in currentType.Properties)
                {
                    if (AccessorMatches(property.GetMethod, accessorMethod) || AccessorMatches(property.SetMethod, accessorMethod))
                    {
                        return property;
                    }
                }

                var baseType = currentType.BaseType?.ResolveTypeDef();
                if (baseType is not null)
                {
                    typesToInspect.Push(baseType);
                }

                foreach (var interfaceImpl in currentType.Interfaces)
                {
                    var resolvedInterface = interfaceImpl.Interface.ResolveTypeDef();
                    if (resolvedInterface is not null)
                    {
                        typesToInspect.Push(resolvedInterface);
                    }
                }
            }

            return null;
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
        /// Infers property metadata from accessor-shaped methods when declaring property metadata is unavailable.
        /// </summary>
        /// <param name="moduleDef">The destination module definition.</param>
        /// <param name="accessorMethod">The accessor method.</param>
        /// <param name="propertyName">The inferred property name.</param>
        /// <param name="propertySig">The inferred property signature.</param>
        /// <param name="propertyKey">The inferred property key.</param>
        /// <returns>
        /// <see langword="true"/> when a property name and signature can be inferred from the accessor method shape;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryInferPropertyMetadataFromAccessor(
            ModuleDef moduleDef,
            MethodDef accessorMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            out string propertyName,
            out PropertySig propertySig,
            out string propertyKey)
        {
            propertyName = string.Empty;
            propertySig = null!;
            propertyKey = string.Empty;

            var accessorName = accessorMethod.Name;
            // Getter pattern: property value is return type, all parameters are indexer parameters.
            if (accessorName.StartsWith("get_", StringComparison.Ordinal))
            {
                // Invalid getter shape: getters cannot return void.
                if (accessorMethod.MethodSig.RetType.ElementType == ElementType.Void)
                {
                    return false;
                }

                propertyName = accessorName.Substring(4);
                var importedReturnType = ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(accessorMethod.MethodSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"getter return type '{accessorMethod.FullName}'");
                var importedParameterTypes = accessorMethod.MethodSig.Params.Select(parameterType => ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(parameterType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"getter parameter type '{accessorMethod.FullName}'")).ToArray();
                propertySig = new PropertySig(hasThis: true, importedReturnType, importedParameterTypes);
                propertyKey = $"{(accessorMethod.DeclaringType is null ? string.Empty : BuildTypeIdentityKey(accessorMethod.DeclaringType))}::{propertyName}::{BuildPropertySignatureCacheKey(propertySig)}";
                return true;
            }

            // Non-getter and non-setter methods cannot describe a property.
            if (!accessorName.StartsWith("set_", StringComparison.Ordinal))
            {
                return false;
            }

            // Valid setter shape: void return and at least one parameter (the value parameter).
            if (accessorMethod.MethodSig.RetType.ElementType != ElementType.Void || accessorMethod.MethodSig.Params.Count == 0)
            {
                return false;
            }

            propertyName = accessorName.Substring(4);
            var valueType = ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(accessorMethod.MethodSig.Params[accessorMethod.MethodSig.Params.Count - 1], closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"setter value type '{accessorMethod.FullName}'");
            var importedIndexParameters = accessorMethod.MethodSig.Params
                                            .Take(accessorMethod.MethodSig.Params.Count - 1)
                                            .Select(parameterType => ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(parameterType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"setter parameter type '{accessorMethod.FullName}'"))
                                            .ToArray();
            propertySig = new PropertySig(hasThis: true, valueType, importedIndexParameters);
            propertyKey = $"{(accessorMethod.DeclaringType is null ? string.Empty : BuildTypeIdentityKey(accessorMethod.DeclaringType))}::{propertyName}::{BuildPropertySignatureCacheKey(propertySig)}";
            return true;
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

        /// <summary>
        /// Determines whether a method is declared directly on the inspected target type.
        /// </summary>
        /// <param name="method">The method definition.</param>
        /// <param name="declaringType">The target type definition.</param>
        /// <returns>true when the method belongs to the target type; otherwise false.</returns>
        private static bool IsMethodDeclaredOnType(MethodDef method, TypeDef declaringType)
        {
            return ReferenceEquals(method.DeclaringType, declaringType) ||
                   (method.DeclaringType is not null &&
                    string.Equals(BuildTypeIdentityKey(method.DeclaringType), BuildTypeIdentityKey(declaringType), StringComparison.Ordinal));
        }

        /// <summary>
        /// Creates an imported property signature for the generated module.
        /// </summary>
        /// <param name="moduleDef">The destination module definition.</param>
        /// <param name="sourcePropertySig">The source property signature.</param>
        /// <returns>The imported property signature.</returns>
        private static PropertySig CreateImportedPropertySig(ModuleDef moduleDef, PropertySig sourcePropertySig, IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments)
        {
            var cacheKey = string.Concat(
                BuildPropertySignatureCacheKey(sourcePropertySig),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericProxyTypeArguments));
            if (_currentExecutionContext?.TryGetPropertySignature(cacheKey, out var cachedPropertySig) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.PropertySignatureCacheHits++;
                }

                return cachedPropertySig!;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.PropertySignatureCacheMisses++;
            }

            var importedReturnType = ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(sourcePropertySig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"property signature return type '{sourcePropertySig}'");
            PropertySig importedPropertySig;
            if (sourcePropertySig.Params.Count == 0)
            {
                importedPropertySig = new PropertySig(hasThis: sourcePropertySig.HasThis, importedReturnType);
                _currentExecutionContext?.CachePropertySignature(cacheKey, importedPropertySig);
                return importedPropertySig;
            }

            var importedParameterTypes = new TypeSig[sourcePropertySig.Params.Count];
            for (var index = 0; index < sourcePropertySig.Params.Count; index++)
            {
                importedParameterTypes[index] = ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(sourcePropertySig.Params[index], closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"property signature parameter '{sourcePropertySig}'");
            }

            importedPropertySig = new PropertySig(hasThis: sourcePropertySig.HasThis, importedReturnType, importedParameterTypes);
            _currentExecutionContext?.CachePropertySignature(cacheKey, importedPropertySig);
            return importedPropertySig;
        }

        /// <summary>
        /// Gets interface method attributes.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static MethodAttributes GetInterfaceMethodAttributes(MethodDef proxyMethod)
        {
            var attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final;
            if (proxyMethod.DeclaringType?.IsInterface == true)
            {
                attributes |= MethodAttributes.NewSlot;
            }

            if (proxyMethod.IsSpecialName)
            {
                attributes |= MethodAttributes.SpecialName;
            }

            if (proxyMethod.IsRuntimeSpecialName)
            {
                attributes |= MethodAttributes.RTSpecialName;
            }

            return attributes;
        }

        /// <summary>
        /// Adds an explicit interface method override for inherited non-generic interface methods.
        /// </summary>
        /// <param name="moduleDef">The module definition value.</param>
        /// <param name="generatedType">The generated type value.</param>
        /// <param name="generatedInterfaceImplementations">The generated interface implementation keys.</param>
        /// <param name="isInterfaceProxy">The is interface proxy value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="generatedMethod">The generated method value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="interfaceMethodContract">The interface contract that declared the method.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed proxy generic type arguments.</param>
        private static void AddInterfaceMethodOverride(
            ModuleDef moduleDef,
            TypeDef generatedType,
            HashSet<string> generatedInterfaceImplementations,
            bool isInterfaceProxy,
            TypeDef proxyType,
            MethodDef generatedMethod,
            MethodDef proxyMethod,
            InterfaceMethodContract? interfaceMethodContract,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments)
        {
            if (!isInterfaceProxy ||
                interfaceMethodContract is null ||
                !interfaceMethodContract.RequiresExplicitInterfaceImplementation)
            {
                return;
            }

            var inheritedInterface = ImportInterfaceContractTypeDefOrRef(
                moduleDef,
                interfaceMethodContract,
                closedGenericProxyTypeArguments,
                $"inherited interface contract '{interfaceMethodContract.InterfaceType.FullName}'");
            if (generatedInterfaceImplementations.Add(BuildTypeDefOrRefIdentityKey(inheritedInterface)))
            {
                generatedType.Interfaces.Add(new InterfaceImplUser(inheritedInterface));
            }

            var importedProxyMethodSig = CreateImportedInterfaceContractMethodSig(
                moduleDef,
                interfaceMethodContract.MethodSig,
                closedGenericProxyTypeArguments,
                $"inherited interface method '{proxyMethod.FullName}'");
            var importedProxyMethod = new MemberRefUser(
                moduleDef,
                proxyMethod.Name,
                importedProxyMethodSig,
                inheritedInterface);
            generatedMethod.Overrides.Add(new MethodOverride(generatedMethod, moduleDef.UpdateRowId(importedProxyMethod)));
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

        /// <summary>
        /// Gets class override method attributes.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static MethodAttributes GetClassOverrideMethodAttributes(MethodDef proxyMethod)
        {
            var memberAccess = proxyMethod.Attributes & MethodAttributes.MemberAccessMask;
            if (memberAccess == 0 || memberAccess == MethodAttributes.Private)
            {
                memberAccess = MethodAttributes.Public;
            }

            var attributes = memberAccess | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.ReuseSlot;
            if (proxyMethod.IsSpecialName)
            {
                attributes |= MethodAttributes.SpecialName;
            }

            if (proxyMethod.IsRuntimeSpecialName)
            {
                attributes |= MethodAttributes.RTSpecialName;
            }

            return attributes;
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

                var proxyMethods = GetInterfaceMethods(proxyInterfaceType).ToList();
                AppendDuckIncludeTargetMethods(proxyMethods, targetType);
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

                    if (!TryResolveForwardBinding(mapping, targetType, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, proxyMethod, out var binding, out failure))
                    {
                        return false;
                    }

                    collectedBindings.Add(binding);
                }

                if (targetUsesReverseMethodAttributes &&
                    !TryValidateReverseImplementationCoverage(mapping, targetType, collectedBindings, out failure))
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

                var proxyMethods = GetClassProxyMethods(proxyClassType).ToList();
                AppendDuckIncludeTargetMethods(proxyMethods, targetType);
                if (proxyMethods.Count == 0)
                {
                    return true;
                }

                foreach (var proxyMethod in proxyMethods)
                {
                    if (!TryResolveForwardBinding(mapping, targetType, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, proxyMethod, out var binding, out failure))
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
                    !TryValidateReverseImplementationCoverage(mapping, targetType, collectedBindings, out failure))
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
        /// Determines whether a proxy method is an inherited abstract method declared on an open generic base type
        /// and already concretely overridden by a derived type in the same proxy hierarchy.
        /// </summary>
        /// <param name="proxyClassType">The proxy class type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsInheritedOpenGenericAbstractMethodWithConcreteOverride(TypeDef proxyClassType, MethodDef proxyMethod)
        {
            if (!proxyMethod.IsAbstract ||
                proxyMethod.DeclaringType is null ||
                ReferenceEquals(proxyMethod.DeclaringType, proxyClassType) ||
                proxyMethod.DeclaringType.GenericParameters.Count == 0)
            {
                return false;
            }

            var current = proxyClassType;
            while (current is not null && !ReferenceEquals(current, proxyMethod.DeclaringType))
            {
                foreach (var candidate in current.Methods)
                {
                    if (candidate.IsAbstract || !candidate.IsVirtual)
                    {
                        continue;
                    }

                    foreach (var methodOverride in candidate.Overrides)
                    {
                        var overriddenMethod = methodOverride.MethodDeclaration.ResolveMethodDef();
                        if (overriddenMethod is not null && MethodsMatch(overriddenMethod, proxyMethod))
                        {
                            return true;
                        }

                        if (overriddenMethod is null &&
                            string.Equals(methodOverride.MethodDeclaration.FullName, proxyMethod.FullName, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }

                current = current.BaseType?.ResolveTypeDef();
            }

            return false;
        }

        /// <summary>
        /// Ensures every [DuckReverseMethod] implementation member is bound to a generated reverse proxy method.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="bindings">The bindings value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryValidateReverseImplementationCoverage(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            failure = null;
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
                if (boundTargetMethodKeys.Contains(GetMethodCandidateKey(reverseImplementationMethod)))
                {
                    continue;
                }

                failure = CreateFailureResult(
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
        /// Gets interface methods.
        /// </summary>
        /// <param name="interfaceType">The interface type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<MethodDef> GetInterfaceMethods(TypeDef interfaceType)
        {
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(interfaceType);
            return proxyTypePlan?.InterfaceMethods ?? Array.Empty<MethodDef>();
        }

        /// <summary>
        /// Gets class proxy methods.
        /// </summary>
        /// <param name="proxyClassType">The proxy class type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<MethodDef> GetClassProxyMethods(TypeDef proxyClassType)
        {
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyClassType);
            return proxyTypePlan?.ClassMethods ?? Array.Empty<MethodDef>();
        }

        private static ProxyMethodPlan GetOrCreateProxyMethodPlan(MethodDef proxyMethod)
        {
            var declaringType = proxyMethod.DeclaringType;
            if (declaringType is null)
            {
                return new ProxyMethodPlan(proxyMethod);
            }

            return _currentExecutionContext?.GetOrCreateProxyTypePlan(declaringType).GetOrCreateMethodPlan(proxyMethod)
                ?? new ProxyMethodPlan(proxyMethod);
        }

        private static MethodPlan GetOrCreateMethodPlan(MethodDef method)
        {
            return _currentExecutionContext?.GetOrCreateMethodPlan(method)
                ?? new MethodPlan(method);
        }

        /// <summary>
        /// Appends target methods explicitly marked with DuckInclude into the proxy method list.
        /// </summary>
        /// <param name="proxyMethods">The proxy methods value.</param>
        /// <param name="targetType">The target type value.</param>
        private static void AppendDuckIncludeTargetMethods(List<MethodDef> proxyMethods, TypeDef targetType)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var visitedMethodKeys = new HashSet<string>(proxyMethods.Select(method => $"{method.Name}::{method.MethodSig}"), StringComparer.Ordinal);
                var duckIncludeMethods = _currentExecutionContext?.GetOrCreateDuckIncludeMethods(targetType) ?? Array.Empty<MethodDef>();
                foreach (var method in duckIncludeMethods)
                {
                    var key = $"{method.Name}::{method.MethodSig}";
                    if (visitedMethodKeys.Add(key))
                    {
                        proxyMethods.Add(method);
                    }
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.DuckIncludeCollectionSeconds += seconds);
            }
        }

        /// <summary>
        /// Determines whether supported class proxy method.
        /// </summary>
        /// <param name="method">The method value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsSupportedClassProxyMethod(MethodDef method)
        {
            if (method.IsConstructor || method.IsStatic || !method.IsVirtual || method.IsFinal)
            {
                return false;
            }

            if (string.Equals(method.DeclaringType?.FullName, "System.Object", StringComparison.Ordinal) &&
                !method.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, DuckIncludeAttributeTypeName, StringComparison.Ordinal)))
            {
                return false;
            }

            if (!method.IsPublic &&
                !method.IsFamily &&
                !method.IsAssembly &&
                !method.IsFamilyOrAssembly &&
                !method.IsFamilyAndAssembly)
            {
                return false;
            }

            return !IsDuckIgnoreMethod(method);
        }

        /// <summary>
        /// Finds a supported parameterless base constructor for class proxy emission.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static MethodDef? FindSupportedProxyBaseConstructor(TypeDef proxyType)
        {
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyType);
            return proxyTypePlan?.SupportedBaseConstructor;
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
                var bindingPlanCacheKey = BuildForwardBindingPlanCacheKey(
                    mapping,
                    targetTypePlan?.IdentityKey ?? BuildTypeIdentityKey(targetType),
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

                    failure = CreateFailureResult(
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
                    !string.IsNullOrWhiteSpace(proxyMethodPlan.ReverseUsageFailureDetail))
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

                if (!fieldOnly)
                {
                    var hasSuccessfulMethodBinding = false;
                    var successfulMethodBinding = default(ForwardBinding);
                    MethodDef? successfulTargetMethod = null;
                    var successfulMethodNameOrdinal = -1;
                    foreach (var targetMethodCandidate in FindForwardTargetMethodCandidates(mapping, targetType, proxyMethodPlan, closedGenericMethodArguments, allowPrivateBaseMethodCandidates))
                    {
                        var targetMethod = targetMethodCandidate.Method;
                        if (TryCreateForwardMethodBinding(proxyMethod, targetMethod, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, closedGenericMethodArguments, isReverseMapping, out var methodBinding, out var methodFailure))
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

                        firstMethodFailure ??= methodFailure;
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

                        if (!string.IsNullOrWhiteSpace(fieldFailureReason))
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

                    failure = CreateFailureResult(
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

                failure = CreateFailureResult(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                    StatusCodeMissingMethod,
                    $"Target member for proxy method '{proxyMethod.FullName}' was not found.");
                _currentExecutionContext?.CacheForwardBindingPlan(bindingPlanCacheKey, CreateFailurePlan(failure));
                return false;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardBindingResolutionSeconds += seconds);
            }
        }

        /// <summary>
        /// Attempts to detect incorrect forward usage of [DuckReverseMethod] on proxy members.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="detail">The detail value.</param>
        /// <returns>true when [DuckReverseMethod] is used on a forward proxy member; otherwise, false.</returns>
        private static bool TryGetForwardReverseUsageFailure(MethodDef proxyMethod, out string? detail)
        {
            detail = null;
            if (proxyMethod.CustomAttributes.Any(IsReverseMethodAttribute))
            {
                detail = $"Proxy method '{proxyMethod.FullName}' is marked with [DuckReverseMethod] but forward mapping requires regular members.";
                return true;
            }

            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty) &&
                declaringProperty!.CustomAttributes.Any(IsReverseMethodAttribute))
            {
                detail = $"Proxy property '{declaringProperty.FullName}' is marked with [DuckReverseMethod] but forward mapping requires regular members.";
                return true;
            }

            return false;
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

            detail = $"Target member '{targetMethod.FullName}' belongs to value type '{targetMethod.DeclaringType.FullName}' and cannot be set by proxy method '{proxyMethod.FullName}'.";
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
        /// Determines whether failure detail corresponds to invalid type conversion semantics.
        /// </summary>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true when the detail indicates invalid type conversion; otherwise, false.</returns>
        private static bool IsInvalidTypeConversionFailure(string failureReason)
        {
            return failureReason.IndexOf("Type conversion is not supported", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Determines whether failure detail corresponds to parameter-signature mismatch semantics.
        /// </summary>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true when the detail indicates parameter-signature mismatch; otherwise, false.</returns>
        private static bool IsParameterSignatureFailure(string failureReason)
        {
            return failureReason.IndexOf("Parameter count mismatch", StringComparison.Ordinal) >= 0 ||
                   failureReason.IndexOf("Parameter direction mismatch", StringComparison.Ordinal) >= 0 ||
                   failureReason.IndexOf("Parameter type mismatch", StringComparison.Ordinal) >= 0 ||
                   failureReason.IndexOf("By-ref parameter mismatch", StringComparison.Ordinal) >= 0 ||
                   failureReason.IndexOf("Generic arity mismatch", StringComparison.Ordinal) >= 0;
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
                        detail = $"Target property '{property.FullName}' can't be written for proxy method '{proxyMethod.FullName}'.";
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

        /// <summary>
        /// Extracts property name from a setter method candidate name.
        /// </summary>
        /// <param name="methodName">The method name value.</param>
        /// <returns>The resulting property name, or null when not a setter accessor.</returns>
        private static string? ExtractSetterPropertyName(string methodName)
        {
            const string setterPrefix = "set_";
            if (string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            var setterIndex = methodName.LastIndexOf(setterPrefix, StringComparison.Ordinal);
            if (setterIndex < 0)
            {
                return null;
            }

            var nameStart = setterIndex + setterPrefix.Length;
            if (nameStart >= methodName.Length)
            {
                return null;
            }

            return methodName.Substring(nameStart);
        }

        /// <summary>
        /// Determines whether property accessors are effectively private-only.
        /// </summary>
        /// <param name="property">The property value.</param>
        /// <returns>true when all available accessors are private; otherwise, false.</returns>
        private static bool IsPropertyPrivate(PropertyDef property)
        {
            var hasAccessor = property.GetMethod is not null || property.SetMethod is not null;
            if (!hasAccessor)
            {
                return false;
            }

            var getterPrivate = property.GetMethod is null || property.GetMethod.IsPrivate;
            var setterPrivate = property.SetMethod is null || property.SetMethod.IsPrivate;
            return getterPrivate && setterPrivate;
        }

        private static bool IsMethodCandidateAllowedByBindingFlags(TargetMethodCandidate candidate, BindingFlags bindingFlags, bool treatInheritedAsDeclared = false)
        {
            var method = candidate.Method;
            return IsMemberAllowedByBindingFlags(method.IsStatic, method.IsPublic, candidate.IsInherited && !treatInheritedAsDeclared, bindingFlags);
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

        /// <summary>
        /// Finds forward-mapping target method candidates for a proxy method.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<ForwardMethodCandidate> FindForwardTargetMethodCandidates(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            ProxyMethodPlan proxyMethodPlan,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            bool allowPrivateBaseMembers)
        {
            var proxyMethod = proxyMethodPlan.Method;
            var explicitInterfaceTypeNames = proxyMethodPlan.ExplicitInterfaceTypeNames;
            var useRelaxedNameComparison = proxyMethodPlan.UseRelaxedNameComparison;
            var configuredParameterTypeNames = proxyMethodPlan.ConfiguredParameterTypeNames;
            var expectedGenericArity = closedGenericMethodArguments?.Count ?? (int)proxyMethod.MethodSig.GenParamCount;

            if (mapping.Mode == DuckTypeAotMappingMode.Reverse)
            {
                if (!HasReverseMethodAttributes(targetType))
                {
                    foreach (var candidate in FindDefaultTargetMethodCandidates(
                                 targetType,
                                 proxyMethodPlan,
                                 explicitInterfaceTypeNames,
                                 useRelaxedNameComparison,
                                 expectedGenericArity,
                                 configuredParameterTypeNames,
                                 allowPrivateBaseMembers,
                                 allowTrailingOptionalTargetParameters: false))
                    {
                        yield return candidate;
                    }

                    yield break;
                }

                var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reverseCandidate in FindReverseTargetMethodCandidates(targetType, proxyMethod))
                {
                    // Candidate must match both generic arity and parameter count before deeper compatibility checks.
                    if (reverseCandidate.MethodSig.GenParamCount != expectedGenericArity ||
                        reverseCandidate.MethodSig.Params.Count != proxyMethod.MethodSig.Params.Count)
                    {
                        continue;
                    }

                    var reverseCandidateKey = GetMethodCandidateKey(reverseCandidate);
                    if (emittedCandidates.Add(reverseCandidateKey))
                    {
                        yield return new ForwardMethodCandidate(reverseCandidate, nameOrdinal: 0);
                    }
                }

                yield break;
            }

            foreach (var candidate in FindDefaultTargetMethodCandidates(
                         targetType,
                         proxyMethodPlan,
                         explicitInterfaceTypeNames,
                         useRelaxedNameComparison,
                         expectedGenericArity,
                         configuredParameterTypeNames,
                         allowPrivateBaseMembers,
                         allowTrailingOptionalTargetParameters: true))
            {
                yield return candidate;
            }
        }

        /// <summary>
        /// Determines whether reverse method attributes are present on the target type hierarchy.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <returns>true when any method or property has [DuckReverseMethod]; otherwise, false.</returns>
        private static bool HasReverseMethodAttributes(TypeDef targetType)
        {
            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
            return targetTypePlan?.HasReverseMethodAttributes == true;
        }

        /// <summary>
        /// Finds default target method candidates by name and signature prefilters.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="explicitInterfaceTypeNames">The explicit interface type names value.</param>
        /// <param name="useRelaxedNameComparison">The use relaxed name comparison value.</param>
        /// <param name="expectedGenericArity">The expected generic arity value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<ForwardMethodCandidate> FindDefaultTargetMethodCandidates(
            TypeDef targetType,
            ProxyMethodPlan proxyMethodPlan,
            IReadOnlyList<string> explicitInterfaceTypeNames,
            bool useRelaxedNameComparison,
            int expectedGenericArity,
            IReadOnlyList<string> configuredParameterTypeNames,
            bool allowPrivateBaseMembers,
            bool allowTrailingOptionalTargetParameters)
        {
            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
            if (targetTypePlan is not null)
            {
                foreach (var candidate in targetTypePlan.GetForwardMethodCandidates(
                             proxyMethodPlan,
                             explicitInterfaceTypeNames,
                             useRelaxedNameComparison,
                             expectedGenericArity,
                             configuredParameterTypeNames,
                             allowPrivateBaseMembers,
                             allowTrailingOptionalTargetParameters))
                {
                    yield return candidate;
                }

                yield break;
            }

            var proxyMethod = proxyMethodPlan.Method;
            var candidateMethodNames = proxyMethodPlan.ForwardTargetMethodNames;
            var proxyParameterCount = proxyMethod.MethodSig.Params.Count;
            for (var candidateMethodNameIndex = 0; candidateMethodNameIndex < candidateMethodNames.Count; candidateMethodNameIndex++)
            {
                var candidateMethodName = candidateMethodNames[candidateMethodNameIndex];
                foreach (var candidateEntry in Array.Empty<TargetMethodCandidate>())
                {
                    var candidate = candidateEntry.Method;
                    var candidateMethodActualName = candidateEntry.MethodName;
                    if (!IsForwardTargetMethodNameMatch(
                            candidateMethodActualName,
                            candidateMethodName,
                            explicitInterfaceTypeNames,
                            useRelaxedNameComparison,
                            proxyMethodPlan.UseIgnoreCaseMemberMatching))
                    {
                        continue;
                    }

                    if (configuredParameterTypeNames.Count > 0 &&
                        !IsForwardCandidateParameterTypeNameMatch(candidateEntry, configuredParameterTypeNames))
                    {
                        continue;
                    }

                    if (!allowPrivateBaseMembers &&
                        candidateEntry.IsInherited &&
                        candidate.IsPrivate)
                    {
                        continue;
                    }

                    yield return new ForwardMethodCandidate(candidate, candidateMethodNameIndex);
                }
            }
        }

        /// <summary>
        /// Determines whether candidate method parameters match configured [Duck(ParameterTypeNames=...)] values.
        /// </summary>
        /// <param name="candidate">The candidate value.</param>
        /// <param name="configuredParameterTypeNames">The configured parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsForwardCandidateParameterTypeNameMatch(TargetMethodCandidate candidate, IReadOnlyList<string> configuredParameterTypeNames)
        {
            if (configuredParameterTypeNames.Count != candidate.ParameterTypeComparisonNames.Count)
            {
                return false;
            }

            for (var i = 0; i < configuredParameterTypeNames.Count; i++)
            {
                var configuredName = configuredParameterTypeNames[i];
                if (string.IsNullOrWhiteSpace(configuredName))
                {
                    return false;
                }

                if (!candidate.ParameterTypeComparisonNames[i].Contains(configuredName))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets method candidate key.
        /// </summary>
        /// <param name="candidate">The candidate value.</param>
        /// <returns>The resulting string value.</returns>
        private static string GetMethodCandidateKey(MethodDef candidate)
        {
            return BuildMethodIdentityKey(candidate);
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

        private static string BuildNullableTypeSigCacheKey(TypeSig? typeSig)
        {
            return typeSig is null ? string.Empty : BuildTypeSigCacheKey(typeSig);
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

        private static string BuildForwardBindingPlanCacheKey(
            DuckTypeAotMapping mapping,
            string targetTypeIdentityKey,
            ProxyMethodPlan proxyMethodPlan,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            return string.Concat(
                mapping.Mode.ToString(),
                "|",
                targetTypeIdentityKey,
                "|",
                proxyMethodPlan.IdentityKey,
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericProxyTypeArguments),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericTargetTypeArguments));
        }

        private static string BuildForwardMethodBindingPlanCacheKey(
            ProxyMethodPlan proxyMethodPlan,
            MethodPlan targetMethodPlan,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            bool isReverseMapping)
        {
            return string.Concat(
                isReverseMapping ? "reverse" : "forward",
                "|",
                proxyMethodPlan.IdentityKey,
                "|",
                targetMethodPlan.IdentityKey,
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericProxyTypeArguments),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericTargetTypeArguments),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericMethodArguments));
        }

        private static string BuildStructCopyBindingPlanCacheKey(TypeDef targetType, FieldDef proxyField, IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            return string.Concat(
                BuildTypeIdentityKey(targetType),
                "|",
                BuildFieldIdentityKey(proxyField),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericTargetTypeArguments));
        }

        private static string BuildMethodReturnConversionCacheKey(TypeSig proxyReturnType, TypeSig targetReturnType, bool isReverseMapping, string discriminator = "")
        {
            return string.Concat(
                discriminator,
                "|",
                isReverseMapping ? "reverse" : "forward",
                "|",
                BuildTypeSigCacheKey(proxyReturnType),
                "|",
                BuildTypeSigCacheKey(targetReturnType));
        }

        private static string BuildMethodArgumentConversionCacheKey(TypeSig proxyParameterType, TypeSig targetParameterType, bool isReverseMapping, bool enforceMethodSelectionRules)
        {
            return string.Concat(
                isReverseMapping ? "reverse" : "forward",
                "|",
                enforceMethodSelectionRules ? "strict" : "loose",
                "|",
                BuildTypeSigCacheKey(proxyParameterType),
                "|",
                BuildTypeSigCacheKey(targetParameterType));
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

        /// <summary>
        /// Finds reverse-mapping target method candidates for a proxy method.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<MethodDef> FindReverseTargetMethodCandidates(TypeDef targetType, MethodDef proxyMethod)
        {
            var proxyMethodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();
            var proxyParameterTypes = proxyMethod.MethodSig.Params.ToArray();
            var proxyParameterTypeNames = proxyMethod.MethodSig.Params
                                              .Select(GetTypeComparisonNames)
                                              .ToArray();
            var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);

            foreach (var method in targetType.Methods)
            {
                if (method.IsConstructor || method.IsStatic)
                {
                    continue;
                }

                foreach (var reverseAttribute in method.CustomAttributes.Where(IsReverseMethodAttribute))
                {
                    if (!IsReverseCandidateMatch(proxyMethodName, proxyParameterTypes, proxyParameterTypeNames, reverseAttribute, method.Name.String ?? method.Name.ToString()))
                    {
                        continue;
                    }

                    var candidateKey = $"{method.DeclaringType.FullName}::{method.Name}::{method.MethodSig}";
                    if (emittedCandidates.Add(candidateKey))
                    {
                        yield return method;
                    }
                }
            }

            var current = targetType;
            while (current is not null)
            {
                foreach (var property in current.Properties)
                {
                    if (!IsReverseImplementationPropertyVisibleToDynamic(property))
                    {
                        continue;
                    }

                    foreach (var reverseAttribute in property.CustomAttributes.Where(IsReverseMethodAttribute))
                    {
                        if (property.GetMethod is not null && IsReverseCandidateMatch(proxyMethodName, proxyParameterTypes, proxyParameterTypeNames, reverseAttribute, "get_" + property.Name))
                        {
                            var candidateKey = $"{property.GetMethod.DeclaringType.FullName}::{property.GetMethod.Name}::{property.GetMethod.MethodSig}";
                            if (emittedCandidates.Add(candidateKey))
                            {
                                yield return property.GetMethod;
                            }
                        }

                        if (property.SetMethod is not null && IsReverseCandidateMatch(proxyMethodName, proxyParameterTypes, proxyParameterTypeNames, reverseAttribute, "set_" + property.Name))
                        {
                            var candidateKey = $"{property.SetMethod.DeclaringType.FullName}::{property.SetMethod.Name}::{property.SetMethod.MethodSig}";
                            if (emittedCandidates.Add(candidateKey))
                            {
                                yield return property.SetMethod;
                            }
                        }
                    }
                }

                current = current.BaseType?.ResolveTypeDef();
            }
        }

        /// <summary>
        /// Checks whether dynamic reverse duck typing would consider the implementation property.
        /// </summary>
        /// <param name="property">Implementation property.</param>
        /// <returns><c>true</c> when the property has at least one public accessor.</returns>
        private static bool IsReverseImplementationPropertyVisibleToDynamic(PropertyDef property)
        {
            return property.GetMethod?.IsPublic == true || property.SetMethod?.IsPublic == true;
        }

        /// <summary>
        /// Determines whether forward target method name match.
        /// </summary>
        /// <param name="candidateMethodName">The candidate method name value.</param>
        /// <param name="requestedMethodName">The requested method name value.</param>
        /// <param name="explicitInterfaceTypeNames">The explicit interface type names value.</param>
        /// <param name="useRelaxedNameComparison">The use relaxed name comparison value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsForwardTargetMethodNameMatch(
            string candidateMethodName,
            string requestedMethodName,
            IReadOnlyList<string> explicitInterfaceTypeNames,
            bool useRelaxedNameComparison,
            bool useIgnoreCaseMemberMatching)
        {
            var comparison = useIgnoreCaseMemberMatching ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(candidateMethodName, requestedMethodName, comparison))
            {
                return true;
            }

            // Relaxed mode accepts explicit-interface method naming (TypeName.MethodName).
            if (useRelaxedNameComparison &&
                candidateMethodName.EndsWith("." + requestedMethodName, StringComparison.Ordinal))
            {
                return true;
            }

            for (var i = 0; i < explicitInterfaceTypeNames.Count; i++)
            {
                var explicitInterfaceTypeName = explicitInterfaceTypeNames[i];
                if (string.IsNullOrWhiteSpace(explicitInterfaceTypeName))
                {
                    continue;
                }

                var normalizedInterfaceTypeName = explicitInterfaceTypeName.Replace("+", ".");
                if (string.Equals(candidateMethodName, $"{normalizedInterfaceTypeName}.{requestedMethodName}", comparison))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to get forward explicit interface type names.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="explicitInterfaceTypeNames">The explicit interface type names value.</param>
        /// <param name="useRelaxedNameComparison">The use relaxed name comparison value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetForwardExplicitInterfaceTypeNames(
            MethodDef proxyMethod,
            out IReadOnlyList<string> explicitInterfaceTypeNames,
            out bool useRelaxedNameComparison)
        {
            var explicitInterfaceTypeNamesList = new List<string>();
            var useRelaxed = false;
            explicitInterfaceTypeNames = explicitInterfaceTypeNamesList;

            AddFrom(proxyMethod.CustomAttributes);
            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty))
            {
                AddFrom(declaringProperty!.CustomAttributes);
            }

            useRelaxedNameComparison = useRelaxed;
            return useRelaxed || explicitInterfaceTypeNamesList.Count > 0;

            void AddFrom(IList<CustomAttribute> customAttributes)
            {
                foreach (var customAttribute in customAttributes)
                {
                    if (!IsDuckAttribute(customAttribute))
                    {
                        continue;
                    }

                    foreach (var namedArgument in customAttribute.NamedArguments)
                    {
                        if (!string.Equals(namedArgument.Name.String, "ExplicitInterfaceTypeName", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (!TryGetStringArgument(namedArgument.Argument.Value, out var configuredName))
                        {
                            continue;
                        }

                        foreach (var candidateName in SplitDuckNames(configuredName!))
                        {
                            if (string.Equals(candidateName, "*", StringComparison.Ordinal))
                            {
                                useRelaxed = true;
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(candidateName))
                            {
                                explicitInterfaceTypeNamesList.Add(candidateName);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to get forward parameter type names configured via [Duck(ParameterTypeNames=...)].
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="parameterTypeNames">The parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetForwardParameterTypeNames(MethodDef proxyMethod, out IReadOnlyList<string> parameterTypeNames)
        {
            var names = new List<string>();
            parameterTypeNames = names;

            if (TryGetForwardParameterTypeNamesFromAttributes(proxyMethod.CustomAttributes, out parameterTypeNames))
            {
                return true;
            }

            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty) &&
                TryGetForwardParameterTypeNamesFromAttributes(declaringProperty!.CustomAttributes, out parameterTypeNames))
            {
                return true;
            }

            parameterTypeNames = names;
            return false;
        }

        /// <summary>
        /// Attempts to get forward parameter type names from custom attributes.
        /// </summary>
        /// <param name="customAttributes">The custom attributes value.</param>
        /// <param name="parameterTypeNames">The parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetForwardParameterTypeNamesFromAttributes(IList<CustomAttribute> customAttributes, out IReadOnlyList<string> parameterTypeNames)
        {
            foreach (var customAttribute in customAttributes)
            {
                if (!IsDuckAttribute(customAttribute))
                {
                    continue;
                }

                if (TryGetDuckAttributeParameterTypeNames(customAttribute, out parameterTypeNames))
                {
                    return true;
                }
            }

            parameterTypeNames = Array.Empty<string>();
            return false;
        }

        /// <summary>
        /// Attempts to build a forward method binding plan between proxy and target methods.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="binding">The binding value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateForwardMethodBinding(
            MethodDef proxyMethod,
            MethodDef targetMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            bool isReverseMapping,
            out ForwardMethodBindingInfo binding,
            out MethodCompatibilityFailure? failure)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var proxyMethodPlan = GetOrCreateProxyMethodPlan(proxyMethod);
                var targetMethodPlan = GetOrCreateMethodPlan(targetMethod);
                var cacheKeyStopwatch = StartProfilePhase();
                var cacheKey = BuildForwardMethodBindingPlanCacheKey(
                    proxyMethodPlan,
                    targetMethodPlan,
                    closedGenericProxyTypeArguments,
                    closedGenericTargetTypeArguments,
                    closedGenericMethodArguments,
                    isReverseMapping);
                StopProfilePhase(cacheKeyStopwatch, seconds => _currentProfile!.ForwardMethodBindingPlanCacheKeyBuildSeconds += seconds);
                if (_currentExecutionContext?.TryGetForwardMethodBindingPlan(cacheKey, out var cachedPlan) == true)
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
                    failure = new MethodCompatibilityFailure(cachedPlan.FailureDetail ?? $"Method binding for '{proxyMethod.FullName}' is not compatible.");
                    return false;
                }

                if (_currentProfile is not null)
                {
                    _currentProfile.ForwardBindingPlanCacheMisses++;
                }

                var proxyParameterCount = proxyMethod.MethodSig.Params.Count;
                var targetParameterCount = targetMethod.MethodSig.Params.Count;
                if (isReverseMapping
                    ? proxyParameterCount != targetParameterCount
                    : !IsParameterCountCompatibleWithProxy(targetMethod, proxyParameterCount, allowTrailingOptionalTargetParameters: true))
                {
                    failure = new MethodCompatibilityFailure(
                        $"Parameter count mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    binding = default;
                    _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure.Value.Detail));
                    return false;
                }

                if (closedGenericMethodArguments is null)
                {
                    if (proxyMethod.MethodSig.GenParamCount != targetMethod.MethodSig.GenParamCount)
                    {
                        failure = new MethodCompatibilityFailure(
                            $"Generic arity mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                        binding = default;
                        _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure.Value.Detail));
                        return false;
                    }
                }
                else
                {
                    if (proxyMethod.MethodSig.GenParamCount != 0 || targetMethod.MethodSig.GenParamCount != closedGenericMethodArguments.Count)
                    {
                        failure = new MethodCompatibilityFailure(
                            $"Generic arity mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                        binding = default;
                        _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure.Value.Detail));
                        return false;
                    }
                }

                var parameterBindings = new MethodParameterBinding[proxyParameterCount];
                for (var parameterIndex = 0; parameterIndex < proxyParameterCount; parameterIndex++)
                {
                    if (!TryCreateForwardMethodParameterBinding(proxyMethod, targetMethod, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, closedGenericMethodArguments, parameterIndex, isReverseMapping, out var parameterBinding, out failure))
                    {
                        binding = default;
                        _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure?.Detail ?? $"Method binding for '{proxyMethod.FullName}' is not compatible."));
                        return false;
                    }

                    parameterBindings[parameterIndex] = parameterBinding;
                }

                var proxyReturnType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                var targetReturnType = SubstituteTypeAndMethodGenericTypeArguments(targetMethod.MethodSig.RetType, closedGenericTargetTypeArguments, closedGenericMethodArguments);
                if (!TryCreateReturnConversion(proxyReturnType, targetReturnType, isReverseMapping, out var returnConversion))
                {
                    failure = new MethodCompatibilityFailure(
                        $"Return type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    binding = default;
                    _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure.Value.Detail));
                    return false;
                }

                binding = new ForwardMethodBindingInfo(parameterBindings, returnConversion, closedGenericMethodArguments, targetParameterCount - proxyParameterCount);
                failure = null;
                _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(binding));
                return true;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardMethodBindingSeconds += seconds);
            }
        }

        private static bool IsParameterCountCompatibleWithProxy(MethodDef targetMethod, int proxyParameterCount, bool allowTrailingOptionalTargetParameters)
        {
            var targetParameterCount = targetMethod.MethodSig.Params.Count;
            if (targetParameterCount == proxyParameterCount)
            {
                return true;
            }

            if (!allowTrailingOptionalTargetParameters || targetParameterCount < proxyParameterCount)
            {
                return false;
            }

            return AreTrailingTargetParametersOptional(targetMethod, proxyParameterCount);
        }

        private static bool AreTrailingTargetParametersOptional(MethodDef targetMethod, int proxyParameterCount)
        {
            for (var parameterIndex = proxyParameterCount; parameterIndex < targetMethod.MethodSig.Params.Count; parameterIndex++)
            {
                if (!IsTargetParameterOptional(targetMethod, parameterIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsTargetParameterOptional(MethodDef targetMethod, int parameterIndex)
        {
            foreach (var parameter in targetMethod.Parameters)
            {
                if (parameter.MethodSigIndex != parameterIndex)
                {
                    continue;
                }

                var paramDef = parameter.ParamDef;
                return paramDef?.IsOptional == true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to build a forward parameter binding plan for a single method parameter.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="parameterIndex">The parameter index value.</param>
        /// <param name="parameterBinding">The parameter binding value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateForwardMethodParameterBinding(
            MethodDef proxyMethod,
            MethodDef targetMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            int parameterIndex,
            bool isReverseMapping,
            out MethodParameterBinding parameterBinding,
            out MethodCompatibilityFailure? failure)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var proxyMethodPlan = GetOrCreateProxyMethodPlan(proxyMethod);
                var targetMethodPlan = GetOrCreateMethodPlan(targetMethod);
                var proxyParameterType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.Params[parameterIndex], closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                var targetParameterType = SubstituteTypeAndMethodGenericTypeArguments(targetMethod.MethodSig.Params[parameterIndex], closedGenericTargetTypeArguments, closedGenericMethodArguments);

                var proxyIsByRef = proxyParameterType.ElementType == ElementType.ByRef;
                var targetIsByRef = targetParameterType.ElementType == ElementType.ByRef;
                if (proxyIsByRef != targetIsByRef)
                {
                    failure = new MethodCompatibilityFailure(
                        $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                if (!proxyIsByRef)
                {
                    if (!TryCreateMethodArgumentConversion(proxyParameterType, targetParameterType, isReverseMapping, enforceMethodSelectionRules: true, out var argumentConversion))
                    {
                        failure = new MethodCompatibilityFailure(
                            $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                        parameterBinding = default;
                        return false;
                    }

                    parameterBinding = MethodParameterBinding.ForStandard(proxyParameterType, targetParameterType, argumentConversion);
                    failure = null;
                    return true;
                }

                _ = proxyMethodPlan.TryGetParameterDirection(parameterIndex, out var proxyParameterDirection);
                _ = targetMethodPlan.TryGetParameterDirection(parameterIndex, out var targetParameterDirection);
                var proxyIsOut = proxyParameterDirection.IsOut;
                var targetIsOut = targetParameterDirection.IsOut;
                if (proxyIsOut != targetIsOut)
                {
                    failure = new MethodCompatibilityFailure(
                        $"Parameter direction mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                // Both proxy and target parameters must expose by-ref element types for by-ref adaptation.
                if (!TryGetByRefElementType(proxyParameterType, out var proxyByRefElementTypeSig) ||
                    !TryGetByRefElementType(targetParameterType, out var targetByRefElementTypeSig))
                {
                    failure = new MethodCompatibilityFailure(
                        $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                if (!MatchesByRefDynamicMethodParameterSelectionRule(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, isReverseMapping))
                {
                    failure = new MethodCompatibilityFailure(
                        $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                if (AreTypesEquivalent(proxyParameterType, targetParameterType))
                {
                    parameterBinding = MethodParameterBinding.ForByRefDirect(
                        proxyParameterType,
                        targetParameterType,
                        proxyByRefElementTypeSig!,
                        targetByRefElementTypeSig!,
                        proxyIsOut);
                    failure = null;
                    return true;
                }

                MethodArgumentConversion preCallConversion;
                if (proxyIsOut)
                {
                    preCallConversion = MethodArgumentConversion.None();
                }
                else if (!TryCreateMethodArgumentConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, isReverseMapping, enforceMethodSelectionRules: true, out preCallConversion))
                {
                    failure = new MethodCompatibilityFailure(
                        $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                if (!TryCreateByRefPostCallConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, isReverseMapping, out var postCallConversion))
                {
                    failure = new MethodCompatibilityFailure(
                        $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                parameterBinding = MethodParameterBinding.ForByRefWithLocal(
                    proxyParameterType,
                    targetParameterType,
                    proxyByRefElementTypeSig!,
                    targetByRefElementTypeSig!,
                    proxyIsOut,
                    preCallConversion,
                    postCallConversion);
                failure = null;
                return true;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardParameterBindingSeconds += seconds);
            }
        }

        /// <summary>
        /// Attempts to select an argument-conversion strategy from proxy parameter type to target parameter type.
        /// </summary>
        /// <param name="proxyParameterType">The proxy parameter type value.</param>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <param name="argumentConversion">The argument conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateMethodArgumentConversion(
            TypeSig proxyParameterType,
            TypeSig targetParameterType,
            bool isReverseMapping,
            bool enforceMethodSelectionRules,
            out MethodArgumentConversion argumentConversion)
        {
            var cacheKeyStopwatch = StartProfilePhase();
            var conversionCacheKey = BuildMethodArgumentConversionCacheKey(proxyParameterType, targetParameterType, isReverseMapping, enforceMethodSelectionRules);
            StopProfilePhase(cacheKeyStopwatch, seconds => _currentProfile!.MethodArgumentConversionCacheKeyBuildSeconds += seconds);
            if (_currentExecutionContext?.TryGetMethodArgumentConversion(conversionCacheKey, out var cachedConversion) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ConversionPlanCacheHits++;
                }

                if (cachedConversion.Succeeded)
                {
                    argumentConversion = cachedConversion.Conversion;
                    return true;
                }

                argumentConversion = default;
                return false;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.ConversionPlanCacheMisses++;
            }

            if (AreTypesEquivalent(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.None();
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            if (TryGetValueWithTypeArgument(proxyParameterType, out var proxyValueWithTypeArgument))
            {
                // ValueWithType<T>: unwrap first, then reuse normal argument conversion rules for T -> target.
                if (!TryCreateMethodArgumentConversion(proxyValueWithTypeArgument!, targetParameterType, isReverseMapping, enforceMethodSelectionRules, out argumentConversion))
                {
                    argumentConversion = default;
                    _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry());
                    return false;
                }

                argumentConversion = argumentConversion.WithValueWithTypeUnwrap(proxyParameterType, proxyValueWithTypeArgument!);
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            var requiresDuckChaining = isReverseMapping
                                           ? IsDuckChainingRequired(proxyParameterType, targetParameterType)
                                           : IsDuckChainingRequired(targetParameterType, proxyParameterType);
            if (requiresDuckChaining)
            {
                if (isReverseMapping && ShouldUseDuckChainForReverseArgument(targetParameterType))
                {
                    argumentConversion = MethodArgumentConversion.DuckChainToProxy(targetParameterType, proxyParameterType);
                }
                else
                {
                    argumentConversion = MethodArgumentConversion.ExtractDuckTypeInstance(proxyParameterType, targetParameterType);
                }

                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            if (enforceMethodSelectionRules &&
                !MatchesDynamicMethodParameterSelectionRule(proxyParameterType, targetParameterType))
            {
                argumentConversion = default;
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry());
                return false;
            }

            if (CanUseTypeConversion(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.TypeConversion(proxyParameterType, targetParameterType);
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            argumentConversion = default;
            _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry());
            return false;
        }

        /// <summary>
        /// Determines whether proxy/target parameter types satisfy dynamic method-candidate selection rules.
        /// </summary>
        /// <param name="proxyParameterType">The proxy parameter type value.</param>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool MatchesDynamicMethodParameterSelectionRule(TypeSig proxyParameterType, TypeSig targetParameterType)
        {
            var proxyRuntimeType = TryResolveRuntimeType(proxyParameterType);
            var targetRuntimeType = TryResolveRuntimeType(targetParameterType);

            if (targetRuntimeType is not null && targetRuntimeType.IsGenericParameter)
            {
                return true;
            }

            // Dynamic selector requires exact matches for non-enum value-type parameters.
            if (proxyRuntimeType is not null && targetRuntimeType is not null)
            {
                if (proxyRuntimeType.IsValueType && !proxyRuntimeType.IsEnum)
                {
                    return proxyRuntimeType == targetRuntimeType;
                }

                // For concrete reference types (except object), dynamic selector requires assignability to the target parameter.
                if (proxyRuntimeType.IsClass &&
                    !proxyRuntimeType.IsAbstract &&
                    proxyRuntimeType != typeof(object))
                {
                    return MatchesDynamicConcreteClassParameterSelectionRule(proxyRuntimeType, targetRuntimeType);
                }

                // Interfaces/abstract/object remain eligible for duck-chaining/type adaptation.
                return true;
            }

            return MatchesDynamicMethodParameterSelectionRuleFromMetadata(proxyParameterType, targetParameterType, proxyRuntimeType, targetRuntimeType);
        }

        /// <summary>
        /// Determines whether by-ref element types satisfy dynamic method-candidate selection rules.
        /// </summary>
        /// <param name="proxyParameterElementType">The proxy parameter element type value.</param>
        /// <param name="targetParameterElementType">The target parameter element type value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <returns>true when the by-ref candidate should remain eligible; otherwise, false.</returns>
        private static bool MatchesByRefDynamicMethodParameterSelectionRule(
            TypeSig proxyParameterElementType,
            TypeSig targetParameterElementType,
            bool isReverseMapping)
        {
            if (MatchesDynamicMethodParameterSelectionRule(proxyParameterElementType, targetParameterElementType))
            {
                return true;
            }

            return isReverseMapping &&
                   (IsDuckChainingRequired(targetParameterElementType, proxyParameterElementType) ||
                    IsDuckChainingRequired(proxyParameterElementType, targetParameterElementType));
        }

        /// <summary>
        /// Determines whether a concrete class proxy parameter matches dynamic selection rules,
        /// including generic-argument compatibility fallbacks used by the runtime resolver.
        /// </summary>
        /// <param name="proxyRuntimeType">The proxy runtime type value.</param>
        /// <param name="targetRuntimeType">The target runtime type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool MatchesDynamicConcreteClassParameterSelectionRule(Type proxyRuntimeType, Type targetRuntimeType)
        {
            if (targetRuntimeType.IsAssignableFrom(proxyRuntimeType))
            {
                return true;
            }

            if (!targetRuntimeType.IsGenericType || !proxyRuntimeType.IsGenericType)
            {
                return false;
            }

            if (targetRuntimeType.ToString() == proxyRuntimeType.ToString())
            {
                return true;
            }

            var targetGenericArguments = targetRuntimeType.GenericTypeArguments;
            var proxyGenericArguments = proxyRuntimeType.GenericTypeArguments;
            if (targetGenericArguments.Length != proxyGenericArguments.Length)
            {
                return false;
            }

            for (var i = 0; i < targetGenericArguments.Length; i++)
            {
                var targetGenericArgument = targetGenericArguments[i];
                var proxyGenericArgument = proxyGenericArguments[i];

                if (targetGenericArgument.IsByRef != proxyGenericArgument.IsByRef)
                {
                    return false;
                }

                if (targetGenericArgument.IsByRef)
                {
                    targetGenericArgument = targetGenericArgument.GetElementType()!;
                    proxyGenericArgument = proxyGenericArgument.GetElementType()!;
                }

                if (targetGenericArgument.IsGenericParameter)
                {
                    continue;
                }

                if (proxyGenericArgument.IsValueType &&
                    !proxyGenericArgument.IsEnum &&
                    proxyGenericArgument != targetGenericArgument)
                {
                    return false;
                }

                if (proxyGenericArgument.IsClass &&
                    !proxyGenericArgument.IsAbstract &&
                    proxyGenericArgument != typeof(object) &&
                    !targetGenericArgument.IsAssignableFrom(proxyGenericArgument))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Applies dynamic-equivalent method-selection rules when runtime type resolution is incomplete.
        /// </summary>
        /// <param name="proxyParameterType">The proxy parameter type value.</param>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <param name="proxyRuntimeType">The proxy runtime type value.</param>
        /// <param name="targetRuntimeType">The target runtime type value.</param>
        /// <returns>true when metadata-only rules permit the candidate; otherwise, false.</returns>
        private static bool MatchesDynamicMethodParameterSelectionRuleFromMetadata(
            TypeSig proxyParameterType,
            TypeSig targetParameterType,
            Type? proxyRuntimeType,
            Type? targetRuntimeType)
        {
            if (targetParameterType.IsGenericParameter || targetRuntimeType?.IsGenericParameter == true)
            {
                return true;
            }

            if (IsNonEnumValueTypeForMethodSelection(proxyParameterType, proxyRuntimeType))
            {
                return AreTypesEquivalent(proxyParameterType, targetParameterType);
            }

            if (!IsConcreteClassForMethodSelection(proxyParameterType, proxyRuntimeType))
            {
                return true;
            }

            return MatchesDynamicConcreteClassParameterSelectionRuleFromMetadata(
                proxyParameterType,
                targetParameterType,
                proxyRuntimeType,
                targetRuntimeType);
        }

        /// <summary>
        /// Applies dynamic-equivalent concrete-class parameter selection when runtime resolution is incomplete.
        /// </summary>
        /// <param name="proxyParameterType">The proxy parameter type value.</param>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <param name="proxyRuntimeType">The proxy runtime type value.</param>
        /// <param name="targetRuntimeType">The target runtime type value.</param>
        /// <returns>true when the candidate should remain eligible; otherwise, false.</returns>
        private static bool MatchesDynamicConcreteClassParameterSelectionRuleFromMetadata(
            TypeSig proxyParameterType,
            TypeSig targetParameterType,
            Type? proxyRuntimeType,
            Type? targetRuntimeType)
        {
            if (proxyRuntimeType is not null && targetRuntimeType is not null)
            {
                return MatchesDynamicConcreteClassParameterSelectionRule(proxyRuntimeType, targetRuntimeType);
            }

            if (IsObjectTypeSig(targetParameterType))
            {
                return true;
            }

            if (IsTypeAssignableFrom(targetParameterType, proxyParameterType))
            {
                return true;
            }

            if (AreTypesEquivalent(proxyParameterType, targetParameterType))
            {
                return true;
            }

            if (proxyParameterType is not GenericInstSig proxyGenericInst ||
                targetParameterType is not GenericInstSig targetGenericInst)
            {
                return false;
            }

            if (string.Equals(targetGenericInst.ToString(), proxyGenericInst.ToString(), StringComparison.Ordinal))
            {
                return true;
            }

            if (targetGenericInst.GenericArguments.Count != proxyGenericInst.GenericArguments.Count)
            {
                return false;
            }

            for (var i = 0; i < targetGenericInst.GenericArguments.Count; i++)
            {
                var targetGenericArgument = targetGenericInst.GenericArguments[i];
                var proxyGenericArgument = proxyGenericInst.GenericArguments[i];

                if (targetGenericArgument.ElementType == ElementType.ByRef || proxyGenericArgument.ElementType == ElementType.ByRef)
                {
                    if (!TryGetByRefElementType(targetGenericArgument, out var targetByRefElementType) ||
                        !TryGetByRefElementType(proxyGenericArgument, out var proxyByRefElementType))
                    {
                        return false;
                    }

                    targetGenericArgument = targetByRefElementType!;
                    proxyGenericArgument = proxyByRefElementType!;
                }

                if (targetGenericArgument.IsGenericParameter)
                {
                    continue;
                }

                var proxyGenericArgumentRuntimeType = TryResolveRuntimeType(proxyGenericArgument);
                if (IsNonEnumValueTypeForMethodSelection(proxyGenericArgument, proxyGenericArgumentRuntimeType) &&
                    !AreTypesEquivalent(proxyGenericArgument, targetGenericArgument))
                {
                    return false;
                }

                if (IsConcreteClassForMethodSelection(proxyGenericArgument, proxyGenericArgumentRuntimeType) &&
                    !IsTypeAssignableFrom(targetGenericArgument, proxyGenericArgument) &&
                    !AreTypesEquivalent(proxyGenericArgument, targetGenericArgument))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether parameter type should be treated as a non-enum value type for method-selection parity.
        /// </summary>
        /// <param name="parameterType">The parameter type value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>true when the parameter is a non-enum value type; otherwise, false.</returns>
        private static bool IsNonEnumValueTypeForMethodSelection(TypeSig parameterType, Type? runtimeType)
        {
            if (runtimeType is not null)
            {
                return runtimeType.IsValueType && !runtimeType.IsEnum;
            }

            if (!parameterType.IsValueType)
            {
                return false;
            }

            var parameterTypeDef = parameterType.ToTypeDefOrRef()?.ResolveTypeDef();
            // Only enforce strict non-enum value-type matching when metadata can prove enum status.
            // Unknown metadata should stay permissive to match dynamic resolver behavior.
            return parameterTypeDef is not null && parameterTypeDef.IsValueType && !parameterTypeDef.IsEnum;
        }

        /// <summary>
        /// Determines whether parameter type should be treated as a concrete class for method-selection parity.
        /// </summary>
        /// <param name="parameterType">The parameter type value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>true when the parameter is a non-abstract concrete class excluding object; otherwise, false.</returns>
        private static bool IsConcreteClassForMethodSelection(TypeSig parameterType, Type? runtimeType)
        {
            if (runtimeType is not null)
            {
                return runtimeType.IsClass &&
                       !runtimeType.IsAbstract &&
                       runtimeType != typeof(object);
            }

            if (IsObjectTypeSig(parameterType))
            {
                return false;
            }

            if (parameterType.ElementType == ElementType.String)
            {
                return true;
            }

            var parameterTypeDef = parameterType.ToTypeDefOrRef()?.ResolveTypeDef();
            return parameterTypeDef?.IsClass == true && !parameterTypeDef.IsAbstract;
        }

        /// <summary>
        /// Determines whether reverse argument conversion should use DuckType.CreateCache&lt;T&gt;.Create().
        /// </summary>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <returns>true if reverse duck chaining should create a proxy; otherwise, false.</returns>
        private static bool ShouldUseDuckChainForReverseArgument(TypeSig targetParameterType)
        {
            var runtimeTargetType = TryResolveRuntimeType(targetParameterType);
            if (runtimeTargetType is not null)
            {
                return runtimeTargetType.IsInterface || runtimeTargetType.IsAbstract;
            }

            var targetTypeDef = targetParameterType.ToTypeDefOrRef()?.ResolveTypeDef();
            return targetTypeDef?.IsInterface == true || targetTypeDef?.IsAbstract == true;
        }

        /// <summary>
        /// Attempts to select post-call conversion for by-ref argument write-back.
        /// </summary>
        /// <param name="proxyParameterElementType">The proxy parameter element type value.</param>
        /// <param name="targetParameterElementType">The target parameter element type value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <param name="returnConversion">The return conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateByRefPostCallConversion(TypeSig proxyParameterElementType, TypeSig targetParameterElementType, bool isReverseMapping, out MethodReturnConversion returnConversion)
        {
            var conversionCacheKey = BuildMethodReturnConversionCacheKey(proxyParameterElementType, targetParameterElementType, isReverseMapping, discriminator: "byref");
            if (_currentExecutionContext?.TryGetMethodReturnConversion(conversionCacheKey, out var cachedConversion) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ConversionPlanCacheHits++;
                }

                if (cachedConversion.Succeeded)
                {
                    returnConversion = cachedConversion.Conversion;
                    return true;
                }

                returnConversion = default;
                return false;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.ConversionPlanCacheMisses++;
            }

            if (isReverseMapping)
            {
                if (AreTypesEquivalent(proxyParameterElementType, targetParameterElementType))
                {
                    returnConversion = MethodReturnConversion.None();
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                if (ShouldUseDuckChainForReverseArgument(targetParameterElementType) &&
                    IsDuckChainingRequired(proxyParameterElementType, targetParameterElementType))
                {
                    returnConversion = MethodReturnConversion.ExtractDuckTypeInstance(targetParameterElementType, proxyParameterElementType);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                if (IsDuckChainingRequired(targetParameterElementType, proxyParameterElementType))
                {
                    returnConversion = MethodReturnConversion.DuckChainToProxy(proxyParameterElementType, targetParameterElementType);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                if (CanUseTypeConversion(targetParameterElementType, proxyParameterElementType))
                {
                    returnConversion = MethodReturnConversion.TypeConversion(targetParameterElementType, proxyParameterElementType);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }
            }
            else if (TryCreateReturnConversion(proxyParameterElementType, targetParameterElementType, out returnConversion))
            {
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                return true;
            }

            returnConversion = default;
            _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry());
            return false;
        }

        /// <summary>
        /// Attempts to get method parameter direction.
        /// </summary>
        /// <param name="method">The method value.</param>
        /// <param name="parameterIndex">The parameter index value.</param>
        /// <param name="direction">The direction value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetMethodParameterDirection(MethodDef method, int parameterIndex, out ParameterDirection direction)
        {
            foreach (var parameter in method.Parameters)
            {
                if (parameter.MethodSigIndex != parameterIndex)
                {
                    continue;
                }

                var paramDef = parameter.ParamDef;
                direction = new ParameterDirection(paramDef?.IsOut ?? false, paramDef?.IsIn ?? false);
                return true;
            }

            direction = default;
            return false;
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
        /// Substitutes method generic parameters with closed generic arguments when provided.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static TypeSig SubstituteMethodGenericTypeArguments(TypeSig typeSig, IReadOnlyList<TypeSig>? closedGenericMethodArguments)
        {
            return SubstituteTypeAndMethodGenericTypeArguments(typeSig, closedGenericTypeArguments: null, closedGenericMethodArguments);
        }

        /// <summary>
        /// Attempts to get value with type argument.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="valueArgument">The value argument value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetValueWithTypeArgument(TypeSig typeSig, out TypeSig? valueArgument)
        {
            valueArgument = null;
            if (typeSig is not GenericInstSig genericInstSig || genericInstSig.GenericArguments.Count != 1)
            {
                return false;
            }

            var genericType = genericInstSig.GenericType?.TypeDefOrRef;
            if (genericType is null)
            {
                return false;
            }

            if (!string.Equals(genericType.FullName, typeof(ValueWithType<>).FullName, StringComparison.Ordinal))
            {
                return false;
            }

            valueArgument = genericInstSig.GenericArguments[0];
            return true;
        }

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
        /// Creates value with type create method ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
        /// <param name="innerTypeSig">The inner type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateValueWithTypeCreateMethodRef(ModuleDef moduleDef, TypeSig wrapperTypeSig, TypeSig innerTypeSig)
        {
            var cacheKey = string.Concat(BuildTypeSigCacheKey(wrapperTypeSig), "|", BuildTypeSigCacheKey(innerTypeSig));
            if (_currentExecutionContext?.TryGetValueWithTypeCreateMethodRef(cacheKey, out var cachedMethod) == true)
            {
                return cachedMethod!;
            }

            var importedWrapperTypeSig = ImportTypeSigCached(moduleDef, wrapperTypeSig, $"ValueWithType wrapper method '{wrapperTypeSig.FullName}'");
            if (importedWrapperTypeSig is not GenericInstSig wrapperGenericInst)
            {
                throw new InvalidOperationException($"Expected ValueWithType<T> generic wrapper, but found '{importedWrapperTypeSig.FullName}'.");
            }

            var typeSpec = moduleDef.UpdateRowId(new TypeSpecUser(importedWrapperTypeSig));
            var returnTypeSig = new GenericInstSig(wrapperGenericInst.GenericType, new GenericVar(0));
            var methodSig = MethodSig.CreateStatic(
                returnTypeSig,
                new GenericVar(0),
                moduleDef.CorLibTypes.GetTypeRef("System", "Type").ToTypeSig());
            var methodRef = new MemberRefUser(moduleDef, "Create", methodSig, typeSpec);
            var importedMethod = moduleDef.UpdateRowId(methodRef);
            _currentExecutionContext?.CacheValueWithTypeCreateMethodRef(cacheKey, importedMethod);
            return importedMethod;
        }

        /// <summary>
        /// Creates duck type create cache create method ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="proxyTypeSig">The proxy type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateDuckTypeCreateCacheCreateMethodRef(ModuleDef moduleDef, TypeSig proxyTypeSig)
        {
            var cacheKey = BuildTypeSigCacheKey(proxyTypeSig);
            if (_currentExecutionContext?.TryGetDuckTypeCreateCacheCreateMethodRef(cacheKey, out var cachedMethod) == true)
            {
                return cachedMethod!;
            }

            var importedProxyTypeSig = ImportTypeSigCached(moduleDef, proxyTypeSig, $"DuckType.CreateCache.Create proxy '{proxyTypeSig.FullName}'");
            var importedCreateCacheOpenType = ImportRuntimeTypeCached(moduleDef, typeof(DuckType.CreateCache<>), "DuckType.CreateCache<> type");

            var importedCreateCacheOpenTypeSig = importedCreateCacheOpenType.ToTypeSig() as ClassOrValueTypeSig
                ?? throw new InvalidOperationException("Unable to resolve DuckType.CreateCache<> signature.");

            var createCacheClosedTypeSig = new GenericInstSig(importedCreateCacheOpenTypeSig, importedProxyTypeSig);
            var createCacheClosedTypeSpec = moduleDef.UpdateRowId(new TypeSpecUser(createCacheClosedTypeSig));
            var createMethodSig = MethodSig.CreateStatic(new GenericVar(0), moduleDef.CorLibTypes.Object);
            var createMethodRef = new MemberRefUser(moduleDef, "Create", createMethodSig, createCacheClosedTypeSpec);
            var importedMethod = moduleDef.UpdateRowId(createMethodRef);
            _currentExecutionContext?.CacheDuckTypeCreateCacheCreateMethodRef(cacheKey, importedMethod);
            return importedMethod;
        }

        private static IMethod CreateDuckTypeCreateCacheCreateFromMethodRef(ModuleDef moduleDef, TypeSig proxyTypeSig, TypeSig targetTypeSig)
        {
            var cacheKey = string.Concat(BuildTypeSigCacheKey(proxyTypeSig), "|", BuildTypeSigCacheKey(targetTypeSig));
            if (_currentExecutionContext?.TryGetDuckTypeCreateCacheCreateFromMethodRef(cacheKey, out var cachedMethod) == true)
            {
                return cachedMethod!;
            }

            var importedProxyTypeSig = ImportTypeSigCached(moduleDef, proxyTypeSig, $"DuckType.CreateCache.CreateFrom proxy '{proxyTypeSig.FullName}'");
            var importedTargetTypeSig = ImportTypeSigCached(moduleDef, targetTypeSig, $"DuckType.CreateCache.CreateFrom target '{targetTypeSig.FullName}'");
            var importedCreateCacheOpenType = ImportRuntimeTypeCached(moduleDef, typeof(DuckType.CreateCache<>), "DuckType.CreateCache<> type");

            var importedCreateCacheOpenTypeSig = importedCreateCacheOpenType.ToTypeSig() as ClassOrValueTypeSig
                ?? throw new InvalidOperationException("Unable to resolve DuckType.CreateCache<> signature.");

            var createCacheClosedTypeSig = new GenericInstSig(importedCreateCacheOpenTypeSig, importedProxyTypeSig);
            var createCacheClosedTypeSpec = moduleDef.UpdateRowId(new TypeSpecUser(createCacheClosedTypeSig));
            var createFromMethodSig = MethodSig.CreateStaticGeneric(1, new GenericVar(0), new GenericMVar(0));
            var createFromMethodRef = new MemberRefUser(moduleDef, "CreateFrom", createFromMethodSig, createCacheClosedTypeSpec);
            var createFromMethodSpec = new MethodSpecUser(createFromMethodRef, new GenericInstMethodSig(importedTargetTypeSig));
            var importedMethod = moduleDef.UpdateRowId(createFromMethodSpec);
            _currentExecutionContext?.CacheDuckTypeCreateCacheCreateFromMethodRef(cacheKey, importedMethod);
            return importedMethod;
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

            var importedType = moduleDef.Import(runtimeType) as ITypeDefOrRef
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

        private static void EmitTrailingOptionalTargetArguments(
            ModuleDef moduleDef,
            CilBody body,
            MethodDef targetMethod,
            ForwardMethodBindingInfo methodBinding,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            if (methodBinding.TrailingOptionalTargetParameterCount == 0)
            {
                return;
            }

            var firstOptionalParameterIndex = targetMethod.MethodSig.Params.Count - methodBinding.TrailingOptionalTargetParameterCount;
            for (var parameterIndex = firstOptionalParameterIndex; parameterIndex < targetMethod.MethodSig.Params.Count; parameterIndex++)
            {
                var parameterType = SubstituteTypeAndMethodGenericTypeArguments(
                    targetMethod.MethodSig.Params[parameterIndex],
                    closedGenericTargetTypeArguments,
                    methodBinding.ClosedGenericMethodArguments);
                EmitOptionalParameterDefault(moduleDef, body, targetMethod, parameterIndex, parameterType);
            }
        }

        private static void EmitOptionalParameterDefault(
            ModuleDef moduleDef,
            CilBody body,
            MethodDef targetMethod,
            int parameterIndex,
            TypeSig parameterType)
        {
            if (TryGetOptionalParameterConstant(targetMethod, parameterIndex, out var constantValue) &&
                TryEmitConstantValue(moduleDef, body, parameterType, constantValue))
            {
                return;
            }

            EmitDefaultValue(moduleDef, body, parameterType, $"optional parameter '{parameterIndex.ToString(CultureInfo.InvariantCulture)}' of method '{targetMethod.FullName}'");
        }

        private static bool TryGetOptionalParameterConstant(MethodDef targetMethod, int parameterIndex, out object? constantValue)
        {
            foreach (var parameter in targetMethod.Parameters)
            {
                if (parameter.MethodSigIndex != parameterIndex)
                {
                    continue;
                }

                if (parameter.ParamDef?.Constant is { } constant)
                {
                    constantValue = constant.Value;
                    return true;
                }

                if (parameter.ParamDef is not null &&
                    TryGetOptionalParameterAttributeConstant(parameter.ParamDef, out constantValue))
                {
                    return true;
                }

                break;
            }

            constantValue = null;
            return false;
        }

        private static bool TryGetOptionalParameterAttributeConstant(ParamDef parameter, out object? constantValue)
        {
            foreach (var customAttribute in parameter.CustomAttributes)
            {
                if (string.Equals(customAttribute.TypeFullName, "System.Runtime.CompilerServices.DecimalConstantAttribute", StringComparison.Ordinal) &&
                    TryGetDecimalConstantAttributeValue(customAttribute, out var decimalValue))
                {
                    constantValue = decimalValue;
                    return true;
                }

                if (string.Equals(customAttribute.TypeFullName, "System.Runtime.CompilerServices.DateTimeConstantAttribute", StringComparison.Ordinal) &&
                    TryGetDateTimeConstantAttributeValue(customAttribute, out var dateTimeValue))
                {
                    constantValue = dateTimeValue;
                    return true;
                }
            }

            constantValue = null;
            return false;
        }

        private static bool TryGetDecimalConstantAttributeValue(CustomAttribute customAttribute, out decimal value)
        {
            value = default;
            if (customAttribute.ConstructorArguments.Count != 5 ||
                !TryGetCustomAttributeByte(customAttribute.ConstructorArguments[0], out var scale) ||
                !TryGetCustomAttributeByte(customAttribute.ConstructorArguments[1], out var sign) ||
                !TryGetCustomAttributeInt32Bits(customAttribute.ConstructorArguments[2], out var high) ||
                !TryGetCustomAttributeInt32Bits(customAttribute.ConstructorArguments[3], out var middle) ||
                !TryGetCustomAttributeInt32Bits(customAttribute.ConstructorArguments[4], out var low))
            {
                return false;
            }

            try
            {
                value = new decimal(low, middle, high, sign != 0, scale);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetDateTimeConstantAttributeValue(CustomAttribute customAttribute, out DateTime value)
        {
            value = default;
            if (customAttribute.ConstructorArguments.Count != 1 ||
                !TryGetCustomAttributeInt64(customAttribute.ConstructorArguments[0], out var ticks))
            {
                return false;
            }

            try
            {
                value = new DateTime(ticks);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryEmitConstantValue(ModuleDef moduleDef, CilBody body, TypeSig parameterType, object? constantValue)
        {
            if (constantValue is null)
            {
                EmitDefaultValue(moduleDef, body, parameterType, "optional null parameter");
                return true;
            }

            var underlyingType = GetUnderlyingTypeForTypeConversion(parameterType);
            if (constantValue is decimal decimalValue && TryEmitDecimalConstantValue(moduleDef, body, underlyingType, decimalValue))
            {
                return true;
            }

            if (constantValue is DateTime dateTimeValue && TryEmitDateTimeConstantValue(moduleDef, body, underlyingType, dateTimeValue))
            {
                return true;
            }

            switch (constantValue)
            {
                case bool boolValue:
                    body.Instructions.Add((boolValue ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0).ToInstruction());
                    return true;
                case char charValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(charValue));
                    return true;
                case sbyte sbyteValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(sbyteValue));
                    return true;
                case byte byteValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(byteValue));
                    return true;
                case short shortValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(shortValue));
                    return true;
                case ushort ushortValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(ushortValue));
                    return true;
                case int intValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(intValue));
                    return true;
                case uint uintValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(unchecked((int)uintValue)));
                    return true;
                case long longValue:
                    body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(longValue));
                    return true;
                case ulong ulongValue:
                    body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(unchecked((long)ulongValue)));
                    return true;
                case float floatValue:
                    body.Instructions.Add(OpCodes.Ldc_R4.ToInstruction(floatValue));
                    return true;
                case double doubleValue:
                    body.Instructions.Add(OpCodes.Ldc_R8.ToInstruction(doubleValue));
                    return true;
                case string stringValue:
                    body.Instructions.Add(OpCodes.Ldstr.ToInstruction(stringValue));
                    return true;
            }

            if (underlyingType.ElementType == ElementType.Boolean && TryConvertConstantToLong(constantValue, out var boolNumber))
            {
                body.Instructions.Add((boolNumber != 0 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0).ToInstruction());
                return true;
            }

            if (TryConvertConstantToLong(constantValue, out var integerValue))
            {
                switch (underlyingType.ElementType)
                {
                    case ElementType.I1:
                    case ElementType.U1:
                    case ElementType.I2:
                    case ElementType.U2:
                    case ElementType.I4:
                    case ElementType.U4:
                    case ElementType.Char:
                        body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(unchecked((int)integerValue)));
                        return true;
                    case ElementType.I8:
                    case ElementType.U8:
                        body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(integerValue));
                        return true;
                }
            }

            return false;
        }

        private static bool TryEmitDecimalConstantValue(ModuleDef moduleDef, CilBody body, TypeSig parameterType, decimal value)
        {
            if (!IsTypeSigNamed(parameterType, "System.Decimal"))
            {
                return false;
            }

            var bits = decimal.GetBits(value);
            var flags = bits[3];
            var scale = (byte)((flags >> 16) & 0x7F);
            var isNegative = (flags & unchecked((int)0x80000000)) != 0;
            var decimalCtor = typeof(decimal).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte) })
                              ?? throw new InvalidOperationException("Unable to resolve decimal constant constructor.");

            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(bits[0]));
            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(bits[1]));
            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(bits[2]));
            body.Instructions.Add((isNegative ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0).ToInstruction());
            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)scale));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(moduleDef.Import(decimalCtor)));
            return true;
        }

        private static bool TryEmitDateTimeConstantValue(ModuleDef moduleDef, CilBody body, TypeSig parameterType, DateTime value)
        {
            if (!IsTypeSigNamed(parameterType, "System.DateTime"))
            {
                return false;
            }

            var dateTimeCtor = typeof(DateTime).GetConstructor(new[] { typeof(long) })
                               ?? throw new InvalidOperationException("Unable to resolve DateTime constant constructor.");
            body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(value.Ticks));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(moduleDef.Import(dateTimeCtor)));
            return true;
        }

        private static bool TryConvertConstantToLong(object value, out long result)
        {
            switch (value)
            {
                case sbyte typedValue:
                    result = typedValue;
                    return true;
                case byte typedValue:
                    result = typedValue;
                    return true;
                case short typedValue:
                    result = typedValue;
                    return true;
                case ushort typedValue:
                    result = typedValue;
                    return true;
                case int typedValue:
                    result = typedValue;
                    return true;
                case uint typedValue:
                    result = typedValue;
                    return true;
                case long typedValue:
                    result = typedValue;
                    return true;
                case ulong typedValue when typedValue <= long.MaxValue:
                    result = (long)typedValue;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        private static bool TryGetCustomAttributeByte(CAArgument argument, out byte value)
        {
            switch (argument.Value)
            {
                case byte byteValue:
                    value = byteValue;
                    return true;
                case sbyte sbyteValue when sbyteValue >= 0:
                    value = (byte)sbyteValue;
                    return true;
                case short shortValue when shortValue >= byte.MinValue && shortValue <= byte.MaxValue:
                    value = (byte)shortValue;
                    return true;
                case ushort ushortValue when ushortValue <= byte.MaxValue:
                    value = (byte)ushortValue;
                    return true;
                case int intValue when intValue >= byte.MinValue && intValue <= byte.MaxValue:
                    value = (byte)intValue;
                    return true;
                case uint uintValue when uintValue <= byte.MaxValue:
                    value = (byte)uintValue;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryGetCustomAttributeInt32Bits(CAArgument argument, out int value)
        {
            switch (argument.Value)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case uint uintValue:
                    value = unchecked((int)uintValue);
                    return true;
                case short shortValue:
                    value = shortValue;
                    return true;
                case ushort ushortValue:
                    value = ushortValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
                case sbyte sbyteValue:
                    value = sbyteValue;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryGetCustomAttributeInt64(CAArgument argument, out long value)
        {
            switch (argument.Value)
            {
                case long longValue:
                    value = longValue;
                    return true;
                case ulong ulongValue when ulongValue <= long.MaxValue:
                    value = (long)ulongValue;
                    return true;
                case int intValue:
                    value = intValue;
                    return true;
                case uint uintValue:
                    value = uintValue;
                    return true;
                case short shortValue:
                    value = shortValue;
                    return true;
                case ushort ushortValue:
                    value = ushortValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
                case sbyte sbyteValue:
                    value = sbyteValue;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static bool IsTypeSigNamed(TypeSig typeSig, string fullName)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            return typeDefOrRef is not null &&
                   string.Equals(typeDefOrRef.FullName, fullName, StringComparison.Ordinal);
        }

        private static void EmitDefaultValue(ModuleDef moduleDef, CilBody body, TypeSig typeSig, string context)
        {
            if (!typeSig.IsValueType)
            {
                body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                return;
            }

            var importedTypeSig = ImportTypeSigCached(moduleDef, typeSig, $"default value local for {context}");
            var defaultLocal = new Local(importedTypeSig);
            body.Variables.Add(defaultLocal);
            body.InitLocals = true;
            var importedType = ResolveImportedTypeForTypeToken(moduleDef, typeSig, context);
            body.Instructions.Add(OpCodes.Ldloca.ToInstruction(defaultLocal));
            body.Instructions.Add(OpCodes.Initobj.ToInstruction(importedType));
            body.Instructions.Add(OpCodes.Ldloc.ToInstruction(defaultLocal));
        }

        /// <summary>
        /// Emits object to expected type conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="body">The body value.</param>
        /// <param name="expectedTypeSig">The expected type sig value.</param>
        /// <param name="context">The context value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitObjectToExpectedTypeConversion(ModuleDef moduleDef, CilBody body, TypeSig expectedTypeSig, string context)
        {
            if (expectedTypeSig.ElementType == ElementType.Object)
            {
                return;
            }

            var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedTypeSig, context);
            if (expectedTypeSig.IsValueType)
            {
                body.Instructions.Add(OpCodes.Unbox_Any.ToInstruction(importedExpectedType));
                return;
            }

            body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
        }

        /// <summary>
        /// Emits duck chain to proxy conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="body">The body value.</param>
        /// <param name="proxyTypeSig">The proxy type sig value.</param>
        /// <param name="targetTypeSig">The target type sig value.</param>
        /// <param name="context">The context value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitDuckChainToProxyConversion(
            ModuleDef moduleDef,
            CilBody body,
            TypeSig proxyTypeSig,
            TypeSig targetTypeSig,
            string context)
        {
            var importedTargetTypeSig = ImportTypeSigCached(moduleDef, targetTypeSig, $"reference conversion target '{targetTypeSig.FullName}'");
            var targetLocal = new Local(importedTargetTypeSig);
            body.Variables.Add(targetLocal);
            body.InitLocals = true;
            body.Instructions.Add(OpCodes.Stloc.ToInstruction(targetLocal));

            body.Instructions.Add(OpCodes.Ldloc.ToInstruction(targetLocal));
            if (targetTypeSig.IsValueType)
            {
                var importedTargetTypeForBox = ResolveImportedTypeForTypeToken(moduleDef, targetTypeSig, context);
                body.Instructions.Add(OpCodes.Box.ToInstruction(importedTargetTypeForBox));
            }

            if (TryGetNullableElementType(proxyTypeSig, out var nullableProxyElementType))
            {
                var boxedTargetLocal = new Local(moduleDef.CorLibTypes.Object);
                var nullableResultLocal = new Local(ImportTypeSigCached(moduleDef, proxyTypeSig, $"nullable result local '{proxyTypeSig.FullName}'"));
                body.Variables.Add(boxedTargetLocal);
                body.Variables.Add(nullableResultLocal);
                body.InitLocals = true;

                var hasValueLabel = Instruction.Create(OpCodes.Nop);
                var endLabel = Instruction.Create(OpCodes.Nop);
                var importedNullableType = ResolveImportedTypeForTypeToken(moduleDef, proxyTypeSig, context);
                var nullableCtor = CreateNullableCtorRef(moduleDef, proxyTypeSig);

                body.Instructions.Add(OpCodes.Stloc.ToInstruction(boxedTargetLocal));
                body.Instructions.Add(OpCodes.Ldloc.ToInstruction(boxedTargetLocal));
                body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));

                body.Instructions.Add(OpCodes.Ldloca.ToInstruction(nullableResultLocal));
                body.Instructions.Add(OpCodes.Initobj.ToInstruction(importedNullableType));
                body.Instructions.Add(OpCodes.Br_S.ToInstruction(endLabel));

                body.Instructions.Add(hasValueLabel);
                body.Instructions.Add(OpCodes.Ldloc.ToInstruction(boxedTargetLocal));
                var createCacheCreateMethodRef = CreateDuckTypeCreateCacheCreateMethodRef(moduleDef, nullableProxyElementType!);
                body.Instructions.Add(OpCodes.Call.ToInstruction(createCacheCreateMethodRef));
                body.Instructions.Add(OpCodes.Newobj.ToInstruction(nullableCtor));
                body.Instructions.Add(OpCodes.Stloc.ToInstruction(nullableResultLocal));

                body.Instructions.Add(endLabel);
                body.Instructions.Add(OpCodes.Ldloc.ToInstruction(nullableResultLocal));
                return;
            }

            var createMethodRef = CreateDuckTypeCreateCacheCreateMethodRef(moduleDef, proxyTypeSig);
            body.Instructions.Add(OpCodes.Call.ToInstruction(createMethodRef));
        }

        /// <summary>
        /// Creates nullable ctor ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="nullableTypeSig">The nullable type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateNullableCtorRef(ModuleDef moduleDef, TypeSig nullableTypeSig)
        {
            if (!TryGetNullableElementType(nullableTypeSig, out _))
            {
                throw new InvalidOperationException($"Expected Nullable<T> type but received '{nullableTypeSig.FullName}'.");
            }

            var cacheKey = BuildTypeSigCacheKey(nullableTypeSig);
            if (_currentExecutionContext?.TryGetNullableCtorRef(cacheKey, out var cachedCtor) == true)
            {
                return cachedCtor!;
            }

            var importedNullableTypeSig = ImportTypeSigCached(moduleDef, nullableTypeSig, $"nullable ctor '{nullableTypeSig.FullName}'");
            var nullableTypeSpec = moduleDef.UpdateRowId(new TypeSpecUser(importedNullableTypeSig));
            var ctorSig = MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, new GenericVar(0));
            var ctorRef = new MemberRefUser(moduleDef, ".ctor", ctorSig, nullableTypeSpec);
            var importedCtor = moduleDef.UpdateRowId(ctorRef);
            _currentExecutionContext?.CacheNullableCtorRef(cacheKey, importedCtor);
            return importedCtor;
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

                    if (!AreFieldAccessorSignatureCompatible(proxyMethod, candidate, accessorKind, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, isReverseMapping, out var candidateFieldBinding, out failureReason))
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
        /// Gets forward target field names.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<string> GetForwardTargetFieldNames(MethodDef proxyMethod)
        {
            var fieldNames = new List<string>();
            var visitedNames = new HashSet<string>(StringComparer.Ordinal);
            var hasConfiguredName = false;

            void AddNames(IEnumerable<string> names)
            {
                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (visitedNames.Add(name))
                    {
                        fieldNames.Add(name);
                    }
                }
            }

            if (TryGetDuckAttributeNames(proxyMethod.CustomAttributes, out var methodAttributeNames))
            {
                AddNames(methodAttributeNames);
                hasConfiguredName = true;
            }

            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames))
            {
                AddNames(propertyAttributeNames);
                hasConfiguredName = true;
            }

            if (!hasConfiguredName && TryGetAccessorPropertyName(proxyMethod.Name.String ?? proxyMethod.Name.ToString(), out var propertyName))
            {
                AddNames(new[] { propertyName! });
            }

            return fieldNames;
        }

        /// <summary>
        /// Attempts to get field accessor kind.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="accessorKind">The accessor kind value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetFieldAccessorKind(MethodDef proxyMethod, out FieldAccessorKind accessorKind)
        {
            accessorKind = default;
            var methodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();

            if (methodName.StartsWith("get_", StringComparison.Ordinal) && proxyMethod.MethodSig.Params.Count == 0)
            {
                accessorKind = FieldAccessorKind.Getter;
                return true;
            }

            if (methodName.StartsWith("set_", StringComparison.Ordinal) && proxyMethod.MethodSig.Params.Count == 1 && proxyMethod.MethodSig.RetType.ElementType == ElementType.Void)
            {
                accessorKind = FieldAccessorKind.Setter;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the method represents a property accessor.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsPropertyAccessorMethod(MethodDef proxyMethod)
        {
            var methodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();
            return TryGetAccessorPropertyName(methodName, out _);
        }

        /// <summary>
        /// Attempts to get accessor property name.
        /// </summary>
        /// <param name="methodName">The method name value.</param>
        /// <param name="propertyName">The property name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetAccessorPropertyName(string methodName, out string? propertyName)
        {
            propertyName = null;
            if (methodName.StartsWith("get_", StringComparison.Ordinal) || methodName.StartsWith("set_", StringComparison.Ordinal))
            {
                propertyName = methodName.Substring(4);
                return !string.IsNullOrWhiteSpace(propertyName);
            }

            return false;
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
                        failureReason = $"Target field '{targetField.FullName}' belongs to value type '{targetField.DeclaringType.FullName}' and cannot be set by proxy method '{proxyMethod.FullName}'.";
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
        /// Determines whether ignore-case binding is enabled for the method or its declaring property.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>true if ignore-case member matching is enabled; otherwise, false.</returns>
        private static bool IsIgnoreCaseBindingEnabled(MethodDef proxyMethod)
        {
            return (GetDuckBindingFlags(proxyMethod) & BindingFlags.IgnoreCase) != 0;
        }

        /// <summary>
        /// Determines whether ignore-case binding is enabled for a custom-attribute set.
        /// </summary>
        /// <param name="customAttributes">The custom attributes value.</param>
        /// <returns>true if ignore-case member matching is enabled; otherwise, false.</returns>
        private static bool IsIgnoreCaseBindingEnabled(IList<CustomAttribute> customAttributes)
        {
            return (GetDuckBindingFlags(customAttributes) & BindingFlags.IgnoreCase) != 0;
        }

        /// <summary>
        /// Determines whether ignore-case binding is enabled for a Duck attribute.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>true if ignore-case member matching is enabled; otherwise, false.</returns>
        private static bool IsIgnoreCaseBindingEnabled(CustomAttribute customAttribute)
        {
            return (GetDuckBindingFlags(customAttribute) & BindingFlags.IgnoreCase) != 0;
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
        /// Gets forward target method names.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<string> GetForwardTargetMethodNames(MethodDef proxyMethod)
        {
            var methodNames = new List<string>();
            var visitedNames = new HashSet<string>(StringComparer.Ordinal);
            var hasConfiguredName = false;

            void AddNames(IEnumerable<string> names)
            {
                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (visitedNames.Add(name))
                    {
                        methodNames.Add(name);
                    }
                }
            }

            if (TryGetDuckAttributeNames(proxyMethod.CustomAttributes, out var methodAttributeNames))
            {
                if (proxyMethod.IsSpecialName && TryGetAccessorPrefix(proxyMethod.Name, out var accessorPrefix))
                {
                    AddNames(methodAttributeNames.Select(name => $"{accessorPrefix}{name}"));
                }
                else
                {
                    AddNames(methodAttributeNames);
                }

                hasConfiguredName = true;
            }

            if (proxyMethod.IsSpecialName && TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames) && TryGetAccessorPrefix(proxyMethod.Name, out var propertyAccessorPrefix))
            {
                AddNames(propertyAttributeNames.Select(name => $"{propertyAccessorPrefix}{name}"));
                hasConfiguredName = true;
            }

            if (!hasConfiguredName)
            {
                AddNames(new[] { proxyMethod.Name.String ?? proxyMethod.Name.ToString() });
            }

            return methodNames;
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
        /// Attempts to resolve forward closed generic method arguments.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryResolveForwardClosedGenericMethodArguments(
            TypeDef targetType,
            ProxyMethodPlan proxyMethodPlan,
            out IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            out string? failureReason)
        {
            var phaseStopwatch = StartProfilePhase();
            if (_currentProfile is not null)
            {
                _currentProfile.ForwardClosedGenericMethodArgumentResolutionCount++;
            }

            try
            {
            closedGenericMethodArguments = null;
            failureReason = null;
            if (!proxyMethodPlan.HasDuckGenericParameterTypeNames)
            {
                return true;
            }

            var resolvedTypeSigs = new List<TypeSig>(proxyMethodPlan.DuckGenericParameterTypeNames.Count);
            foreach (var genericParameterTypeName in proxyMethodPlan.DuckGenericParameterTypeNames)
            {
                if (!TryResolveRuntimeTypeByName(genericParameterTypeName, out var runtimeType))
                {
                    failureReason =
                        $"Generic parameter type '{genericParameterTypeName}' for proxy method '{proxyMethodPlan.Method.FullName}' could not be resolved.";
                    return false;
                }

                resolvedTypeSigs.Add(targetType.Module.Import(runtimeType!).ToTypeSig());
            }

            closedGenericMethodArguments = resolvedTypeSigs;
            return true;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardClosedGenericMethodArgumentResolutionSeconds += seconds);
            }
        }

        /// <summary>
        /// Attempts to get duck generic parameter type names.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="genericParameterTypeNames">The generic parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetDuckGenericParameterTypeNames(MethodDef proxyMethod, out IReadOnlyList<string> genericParameterTypeNames)
        {
            var names = new List<string>();
            genericParameterTypeNames = names;

            foreach (var duckAttribute in EnumerateDuckAttributes(proxyMethod))
            {
                foreach (var namedArgument in duckAttribute.NamedArguments)
                {
                    if (!string.Equals(namedArgument.Name.String, "GenericParameterTypeNames", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (TryGetStringArrayArgument(namedArgument.Argument.Value, out var argumentNames))
                    {
                        names.AddRange(argumentNames);
                    }
                }
            }

            return names.Count > 0;
        }

        /// <summary>
        /// Attempts to resolve runtime type by name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
#if NET6_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner reflects over loaded assemblies as part of build-time compatibility analysis.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Type names are supplied by mapping metadata and validated by discovery before emission.")]
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
            if (!string.IsNullOrWhiteSpace(parsedTypeName) &&
                !string.IsNullOrWhiteSpace(parsedAssemblyName) &&
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
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner reflects over loaded assemblies as part of build-time compatibility analysis.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Type names are supplied by mapping metadata and validated by discovery before emission.")]
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
            if (!string.IsNullOrWhiteSpace(parsedTypeName) &&
                !string.IsNullOrWhiteSpace(parsedAssemblyName) &&
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
        /// Determines whether reverse method attribute.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsReverseMethodAttribute(CustomAttribute customAttribute)
        {
            return string.Equals(customAttribute.TypeFullName, DuckReverseMethodAttributeTypeName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether reverse candidate match.
        /// </summary>
        /// <param name="proxyMethodName">The proxy method name value.</param>
        /// <param name="proxyParameterTypes">The proxy parameter types value.</param>
        /// <param name="proxyParameterTypeNames">The proxy parameter type names value.</param>
        /// <param name="reverseAttribute">The reverse attribute value.</param>
        /// <param name="targetMethodName">The target method name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsReverseCandidateMatch(
            string proxyMethodName,
            IReadOnlyList<TypeSig> proxyParameterTypes,
            IReadOnlyList<HashSet<string>> proxyParameterTypeNames,
            CustomAttribute reverseAttribute,
            string targetMethodName)
        {
            // Property accessor prefix (get_/set_) must remain consistent between proxy and candidate target method.
            if (TryGetAccessorPrefix(proxyMethodName, out var proxyAccessorPrefix) &&
                TryGetAccessorPrefix(targetMethodName, out var targetAccessorPrefix) &&
                !string.Equals(proxyAccessorPrefix, targetAccessorPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var mappedNames = new List<string> { targetMethodName };
            if (TryGetDuckAttributeName(reverseAttribute, out var explicitMappedName) &&
                !string.IsNullOrWhiteSpace(explicitMappedName))
            {
                mappedNames.Clear();
                foreach (var candidateName in explicitMappedName!.Split(','))
                {
                    var normalizedName = candidateName.Trim();
                    if (normalizedName.Length == 0)
                    {
                        continue;
                    }

                    // Keep accessor prefix when mapping reverse accessor methods with explicit renamed member.
                    if (proxyMethodName.StartsWith("get_", StringComparison.Ordinal) ||
                        proxyMethodName.StartsWith("set_", StringComparison.Ordinal))
                    {
                        mappedNames.Add(proxyMethodName.Substring(0, 4) + normalizedName);
                    }
                    else
                    {
                        mappedNames.Add(normalizedName);
                    }
                }
            }

            if (!mappedNames.Any(mappedName => string.Equals(proxyMethodName, mappedName, StringComparison.Ordinal)))
            {
                return false;
            }

            if (!TryGetDuckAttributeParameterTypeNames(reverseAttribute, out var configuredParameterTypeNames))
            {
                return true;
            }

            if (configuredParameterTypeNames.Count != proxyParameterTypeNames.Count)
            {
                return false;
            }

            for (var i = 0; i < configuredParameterTypeNames.Count; i++)
            {
                if (IsTypeGenericParameter(proxyParameterTypes[i]))
                {
                    continue;
                }

                if (!proxyParameterTypeNames[i].Contains(configuredParameterTypeNames[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a parameter type is an open generic placeholder.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>true when the type represents a generic parameter; otherwise, false.</returns>
        private static bool IsTypeGenericParameter(TypeSig typeSig)
        {
            if (typeSig is ByRefSig byRefSig)
            {
                typeSig = byRefSig.Next;
            }

            return typeSig is GenericVar or GenericMVar;
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
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                names.Add(fullName);
            }

            var typeName = normalizedType.TypeName;
            if (!string.IsNullOrWhiteSpace(typeName))
            {
                names.Add(typeName);
            }

            var typeDefOrRef = normalizedType.ToTypeDefOrRef();
            if (typeDefOrRef is not null)
            {
                var definitionFullName = typeDefOrRef.FullName;
                if (!string.IsNullOrWhiteSpace(definitionFullName))
                {
                    names.Add(definitionFullName);
                }

                var reflectionFullName = typeDefOrRef.ReflectionFullName?.Replace('/', '+');
                if (!string.IsNullOrWhiteSpace(reflectionFullName))
                {
                    names.Add(reflectionFullName!);
                }

                var simpleTypeName = typeDefOrRef.Name.String ?? typeDefOrRef.Name.ToString();
                if (!string.IsNullOrWhiteSpace(simpleTypeName))
                {
                    names.Add(simpleTypeName);
                }

                var assemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(typeDefOrRef.DefinitionAssembly?.Name.String ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    if (!string.IsNullOrWhiteSpace(definitionFullName))
                    {
                        names.Add($"{definitionFullName}, {assemblyName}");
                    }

                    if (!string.IsNullOrWhiteSpace(reflectionFullName))
                    {
                        names.Add($"{reflectionFullName}, {assemblyName}");
                    }
                }
            }

            var runtimeType = TryResolveRuntimeType(normalizedType);
            if (runtimeType is not null)
            {
                names.Add(runtimeType.Name);
                if (!string.IsNullOrWhiteSpace(runtimeType.FullName))
                {
                    names.Add(runtimeType.FullName);
                    var assemblyName = runtimeType.Assembly.GetName().Name;
                    if (!string.IsNullOrWhiteSpace(assemblyName))
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
                            if (!string.IsNullOrWhiteSpace(name))
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
        /// Determines whether a method carries DuckIgnore semantics directly or via its declaring property.
        /// </summary>
        /// <param name="method">The method value.</param>
        /// <returns>true if the method is marked with DuckIgnore semantics; otherwise, false.</returns>
        private static bool IsDuckIgnoreMethod(MethodDef method)
        {
            if (method.CustomAttributes.Any(IsDuckIgnoreAttribute))
            {
                return true;
            }

            if (TryGetDeclaringProperty(method, out var declaringProperty) &&
                declaringProperty is not null &&
                declaringProperty.CustomAttributes.Any(IsDuckIgnoreAttribute))
            {
                return true;
            }

            return false;
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
                    return !string.IsNullOrWhiteSpace(text);
                case string stringValue:
                    text = stringValue;
                    return !string.IsNullOrWhiteSpace(text);
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
                        if (!string.IsNullOrWhiteSpace(valueText))
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
            return configuredName
                .Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name));
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
        /// Determines whether two methods have equivalent signatures for mapping purposes.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool AreMethodsSignatureCompatible(MethodDef proxyMethod, MethodDef targetMethod)
        {
            if (!AreTypesEquivalent(proxyMethod.MethodSig.RetType, targetMethod.MethodSig.RetType))
            {
                return false;
            }

            for (var i = 0; i < proxyMethod.MethodSig.Params.Count; i++)
            {
                if (!AreTypesEquivalent(proxyMethod.MethodSig.Params[i], targetMethod.MethodSig.Params[i]))
                {
                    return false;
                }
            }

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
        /// Determines whether duck chaining required.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static bool IsDuckChainingRequired(TypeSig targetType, TypeSig proxyType)
        {
            if (proxyType.ContainsGenericParameter || targetType.ContainsGenericParameter)
            {
                return false;
            }

            if (proxyType.ElementType == ElementType.ByRef || targetType.ElementType == ElementType.ByRef)
            {
                return false;
            }

            if (AreTypesEquivalent(proxyType, targetType))
            {
                return false;
            }

            if (!TryGetDuckChainingProxyType(proxyType, out var proxyTypeForCache))
            {
                return false;
            }

            var proxyTypeDefOrRef = proxyTypeForCache.ToTypeDefOrRef();
            var targetTypeDefOrRef = targetType.ToTypeDefOrRef();
            if (proxyTypeDefOrRef is null || targetTypeDefOrRef is null)
            {
                return false;
            }

            if (IsAssignableFrom(proxyTypeDefOrRef, targetTypeDefOrRef))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to get duck chaining proxy type.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="proxyTypeForCache">The proxy type for cache value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static bool TryGetDuckChainingProxyType(TypeSig proxyType, out TypeSig proxyTypeForCache)
        {
            if (TryGetNullableElementType(proxyType, out var nullableInnerType) && IsDuckProxyCandidate(nullableInnerType!))
            {
                proxyTypeForCache = nullableInnerType!;
                return true;
            }

            if (IsDuckProxyCandidate(proxyType))
            {
                proxyTypeForCache = proxyType;
                return true;
            }

            proxyTypeForCache = null!;
            return false;
        }

        /// <summary>
        /// Determines whether duck proxy candidate.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsDuckProxyCandidate(TypeSig typeSig)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            if (typeDefOrRef is null)
            {
                return false;
            }

            var typeDef = typeDefOrRef.ResolveTypeDef();
            if (typeDef is null)
            {
                return false;
            }

            if (typeDef.IsEnum)
            {
                return false;
            }

            if (typeDef.IsInterface)
            {
                return true;
            }

            if (typeDef.IsClass)
            {
                return typeSig.DefinitionAssembly?.IsCorLib() != true;
            }

            if (typeDef.IsValueType)
            {
                return IsDuckCopyValueType(typeDef);
            }

            return false;
        }

        /// <summary>
        /// Determines whether duck copy value type.
        /// </summary>
        /// <param name="typeDef">The type def value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsDuckCopyValueType(TypeDef typeDef)
        {
            foreach (var customAttribute in typeDef.CustomAttributes)
            {
                if (string.Equals(customAttribute.TypeFullName, DuckCopyAttributeTypeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Applies reverse-target custom attributes to the generated proxy type while preserving dynamic behavior.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="generatedType">The generated type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryApplyReverseTargetCustomAttributes(
            ModuleDef moduleDef,
            TypeDef generatedType,
            TypeDef targetType,
            DuckTypeAotMapping mapping,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var reverseCustomAttributePlan = GetOrCreateReverseCustomAttributePlan(moduleDef, targetType);
            if (!reverseCustomAttributePlan.Succeeded)
            {
                failure = CreateFailureResult(
                    mapping,
                    reverseCustomAttributePlan.FailureStatus!,
                    reverseCustomAttributePlan.FailureDiagnosticCode!,
                    reverseCustomAttributePlan.FailureDetail!);
                return false;
            }

            foreach (var clonedAttributePlan in reverseCustomAttributePlan.ClonedAttributes)
            {
                var clonedAttribute = new CustomAttribute(clonedAttributePlan.Constructor);
                foreach (var constructorArgument in clonedAttributePlan.ConstructorArguments)
                {
                    clonedAttribute.ConstructorArguments.Add(constructorArgument);
                }

                generatedType.CustomAttributes.Add(clonedAttribute);
            }

            failure = null;
            return true;
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
                || string.Equals(fullName, DuckReverseMethodAttributeTypeName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Attempts to clone a custom attribute into the generated module context.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="sourceAttribute">The source attribute value.</param>
        /// <param name="clonedAttribute">The cloned attribute value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCloneCustomAttribute(
            ModuleDef moduleDef,
            CustomAttribute sourceAttribute,
            out CustomAttribute? clonedAttribute)
        {
            clonedAttribute = null;
            if (!TryImportCustomAttributeTypeCached(moduleDef, sourceAttribute.Constructor, out var importedCtor))
            {
                return false;
            }

            var importedAttribute = new CustomAttribute(importedCtor);
            foreach (var constructorArgument in sourceAttribute.ConstructorArguments)
            {
                importedAttribute.ConstructorArguments.Add(ImportCustomAttributeArgument(moduleDef, constructorArgument));
            }

            clonedAttribute = importedAttribute;
            return true;
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
            if (string.Equals(candidateBaseType.FullName, derivedType.FullName, StringComparison.Ordinal))
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

                if (string.Equals(current.FullName, candidateBaseType.FullName, StringComparison.Ordinal))
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
                    if (string.Equals(interfaceImpl.Interface.FullName, candidateBaseType.FullName, StringComparison.Ordinal))
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
        /// Resolves an inherited interface type for proxy traversal.
        /// </summary>
        /// <param name="ownerType">The type that owns the interface reference.</param>
        /// <param name="interfaceReference">The interface reference.</param>
        /// <returns>The resolved interface definition when available; otherwise, null.</returns>
        private static TypeDef? ResolveInterfaceTypeDefForTraversal(TypeDef ownerType, ITypeDefOrRef interfaceReference)
        {
            var resolvedInterface = interfaceReference.ResolveTypeDef();
            if (resolvedInterface is not null)
            {
                return resolvedInterface;
            }

            var interfaceTypeName = interfaceReference.ReflectionFullName?.Replace('/', '+')
                                 ?? interfaceReference.FullName.Replace('/', '+');
            var interfaceAssemblyName = interfaceReference.DefinitionAssembly?.Name?.String ?? string.Empty;
            if (!TryResolveRuntimeType(interfaceAssemblyName, assemblyPath: string.Empty, interfaceTypeName, out var runtimeInterfaceType) ||
                runtimeInterfaceType is null)
            {
                return null;
            }

            var runtimeInterfaceDefinition = GetRuntimeTypeDefinition(runtimeInterfaceType);
            var runtimeAssemblyPath = runtimeInterfaceDefinition.Assembly.Location;
            if (string.IsNullOrWhiteSpace(runtimeAssemblyPath) ||
                !File.Exists(runtimeAssemblyPath))
            {
                return null;
            }

            var moduleContext = ownerType.Module?.Context ?? ModuleDef.CreateModuleContext();
            if (moduleContext.AssemblyResolver is AssemblyResolver assemblyResolver)
            {
                AddAssemblyResolverSearchPath(assemblyResolver, Path.GetDirectoryName(runtimeAssemblyPath));
            }

            var runtimeModule = ModuleDefMD.Load(runtimeAssemblyPath, moduleContext);
            runtimeModule.EnableTypeDefFindCache = true;
            if (moduleContext.AssemblyResolver is AssemblyResolver resolver)
            {
                resolver.AddToCache(runtimeModule);
            }

            var runtimeMetadataName = runtimeInterfaceDefinition.FullName?.Replace('+', '/') ?? interfaceTypeName.Replace('+', '/');
            return TryResolveType(runtimeModule, runtimeMetadataName, out var runtimeInterfaceDefinitionType)
                       ? runtimeInterfaceDefinitionType
                       : null;
        }

        /// <summary>
        /// Adds add ignores access checks to attributes.
        /// </summary>
        /// <param name="assemblyDef">The assembly def value.</param>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="ignoresAccessChecksToAttributeCtor">The ignores access checks to attribute ctor value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        private static void AddIgnoresAccessChecksToAttributes(
            AssemblyDef assemblyDef,
            ModuleDef moduleDef,
            ICustomAttributeType ignoresAccessChecksToAttributeCtor,
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            IReadOnlyCollection<string> requiredAccessCheckAssemblyNames)
        {
            var assemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                DatadogTraceAssemblyName
            };
            foreach (var assemblyName in mappingResolutionResult.ProxyAssemblyPathsByName.Keys)
            {
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            foreach (var assemblyName in mappingResolutionResult.TargetAssemblyPathsByName.Keys)
            {
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            foreach (var assemblyName in requiredAccessCheckAssemblyNames)
            {
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            var generatedAssemblyName = assemblyDef.Name?.String;
            foreach (var assemblyName in assemblyNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(assemblyName, generatedAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var customAttribute = new CustomAttribute(ignoresAccessChecksToAttributeCtor);
                customAttribute.ConstructorArguments.Add(new CAArgument(moduleDef.CorLibTypes.String, assemblyName));
                assemblyDef.CustomAttributes.Add(customAttribute);
            }
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
            if (string.IsNullOrWhiteSpace(directory) ||
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
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        yield return directory!;
                    }
                }
            }

            var coreLibraryDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(coreLibraryDirectory))
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
        /// Computes deterministic mvid.
        /// </summary>
        /// <param name="generatedAssemblyName">The generated assembly name value.</param>
        /// <param name="mappings">The mappings value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static Guid ComputeDeterministicMvid(string generatedAssemblyName, IReadOnlyList<DuckTypeAotMapping> mappings)
        {
            var deterministicInput = new StringBuilder()
                .Append(generatedAssemblyName)
                .Append('\n');

            foreach (var mapping in mappings.OrderBy(m => m.Key, StringComparer.Ordinal))
            {
                _ = deterministicInput
                    .Append(mapping.Key)
                    .Append('\n');
            }

            using var sha256 = SHA256.Create();
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
        /// Represents the binding plan used to determine whether an assignable alias is safe.
        /// </summary>
        private readonly struct AliasSemanticBindingPlan
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="AliasSemanticBindingPlan"/> struct.
            /// </summary>
            /// <param name="kind">The binding plan kind.</param>
            /// <param name="targetType">The target type.</param>
            /// <param name="forwardBindings">The forward bindings.</param>
            /// <param name="structCopyBindings">The DuckCopy bindings.</param>
            /// <param name="closedGenericTargetTypeArguments">The closed generic target type arguments.</param>
            private AliasSemanticBindingPlan(
                AliasSemanticBindingPlanKind kind,
                TypeDef targetType,
                IReadOnlyList<ForwardBinding>? forwardBindings,
                IReadOnlyList<StructCopyFieldBinding>? structCopyBindings,
                IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
            {
                Kind = kind;
                TargetType = targetType;
                ForwardBindings = forwardBindings;
                StructCopyBindings = structCopyBindings;
                ClosedGenericTargetTypeArguments = closedGenericTargetTypeArguments;
            }

            /// <summary>
            /// Gets the binding plan kind.
            /// </summary>
            internal AliasSemanticBindingPlanKind Kind { get; }

            /// <summary>
            /// Gets the target type.
            /// </summary>
            internal TypeDef TargetType { get; }

            /// <summary>
            /// Gets the forward bindings.
            /// </summary>
            internal IReadOnlyList<ForwardBinding>? ForwardBindings { get; }

            /// <summary>
            /// Gets the DuckCopy bindings.
            /// </summary>
            internal IReadOnlyList<StructCopyFieldBinding>? StructCopyBindings { get; }

            /// <summary>
            /// Gets the closed generic target type arguments.
            /// </summary>
            internal IReadOnlyList<TypeSig>? ClosedGenericTargetTypeArguments { get; }

            /// <summary>
            /// Creates a forward binding plan.
            /// </summary>
            /// <param name="targetType">The target type.</param>
            /// <param name="bindings">The forward bindings.</param>
            /// <param name="closedGenericTargetTypeArguments">The closed generic target type arguments.</param>
            /// <returns>The created binding plan.</returns>
            internal static AliasSemanticBindingPlan ForForward(TypeDef targetType, IReadOnlyList<ForwardBinding> bindings, IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
            {
                return new AliasSemanticBindingPlan(AliasSemanticBindingPlanKind.Forward, targetType, bindings, structCopyBindings: null, closedGenericTargetTypeArguments: closedGenericTargetTypeArguments);
            }

            /// <summary>
            /// Creates a DuckCopy binding plan.
            /// </summary>
            /// <param name="targetType">The target type.</param>
            /// <param name="bindings">The DuckCopy bindings.</param>
            /// <param name="closedGenericTargetTypeArguments">The closed generic target type arguments.</param>
            /// <returns>The created binding plan.</returns>
            internal static AliasSemanticBindingPlan ForStructCopy(TypeDef targetType, IReadOnlyList<StructCopyFieldBinding> bindings, IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
            {
                return new AliasSemanticBindingPlan(AliasSemanticBindingPlanKind.StructCopy, targetType, forwardBindings: null, structCopyBindings: bindings, closedGenericTargetTypeArguments: closedGenericTargetTypeArguments);
            }
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
        /// Represents forward method binding info.
        /// </summary>
        private readonly struct ForwardMethodBindingInfo
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ForwardMethodBindingInfo"/> struct.
            /// </summary>
            /// <param name="parameterBindings">The parameter bindings value.</param>
            /// <param name="returnConversion">The return conversion value.</param>
            /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
            internal ForwardMethodBindingInfo(
                IReadOnlyList<MethodParameterBinding> parameterBindings,
                MethodReturnConversion returnConversion,
                IReadOnlyList<TypeSig>? closedGenericMethodArguments,
                int trailingOptionalTargetParameterCount)
            {
                ParameterBindings = parameterBindings;
                ReturnConversion = returnConversion;
                ClosedGenericMethodArguments = closedGenericMethodArguments;
                TrailingOptionalTargetParameterCount = trailingOptionalTargetParameterCount;
            }

            /// <summary>
            /// Gets parameter bindings.
            /// </summary>
            /// <value>The parameter bindings value.</value>
            internal IReadOnlyList<MethodParameterBinding> ParameterBindings { get; }

            /// <summary>
            /// Gets return conversion.
            /// </summary>
            /// <value>The return conversion value.</value>
            internal MethodReturnConversion ReturnConversion { get; }

            /// <summary>
            /// Gets closed generic method arguments.
            /// </summary>
            /// <value>The closed generic method arguments value.</value>
            internal IReadOnlyList<TypeSig>? ClosedGenericMethodArguments { get; }

            /// <summary>
            /// Gets the number of trailing target parameters that should be emitted as optional defaults.
            /// </summary>
            /// <value>The trailing optional target parameter count value.</value>
            internal int TrailingOptionalTargetParameterCount { get; }
        }

        /// <summary>
        /// Represents method parameter binding.
        /// </summary>
        private readonly struct MethodParameterBinding
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MethodParameterBinding"/> struct.
            /// </summary>
            /// <param name="isByRef">The is by ref value.</param>
            /// <param name="useLocalForByRef">The use local for by ref value.</param>
            /// <param name="isOut">The is out value.</param>
            /// <param name="proxyTypeSig">The proxy type sig value.</param>
            /// <param name="targetTypeSig">The target type sig value.</param>
            /// <param name="proxyByRefElementTypeSig">The proxy by ref element type sig value.</param>
            /// <param name="targetByRefElementTypeSig">The target by ref element type sig value.</param>
            /// <param name="preCallConversion">The pre call conversion value.</param>
            /// <param name="postCallConversion">The post call conversion value.</param>
            private MethodParameterBinding(
                bool isByRef,
                bool useLocalForByRef,
                bool isOut,
                TypeSig proxyTypeSig,
                TypeSig targetTypeSig,
                TypeSig? proxyByRefElementTypeSig,
                TypeSig? targetByRefElementTypeSig,
                MethodArgumentConversion preCallConversion,
                MethodReturnConversion postCallConversion)
            {
                IsByRef = isByRef;
                UseLocalForByRef = useLocalForByRef;
                IsOut = isOut;
                ProxyTypeSig = proxyTypeSig;
                TargetTypeSig = targetTypeSig;
                ProxyByRefElementTypeSig = proxyByRefElementTypeSig;
                TargetByRefElementTypeSig = targetByRefElementTypeSig;
                PreCallConversion = preCallConversion;
                PostCallConversion = postCallConversion;
            }

            /// <summary>
            /// Gets a value indicating whether is by ref.
            /// </summary>
            /// <value>The is by ref value.</value>
            internal bool IsByRef { get; }

            /// <summary>
            /// Gets a value indicating whether use local for by ref.
            /// </summary>
            /// <value>The use local for by ref value.</value>
            internal bool UseLocalForByRef { get; }

            /// <summary>
            /// Gets a value indicating whether is out.
            /// </summary>
            /// <value>The is out value.</value>
            internal bool IsOut { get; }

            /// <summary>
            /// Gets proxy type sig.
            /// </summary>
            /// <value>The proxy type sig value.</value>
            internal TypeSig ProxyTypeSig { get; }

            /// <summary>
            /// Gets target type sig.
            /// </summary>
            /// <value>The target type sig value.</value>
            internal TypeSig TargetTypeSig { get; }

            /// <summary>
            /// Gets proxy by ref element type sig.
            /// </summary>
            /// <value>The proxy by ref element type sig value.</value>
            internal TypeSig? ProxyByRefElementTypeSig { get; }

            /// <summary>
            /// Gets target by ref element type sig.
            /// </summary>
            /// <value>The target by ref element type sig value.</value>
            internal TypeSig? TargetByRefElementTypeSig { get; }

            /// <summary>
            /// Gets pre call conversion.
            /// </summary>
            /// <value>The pre call conversion value.</value>
            internal MethodArgumentConversion PreCallConversion { get; }

            /// <summary>
            /// Gets post call conversion.
            /// </summary>
            /// <value>The post call conversion value.</value>
            internal MethodReturnConversion PostCallConversion { get; }

            /// <summary>
            /// Creates a parameter binding for standard (non-byref) arguments.
            /// </summary>
            /// <param name="proxyTypeSig">The proxy type sig value.</param>
            /// <param name="targetTypeSig">The target type sig value.</param>
            /// <param name="preCallConversion">The pre call conversion value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodParameterBinding ForStandard(TypeSig proxyTypeSig, TypeSig targetTypeSig, MethodArgumentConversion preCallConversion)
            {
                return new MethodParameterBinding(
                    isByRef: false,
                    useLocalForByRef: false,
                    isOut: false,
                    proxyTypeSig,
                    targetTypeSig,
                    proxyByRefElementTypeSig: null,
                    targetByRefElementTypeSig: null,
                    preCallConversion,
                    MethodReturnConversion.None());
            }

            /// <summary>
            /// Creates a by-ref parameter binding that passes through directly.
            /// </summary>
            /// <param name="proxyTypeSig">The proxy type sig value.</param>
            /// <param name="targetTypeSig">The target type sig value.</param>
            /// <param name="proxyByRefElementTypeSig">The proxy by ref element type sig value.</param>
            /// <param name="targetByRefElementTypeSig">The target by ref element type sig value.</param>
            /// <param name="isOut">The is out value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodParameterBinding ForByRefDirect(
                TypeSig proxyTypeSig,
                TypeSig targetTypeSig,
                TypeSig proxyByRefElementTypeSig,
                TypeSig targetByRefElementTypeSig,
                bool isOut)
            {
                return new MethodParameterBinding(
                    isByRef: true,
                    useLocalForByRef: false,
                    isOut,
                    proxyTypeSig,
                    targetTypeSig,
                    proxyByRefElementTypeSig,
                    targetByRefElementTypeSig,
                    MethodArgumentConversion.None(),
                    MethodReturnConversion.None());
            }

            /// <summary>
            /// Creates a by-ref parameter binding that stages through a local temporary.
            /// </summary>
            /// <param name="proxyTypeSig">The proxy type sig value.</param>
            /// <param name="targetTypeSig">The target type sig value.</param>
            /// <param name="proxyByRefElementTypeSig">The proxy by ref element type sig value.</param>
            /// <param name="targetByRefElementTypeSig">The target by ref element type sig value.</param>
            /// <param name="isOut">The is out value.</param>
            /// <param name="preCallConversion">The pre call conversion value.</param>
            /// <param name="postCallConversion">The post call conversion value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodParameterBinding ForByRefWithLocal(
                TypeSig proxyTypeSig,
                TypeSig targetTypeSig,
                TypeSig proxyByRefElementTypeSig,
                TypeSig targetByRefElementTypeSig,
                bool isOut,
                MethodArgumentConversion preCallConversion,
                MethodReturnConversion postCallConversion)
            {
                return new MethodParameterBinding(
                    isByRef: true,
                    useLocalForByRef: true,
                    isOut,
                    proxyTypeSig,
                    targetTypeSig,
                    proxyByRefElementTypeSig,
                    targetByRefElementTypeSig,
                    preCallConversion,
                    postCallConversion);
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
            /// Creates an argument conversion that chains through DuckType.CreateCache&lt;T&gt;.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo DuckChainArgumentToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.DuckChainToProxy(wrapperTypeSig, innerTypeSig), MethodReturnConversion.None());
            }

            /// <summary>
            /// Creates a conversion that chains through DuckType.CreateCache<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
            internal static ForwardFieldBindingInfo DuckChainToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.DuckChainToProxy(wrapperTypeSig, innerTypeSig));
            }

            /// <summary>
            /// Creates a conversion that adapts return-type semantics.
            /// </summary>
            /// <param name="actualTypeSig">The actual type sig value.</param>
            /// <param name="expectedTypeSig">The expected type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo ReturnTypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.TypeConversion(actualTypeSig, expectedTypeSig));
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
        /// Represents method argument conversion.
        /// </summary>
        private readonly struct MethodArgumentConversion
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MethodArgumentConversion"/> struct.
            /// </summary>
            /// <param name="kind">The kind value.</param>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <param name="unwrapWrapperTypeSig">The unwrap wrapper type sig value.</param>
            /// <param name="unwrapInnerTypeSig">The unwrap inner type sig value.</param>
            private MethodArgumentConversion(
                MethodArgumentConversionKind kind,
                TypeSig? wrapperTypeSig,
                TypeSig? innerTypeSig,
                TypeSig? unwrapWrapperTypeSig,
                TypeSig? unwrapInnerTypeSig)
            {
                Kind = kind;
                WrapperTypeSig = wrapperTypeSig;
                InnerTypeSig = innerTypeSig;
                UnwrapWrapperTypeSig = unwrapWrapperTypeSig;
                UnwrapInnerTypeSig = unwrapInnerTypeSig;
            }

            /// <summary>
            /// Gets kind.
            /// </summary>
            /// <value>The kind value.</value>
            internal MethodArgumentConversionKind Kind { get; }

            /// <summary>
            /// Gets wrapper type sig.
            /// </summary>
            /// <value>The wrapper type sig value.</value>
            internal TypeSig? WrapperTypeSig { get; }

            /// <summary>
            /// Gets inner type sig.
            /// </summary>
            /// <value>The inner type sig value.</value>
            internal TypeSig? InnerTypeSig { get; }

            /// <summary>
            /// Gets unwrap wrapper type sig.
            /// </summary>
            /// <value>The unwrap wrapper type sig value.</value>
            internal TypeSig? UnwrapWrapperTypeSig { get; }

            /// <summary>
            /// Gets unwrap inner type sig.
            /// </summary>
            /// <value>The unwrap inner type sig value.</value>
            internal TypeSig? UnwrapInnerTypeSig { get; }

            /// <summary>
            /// Gets a value indicating whether ValueWithType unwrapping is required.
            /// </summary>
            /// <value>The requires value with type unwrap value.</value>
            internal bool RequiresValueWithTypeUnwrap => UnwrapWrapperTypeSig is not null;

            /// <summary>
            /// Creates a no-op conversion descriptor.
            /// </summary>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion None()
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.None,
                    wrapperTypeSig: null,
                    innerTypeSig: null,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that unwraps ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion UnwrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.UnwrapValueWithType,
                    wrapperTypeSig,
                    innerTypeSig,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that extracts IDuckType.Instance.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.ExtractDuckTypeInstance,
                    wrapperTypeSig,
                    innerTypeSig,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that chains through DuckType.CreateCache&lt;T&gt;.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion DuckChainToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.DuckChainToProxy,
                    wrapperTypeSig,
                    innerTypeSig,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that applies runtime type adaptation.
            /// </summary>
            /// <param name="actualTypeSig">The actual type sig value.</param>
            /// <param name="expectedTypeSig">The expected type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion TypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.TypeConversion,
                    actualTypeSig,
                    expectedTypeSig,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that first unwraps ValueWithType&lt;T&gt; before applying this conversion.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal MethodArgumentConversion WithValueWithTypeUnwrap(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(
                    Kind,
                    WrapperTypeSig,
                    InnerTypeSig,
                    wrapperTypeSig,
                    innerTypeSig);
            }
        }

        /// <summary>
        /// Represents method return conversion.
        /// </summary>
        private readonly struct MethodReturnConversion
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MethodReturnConversion"/> struct.
            /// </summary>
            /// <param name="kind">The kind value.</param>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            private MethodReturnConversion(MethodReturnConversionKind kind, TypeSig? wrapperTypeSig, TypeSig? innerTypeSig)
            {
                Kind = kind;
                WrapperTypeSig = wrapperTypeSig;
                InnerTypeSig = innerTypeSig;
            }

            /// <summary>
            /// Gets kind.
            /// </summary>
            /// <value>The kind value.</value>
            internal MethodReturnConversionKind Kind { get; }

            /// <summary>
            /// Gets wrapper type sig.
            /// </summary>
            /// <value>The wrapper type sig value.</value>
            internal TypeSig? WrapperTypeSig { get; }

            /// <summary>
            /// Gets inner type sig.
            /// </summary>
            /// <value>The inner type sig value.</value>
            internal TypeSig? InnerTypeSig { get; }

            /// <summary>
            /// Creates a no-op conversion descriptor.
            /// </summary>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion None()
            {
                return new MethodReturnConversion(MethodReturnConversionKind.None, wrapperTypeSig: null, innerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that wraps a value into ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="sourceTypeSig">The source type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion WrapValueWithType(TypeSig wrapperTypeSig, TypeSig sourceTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.WrapValueWithType, wrapperTypeSig, sourceTypeSig);
            }

            /// <summary>
            /// Creates a conversion that duck-chains then wraps into ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="sourceTypeSig">The source type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion WrapValueWithTypeAfterDuckChainToProxy(TypeSig wrapperTypeSig, TypeSig sourceTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.WrapValueWithTypeAfterDuckChainToProxy, wrapperTypeSig, sourceTypeSig);
            }

            /// <summary>
            /// Creates a conversion that applies type conversion then wraps into ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="sourceTypeSig">The source type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion WrapValueWithTypeAfterTypeConversion(TypeSig wrapperTypeSig, TypeSig sourceTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.WrapValueWithTypeAfterTypeConversion, wrapperTypeSig, sourceTypeSig);
            }

            /// <summary>
            /// Creates a conversion that chains through DuckType.CreateCache<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
            internal static MethodReturnConversion DuckChainToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.DuckChainToProxy, wrapperTypeSig, innerTypeSig);
            }

            /// <summary>
            /// Creates a conversion that extracts IDuckType.Instance.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.ExtractDuckTypeInstance, wrapperTypeSig, innerTypeSig);
            }

            /// <summary>
            /// Creates a conversion that applies runtime type adaptation.
            /// </summary>
            /// <param name="actualTypeSig">The actual type sig value.</param>
            /// <param name="expectedTypeSig">The expected type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion TypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.TypeConversion, actualTypeSig, expectedTypeSig);
            }
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

        /// <summary>
        /// Represents method compatibility failure.
        /// </summary>
        private readonly struct MethodCompatibilityFailure
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MethodCompatibilityFailure"/> struct.
            /// </summary>
            /// <param name="detail">The detail value.</param>
            /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
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
        /// Represents a forward target method candidate and the configured proxy-name alternative that found it.
        /// </summary>
        private readonly struct ForwardMethodCandidate
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ForwardMethodCandidate"/> struct.
            /// </summary>
            /// <param name="method">The method value.</param>
            /// <param name="nameOrdinal">The configured name ordinal value.</param>
            internal ForwardMethodCandidate(MethodDef method, int nameOrdinal)
            {
                Method = method;
                NameOrdinal = nameOrdinal;
            }

            /// <summary>
            /// Gets method.
            /// </summary>
            /// <value>The method value.</value>
            internal MethodDef Method { get; }

            /// <summary>
            /// Gets name ordinal.
            /// </summary>
            /// <value>The name ordinal value.</value>
            internal int NameOrdinal { get; }
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

                var funcObjectObjectCtor = typeof(Func<object?, object?>).GetConstructor(new[] { typeof(object), typeof(IntPtr) });
                if (funcObjectObjectCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve Func<object?, object?> constructor.");
                }

                var actionCtor = typeof(Action).GetConstructor(new[] { typeof(object), typeof(IntPtr) });
                if (actionCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve Action constructor.");
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

                var registerAotProxyFailureMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotProxyFailure),
                    new[] { typeof(Type), typeof(Type), typeof(Action) });
                if (registerAotProxyFailureMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotProxyFailure(Type, Type, Action).");
                }

                var registerAotReverseProxyFailureMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotReverseProxyFailure),
                    new[] { typeof(Type), typeof(Type), typeof(Action) });
                if (registerAotReverseProxyFailureMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotReverseProxyFailure(Type, Type, Action).");
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

                var iDuckTypeType = moduleDef.Import(typeof(IDuckType));
                if (iDuckTypeType is null)
                {
                    throw new InvalidOperationException("Unable to import IDuckType.");
                }

                var iDuckTypeInstanceGetter = typeof(IDuckType).GetProperty(nameof(IDuckType.Instance), BindingFlags.Instance | BindingFlags.Public)?.GetMethod;
                if (iDuckTypeInstanceGetter is null)
                {
                    throw new InvalidOperationException("Unable to resolve IDuckType.Instance getter.");
                }

                var ignoresAccessChecksToCtor = typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute).GetConstructor(new[] { typeof(string) });
                if (ignoresAccessChecksToCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve IgnoresAccessChecksToAttribute(string).");
                }

                var duckTypeAotRegisteredFailureThrowMethod = typeof(DuckTypeAotRegisteredFailureException).GetMethod(
                    nameof(DuckTypeAotRegisteredFailureException.Throw),
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    binder: null,
                    types: new[] { typeof(string), typeof(string) },
                    modifiers: null);
                if (duckTypeAotRegisteredFailureThrowMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckTypeAotRegisteredFailureException.Throw(string, string).");
                }

                GetTypeFromHandleMethod = moduleDef.Import(getTypeFromHandleMethod);
                FuncObjectObjectCtor = moduleDef.Import(funcObjectObjectCtor);
                ActionCtor = moduleDef.Import(actionCtor);
                RegisterAotProxyMethod = moduleDef.Import(registerAotProxyMethod);
                RegisterAotReverseProxyMethod = moduleDef.Import(registerAotReverseProxyMethod);
                RegisterAotProxyFailureMethod = moduleDef.Import(registerAotProxyFailureMethod);
                RegisterAotReverseProxyFailureMethod = moduleDef.Import(registerAotReverseProxyFailureMethod);
                EnableAotModeMethod = moduleDef.Import(enableAotModeMethod);
                ValidateAotRegistryContractMethod = moduleDef.Import(validateAotRegistryContractMethod);
                DuckTypeAotRegisteredFailureThrowMethod = moduleDef.Import(duckTypeAotRegisteredFailureThrowMethod);
                ObjectCtor = moduleDef.Import(objectCtor);
                ObjectToStringMethod = moduleDef.Import(objectToStringMethod);
                IDuckTypeType = iDuckTypeType;
                IDuckTypeInstanceGetter = moduleDef.Import(iDuckTypeInstanceGetter);
                var importedIgnoresAccessChecksToCtor = moduleDef.Import(ignoresAccessChecksToCtor) as ICustomAttributeType;
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
            /// Gets Action constructor.
            /// </summary>
            internal IMethod ActionCtor { get; }

            /// <summary>
            /// Gets register aot proxy method.
            /// </summary>
            /// <value>The register aot proxy method value.</value>
            internal IMethod RegisterAotProxyMethod { get; }

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
            internal IMethod DuckTypeAotRegisteredFailureThrowMethod { get; }

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

        private sealed class EmitterExecutionContext
        {
            private readonly Dictionary<ModuleDef, IReadOnlyDictionary<string, TypeDef>> _typeLookupsByModule = new();
            private readonly Dictionary<string, RuntimeTypeResolutionCacheEntry> _runtimeTypesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, AssemblyResolutionCacheEntry> _preferredRuntimeAssembliesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, AssemblyResolutionCacheEntry> _resolvedRuntimeAssembliesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, RuntimeTypeResolutionCacheEntry> _typesByName = new(StringComparer.Ordinal);
            private readonly Dictionary<string, FailureProbeCacheEntry> _failureProbesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, FailureProbeCacheEntry> _failureProbesByFingerprint = new(StringComparer.Ordinal);
            private readonly Dictionary<Assembly, RuntimeAssemblyTypeIndex> _runtimeTypeIndexesByAssembly = new();
            private readonly Dictionary<TypeDef, TargetTypePlan> _targetTypePlans = new();
            private readonly Dictionary<TypeDef, ProxyTypePlan> _proxyTypePlans = new();
            private readonly Dictionary<MethodDef, MethodPlan> _methodPlans = new(ReferenceIdentityComparer<MethodDef>.Instance);
            private readonly Dictionary<ITypeDefOrRef, ITypeDefOrRef> _importedTypeDefOrRefs = new(ReferenceIdentityComparer<ITypeDefOrRef>.Instance);
            private readonly Dictionary<Type, ITypeDefOrRef> _importedRuntimeTypes = new();
            private readonly Dictionary<string, TypeSig> _importedTypeSigs = new(StringComparer.Ordinal);
            private readonly Dictionary<string, TypeSig> _substitutedTypeSigs = new(StringComparer.Ordinal);
            private readonly Dictionary<IMethod, IMethod> _importedMethods = new(ReferenceIdentityComparer<IMethod>.Instance);
            private readonly Dictionary<IField, IField> _importedFields = new(ReferenceIdentityComparer<IField>.Instance);
            private readonly Dictionary<string, ForwardBindingPlanCacheEntry> _forwardBindingsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, ForwardMethodBindingPlanCacheEntry> _forwardMethodBindingsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, StructCopyFieldBindingPlanCacheEntry> _structCopyBindingsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, MethodReturnConversionCacheEntry> _methodReturnConversionsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, MethodArgumentConversionCacheEntry> _methodArgumentConversionsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IMethod> _methodCallTargetsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, PropertySig> _propertySignaturesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IField> _valueWithTypeValueFieldRefsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IMethodDefOrRef> _valueWithTypeCreateMethodRefsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IMethodDefOrRef> _duckTypeCreateCacheCreateMethodRefsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IMethod> _duckTypeCreateCacheCreateFromMethodRefsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IMethodDefOrRef> _nullableCtorRefsByKey = new(StringComparer.Ordinal);
            private readonly HashSet<string> _requiredAccessCheckAssemblyNames = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, MethodDef> _failureThrowerMethodsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<TypeDef, ReverseCustomAttributePlan> _reverseCustomAttributePlansByType = new();
            private readonly Dictionary<TypeDef, IReadOnlyList<MethodDef>> _duckIncludeMethodsByTargetType = new();
            private readonly Dictionary<ICustomAttributeType, ICustomAttributeType> _importedCustomAttributeTypes = new(ReferenceIdentityComparer<ICustomAttributeType>.Instance);

            internal EmitterExecutionContext(IReadOnlyDictionary<string, string> runtimeTypeResolutionAssemblyPathsByName, TargetTypeIndex targetTypeIndex)
            {
                RuntimeTypeResolutionAssemblyPathsByName = runtimeTypeResolutionAssemblyPathsByName;
                TargetTypeIndex = targetTypeIndex;
                LoadedRuntimeAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            }

            internal IReadOnlyDictionary<string, string> RuntimeTypeResolutionAssemblyPathsByName { get; }

            internal TargetTypeIndex TargetTypeIndex { get; }

            internal IReadOnlyList<Assembly> LoadedRuntimeAssemblies { get; }

            internal IReadOnlyCollection<string> RequiredAccessCheckAssemblyNames => _requiredAccessCheckAssemblyNames;

            internal void AddRequiredAccessCheckAssemblyName(string? assemblyName)
            {
                if (!string.IsNullOrWhiteSpace(assemblyName))
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
                    if (!string.IsNullOrWhiteSpace(type.FullName))
                    {
                        typeLookup[type.FullName] = type;
                    }

                    if (!string.IsNullOrWhiteSpace(type.ReflectionFullName))
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

            internal bool TryGetFailureProbe(string key, out FailureProbeCacheEntry failureProbe)
            {
                return _failureProbesByKey.TryGetValue(key, out failureProbe!);
            }

            internal void CacheFailureProbe(string key, Type? exceptionType, string? exceptionMessage, bool succeeded)
            {
                _failureProbesByKey[key] = new FailureProbeCacheEntry(exceptionType, exceptionMessage, succeeded);
            }

            internal bool TryGetFailureProbeByFingerprint(string key, out FailureProbeCacheEntry failureProbe)
            {
                return _failureProbesByFingerprint.TryGetValue(key, out failureProbe!);
            }

            internal void CacheFailureProbeByFingerprint(string key, Type? exceptionType, string? exceptionMessage, bool succeeded)
            {
                _failureProbesByFingerprint[key] = new FailureProbeCacheEntry(exceptionType, exceptionMessage, succeeded);
            }

            internal bool TryGetRuntimeTypeFromAssemblyIndex(Assembly assembly, string typeName, out Type? runtimeType)
            {
                var typeIndex = GetOrCreateRuntimeTypeIndex(assembly);
                return typeIndex.TryGetType(typeName, out runtimeType);
            }

            internal TargetTypePlan GetOrCreateTargetTypePlan(TypeDef targetType)
            {
                if (_targetTypePlans.TryGetValue(targetType, out var cachedPlan))
                {
                    return cachedPlan;
                }

                var plan = new TargetTypePlan(targetType);
                _targetTypePlans[targetType] = plan;
                return plan;
            }

            internal ProxyTypePlan GetOrCreateProxyTypePlan(TypeDef proxyType)
            {
                if (_proxyTypePlans.TryGetValue(proxyType, out var cachedPlan))
                {
                    return cachedPlan;
                }

                var plan = new ProxyTypePlan(proxyType);
                _proxyTypePlans[proxyType] = plan;
                return plan;
            }

            internal MethodPlan GetOrCreateMethodPlan(MethodDef method)
            {
                if (_methodPlans.TryGetValue(method, out var cachedPlan))
                {
                    return cachedPlan;
                }

                var plan = new MethodPlan(method);
                _methodPlans[method] = plan;
                return plan;
            }

            private RuntimeAssemblyTypeIndex GetOrCreateRuntimeTypeIndex(Assembly assembly)
            {
                if (_runtimeTypeIndexesByAssembly.TryGetValue(assembly, out var cachedIndex))
                {
                    return cachedIndex;
                }

                var phaseStopwatch = StartProfilePhase();
                var index = RuntimeAssemblyTypeIndex.Create(assembly);
                StopProfilePhase(
                    phaseStopwatch,
                    seconds => _currentProfile!.BuildRuntimeTypeIndexSeconds += seconds);
                _runtimeTypeIndexesByAssembly[assembly] = index;
                return index;
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

            internal bool TryGetForwardBindingPlan(string key, out ForwardBindingPlanCacheEntry plan)
                => _forwardBindingsByKey.TryGetValue(key, out plan!);

            internal void CacheForwardBindingPlan(string key, ForwardBindingPlanCacheEntry plan)
                => _forwardBindingsByKey[key] = plan;

            internal bool TryGetForwardMethodBindingPlan(string key, out ForwardMethodBindingPlanCacheEntry plan)
                => _forwardMethodBindingsByKey.TryGetValue(key, out plan!);

            internal void CacheForwardMethodBindingPlan(string key, ForwardMethodBindingPlanCacheEntry plan)
                => _forwardMethodBindingsByKey[key] = plan;

            internal bool TryGetStructCopyBindingPlan(string key, out StructCopyFieldBindingPlanCacheEntry plan)
                => _structCopyBindingsByKey.TryGetValue(key, out plan!);

            internal void CacheStructCopyBindingPlan(string key, StructCopyFieldBindingPlanCacheEntry plan)
                => _structCopyBindingsByKey[key] = plan;

            internal bool TryGetMethodReturnConversion(string key, out MethodReturnConversionCacheEntry conversion)
                => _methodReturnConversionsByKey.TryGetValue(key, out conversion!);

            internal void CacheMethodReturnConversion(string key, MethodReturnConversionCacheEntry conversion)
                => _methodReturnConversionsByKey[key] = conversion;

            internal bool TryGetMethodArgumentConversion(string key, out MethodArgumentConversionCacheEntry conversion)
                => _methodArgumentConversionsByKey.TryGetValue(key, out conversion!);

            internal void CacheMethodArgumentConversion(string key, MethodArgumentConversionCacheEntry conversion)
                => _methodArgumentConversionsByKey[key] = conversion;

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

            internal bool TryGetReverseCustomAttributePlan(TypeDef targetType, out ReverseCustomAttributePlan plan)
                => _reverseCustomAttributePlansByType.TryGetValue(targetType, out plan!);

            internal void CacheReverseCustomAttributePlan(TypeDef targetType, ReverseCustomAttributePlan plan)
                => _reverseCustomAttributePlansByType[targetType] = plan;

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

                var methods = new List<MethodDef>();
                var current = targetType;
                while (current is not null)
                {
                    foreach (var method in current.Methods)
                    {
                        if (method.IsConstructor || method.IsStatic || method.IsFinal || method.IsPrivate || method.IsSpecialName)
                        {
                            continue;
                        }

                        if (!method.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, DuckIncludeAttributeTypeName, StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        methods.Add(method);
                    }

                    current = current.BaseType?.ResolveTypeDef();
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

        private sealed class CanonicalTargetAliasPlan
        {
            internal CanonicalTargetAliasPlan(NullableAliasTargetInfo? nullableAlias, IReadOnlyList<TargetAliasTargetInfo> assignableTargets)
            {
                NullableAlias = nullableAlias;
                AssignableTargets = assignableTargets;
            }

            internal NullableAliasTargetInfo? NullableAlias { get; }

            internal IReadOnlyList<TargetAliasTargetInfo> AssignableTargets { get; }
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

        private sealed class NullableAliasTargetInfo
        {
            internal NullableAliasTargetInfo(string typeName, string assemblyName)
            {
                TypeName = typeName;
                AssemblyName = assemblyName;
            }

            internal string TypeName { get; }

            internal string AssemblyName { get; }
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

        private sealed class FailureProbeCacheEntry
        {
            internal FailureProbeCacheEntry(Type? exceptionType, string? exceptionMessage, bool succeeded)
            {
                ExceptionType = exceptionType;
                ExceptionMessage = exceptionMessage;
                Succeeded = succeeded;
            }

            internal Type? ExceptionType { get; }

            internal string? ExceptionMessage { get; }

            internal bool Succeeded { get; }
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

        private sealed class ForwardMethodBindingPlanCacheEntry
        {
            internal ForwardMethodBindingPlanCacheEntry(ForwardMethodBindingInfo binding)
            {
                Succeeded = true;
                Binding = binding;
            }

            internal ForwardMethodBindingPlanCacheEntry(string detail)
            {
                Succeeded = false;
                FailureDetail = detail;
            }

            internal bool Succeeded { get; }

            internal ForwardMethodBindingInfo Binding { get; }

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

        private sealed class MethodReturnConversionCacheEntry
        {
            internal MethodReturnConversionCacheEntry(MethodReturnConversion conversion)
            {
                Succeeded = true;
                Conversion = conversion;
            }

            internal MethodReturnConversionCacheEntry()
            {
                Succeeded = false;
            }

            internal bool Succeeded { get; }

            internal MethodReturnConversion Conversion { get; }
        }

        private sealed class MethodArgumentConversionCacheEntry
        {
            internal MethodArgumentConversionCacheEntry(MethodArgumentConversion conversion)
            {
                Succeeded = true;
                Conversion = conversion;
            }

            internal MethodArgumentConversionCacheEntry()
            {
                Succeeded = false;
            }

            internal bool Succeeded { get; }

            internal MethodArgumentConversion Conversion { get; }
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

        private sealed class RuntimeAssemblyTypeIndex
        {
            private readonly IReadOnlyDictionary<string, Type> _typesByName;

            private RuntimeAssemblyTypeIndex(IReadOnlyDictionary<string, Type> typesByName)
            {
                _typesByName = typesByName;
            }

#if NET6_0_OR_GREATER
            [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ducktype AOT runner indexes runtime assembly types during build-time analysis only. The standalone tool build is trimmed, but this reflection is not part of the emitted application runtime.")]
#endif
            internal static RuntimeAssemblyTypeIndex Create(Assembly assembly)
            {
                var typesByName = new Dictionary<string, Type>(StringComparer.Ordinal);
                IEnumerable<Type> types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type is not null)!;
                }
                catch
                {
                    types = Array.Empty<Type>();
                }

                foreach (var type in types)
                {
                    if (string.IsNullOrWhiteSpace(type.FullName))
                    {
                        continue;
                    }

                    if (!typesByName.ContainsKey(type.FullName))
                    {
                        typesByName[type.FullName] = type;
                    }

                    var displayName = type.ToString();
                    if (!string.IsNullOrWhiteSpace(displayName) && !typesByName.ContainsKey(displayName))
                    {
                        typesByName[displayName] = type;
                    }
                }

                return new RuntimeAssemblyTypeIndex(typesByName);
            }

            internal bool TryGetType(string typeName, out Type? runtimeType)
            {
                if (_typesByName.TryGetValue(typeName, out var cachedType))
                {
                    runtimeType = cachedType;
                    return true;
                }

                runtimeType = null;
                return false;
            }
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

        private sealed class TargetTypePlan
        {
            private readonly TypeDef _targetType;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetMethodCandidate>> _methodsByExactName;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetMethodCandidate>> _methodsBySimpleName;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetMethodCandidate>> _methodsByExactNameAndShape;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetMethodCandidate>> _methodsBySimpleNameAndShape;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetPropertyCandidate>> _propertiesByName;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetFieldCandidate>> _fieldsByName;
            private readonly Dictionary<string, IReadOnlyList<ForwardMethodCandidate>> _forwardMethodCandidatesByKey = new(StringComparer.Ordinal);
            private bool _reverseMetadataInitialized;
            private bool _hasReverseMethodAttributes;
            private IReadOnlyList<MethodDef>? _declaredReverseImplementationMethods;

            internal TargetTypePlan(TypeDef targetType)
            {
                _targetType = targetType;
                IdentityKey = BuildTypeIdentityKey(targetType);
                var hierarchy = new List<TypeDef>();
                var current = targetType;
                while (current is not null)
                {
                    hierarchy.Add(current);
                    current = current.BaseType?.ResolveTypeDef();
                }

                Hierarchy = hierarchy;

                var exactMethodIndex = new Dictionary<string, List<TargetMethodCandidate>>(StringComparer.Ordinal);
                var simpleMethodIndex = new Dictionary<string, List<TargetMethodCandidate>>(StringComparer.Ordinal);
                var exactMethodShapeIndex = new Dictionary<string, List<TargetMethodCandidate>>(StringComparer.Ordinal);
                var simpleMethodShapeIndex = new Dictionary<string, List<TargetMethodCandidate>>(StringComparer.Ordinal);
                var propertyIndex = new Dictionary<string, List<TargetPropertyCandidate>>(StringComparer.Ordinal);
                var fieldIndex = new Dictionary<string, List<TargetFieldCandidate>>(StringComparer.Ordinal);

                foreach (var hierarchyType in hierarchy)
                {
                    var isInherited = !ReferenceEquals(hierarchyType, targetType);
                    foreach (var method in hierarchyType.Methods)
                    {
                        var candidate = new TargetMethodCandidate(method, isInherited);
                        var methodName = method.Name.String ?? method.Name.ToString();
                        AddIndexEntry(exactMethodIndex, methodName, candidate);
                        AddIndexEntry(exactMethodShapeIndex, BuildMethodShapeIndexKey(methodName, method), candidate);

                        var simpleName = ResolveSimpleMethodName(methodName);
                        if (!string.IsNullOrWhiteSpace(simpleName))
                        {
                            AddIndexEntry(simpleMethodIndex, simpleName!, candidate);
                            AddIndexEntry(simpleMethodShapeIndex, BuildMethodShapeIndexKey(simpleName!, method), candidate);
                        }
                    }

                    foreach (var property in hierarchyType.Properties)
                    {
                        AddIndexEntry(propertyIndex, property.Name.String ?? property.Name.ToString(), new TargetPropertyCandidate(property, isInherited));
                    }

                    foreach (var field in hierarchyType.Fields)
                    {
                        AddIndexEntry(fieldIndex, field.Name.String ?? field.Name.ToString(), new TargetFieldCandidate(field, isInherited));
                    }
                }

                _methodsByExactName = exactMethodIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetMethodCandidate>)kvp.Value, StringComparer.Ordinal);
                _methodsBySimpleName = simpleMethodIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetMethodCandidate>)kvp.Value, StringComparer.Ordinal);
                _methodsByExactNameAndShape = exactMethodShapeIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetMethodCandidate>)kvp.Value, StringComparer.Ordinal);
                _methodsBySimpleNameAndShape = simpleMethodShapeIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetMethodCandidate>)kvp.Value, StringComparer.Ordinal);
                _propertiesByName = propertyIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetPropertyCandidate>)kvp.Value, StringComparer.Ordinal);
                _fieldsByName = fieldIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetFieldCandidate>)kvp.Value, StringComparer.Ordinal);
            }

            internal IReadOnlyList<TypeDef> Hierarchy { get; }

            internal string IdentityKey { get; }

            internal bool HasReverseMethodAttributes
            {
                get
                {
                    EnsureReverseMetadata();
                    return _hasReverseMethodAttributes;
                }
            }

            internal IReadOnlyList<MethodDef> DeclaredReverseImplementationMethods
            {
                get
                {
                    EnsureReverseMetadata();
                    return _declaredReverseImplementationMethods ?? Array.Empty<MethodDef>();
                }
            }

            internal IEnumerable<TargetMethodCandidate> GetMethodCandidatesByExactName(string methodName)
                => _methodsByExactName.TryGetValue(methodName, out var candidates) ? candidates : Array.Empty<TargetMethodCandidate>();

            internal IEnumerable<TargetMethodCandidate> GetMethodCandidatesBySimpleName(string methodName)
                => _methodsBySimpleName.TryGetValue(methodName, out var candidates) ? candidates : Array.Empty<TargetMethodCandidate>();

            internal IEnumerable<TargetMethodCandidate> GetMethodCandidatesByExactNameAndShape(string methodName, int genericArity, int parameterCount)
                => _methodsByExactNameAndShape.TryGetValue(BuildMethodShapeIndexKey(methodName, genericArity, parameterCount), out var candidates) ? candidates : Array.Empty<TargetMethodCandidate>();

            internal IEnumerable<TargetMethodCandidate> GetMethodCandidatesBySimpleNameAndShape(string methodName, int genericArity, int parameterCount)
                => _methodsBySimpleNameAndShape.TryGetValue(BuildMethodShapeIndexKey(methodName, genericArity, parameterCount), out var candidates) ? candidates : Array.Empty<TargetMethodCandidate>();

            internal IEnumerable<TargetPropertyCandidate> GetPropertyCandidates(string propertyName, bool useIgnoreCaseMemberMatching)
            {
                if (!useIgnoreCaseMemberMatching)
                {
                    return _propertiesByName.TryGetValue(propertyName, out var candidates) ? candidates : Array.Empty<TargetPropertyCandidate>();
                }

                return _propertiesByName
                      .Where(kvp => string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                      .SelectMany(kvp => kvp.Value);
            }

            internal IEnumerable<TargetFieldCandidate> GetFieldCandidates(string fieldName, bool useIgnoreCaseMemberMatching)
            {
                if (!useIgnoreCaseMemberMatching)
                {
                    return _fieldsByName.TryGetValue(fieldName, out var candidates) ? candidates : Array.Empty<TargetFieldCandidate>();
                }

                return _fieldsByName
                      .Where(kvp => string.Equals(kvp.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                      .SelectMany(kvp => kvp.Value);
            }

            internal IReadOnlyList<ForwardMethodCandidate> GetForwardMethodCandidates(
                ProxyMethodPlan proxyMethodPlan,
                IReadOnlyList<string> explicitInterfaceTypeNames,
                bool useRelaxedNameComparison,
                int expectedGenericArity,
                IReadOnlyList<string> configuredParameterTypeNames,
                bool allowPrivateBaseMembers,
                bool allowTrailingOptionalTargetParameters)
            {
                var phaseStopwatch = StartProfilePhase();
                var profile = _currentProfile;
                try
                {
                    var cacheKey = string.Concat(
                        proxyMethodPlan.ForwardTargetMethodNamesCacheKey,
                        "::",
                        proxyMethodPlan.ExplicitInterfaceTypeNamesCacheKey,
                        "::",
                        useRelaxedNameComparison ? "relaxed" : "strict",
                        "::",
                        expectedGenericArity.ToString(CultureInfo.InvariantCulture),
                        "::",
                        proxyMethodPlan.ParameterCountCacheKey,
                        "::",
                        allowPrivateBaseMembers ? "base" : "declared",
                        "::",
                        proxyMethodPlan.DuckBindingFlagsCacheKey,
                        "::",
                        allowTrailingOptionalTargetParameters ? "optional" : "exact",
                        "::",
                        proxyMethodPlan.ConfiguredParameterTypeNamesCacheKey);
                    if (_forwardMethodCandidatesByKey.TryGetValue(cacheKey, out var cachedCandidates))
                    {
                        if (profile is not null)
                        {
                            profile.ForwardCandidateListCacheHits++;
                        }

                        return cachedCandidates;
                    }

                    if (profile is not null)
                    {
                        profile.ForwardCandidateListCacheMisses++;
                    }

                    var proxyParameterCount = proxyMethodPlan.Method.MethodSig.Params.Count;
                    var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);
                    var candidates = new List<ForwardMethodCandidate>();
                    for (var candidateMethodNameIndex = 0; candidateMethodNameIndex < proxyMethodPlan.ForwardTargetMethodNames.Count; candidateMethodNameIndex++)
                    {
                        var candidateMethodName = proxyMethodPlan.ForwardTargetMethodNames[candidateMethodNameIndex];
                        foreach (var candidateEntry in GetForwardMethodCandidateEntries(candidateMethodName, expectedGenericArity, proxyParameterCount, proxyMethodPlan.UseIgnoreCaseMemberMatching, allowTrailingOptionalTargetParameters))
                        {
                            if (profile is not null)
                            {
                                profile.ForwardCandidateEnumeratedCount++;
                            }

                            var candidate = candidateEntry.Method;
                            if (!emittedCandidates.Add(GetMethodCandidateKey(candidate)))
                            {
                                if (profile is not null)
                                {
                                    profile.ForwardCandidateDedupRejectCount++;
                                }

                                continue;
                            }

                            var candidateMethodActualName = candidateEntry.MethodName;
                            if (!IsForwardTargetMethodNameMatch(
                                    candidateMethodActualName,
                                    candidateMethodName,
                                    explicitInterfaceTypeNames,
                                    useRelaxedNameComparison,
                                    proxyMethodPlan.UseIgnoreCaseMemberMatching))
                            {
                                if (profile is not null)
                                {
                                    profile.ForwardCandidateNameRejectCount++;
                                }

                                continue;
                            }

                            if (configuredParameterTypeNames.Count > 0 &&
                                !IsForwardCandidateParameterTypeNameMatch(candidateEntry, configuredParameterTypeNames))
                            {
                                if (profile is not null)
                                {
                                    profile.ForwardCandidateParameterTypeRejectCount++;
                                }

                                continue;
                            }

                            if (!allowPrivateBaseMembers &&
                                candidateEntry.IsInherited &&
                                candidate.IsPrivate)
                            {
                                if (profile is not null)
                                {
                                    profile.ForwardCandidatePrivateRejectCount++;
                                }

                                continue;
                            }

                            if (!IsMethodCandidateAllowedByBindingFlags(candidateEntry, proxyMethodPlan.DuckBindingFlags, allowPrivateBaseMembers))
                            {
                                if (profile is not null)
                                {
                                    profile.ForwardCandidatePrivateRejectCount++;
                                }

                                continue;
                            }

                            if (profile is not null)
                            {
                                profile.ForwardCandidateAcceptedCount++;
                            }

                            candidates.Add(new ForwardMethodCandidate(candidate, candidateMethodNameIndex));
                        }
                    }

                    _forwardMethodCandidatesByKey[cacheKey] = candidates;
                    return candidates;
                }
                finally
                {
                    StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardCandidateListBuildSeconds += seconds);
                }
            }

            private static bool IsSimpleMethodNameMatch(string methodName, string requestedMethodName, StringComparison comparison)
            {
                var simpleName = ResolveSimpleMethodName(methodName);
                return simpleName is not null && string.Equals(simpleName, requestedMethodName, comparison);
            }

            private static void AddIndexEntry<TCandidate>(IDictionary<string, List<TCandidate>> index, string key, TCandidate candidate)
            {
                if (!index.TryGetValue(key, out var candidates))
                {
                    candidates = [];
                    index[key] = candidates;
                }

                candidates.Add(candidate);
            }

            private static string? ResolveSimpleMethodName(string methodName)
            {
                var separatorIndex = methodName.LastIndexOf('.');
                if (separatorIndex < 0 || separatorIndex == methodName.Length - 1)
                {
                    return null;
                }

                return methodName.Substring(separatorIndex + 1);
            }

            private static string BuildMethodShapeIndexKey(string methodName, MethodDef method)
            {
                return BuildMethodShapeIndexKey(methodName, (int)method.MethodSig.GenParamCount, method.MethodSig.Params.Count);
            }

            private static string BuildMethodShapeIndexKey(string methodName, int genericArity, int parameterCount)
            {
                return string.Concat(methodName, "::", genericArity.ToString(CultureInfo.InvariantCulture), "::", parameterCount.ToString(CultureInfo.InvariantCulture));
            }

            private IEnumerable<TargetMethodCandidate> GetForwardMethodCandidateEntries(
                string candidateMethodName,
                int expectedGenericArity,
                int proxyParameterCount,
                bool useIgnoreCaseMemberMatching,
                bool allowTrailingOptionalTargetParameters)
            {
                if (!useIgnoreCaseMemberMatching && !allowTrailingOptionalTargetParameters)
                {
                    return GetMethodCandidatesByExactNameAndShape(candidateMethodName, expectedGenericArity, proxyParameterCount)
                          .Concat(GetMethodCandidatesBySimpleNameAndShape(candidateMethodName, expectedGenericArity, proxyParameterCount));
                }

                var comparison = useIgnoreCaseMemberMatching ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return _methodsByExactName
                      .Where(kvp => string.Equals(kvp.Key, candidateMethodName, comparison) || IsSimpleMethodNameMatch(kvp.Key, candidateMethodName, comparison))
                      .SelectMany(kvp => kvp.Value)
                      .Where(candidate => candidate.Method.MethodSig.GenParamCount == expectedGenericArity &&
                                          IsParameterCountCompatibleWithProxy(candidate.Method, proxyParameterCount, allowTrailingOptionalTargetParameters));
            }

            private void EnsureReverseMetadata()
            {
                if (_reverseMetadataInitialized)
                {
                    return;
                }

                var declaredReverseImplementationMethods = new List<MethodDef>();
                var emittedReverseImplementationKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var hierarchyType in Hierarchy)
                {
                    var isDeclaredOnTargetType = ReferenceEquals(hierarchyType, _targetType);
                    foreach (var method in hierarchyType.Methods)
                    {
                        if (!_hasReverseMethodAttributes &&
                            method.CustomAttributes.Any(IsReverseMethodAttribute))
                        {
                            _hasReverseMethodAttributes = true;
                        }

                        if (!isDeclaredOnTargetType ||
                            method.IsConstructor ||
                            method.IsStatic ||
                            !method.CustomAttributes.Any(IsReverseMethodAttribute))
                        {
                            continue;
                        }

                        var methodKey = GetMethodCandidateKey(method);
                        if (emittedReverseImplementationKeys.Add(methodKey))
                        {
                            declaredReverseImplementationMethods.Add(method);
                        }
                    }

                    foreach (var property in hierarchyType.Properties)
                    {
                        if (!_hasReverseMethodAttributes &&
                            property.CustomAttributes.Any(IsReverseMethodAttribute))
                        {
                            _hasReverseMethodAttributes = true;
                        }

                        if (!IsReverseImplementationPropertyVisibleToDynamic(property) ||
                            !property.CustomAttributes.Any(IsReverseMethodAttribute))
                        {
                            continue;
                        }

                        if (property.GetMethod is not null)
                        {
                            var getterKey = GetMethodCandidateKey(property.GetMethod);
                            if (emittedReverseImplementationKeys.Add(getterKey))
                            {
                                declaredReverseImplementationMethods.Add(property.GetMethod);
                            }
                        }

                        if (property.SetMethod is not null)
                        {
                            var setterKey = GetMethodCandidateKey(property.SetMethod);
                            if (emittedReverseImplementationKeys.Add(setterKey))
                            {
                                declaredReverseImplementationMethods.Add(property.SetMethod);
                            }
                        }
                    }
                }

                _declaredReverseImplementationMethods = declaredReverseImplementationMethods;
                _reverseMetadataInitialized = true;
            }
        }

        private sealed class TargetMethodCandidate
        {
            internal TargetMethodCandidate(MethodDef method, bool isInherited)
            {
                Method = method;
                MethodName = method.Name.String ?? method.Name.ToString();
                IsInherited = isInherited;
                ParameterTypeComparisonNames = BuildParameterTypeComparisonNames(method);
            }

            internal MethodDef Method { get; }

            internal string MethodName { get; }

            internal bool IsInherited { get; }

            internal IReadOnlyList<HashSet<string>> ParameterTypeComparisonNames { get; }

            private static IReadOnlyList<HashSet<string>> BuildParameterTypeComparisonNames(MethodDef method)
            {
                if (method.MethodSig.Params.Count == 0)
                {
                    return Array.Empty<HashSet<string>>();
                }

                var parameterTypeNames = new HashSet<string>[method.MethodSig.Params.Count];
                for (var parameterIndex = 0; parameterIndex < parameterTypeNames.Length; parameterIndex++)
                {
                    parameterTypeNames[parameterIndex] = GetTypeComparisonNames(method.MethodSig.Params[parameterIndex]);
                }

                return parameterTypeNames;
            }
        }

        private sealed class TargetPropertyCandidate
        {
            internal TargetPropertyCandidate(PropertyDef property, bool isInherited)
            {
                Property = property;
                IsInherited = isInherited;
                IsEffectivelyPrivate = IsPropertyPrivate(property);
            }

            internal PropertyDef Property { get; }

            internal bool IsInherited { get; }

            internal bool IsEffectivelyPrivate { get; }
        }

        private sealed class TargetFieldCandidate
        {
            internal TargetFieldCandidate(FieldDef field, bool isInherited)
            {
                Field = field;
                IsInherited = isInherited;
            }

            internal FieldDef Field { get; }

            internal bool IsInherited { get; }
        }

        private sealed class InterfaceTraversalEntry
        {
            internal InterfaceTraversalEntry(TypeDef interfaceType, ITypeDefOrRef interfaceReference, TypeSig interfaceTypeSig, bool isProxyType, IReadOnlyList<TypeSig>? genericArguments)
            {
                InterfaceType = interfaceType;
                InterfaceReference = interfaceReference;
                InterfaceTypeSig = interfaceTypeSig;
                IsProxyType = isProxyType;
                GenericArguments = genericArguments;
            }

            internal TypeDef InterfaceType { get; }

            internal ITypeDefOrRef InterfaceReference { get; }

            internal TypeSig InterfaceTypeSig { get; }

            internal bool IsProxyType { get; }

            internal IReadOnlyList<TypeSig>? GenericArguments { get; }
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

        private sealed class InterfaceMethodCollection
        {
            internal InterfaceMethodCollection(IReadOnlyList<MethodDef> methods, IReadOnlyDictionary<MethodDef, InterfaceMethodContract> contractsByMethod)
            {
                Methods = methods;
                ContractsByMethod = contractsByMethod;
            }

            internal IReadOnlyList<MethodDef> Methods { get; }

            internal IReadOnlyDictionary<MethodDef, InterfaceMethodContract> ContractsByMethod { get; }
        }

        private sealed class ProxyTypePlan
        {
            private readonly Dictionary<MethodDef, ProxyMethodPlan> _methodPlans = new(ReferenceIdentityComparer<MethodDef>.Instance);
            private readonly IReadOnlyDictionary<MethodDef, InterfaceMethodContract> _interfaceMethodContractsByMethod;
            private readonly IReadOnlyDictionary<MethodDef, PropertyDef> _propertiesByAccessorMethod;

            internal ProxyTypePlan(TypeDef proxyType)
            {
                var interfaceMethods = BuildInterfaceMethods(proxyType);
                InterfaceMethods = interfaceMethods.Methods;
                _interfaceMethodContractsByMethod = interfaceMethods.ContractsByMethod;
                ClassMethods = BuildClassMethods(proxyType);
                SupportedBaseConstructor = BuildSupportedBaseConstructor(proxyType);
                _propertiesByAccessorMethod = BuildAccessorPropertyMap(proxyType);
            }

            internal IReadOnlyList<MethodDef> InterfaceMethods { get; }

            internal IReadOnlyList<MethodDef> ClassMethods { get; }

            internal MethodDef? SupportedBaseConstructor { get; }

            internal bool TryGetPropertyFromAccessor(MethodDef accessorMethod, out PropertyDef property)
                => _propertiesByAccessorMethod.TryGetValue(accessorMethod, out property!);

            internal InterfaceMethodContract? GetInterfaceMethodContract(MethodDef method)
                => _interfaceMethodContractsByMethod.TryGetValue(method, out var contract) ? contract : null;

            internal ProxyMethodPlan GetOrCreateMethodPlan(MethodDef proxyMethod)
            {
                if (_methodPlans.TryGetValue(proxyMethod, out var cachedPlan))
                {
                    return cachedPlan;
                }

                var plan = new ProxyMethodPlan(proxyMethod);
                _methodPlans[proxyMethod] = plan;
                return plan;
            }

            private static InterfaceMethodCollection BuildInterfaceMethods(TypeDef interfaceType)
            {
                var results = new List<MethodDef>();
                var contractsByMethod = new Dictionary<MethodDef, InterfaceMethodContract>(ReferenceIdentityComparer<MethodDef>.Instance);
                var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
                var visitedMethods = new HashSet<string>(StringComparer.Ordinal);
                var stack = new Stack<InterfaceTraversalEntry>();
                stack.Push(new InterfaceTraversalEntry(interfaceType, interfaceType, interfaceType.ToTypeSig(), isProxyType: true, genericArguments: null));

                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    var currentType = current.InterfaceType;
                    var currentKey = BuildInterfaceTraversalKey(current);
                    if (!visitedTypes.Add(currentKey))
                    {
                        continue;
                    }

                    if (!IsDuckTypeInfrastructureInterface(currentType))
                    {
                        foreach (var method in currentType.Methods)
                        {
                            if (method.IsConstructor || method.IsStatic || IsDuckIgnoreMethod(method))
                            {
                                continue;
                            }

                            var resolvedMethod = CreateInterfaceMethodForTraversal(method, current.GenericArguments);
                            var key = $"{resolvedMethod.Name}::{resolvedMethod.MethodSig}";
                            if (visitedMethods.Add(key))
                            {
                                results.Add(resolvedMethod);
                                contractsByMethod[resolvedMethod] = new InterfaceMethodContract(
                                    current.InterfaceType,
                                    current.InterfaceReference,
                                    current.InterfaceTypeSig,
                                    current.IsProxyType,
                                    resolvedMethod.MethodSig);
                            }
                        }
                    }

                    foreach (var interfaceImpl in currentType.Interfaces)
                    {
                        var resolvedInterface = ResolveInterfaceTypeDefForTraversal(currentType, interfaceImpl.Interface);
                        if (resolvedInterface is not null)
                        {
                            var interfaceTypeSig = interfaceImpl.Interface.ToTypeSig();
                            stack.Push(new InterfaceTraversalEntry(
                                resolvedInterface,
                                interfaceImpl.Interface,
                                interfaceTypeSig,
                                isProxyType: false,
                                ResolveInterfaceGenericArguments(interfaceTypeSig, current.GenericArguments)));
                        }
                    }
                }

                return new InterfaceMethodCollection(results, contractsByMethod);
            }

            private static string BuildInterfaceTraversalKey(InterfaceTraversalEntry entry)
            {
                return string.Concat(
                    entry.InterfaceType.FullName,
                    "::",
                    BuildTypeSigSequenceCacheKey(entry.GenericArguments));
            }

            private static IReadOnlyList<TypeSig>? ResolveInterfaceGenericArguments(TypeSig interfaceTypeSig, IReadOnlyList<TypeSig>? inheritedGenericArguments)
            {
                if (interfaceTypeSig is not GenericInstSig genericInstSig || genericInstSig.GenericArguments.Count == 0)
                {
                    return null;
                }

                var genericArguments = new TypeSig[genericInstSig.GenericArguments.Count];
                for (var i = 0; i < genericArguments.Length; i++)
                {
                    genericArguments[i] = SubstituteTypeAndMethodGenericTypeArguments(
                        genericInstSig.GenericArguments[i],
                        inheritedGenericArguments,
                        closedGenericMethodArguments: null);
                }

                return genericArguments;
            }

            private static MethodDef CreateInterfaceMethodForTraversal(MethodDef method, IReadOnlyList<TypeSig>? inheritedGenericArguments)
            {
                if (inheritedGenericArguments is null || inheritedGenericArguments.Count == 0)
                {
                    return method;
                }

                var substitutedSig = CreateSubstitutedMethodSig(method.MethodSig, inheritedGenericArguments);
                if (string.Equals(substitutedSig.ToString(), method.MethodSig.ToString(), StringComparison.Ordinal))
                {
                    return method;
                }

                var copiedMethod = new MethodDefUser(
                    method.Name,
                    substitutedSig,
                    method.ImplAttributes,
                    method.Attributes);

                foreach (var genericParameter in method.GenericParameters)
                {
                    copiedMethod.GenericParameters.Add(new GenericParamUser(genericParameter.Number, genericParameter.Flags, genericParameter.Name)
                    {
                        Kind = genericParameter.Kind
                    });
                }

                foreach (var customAttribute in method.CustomAttributes)
                {
                    copiedMethod.CustomAttributes.Add(customAttribute);
                }

                foreach (var parameterDefinition in method.ParamDefs)
                {
                    var copiedParameterDefinition = new ParamDefUser(parameterDefinition.Name, parameterDefinition.Sequence, parameterDefinition.Attributes);
                    foreach (var customAttribute in parameterDefinition.CustomAttributes)
                    {
                        copiedParameterDefinition.CustomAttributes.Add(customAttribute);
                    }

                    copiedMethod.ParamDefs.Add(copiedParameterDefinition);
                }

                if (TryFindDeclaringPropertyOnType(method.DeclaringType, method, out var property))
                {
                    foreach (var customAttribute in property!.CustomAttributes)
                    {
                        copiedMethod.CustomAttributes.Add(customAttribute);
                    }
                }

                return copiedMethod;
            }

            private static MethodSig CreateSubstitutedMethodSig(MethodSig sourceSig, IReadOnlyList<TypeSig> inheritedGenericArguments)
            {
                var returnType = SubstituteTypeAndMethodGenericTypeArguments(
                    sourceSig.RetType,
                    inheritedGenericArguments,
                    closedGenericMethodArguments: null);
                var parameterTypes = new TypeSig[sourceSig.Params.Count];
                for (var parameterIndex = 0; parameterIndex < parameterTypes.Length; parameterIndex++)
                {
                    parameterTypes[parameterIndex] = SubstituteTypeAndMethodGenericTypeArguments(
                        sourceSig.Params[parameterIndex],
                        inheritedGenericArguments,
                        closedGenericMethodArguments: null);
                }

                var substitutedSig = sourceSig.Generic
                                         ? (sourceSig.HasThis
                                                ? MethodSig.CreateInstanceGeneric(sourceSig.GenParamCount, returnType, parameterTypes)
                                                : MethodSig.CreateStaticGeneric(sourceSig.GenParamCount, returnType, parameterTypes))
                                         : (sourceSig.HasThis
                                                ? MethodSig.CreateInstance(returnType, parameterTypes)
                                                : MethodSig.CreateStatic(returnType, parameterTypes));
                substitutedSig.ExplicitThis = sourceSig.ExplicitThis;
                return substitutedSig;
            }

            private static bool TryFindDeclaringPropertyOnType(TypeDef? declaringType, MethodDef accessorMethod, out PropertyDef? property)
            {
                if (declaringType is not null)
                {
                    foreach (var candidateProperty in declaringType.Properties)
                    {
                        if (AccessorMatches(candidateProperty.GetMethod, accessorMethod) ||
                            AccessorMatches(candidateProperty.SetMethod, accessorMethod))
                        {
                            property = candidateProperty;
                            return true;
                        }
                    }
                }

                property = null;
                return false;
            }

            private static bool IsDuckTypeInfrastructureInterface(TypeDef interfaceType)
                => string.Equals(interfaceType.FullName, "Datadog.Trace.DuckTyping.IDuckType", StringComparison.Ordinal);

            private static IReadOnlyList<MethodDef> BuildClassMethods(TypeDef proxyClassType)
            {
                var results = new List<MethodDef>();
                var visitedMethodKeys = new HashSet<string>(StringComparer.Ordinal);
                var current = proxyClassType;

                while (current is not null)
                {
                    foreach (var method in current.Methods)
                    {
                        if (!IsSupportedClassProxyMethod(method))
                        {
                            continue;
                        }

                        var key = $"{method.Name}::{method.MethodSig}";
                        if (visitedMethodKeys.Add(key))
                        {
                            results.Add(method);
                        }
                    }

                    current = current.BaseType?.ResolveTypeDef();
                }

                return results;
            }

            private static MethodDef? BuildSupportedBaseConstructor(TypeDef proxyType)
            {
                foreach (var constructor in proxyType.Methods)
                {
                    if (!constructor.IsConstructor || constructor.IsStatic || constructor.MethodSig.Params.Count != 0)
                    {
                        continue;
                    }

                    return constructor;
                }

                return null;
            }

            private static IReadOnlyDictionary<MethodDef, PropertyDef> BuildAccessorPropertyMap(TypeDef proxyType)
            {
                var propertiesByAccessorMethod = new Dictionary<MethodDef, PropertyDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
                var typesToInspect = new Stack<TypeDef>();
                typesToInspect.Push(proxyType);

                while (typesToInspect.Count > 0)
                {
                    var currentType = typesToInspect.Pop();
                    if (!visitedTypes.Add(currentType.FullName))
                    {
                        continue;
                    }

                    foreach (var property in currentType.Properties)
                    {
                        if (property.GetMethod is not null && !propertiesByAccessorMethod.ContainsKey(property.GetMethod))
                        {
                            propertiesByAccessorMethod[property.GetMethod] = property;
                        }

                        if (property.SetMethod is not null && !propertiesByAccessorMethod.ContainsKey(property.SetMethod))
                        {
                            propertiesByAccessorMethod[property.SetMethod] = property;
                        }
                    }

                    var baseType = currentType.BaseType?.ResolveTypeDef();
                    if (baseType is not null)
                    {
                        typesToInspect.Push(baseType);
                    }

                    foreach (var interfaceImpl in currentType.Interfaces)
                    {
                        var resolvedInterface = ResolveInterfaceTypeDefForTraversal(currentType, interfaceImpl.Interface);
                        if (resolvedInterface is not null)
                        {
                            typesToInspect.Push(resolvedInterface);
                        }
                    }
                }

                return propertiesByAccessorMethod;
            }
        }

        private sealed class ProxyMethodPlan
        {
            internal ProxyMethodPlan(MethodDef proxyMethod)
            {
                Method = proxyMethod;
                IdentityKey = BuildMethodIdentityKey(proxyMethod);
                DeclaringProperty = FindPropertyFromAccessor(proxyMethod);
                FieldResolutionMode = GetFieldResolutionMode(proxyMethod);
                AllowPrivateBaseMembers = IsFallbackToBaseTypesEnabled(proxyMethod);
                AllowPrivateBaseMethodCandidates = AllowPrivateBaseMembers && IsPropertyAccessorMethod(proxyMethod);
                DuckBindingFlags = GetDuckBindingFlags(proxyMethod);
                DuckBindingFlagsCacheKey = ((int)DuckBindingFlags).ToString(CultureInfo.InvariantCulture);
                UseIgnoreCaseMemberMatching = (DuckBindingFlags & BindingFlags.IgnoreCase) != 0;
                ForwardTargetMethodNames = GetForwardTargetMethodNames(proxyMethod);
                ForwardTargetMethodNamesCacheKey = string.Join("|", ForwardTargetMethodNames);
                ForwardTargetFieldNames = GetForwardTargetFieldNames(proxyMethod);
                HasFieldAccessorKind = TryGetFieldAccessorKind(proxyMethod, out var accessorKind);
                FieldAccessorKind = accessorKind;
                ReverseUsageFailureDetail = TryGetForwardReverseUsageFailure(proxyMethod, out var reverseUsageFailureDetail)
                                                ? reverseUsageFailureDetail
                                                : null;
                HasExplicitInterfaceTypeNames = TryGetForwardExplicitInterfaceTypeNames(proxyMethod, out var explicitInterfaceTypeNames, out var useRelaxedNameComparison);
                ExplicitInterfaceTypeNames = explicitInterfaceTypeNames;
                ExplicitInterfaceTypeNamesCacheKey = string.Join("|", ExplicitInterfaceTypeNames);
                UseRelaxedNameComparison = useRelaxedNameComparison;
                HasConfiguredParameterTypeNames = TryGetForwardParameterTypeNames(proxyMethod, out var configuredParameterTypeNames);
                ConfiguredParameterTypeNames = configuredParameterTypeNames;
                ConfiguredParameterTypeNamesCacheKey = string.Join("|", ConfiguredParameterTypeNames);
                HasDuckGenericParameterTypeNames = TryGetDuckGenericParameterTypeNames(proxyMethod, out var genericParameterTypeNames);
                DuckGenericParameterTypeNames = genericParameterTypeNames;
                SetterTargetPropertyNames = BuildSetterTargetPropertyNames(ForwardTargetMethodNames);
                ParameterDirections = BuildParameterDirections(proxyMethod);
                ParameterCountCacheKey = proxyMethod.MethodSig.Params.Count.ToString(CultureInfo.InvariantCulture);
            }

            internal MethodDef Method { get; }

            internal string IdentityKey { get; }

            internal PropertyDef? DeclaringProperty { get; }

            internal FieldResolutionMode FieldResolutionMode { get; }

            internal bool AllowPrivateBaseMembers { get; }

            internal bool AllowPrivateBaseMethodCandidates { get; }

            internal bool UseIgnoreCaseMemberMatching { get; }

            internal BindingFlags DuckBindingFlags { get; }

            internal string DuckBindingFlagsCacheKey { get; }

            internal IReadOnlyList<string> ForwardTargetMethodNames { get; }

            internal string ForwardTargetMethodNamesCacheKey { get; }

            internal IReadOnlyList<string> ForwardTargetFieldNames { get; }

            internal bool HasFieldAccessorKind { get; }

            internal FieldAccessorKind FieldAccessorKind { get; }

            internal string? ReverseUsageFailureDetail { get; }

            internal bool HasExplicitInterfaceTypeNames { get; }

            internal IReadOnlyList<string> ExplicitInterfaceTypeNames { get; }

            internal string ExplicitInterfaceTypeNamesCacheKey { get; }

            internal bool UseRelaxedNameComparison { get; }

            internal bool HasConfiguredParameterTypeNames { get; }

            internal IReadOnlyList<string> ConfiguredParameterTypeNames { get; }

            internal string ConfiguredParameterTypeNamesCacheKey { get; }

            internal bool HasDuckGenericParameterTypeNames { get; }

            internal IReadOnlyList<string> DuckGenericParameterTypeNames { get; }

            internal IReadOnlyList<string> SetterTargetPropertyNames { get; }

            internal IReadOnlyList<ParameterDirection> ParameterDirections { get; }

            internal string ParameterCountCacheKey { get; }

            internal bool TryGetParameterDirection(int parameterIndex, out ParameterDirection direction)
            {
                if (parameterIndex < 0 || parameterIndex >= ParameterDirections.Count)
                {
                    direction = default;
                    return false;
                }

                direction = ParameterDirections[parameterIndex];
                return true;
            }

            private static IReadOnlyList<ParameterDirection> BuildParameterDirections(MethodDef method)
            {
                if (method.MethodSig.Params.Count == 0)
                {
                    return Array.Empty<ParameterDirection>();
                }

                var directions = new ParameterDirection[method.MethodSig.Params.Count];
                for (var parameterIndex = 0; parameterIndex < directions.Length; parameterIndex++)
                {
                    _ = TryGetMethodParameterDirection(method, parameterIndex, out directions[parameterIndex]);
                }

                return directions;
            }

            private static IReadOnlyList<string> BuildSetterTargetPropertyNames(IReadOnlyList<string> forwardTargetMethodNames)
            {
                if (forwardTargetMethodNames.Count == 0)
                {
                    return Array.Empty<string>();
                }

                var propertyNames = new List<string>(forwardTargetMethodNames.Count);
                var seenPropertyNames = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < forwardTargetMethodNames.Count; i++)
                {
                    var propertyName = ExtractSetterPropertyName(forwardTargetMethodNames[i]);
                    if (string.IsNullOrWhiteSpace(propertyName))
                    {
                        continue;
                    }

                    var nonNullPropertyName = propertyName!;
                    if (!seenPropertyNames.Add(nonNullPropertyName))
                    {
                        continue;
                    }

                    propertyNames.Add(nonNullPropertyName);
                }

                return propertyNames;
            }
        }

        private sealed class MethodPlan
        {
            internal MethodPlan(MethodDef method)
            {
                Method = method;
                IdentityKey = BuildMethodIdentityKey(method);
                ParameterDirections = BuildParameterDirections(method);
            }

            internal MethodDef Method { get; }

            internal string IdentityKey { get; }

            internal IReadOnlyList<ParameterDirection> ParameterDirections { get; }

            internal bool TryGetParameterDirection(int parameterIndex, out ParameterDirection direction)
            {
                if (parameterIndex < 0 || parameterIndex >= ParameterDirections.Count)
                {
                    direction = default;
                    return false;
                }

                direction = ParameterDirections[parameterIndex];
                return true;
            }

            private static IReadOnlyList<ParameterDirection> BuildParameterDirections(MethodDef method)
            {
                if (method.MethodSig.Params.Count == 0)
                {
                    return Array.Empty<ParameterDirection>();
                }

                var directions = new ParameterDirection[method.MethodSig.Params.Count];
                for (var parameterIndex = 0; parameterIndex < directions.Length; parameterIndex++)
                {
                    _ = TryGetMethodParameterDirection(method, parameterIndex, out directions[parameterIndex]);
                }

                return directions;
            }
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

            internal double BuildRuntimeTypeIndexSeconds { get; set; }

            internal int RuntimeTypeCacheHits { get; set; }

            internal int RuntimeTypeCacheMisses { get; set; }

            internal int RuntimeTypeIndexHits { get; set; }

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
