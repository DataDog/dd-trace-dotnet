// <copyright file="DuckTypeAotRegistryAssemblyEmitter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.Loader;
#endif
using System.Threading.Tasks;
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
    internal static partial class DuckTypeAotRegistryAssemblyEmitter
    {
        /// <summary>
        /// Emits the AOT registry assembly and metadata artifacts.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <param name="artifactPaths">The artifact paths value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <returns>The result produced by this operation.</returns>
        internal static DuckTypeAotRegistryEmissionResult Emit(
            DuckTypeAotGenerateOptions options,
            DuckTypeAotArtifactPaths artifactPaths,
            DuckTypeAotMappingResolutionResult mappingResolutionResult)
        {
            // The mappings of open generic target types are emitted as generic proxies (see PrepareGenericProxyTemplates).
            var templatedResolution = PrepareGenericProxyTemplates(mappingResolutionResult, out var templates);
            genericProxyTemplates = templates;
            try
            {
                return RestoreGenericProxyTemplateMappings(EmitAndValidate(options, artifactPaths, templatedResolution), templates);
            }
            finally
            {
                genericProxyTemplates = null;
            }
        }

        private static DuckTypeAotRegistryEmissionResult EmitAndValidate(
            DuckTypeAotGenerateOptions options,
            DuckTypeAotArtifactPaths artifactPaths,
            DuckTypeAotMappingResolutionResult mappingResolutionResult)
        {
            var unloadableProxyTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            var dynamicOracleCache = new DynamicOracleCache();
            DuckTypeAotRegistryEmissionResult emissionResult;
            List<string> validationWarnings;

            // A generated proxy type the runtime can't load is a proxy dynamic duck typing fails to create: when the generator can
            // load the registry, it checks them, and emits it again with the failure of those instead, until nothing else fails
            // or MaxEmissionPasses times (a warning reports the ones still left, whose registrations fail at startup).
            // (A registration referencing a type the application's runtime can't load fails on its own, see
            // EmitBootstrapRegistrationChunks.)
            for (var pass = 1; ; pass++)
            {
                emissionResult = EmitPass(options, artifactPaths, mappingResolutionResult, unloadableProxyTypes, dynamicOracleCache, out var generatedProxyTypes);
                validationWarnings = [];
                var validation = ValidateGeneratedRegistry(artifactPaths.OutputAssemblyPath, generatedProxyTypes, mappingResolutionResult, validationWarnings);
                var newFailures = 0;
                foreach (var unloadableProxyType in validation.UnloadableProxyTypes)
                {
                    if (!unloadableProxyTypes.ContainsKey(unloadableProxyType.Key))
                    {
                        unloadableProxyTypes[unloadableProxyType.Key] = unloadableProxyType.Value;
                        newFailures++;
                    }
                }

                if (newFailures == 0)
                {
                    break;
                }

                if (pass == MaxEmissionPasses)
                {
                    validationWarnings.Add($"The generated registry still had proxy types the runtime can't load after {pass} emissions: their registrations fail.");
                    break;
                }
            }

            return validationWarnings.Count == 0
                       ? emissionResult
                       : new DuckTypeAotRegistryEmissionResult(
                           emissionResult.RegistryAssemblyInfo,
                           emissionResult.MappingResultsByKey,
                           emissionResult.RuntimeRegistrations,
                           emissionResult.Warnings.Concat(validationWarnings).ToArray());
        }

        /// <summary>
        /// Loads the generated registry and finds the generated proxy types the runtime can't load (a sealed base type, a member
        /// left without implementation...), with the load failure, by registration key. Nothing of the registry runs (its module
        /// initializer would enable AOT mode in the generator). What the generator can't load (e.g. target assemblies that need a
        /// newer runtime) isn't checked, with a warning.
        /// </summary>
        /// <param name="registryPath">The generated registry path.</param>
        /// <param name="generatedProxyTypes">The generated proxy type names, by registration key.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="warnings">The warnings about what couldn't be checked.</param>
        /// <returns>What the runtime can't load.</returns>
        private static RegistryValidation ValidateGeneratedRegistry(
            string registryPath,
            IReadOnlyList<KeyValuePair<string, string>> generatedProxyTypes,
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            ICollection<string> warnings)
        {
            var validation = new RegistryValidation();
            if (generatedProxyTypes.Count == 0)
            {
                return validation;
            }

            var assemblyPathsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assemblyPath in mappingResolutionResult.ProxyAssemblyPathsByName.Concat(mappingResolutionResult.TargetAssemblyPathsByName))
            {
                assemblyPathsByName[assemblyPath.Key] = assemblyPath.Value;
            }

#if NETCOREAPP3_0_OR_GREATER
            // Datadog.Trace, and the assemblies the generator already loaded, come from the default context.
            var loadContext = new AssemblyLoadContext("ducktype-aot-registry-validation", isCollectible: true);
            loadContext.Resolving += (context, assemblyName) =>
                assemblyName.Name is { } name &&
                !string.Equals(name, DatadogTraceAssemblyName, StringComparison.OrdinalIgnoreCase) &&
                assemblyPathsByName.TryGetValue(name, out var path)
                    ? context.LoadFromAssemblyPath(path)
                    : null;
#else
            // Without collectible load contexts, the registry is loaded in the default context, where it stays.
            ResolveEventHandler resolveInputAssembly = (_, args) =>
                new AssemblyName(args.Name).Name is { } name && assemblyPathsByName.TryGetValue(name, out var path) ? Assembly.LoadFrom(path) : null;
            AppDomain.CurrentDomain.AssemblyResolve += resolveInputAssembly;
#endif
            try
            {
                Assembly registry;
#if NETCOREAPP3_0_OR_GREATER
                using (var registryStream = File.OpenRead(registryPath))
                {
                    registry = loadContext.LoadFromStream(registryStream);
                }
#else
                registry = Assembly.Load(File.ReadAllBytes(registryPath));
#endif

                var uncheckedTypes = 0;
                Exception? uncheckedReason = null;
                foreach (var generatedProxyType in generatedProxyTypes)
                {
                    try
                    {
                        _ = registry.GetType(generatedProxyType.Value, throwOnError: true);
                    }
                    catch (TypeLoadException ex) when (string.Equals(ex.TypeName, generatedProxyType.Value, StringComparison.Ordinal) ||
                                                       (StringUtil.IsNullOrEmpty(ex.TypeName) && ex.Message.IndexOf(generatedProxyType.Value, StringComparison.Ordinal) >= 0))
                    {
                        // Some failures (e.g. a covariant return type the runtime rejects) only name the type in their message.
                        validation.UnloadableProxyTypes[generatedProxyType.Key] = ex.Message;
                    }
                    catch (Exception ex)
                    {
                        // Another type (a dependency the generator can't load) fails: the generated type can't be checked here.
                        uncheckedTypes++;
                        uncheckedReason ??= ex;
                    }
                }

                if (uncheckedTypes > 0)
                {
                    warnings.Add($"{uncheckedTypes} generated proxy types couldn't be checked to load, because the generator can't load what they use ({uncheckedReason!.GetType().Name}: {uncheckedReason.Message}). A proxy type that doesn't load makes the whole registry fail to load: run the generator on a runtime that can load the application's assemblies.");
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"The generated registry couldn't be loaded to check that its proxy types load ({ex.GetType().Name}: {ex.Message}). A proxy type that doesn't load makes the whole registry fail to load: run the generator on a runtime that can load the application's assemblies.");
            }
            finally
            {
#if NETCOREAPP3_0_OR_GREATER
                loadContext.Unload();
#else
                AppDomain.CurrentDomain.AssemblyResolve -= resolveInputAssembly;
#endif
            }

            return validation;
        }

        /// <summary>
        /// Emits the AOT registry assembly once.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <param name="artifactPaths">The artifact paths value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="unloadableProxyTypes">The registrations whose proxy type a previous pass generated but can't be loaded.</param>
        /// <param name="dynamicOracleCache">The answers of dynamic duck typing in the generator, kept across the passes.</param>
        /// <param name="generatedProxyTypes">The generated proxy type names, by registration key.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotRegistryEmissionResult EmitPass(
            DuckTypeAotGenerateOptions options,
            DuckTypeAotArtifactPaths artifactPaths,
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            IReadOnlyDictionary<string, string>? unloadableProxyTypes,
            DynamicOracleCache dynamicOracleCache,
            out IReadOnlyList<KeyValuePair<string, string>> generatedProxyTypes)
        {
            _currentProfile = DuckTypeAotProfiling.IsEnabled() ? new EmitterProfile() : null;
            var generatedAssemblyName = options.AssemblyName ?? Path.GetFileNameWithoutExtension(artifactPaths.OutputAssemblyPath);
            var generatedAssemblyVersion = new Version(1, 0, 0, 0);
            var generatedAssemblyFullName = new AssemblyName(generatedAssemblyName) { Version = generatedAssemblyVersion }.FullName ?? generatedAssemblyName;
            var datadogTraceAssemblyPath = mappingResolutionResult.GetDatadogTraceAssemblyPath(out var usesGeneratorDatadogTrace);
            var deterministicMvid = ComputeDeterministicMvid(generatedAssemblyName, Path.GetFileName(artifactPaths.OutputAssemblyPath), options.StrongNameKeyFile, mappingResolutionResult, datadogTraceAssemblyPath);
            var generatedCorLibAssemblyRef = ResolveGeneratedCorLibAssemblyRef(mappingResolutionResult, datadogTraceAssemblyPath);

            var assemblyDef = new AssemblyDefUser(generatedAssemblyName, generatedAssemblyVersion);
            var moduleDef = new ModuleDefUser(Path.GetFileName(artifactPaths.OutputAssemblyPath), deterministicMvid, generatedCorLibAssemblyRef)
            {
                Kind = ModuleKind.Dll
            };
            assemblyDef.Modules.Add(moduleDef);

            // The registry references the Datadog.Trace its contract is validated against, even when another copy is a proxy
            // or target assembly, and even for the members imported from the generator's own copy.
            var assemblyReferences = new Dictionary<string, AssemblyRef>(StringComparer.OrdinalIgnoreCase);
            var datadogTraceAssemblyRef = AddAssemblyReference(moduleDef, assemblyReferences, datadogTraceAssemblyPath);
            runtimeImporter = new Importer(moduleDef, default(ImporterOptions), default(GenericParamContext), new DatadogTraceImportMapper(moduleDef, datadogTraceAssemblyRef));
            var importedMembers = new ImportedMembers(moduleDef);
            var mappingResults = new Dictionary<string, DuckTypeAotMappingEmissionResult>(StringComparer.Ordinal);
            var emissionWarnings = new List<string>();
            var bindsAnotherDatadogTraceBuild = !usesGeneratorDatadogTrace &&
                                                !string.Equals(ResolveAssemblyMvid(datadogTraceAssemblyPath), typeof(DuckType).Module.ModuleVersionId.ToString("D"), StringComparison.OrdinalIgnoreCase);
            if (usesGeneratorDatadogTrace)
            {
                emissionWarnings.Add($"No Datadog.Trace.dll was passed as a target or proxy assembly: the registry is bound to the generator's own copy ('{datadogTraceAssemblyPath}'), and its startup validation fails with any other build of Datadog.Trace.");
            }
            else if (bindsAnotherDatadogTraceBuild)
            {
                // The generator can't load a second build of Datadog.Trace, so it can't ask the dynamic engine which members the
                // proxies declared with that build bind to.
                emissionWarnings.Add($"The application's Datadog.Trace.dll ('{datadogTraceAssemblyPath}') is another build than the generator's: member selection for proxies that use its types falls back to metadata, which may differ from dynamic duck typing. Use the generator of the same Datadog.Trace build.");
            }

            if (mappingResolutionResult.ProxyAssemblyPathsByName.TryGetValue(DatadogTraceAssemblyName, out var proxyDatadogTracePath) &&
                mappingResolutionResult.TargetAssemblyPathsByName.TryGetValue(DatadogTraceAssemblyName, out var targetDatadogTracePath) &&
                !string.Equals(ResolveAssemblyMvid(proxyDatadogTracePath), ResolveAssemblyMvid(targetDatadogTracePath), StringComparison.OrdinalIgnoreCase))
            {
                emissionWarnings.Add($"The proxy ('{proxyDatadogTracePath}') and target ('{targetDatadogTracePath}') copies of Datadog.Trace.dll are different builds: the registry is bound to '{datadogTraceAssemblyPath}'.");
            }

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

            // Initialize runs once: from the module initializer, and again when the application calls it explicitly. The flag
            // is only set once it succeeded, so a failed initialization fails the same way when called again.
            var initializedField = new FieldDefUser("_initialized", new FieldSig(moduleDef.CorLibTypes.Boolean), FieldAttributes.Private | FieldAttributes.Static);
            bootstrapType.Fields.Add(initializedField);
            var initializeReturn = OpCodes.Ret.ToInstruction();
            initializeMethod.Body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(initializedField));
            initializeMethod.Body.Instructions.Add(OpCodes.Brtrue.ToInstruction(initializeReturn));

            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.EnableAotModeMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(AotContractSchemaVersion));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(datadogTraceAssemblyVersion));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(datadogTraceAssemblyMvid));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(generatedAssemblyFullName));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(deterministicMvid.ToString("D")));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.ValidateAotRegistryContractMethod));

            var moduleContext = CreateModuleLoadContext(mappingResolutionResult.ProxyAssemblyPathsByName, mappingResolutionResult.TargetAssemblyPathsByName);

            // The types the registry references resolve to the loaded inputs, e.g. the base types of the reverse proxy types it
            // generates, which forward proxies of reverse proxies bind (see EmitGeneratedProxyTargetAliases).
            moduleDef.Context = moduleContext;

            var phaseStopwatch = StartProfilePhase();
            var proxyModulesByAssemblyName = LoadModules(mappingResolutionResult.ProxyAssemblyPathsByName, moduleContext);
            StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.LoadProxyModulesSeconds += seconds);

            phaseStopwatch = StartProfilePhase();
            var targetModulesByAssemblyName = LoadModules(mappingResolutionResult.TargetAssemblyPathsByName, moduleContext);
            StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.LoadTargetModulesSeconds += seconds);

            var bootstrapRegistrationMethods = new List<MethodDef>();
            IReadOnlyCollection<string> requiredAccessCheckAssemblyNames = Array.Empty<string>();
            var runtimeRegistrations = new List<DuckTypeAotRuntimeRegistration>();
            generatedProxyTypes = Array.Empty<KeyValuePair<string, string>>();
            try
            {
                // Per-emission state, cleared in the finally block.
                unloadableGeneratedProxyTypes = unloadableProxyTypes;
                phaseStopwatch = StartProfilePhase();
                runtimeTypeResolutionAssemblyPathsByName = BuildRuntimeTypeResolutionAssemblyPathMap(
                    mappingResolutionResult.ProxyAssemblyPathsByName,
                    mappingResolutionResult.TargetAssemblyPathsByName);
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.BuildRuntimeTypeResolutionMapSeconds += seconds);
                phaseStopwatch = StartProfilePhase();
                var queriedTargetTypeKeys = BuildQueriedTargetTypeKeys(mappingResolutionResult.Mappings);
                var targetTypeIndex = BuildTargetTypeIndex(targetModulesByAssemblyName, queriedTargetTypeKeys, mappingResolutionResult.ApplicationAssemblyNames);
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.BuildTargetTypeIndexSeconds += seconds);
                phaseStopwatch = StartProfilePhase();
                _currentExecutionContext = new EmitterExecutionContext(targetTypeIndex, dynamicOracleCache);
                var runtimeDuplicateKeys = new Dictionary<string, string>(StringComparer.Ordinal);
                runtimeRegistrations.AddRange(BuildRuntimeRegistrations(
                    mappingResolutionResult.Mappings,
                    mappingResolutionResult.GenericTypeRoots,
                    targetTypeIndex,
                    runtimeDuplicateKeys));
                // A generic proxy serves the runtime types of the instantiations itself (see CompleteGenericProxyTemplate).
                runtimeRegistrations.RemoveAll(registration => !registration.IsCanonical && genericProxyTemplates?.ContainsKey(registration.CanonicalMappingKey) == true);
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.BuildRuntimeRegistrationsSeconds += seconds);
                var generatedProxyTargets = new List<KeyValuePair<DuckTypeAotMapping, TypeDef>>();
                var generatedTypes = new GeneratedTypeIndex(moduleDef);
                var registrationResults = new Dictionary<string, DuckTypeAotMappingEmissionResult>(StringComparer.Ordinal);

                phaseStopwatch = StartProfilePhase();
                for (var i = 0; i < runtimeRegistrations.Count; i++)
                {
                    var registrationMethod = GetBootstrapRegistrationMethod(moduleDef, bootstrapType, bootstrapRegistrationMethods, i);

                    var runtimeRegistration = runtimeRegistrations[i];
                    var mapping = runtimeRegistration.Mapping;
                    var emitMappingStopwatch = StartProfilePhase();
                    DuckTypeAotMapping? genericProxyMapping = null;
                    _ = genericProxyTemplates?.TryGetValue(mapping.Key, out genericProxyMapping);
                    var typeCountBefore = moduleDef.Types.Count;
                    var bootstrapMethodCountBefore = bootstrapType.Methods.Count;
                    // Alias targets (types assignable to a mapped target, the underlying type of a mapped Nullable<T>) get
                    // their own proxy, like dynamic duck typing creates one per runtime type: it reports its own
                    // IDuckType.Type and ProxyType, binds the alias's own members and replays the alias's own failures.
                    var emissionResult = EmitMapping(
                        moduleDef,
                        bootstrapType,
                        registrationMethod,
                        importedMembers,
                        mapping,
                        i + 1,
                        proxyModulesByAssemblyName,
                        targetModulesByAssemblyName,
                        mappingResolutionResult.ProxyAssemblyPathsByName,
                        mappingResolutionResult.TargetAssemblyPathsByName,
                        emissionWarnings);
                    if (genericProxyMapping is not null)
                    {
                        emissionResult = CompleteGenericProxyTemplate(
                            moduleDef,
                            bootstrapType,
                            registrationMethod,
                            importedMembers,
                            genericProxyMapping,
                            i + 1,
                            emissionResult,
                            typeCountBefore,
                            bootstrapMethodCountBefore,
                            proxyModulesByAssemblyName,
                            targetModulesByAssemblyName);
                    }

                    if (_currentExecutionContext.ReplaysDynamicFailure(mapping.Key) &&
                        !string.Equals(emissionResult.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal))
                    {
                        emissionResult = emissionResult.WithDynamicFailureReplayed();
                    }

                    if (IsCheckedAgainstMetadataOnly(mapping.Key))
                    {
                        emissionResult = emissionResult.WithCheckedAgainstMetadataOnly();
                    }

                    // The application's runtime may not have the non-public types and members of the generator's core library: a
                    // mapping it declares that uses them (not the aliases of the non-public types, isolated and served otherwise).
                    if (runtimeRegistration.IsCanonical &&
                        string.Equals(emissionResult.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal) &&
                        _currentExecutionContext.RuntimeSpecificMappings.TryGetValue(mapping.Key, out var runtimeSpecificReason))
                    {
                        emissionResult = emissionResult.WithRuntimeSpecific();
                        emissionWarnings.Add($"{StatusCodeRuntimeSpecific}: Mapping '{mapping.Key}' uses {runtimeSpecificReason} of the core library, which isn't public: other runtimes (e.g. NativeAOT) may not have it, and then its registration or the call fails. Map a public type of the core library, with public members.");
                    }

                    StopProfilePhase(
                        emitMappingStopwatch,
                        seconds =>
                        {
                            _currentProfile!.EmitMappingSeconds += seconds;
                            _currentProfile!.EmitMappingCount++;
                        });
                    registrationResults[mapping.Key] = emissionResult;
                    if (runtimeRegistration.IsCanonical)
                    {
                        mappingResults[mapping.Key] = emissionResult;
                    }

                    // The proxy types the registry generates, whose instances dynamic duck typing creates forward proxies for: a
                    // reverse proxy type, and a forward proxy type (a proxy of a proxy), see EmitGeneratedProxyTargetAliases.
                    if (genericProxyMapping is null &&
                        emissionResult.Status == DuckTypeAotCompatibilityStatuses.Compatible &&
                        generatedTypes.Find(emissionResult.GeneratedProxyTypeName) is { } generatedProxyType &&
                        (mapping.Mode == DuckTypeAotMappingMode.Reverse || generatedProxyType.Interfaces.Any(implementation => string.Equals(implementation.Interface?.FullName, "Datadog.Trace.DuckTyping.IDuckType", StringComparison.Ordinal))))
                    {
                        generatedProxyTargets.Add(new KeyValuePair<DuckTypeAotMapping, TypeDef>(mapping, generatedProxyType));
                    }
                }

                var aliasFailures = new List<KeyValuePair<string, DuckTypeAotMappingEmissionResult>>();
                foreach (var runtimeRegistration in runtimeRegistrations)
                {
                    // A runtime type of the target checked against metadata only: so is the mapping.
                    if (!runtimeRegistration.IsCanonical &&
                        registrationResults.TryGetValue(runtimeRegistration.Mapping.Key, out var checkedAlias) &&
                        checkedAlias.CheckedAgainstMetadataOnly &&
                        mappingResults.TryGetValue(runtimeRegistration.CanonicalMappingKey, out var canonicalOfAlias) &&
                        !canonicalOfAlias.CheckedAgainstMetadataOnly)
                    {
                        mappingResults[runtimeRegistration.CanonicalMappingKey] = canonicalOfAlias.WithCheckedAgainstMetadataOnly();
                    }

                    if (!runtimeRegistration.IsCanonical &&
                        registrationResults.TryGetValue(runtimeRegistration.Mapping.Key, out var aliasResult) &&
                        !aliasResult.BehavesLikeDynamicDuckTyping)
                    {
                        var aliasFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                            aliasResult.Mapping,
                            aliasResult.Status,
                            aliasResult.DiagnosticCode ?? string.Empty,
                            $"The registry can't create the proxy of '{runtimeRegistration.Mapping.TargetTypeName}', a runtime type of the mapped target, like dynamic duck typing does: {aliasResult.Detail}");
                        aliasFailures.Add(new KeyValuePair<string, DuckTypeAotMappingEmissionResult>(
                            runtimeRegistration.CanonicalMappingKey,
                            aliasResult.CheckedAgainstMetadataOnly ? aliasFailure.WithCheckedAgainstMetadataOnly() : aliasFailure));
                    }
                }

                aliasFailures.AddRange(EmitGeneratedProxyTargetAliases(
                    moduleDef,
                    bootstrapType,
                    bootstrapRegistrationMethods,
                    importedMembers,
                    mappingResolutionResult,
                    mappingResults,
                    generatedProxyTargets,
                    proxyModulesByAssemblyName,
                    runtimeRegistrations,
                    emissionWarnings));

                // The compatibility matrix only lists the mappings: a runtime type of a mapped target (an alias) whose proxy only
                // fails in the registry makes its mapping incompatible, so the gates see it, even when the mapping itself replays a
                // failure of dynamic duck typing.
                foreach (var aliasFailure in aliasFailures)
                {
                    if (mappingResults.TryGetValue(aliasFailure.Key, out var canonicalResult) && canonicalResult.BehavesLikeDynamicDuckTyping)
                    {
                        mappingResults[aliasFailure.Key] = canonicalResult.WithOtherRuntimeTypeFailure(aliasFailure.Value);
                    }
                }

                // Another spelling of already registered runtime types reports the outcome of that registration.
                foreach (var runtimeDuplicate in runtimeDuplicateKeys)
                {
                    if (mappingResults.TryGetValue(runtimeDuplicate.Value, out var originalResult) ||
                        registrationResults.TryGetValue(runtimeDuplicate.Value, out originalResult))
                    {
                        mappingResults[runtimeDuplicate.Key] = originalResult.WithMapping(mappingResolutionResult.Mappings.First(item => item.Key == runtimeDuplicate.Key));
                    }
                }

                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.EmitLoopSeconds += seconds);
                requiredAccessCheckAssemblyNames = _currentExecutionContext.RequiredAccessCheckAssemblyNames.ToArray();
                emissionWarnings.AddRange(_currentExecutionContext.Warnings.Distinct(StringComparer.Ordinal));
                generatedProxyTypes = _currentExecutionContext.GeneratedProxyTypes.ToArray();
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
                metadataOnlyDuckAttributesByType = null;
                unloadableGeneratedProxyTypes = null;
                runtimeImporter = null;
            }

            foreach (var registrationChunk in EmitBootstrapRegistrationChunks(moduleDef, bootstrapType, bootstrapRegistrationMethods, importedMembers))
            {
                initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(registrationChunk));
            }

            initializeMethod.Body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
            initializeMethod.Body.Instructions.Add(OpCodes.Stsfld.ToInstruction(initializedField));
            initializeMethod.Body.Instructions.Add(initializeReturn);

            // NativeAOT static-link bootstrap path: the module initializer initializes the registry when dynamic code isn't
            // supported. Under the JIT, it runs as soon as a method referencing the registry is compiled (e.g. one calling
            // Initialize() only when dynamic code isn't supported): the application keeps dynamic duck typing unless it calls
            // Initialize().
            var moduleInitializer = new MethodDefUser(
                ".cctor",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Void),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            moduleInitializer.Body = new CilBody();
            var moduleInitializerReturn = OpCodes.Ret.ToInstruction();
            moduleInitializer.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.IsDynamicCodeSupportedMethod));
            moduleInitializer.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(moduleInitializerReturn));
            moduleInitializer.Body.Instructions.Add(OpCodes.Call.ToInstruction(initializeMethod));
            moduleInitializer.Body.Instructions.Add(moduleInitializerReturn);
            moduleDef.GlobalType.Methods.Add(moduleInitializer);

            AddIgnoresAccessChecksToAttributes(assemblyDef, moduleDef, importedMembers.IgnoresAccessChecksToAttributeCtor, mappingResolutionResult, requiredAccessCheckAssemblyNames);

            // A timestamp derived from the MVID instead of the current time: the same inputs produce the same registry.
            var writeOptions = new ModuleWriterOptions(moduleDef);
            writeOptions.PEHeadersOptions.TimeDateStamp = BitConverter.ToUInt32(deterministicMvid.ToByteArray(), 0) | 0x80000000;
            if (!StringUtil.IsNullOrWhiteSpace(options.StrongNameKeyFile))
            {
                writeOptions.InitializeStrongNameSigning(moduleDef, new StrongNameKey(options.StrongNameKeyFile!));
            }

            using (var registryStream = new MemoryStream())
            {
                moduleDef.Write(registryStream, writeOptions);
                if (bindsAnotherDatadogTraceBuild)
                {
                    EnsureDatadogTraceDefinesRegistryReferences(registryStream.ToArray(), datadogTraceAssemblyPath);
                }

                File.WriteAllBytes(artifactPaths.OutputAssemblyPath, registryStream.ToArray());
            }

            var registryInfo = new DuckTypeAotRegistryAssemblyInfo(
                generatedAssemblyName,
                bootstrapType.FullName,
                Path.GetFullPath(artifactPaths.OutputAssemblyPath),
                deterministicMvid);
            WriteProfileSummary(mappingResolutionResult, runtimeRegistrations.Count);
            _currentProfile = null;

            return new DuckTypeAotRegistryEmissionResult(registryInfo, mappingResults, runtimeRegistrations, emissionWarnings);
        }

        /// <summary>
        /// Fails when the application's Datadog.Trace, another build than the generator's, doesn't define a type or member of
        /// Datadog.Trace the registry uses: the registry calls the generator's API, e.g. an older Datadog.Trace misses the
        /// registration methods a newer generator emits calls to, and the registry would fail at startup.
        /// </summary>
        /// <param name="registryBytes">The registry assembly.</param>
        /// <param name="datadogTraceAssemblyPath">The application's Datadog.Trace assembly.</param>
        private static void EnsureDatadogTraceDefinesRegistryReferences(byte[] registryBytes, string datadogTraceAssemblyPath)
        {
            const SigComparerOptions MemberComparisonOptions = SigComparerOptions.DontCompareTypeScope | SigComparerOptions.PrivateScopeIsComparable;
            using var registryModule = ModuleDefMD.Load(registryBytes);
            using var datadogTraceModule = ModuleDefMD.Load(datadogTraceAssemblyPath);
            var missingReferences = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var typeRef in registryModule.GetTypeRefs())
            {
                if (IsDatadogTraceTypeRef(typeRef) && datadogTraceModule.Find(typeRef.FullName, isReflectionName: false) is null)
                {
                    missingReferences.Add(typeRef.FullName);
                }
            }

            var memberRefCount = registryModule.TablesStream.MemberRefTable.Rows;
            for (uint rid = 1; rid <= memberRefCount; rid++)
            {
                var memberRef = registryModule.ResolveMemberRef(rid);
                // A member of an array (or pointer) of a Datadog.Trace type is the runtime's, not the element type's.
                if (memberRef?.DeclaringType is TypeSpec { TypeSig: ArraySigBase or PtrSig or ByRefSig } ||
                    memberRef?.DeclaringType?.ScopeType is not TypeRef declaringTypeRef ||
                    !IsDatadogTraceTypeRef(declaringTypeRef) ||
                    datadogTraceModule.Find(declaringTypeRef.FullName, isReflectionName: false) is not { } declaringType)
                {
                    continue;
                }

                var isDefined = false;
                for (var type = declaringType; type is not null && !isDefined; type = type.BaseType?.ScopeType as TypeDef)
                {
                    isDefined = memberRef.IsMethodRef
                                    ? type.FindMethod(memberRef.Name, memberRef.MethodSig, MemberComparisonOptions) is not null
                                    : type.FindField(memberRef.Name, memberRef.FieldSig, MemberComparisonOptions) is not null;
                }

                if (!isDefined)
                {
                    missingReferences.Add(memberRef.FullName);
                }
            }

            if (missingReferences.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The application's Datadog.Trace.dll ('{datadogTraceAssemblyPath}') doesn't define {missingReferences.Count} types or members the registry uses, so the registry would fail at startup: {string.Join(", ", missingReferences)}. Use the generator of the application's Datadog.Trace version.");
            }

            static bool IsDatadogTraceTypeRef(TypeRef typeRef)
                => string.Equals(typeRef.DefinitionAssembly?.Name.String, DatadogTraceAssemblyName, StringComparison.OrdinalIgnoreCase);
        }

        private static ISet<string> BuildQueriedTargetTypeKeys(IReadOnlyList<DuckTypeAotMapping> mappings)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in mappings)
            {
                if (!DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.TargetTypeName) &&
                    !StringUtil.IsNullOrWhiteSpace(mapping.TargetAssemblyName) &&
                    !StringUtil.IsNullOrWhiteSpace(mapping.TargetTypeName))
                {
                    _ = keys.Add(BuildAssemblyTypeCacheKey(mapping.TargetAssemblyName, mapping.TargetTypeName));
                }
            }

            return keys;
        }

        /// <summary>
        /// Emits the bounded bootstrap chunks calling the registration methods, each under a handler for the failures to load
        /// the types it references: a type the application's runtime can't load (e.g. a type of an assembly the application
        /// doesn't ship, or a type a NativeAOT application's core library doesn't have, for which the NativeAOT compiler makes the
        /// method throw) can't have instances, and fails its own registration only, not the whole registry.
        /// </summary>
        /// <param name="module">The registry module.</param>
        /// <param name="bootstrap">The bootstrap type.</param>
        /// <param name="registrationMethods">The registration methods.</param>
        /// <returns>The chunk methods, in order.</returns>
        private static IReadOnlyList<MethodDef> EmitBootstrapRegistrationChunks(ModuleDef module, TypeDef bootstrap, IReadOnlyList<MethodDef> registrationMethods, ImportedMembers importedMembers)
        {
            Type[] typeLoadFailureRuntimeTypes = [typeof(TypeLoadException), typeof(FileNotFoundException), typeof(FileLoadException), typeof(BadImageFormatException)];
            var typeLoadFailureTypes = typeLoadFailureRuntimeTypes.Select(type => RuntimeImporter(module).Import(type)).ToArray();
            var recordRegistrationFailureMethod = importedMembers.RecordRegistrationFailureMethod;
            List<MethodDef> chunks = [];
            MethodDef? chunk = null;
            foreach (var registrationMethod in registrationMethods)
            {
                if (registrationMethod.Body.Instructions.Count == 0)
                {
                    _ = bootstrap.Methods.Remove(registrationMethod);
                    continue;
                }

                registrationMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                if (chunk is null || chunk.Body.ExceptionHandlers.Count >= BootstrapMappingsPerMethod * typeLoadFailureTypes.Length)
                {
                    chunk = new MethodDefUser(
                        $"RegisterMappingsChunk_{chunks.Count + 1:D4}",
                        MethodSig.CreateStatic(module.CorLibTypes.Void),
                        MethodImplAttributes.IL | MethodImplAttributes.Managed,
                        MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig)
                    {
                        Body = new CilBody()
                    };
                    bootstrap.Methods.Add(chunk);
                    chunks.Add(chunk);
                }

                // try { RegisterMapping_N(); } catch (TypeLoadException ex) { DuckTypeAotEngine.RecordRegistrationFailure(ex); } ...
                var instructions = chunk.Body.Instructions;
                var next = OpCodes.Nop.ToInstruction();
                var tryStart = OpCodes.Call.ToInstruction(registrationMethod);
                instructions.Add(tryStart);
                instructions.Add(OpCodes.Leave.ToInstruction(next));
                List<Instruction> handlerStarts = [];
                for (var i = 0; i < typeLoadFailureTypes.Length; i++)
                {
                    var handlerStart = OpCodes.Call.ToInstruction(recordRegistrationFailureMethod);
                    handlerStarts.Add(handlerStart);
                    instructions.Add(handlerStart);
                    instructions.Add(OpCodes.Leave.ToInstruction(next));
                }

                instructions.Add(next);
                for (var i = 0; i < typeLoadFailureTypes.Length; i++)
                {
                    chunk.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
                    {
                        TryStart = tryStart,
                        TryEnd = handlerStarts[0],
                        HandlerStart = handlerStarts[i],
                        HandlerEnd = i + 1 < handlerStarts.Count ? handlerStarts[i + 1] : next,
                        CatchType = typeLoadFailureTypes[i],
                    });
                }
            }

            foreach (var chunkMethod in chunks)
            {
                chunkMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            }

            return chunks;
        }

        /// <summary>
        /// Registers the forward mappings for the proxy types the registry generates: dynamic duck typing creates a forward proxy
        /// for the runtime type of the instance, and a proxy is an instance of its generated type, which derives from (or
        /// implements) its proxy definition type. A reverse proxy type is served by the mappings of the contract it derives
        /// from or implements (or of a type assignable from it); a forward proxy type (a proxy of a proxy) by the mappings of its
        /// proxy definition type, or of one of its base types other than object, ValueType and IDuckType, which every proxy has.
        /// </summary>
        /// <param name="module">The generated registry module.</param>
        /// <param name="bootstrap">The bootstrap type.</param>
        /// <param name="chunks">The bootstrap registration methods.</param>
        /// <param name="members">The imported members.</param>
        /// <param name="resolution">The mapping resolution result.</param>
        /// <param name="results">The emission results of the mappings, flagged when a proxy for a generated proxy type is bound
        /// from metadata.</param>
        /// <param name="generatedProxyTargets">The generated proxy types, by the mapping they were generated for.</param>
        /// <param name="proxyModules">The proxy modules by assembly name.</param>
        /// <param name="runtimeRegistrations">The runtime registrations, completed with the ones emitted here.</param>
        /// <param name="emissionWarnings">The emission warnings.</param>
        /// <returns>The failures only the registry has (dynamic duck typing creates those proxies), by canonical mapping key.</returns>
        private static IReadOnlyList<KeyValuePair<string, DuckTypeAotMappingEmissionResult>> EmitGeneratedProxyTargetAliases(
            ModuleDef module,
            TypeDef bootstrap,
            IList<MethodDef> chunks,
            ImportedMembers members,
            DuckTypeAotMappingResolutionResult resolution,
            IDictionary<string, DuckTypeAotMappingEmissionResult> results,
            IReadOnlyList<KeyValuePair<DuckTypeAotMapping, TypeDef>> generatedProxyTargets,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModules,
            IList<DuckTypeAotRuntimeRegistration> runtimeRegistrations,
            ICollection<string> emissionWarnings)
        {
            var aliasFailures = new List<KeyValuePair<string, DuckTypeAotMappingEmissionResult>>();
            var registrationIndex = runtimeRegistrations.Count;
            var context = _currentExecutionContext;

            // The reverse proxy type dynamic duck typing creates for each pair, which the generated one stands for.
            foreach (var generatedTarget in generatedProxyTargets)
            {
                if (context is not null && context.DynamicReverseProxyTypes.TryGetValue(generatedTarget.Key.Key, out var dynamicReverseProxyType))
                {
                    context.GeneratedTypeRuntimeTypes[generatedTarget.Value] = dynamicReverseProxyType;
                    context.RuntimeTypeGeneratedTypes[dynamicReverseProxyType] = generatedTarget.Value;
                }
            }

            // The forward mappings whose target a generated proxy type is assignable to, grouped by proxy type and generated proxy
            // type: one registration each. A failure dynamic duck typing has too is replayed for the generated proxy types as well.
            var groups = new Dictionary<Tuple<Type, TypeDef>, List<DuckTypeAotMapping>>();
            var orderedGroups = new List<Tuple<Type, TypeDef, KeyValuePair<DuckTypeAotMapping, TypeDef>>>();
            foreach (var mapping in resolution.Mappings.Where(mapping => mapping.Mode == DuckTypeAotMappingMode.Forward).OrderBy(mapping => mapping.Key, StringComparer.Ordinal))
            {
                if (!results.TryGetValue(mapping.Key, out var result) || !result.BehavesLikeDynamicDuckTyping)
                {
                    continue;
                }

                var proxyPath = resolution.ProxyAssemblyPathsByName[mapping.ProxyAssemblyName];
                var targetPath = resolution.TargetAssemblyPathsByName[mapping.TargetAssemblyName];
                if (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyPath, mapping.ProxyTypeName, out var proxyType) || proxyType is null ||
                    !TryResolveRuntimeType(mapping.TargetAssemblyName, targetPath, mapping.TargetTypeName, out var targetType) || targetType is null)
                {
                    if (generatedProxyTargets.Any(generatedTarget => generatedTarget.Key.Mode == DuckTypeAotMappingMode.Reverse))
                    {
                        emissionWarnings.Add($"Mapping '{mapping.Key}' gets no registration for the generated proxy types: its types couldn't be loaded.");
                    }

                    continue;
                }

                foreach (var generatedTarget in generatedProxyTargets)
                {
                    var contractPath = resolution.ProxyAssemblyPathsByName[generatedTarget.Key.ProxyAssemblyName];
                    if (!TryResolveRuntimeType(generatedTarget.Key.ProxyAssemblyName, contractPath, generatedTarget.Key.ProxyTypeName, out var contract) || contract is null)
                    {
                        continue;
                    }

                    if (generatedTarget.Key.Mode == DuckTypeAotMappingMode.Forward)
                    {
                        if (targetType.FullName is "System.Object" or "System.ValueType" or "Datadog.Trace.DuckTyping.IDuckType" ||
                            !targetType.IsAssignableFrom(contract) ||
                            !TryMapDynamicForwardProxyType(generatedTarget, contract))
                        {
                            continue;
                        }
                    }
                    else if (!targetType.IsAssignableFrom(context?.GeneratedTypeRuntimeTypes.TryGetValue(generatedTarget.Value, out var dynamicReverseProxyType) == true ? dynamicReverseProxyType : contract))
                    {
                        continue;
                    }

                    // Other spellings of the same proxy type, and other mapped targets of the generated proxy type, share its
                    // registration.
                    var groupKey = Tuple.Create(proxyType, generatedTarget.Value);
                    if (!groups.TryGetValue(groupKey, out var groupMappings))
                    {
                        groupMappings = [];
                        groups[groupKey] = groupMappings;
                        orderedGroups.Add(Tuple.Create(proxyType, generatedTarget.Value, generatedTarget));
                    }

                    groupMappings.Add(mapping);
                }
            }

            foreach (var group in orderedGroups)
            {
                var proxyType = group.Item1;
                var generatedReverseType = group.Item2;
                var reverseMapping = group.Item3.Key;
                var groupMappings = groups[Tuple.Create(proxyType, generatedReverseType)];
                var mapping = groupMappings[0];
                var aliasMapping = new DuckTypeAotMapping(
                    mapping.ProxyTypeName,
                    mapping.ProxyAssemblyName,
                    generatedReverseType.FullName,
                    module.Assembly.Name,
                    mapping.Mode,
                    mapping.Source,
                    mapping.ScenarioId);
                var registrationMethod = GetBootstrapRegistrationMethod(module, bootstrap, chunks, registrationIndex);
                runtimeRegistrations.Add(new DuckTypeAotRuntimeRegistration(aliasMapping, mapping.Key, DuckTypeAotRuntimeRegistrationKind.AssignableAlias));

                // What dynamic duck typing does with the proxy type it creates: when it can't create the proxy, the
                // registry replays its failure.
                var dynamicOutcome = GetDynamicGeneratedReverseAliasOutcome(
                    groupMappings,
                    reverseMapping,
                    aliasMapping,
                    proxyType,
                    generatedReverseType,
                    out var dynamicExceptionType,
                    out var dynamicExceptionMessage);
                if (dynamicOutcome == DynamicCreationOutcome.Failed)
                {
                    var failureTypeName = dynamicExceptionType!.FullName ?? dynamicExceptionType.Name;
                    var importedProxyType = ImportRuntimeTypeCached(module, proxyType, $"proxy type '{mapping.ProxyTypeName}'");
                    EmitFailureRegistration(
                        module,
                        bootstrap,
                        registrationMethod,
                        members,
                        mapping.Mode,
                        ++registrationIndex,
                        importedProxyType,
                        generatedReverseType,
                        failureTypeName,
                        GetFailureReplayDetail(aliasMapping, importedProxyType, generatedReverseType, dynamicExceptionMessage),
                        context?.DynamicFailureInnerExceptions.TryGetValue(aliasMapping.Key, out var innerException) == true ? innerException : null);
                    emissionWarnings.Add($"Registered AOT failure mapping '{aliasMapping.Key}' to throw '{failureTypeName}' (replayed from dynamic duck typing): {dynamicExceptionMessage}");
                    continue;
                }

                // Like the proxy dynamic duck typing creates for the runtime type of the instance, it binds the members of the
                // generated proxy type (its implementations of the contract or proxy definition, its IDuckType members, its
                // ToString...), and IDuckType.Type is the generated proxy type.
                var aliasResult = EmitGeneratedReverseAliasMapping(
                    module,
                    bootstrap,
                    registrationMethod,
                    members,
                    aliasMapping,
                    ++registrationIndex,
                    proxyModules,
                    resolution.ProxyAssemblyPathsByName,
                    generatedReverseType,
                    emissionWarnings);
                // A failure only the registry has makes the mappings incompatible. Without dynamic duck typing to ask, a proxy type
                // the runtime can't load fails in dynamic duck typing too: the registry replays it; other failures can't be told.
                var checkedAgainstMetadataOnly = dynamicOutcome == DynamicCreationOutcome.Unavailable;
                if (string.Equals(aliasResult.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal) ||
                    (checkedAgainstMetadataOnly && string.Equals(aliasResult.DiagnosticCode, StatusCodeUnloadableProxyType, StringComparison.Ordinal)))
                {
                    // The mappings serve the generated proxy type with a proxy bound from metadata: they say so.
                    if (checkedAgainstMetadataOnly || IsCheckedAgainstMetadataOnly(aliasMapping.Key))
                    {
                        foreach (var groupMapping in groupMappings)
                        {
                            if (results.TryGetValue(groupMapping.Key, out var groupResult) && !groupResult.CheckedAgainstMetadataOnly)
                            {
                                results[groupMapping.Key] = groupResult.WithCheckedAgainstMetadataOnly();
                            }
                        }
                    }

                    continue;
                }

                emissionWarnings.Add($"The registry can't create the proxy of mapping '{mapping.Key}' for the generated proxy type '{generatedReverseType.FullName}': {aliasResult.Detail}");
                var aliasFailureDetail = checkedAgainstMetadataOnly
                                             ? $"The registry can't create the proxy of the generated proxy type of '{reverseMapping.ProxyTypeName}', an instance of the mapped target, and dynamic duck typing couldn't be asked whether it can: {aliasResult.Detail}"
                                             : $"The registry can't create the proxy of the generated proxy type of '{reverseMapping.ProxyTypeName}', an instance of the mapped target, like dynamic duck typing does: {aliasResult.Detail}";
                foreach (var groupMapping in groupMappings)
                {
                    var aliasFailure = DuckTypeAotMappingEmissionResult.NotCompatible(groupMapping, aliasResult.Status, aliasResult.DiagnosticCode ?? string.Empty, aliasFailureDetail);
                    aliasFailures.Add(new KeyValuePair<string, DuckTypeAotMappingEmissionResult>(
                        groupMapping.Key,
                        checkedAgainstMetadataOnly || IsCheckedAgainstMetadataOnly(aliasMapping.Key) ? aliasFailure.WithCheckedAgainstMetadataOnly() : aliasFailure));
                }
            }

            return aliasFailures;
        }

        /// <summary>
        /// Maps a forward proxy type the registry generates to the one dynamic duck typing creates for the same pair, whose members
        /// the forward proxies of its instances bind (see TryResolveDynamicSelectionTypes and FindMemberDefInHierarchy).
        /// </summary>
        /// <param name="generatedTarget">The generated forward proxy type, by the mapping it was generated for.</param>
        /// <param name="proxyDefinitionType">The proxy definition type of the mapping.</param>
        /// <returns>true if dynamic duck typing created the proxy type of the pair; otherwise, false.</returns>
        private static bool TryMapDynamicForwardProxyType(KeyValuePair<DuckTypeAotMapping, TypeDef> generatedTarget, Type proxyDefinitionType)
        {
            if (_currentExecutionContext is not { } context)
            {
                return false;
            }

            if (context.GeneratedTypeRuntimeTypes.ContainsKey(generatedTarget.Value))
            {
                return true;
            }

            var mapping = generatedTarget.Key;
            if (runtimeTypeResolutionAssemblyPathsByName is not { } assemblyPaths ||
                !assemblyPaths.TryGetValue(DuckTypeAotNameHelpers.NormalizeAssemblyName(mapping.TargetAssemblyName), out var targetPath) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetPath, mapping.TargetTypeName, out var targetType) ||
                targetType is null)
            {
                return false;
            }

            var result = DuckType.GetOrCreateProxyType(proxyDefinitionType, targetType);
            if (!result.CanCreate() || result.ProxyType is not { } dynamicProxyType)
            {
                return false;
            }

            context.GeneratedTypeRuntimeTypes[generatedTarget.Value] = dynamicProxyType;
            context.RuntimeTypeGeneratedTypes[dynamicProxyType] = generatedTarget.Value;
            return true;
        }

        /// <summary>
        /// Emits the proxy of a forward mapping for a proxy type the registry generates, bound to the members of that type.
        /// </summary>
        /// <param name="moduleDef">The generated registry module.</param>
        /// <param name="bootstrapType">The bootstrap type.</param>
        /// <param name="registrationMethod">The bootstrap registration method.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="aliasMapping">The registration of the proxy for the generated reverse proxy type.</param>
        /// <param name="mappingIndex">The registration index.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name.</param>
        /// <param name="generatedReverseType">The generated reverse proxy type.</param>
        /// <param name="emissionWarnings">The emission warnings.</param>
        /// <returns>The emission result.</returns>
        private static DuckTypeAotMappingEmissionResult EmitGeneratedReverseAliasMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef registrationMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping aliasMapping,
            int mappingIndex,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            TypeDef generatedReverseType,
            ICollection<string> emissionWarnings)
        {
            if (!proxyModulesByAssemblyName.TryGetValue(aliasMapping.ProxyAssemblyName, out var proxyModule) ||
                !proxyAssemblyPathsByName.TryGetValue(aliasMapping.ProxyAssemblyName, out var proxyAssemblyPath) ||
                !TryResolveRuntimeType(aliasMapping.ProxyAssemblyName, proxyAssemblyPath, aliasMapping.ProxyTypeName, out var proxyRuntimeType) ||
                proxyRuntimeType is null ||
                !TryResolveType(proxyModule, proxyRuntimeType.IsGenericType ? proxyRuntimeType.GetGenericTypeDefinition().FullName! : aliasMapping.ProxyTypeName, out var proxyType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    aliasMapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy type '{aliasMapping.ProxyTypeName}' was not found in '{aliasMapping.ProxyAssemblyName}'.");
            }

            return EmitResolvedTypeMapping(
                moduleDef,
                bootstrapType,
                registrationMethod,
                importedMembers,
                aliasMapping,
                mappingIndex,
                isReverseMapping: false,
                proxyType,
                generatedReverseType,
                generatedReverseType,
                generatedReverseType.ToTypeSig(),
                generatedReverseType.IsValueType,
                closedGenericTargetTypeArguments: null,
                proxyRuntimeType.IsGenericType ? ImportRuntimeTypeCached(moduleDef, proxyRuntimeType, $"closed generic proxy type '{aliasMapping.ProxyTypeName}'") : null,
                proxyRuntimeType.IsGenericType ? ImportRuntimeTypeSig(moduleDef, proxyRuntimeType) : null,
                proxyRuntimeType.IsGenericType ? proxyRuntimeType.GetGenericArguments().Select(runtimeType => ImportRuntimeTypeSig(moduleDef, runtimeType)).ToArray() : null,
                proxyAssemblyPathsByName,
                proxyAssemblyPathsByName,
                emissionWarnings);
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
                if (StringUtil.IsNullOrWhiteSpace(assemblyPath) ||
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
                if (StringUtil.IsNullOrWhiteSpace(normalizedAssemblyName) ||
                    StringUtil.IsNullOrWhiteSpace(entry.Value) ||
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
                if (StringUtil.IsNullOrWhiteSpace(normalizedAssemblyName) ||
                    StringUtil.IsNullOrWhiteSpace(entry.Value) ||
                    !File.Exists(entry.Value) ||
                    combinedPaths.ContainsKey(normalizedAssemblyName))
                {
                    continue;
                }

                combinedPaths[normalizedAssemblyName] = entry.Value;
            }

            // The representative array aliases of mapped types that arrays implement (object[]...) are types of the core library,
            // which isn't always an input: the generator's own is used then.
            var coreLibrary = typeof(object).Assembly;
            if (coreLibrary.GetName().Name is { } coreLibraryName &&
                !combinedPaths.ContainsKey(coreLibraryName) &&
                !StringUtil.IsNullOrWhiteSpace(coreLibrary.Location) &&
                File.Exists(coreLibrary.Location))
            {
                combinedPaths[coreLibraryName] = coreLibrary.Location;
            }

            return combinedPaths;
        }

        /// <summary>
        /// Builds the target-type index used to expand assignable runtime registrations without rescanning all module types per mapping.
        /// </summary>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name value.</param>
        /// <param name="queriedTargetTypeKeys">The target types the mappings query.</param>
        /// <param name="applicationAssemblyNames">The names of every assembly of the application, or null when they aren't known.</param>
        /// <returns>The resulting target-type index.</returns>
        private static TargetTypeIndex BuildTargetTypeIndex(IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName, ISet<string> queriedTargetTypeKeys, ISet<string>? applicationAssemblyNames)
        {
            var applicationTypeLayouts = applicationAssemblyNames is null ? null : new ApplicationTypeLayouts(applicationAssemblyNames);
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
                    if (StringUtil.IsNullOrWhiteSpace(candidateType.FullName))
                    {
                        continue;
                    }

                    typeByAssemblyAndName[string.Concat(assemblyTypeCacheKeyPrefix, candidateType.FullName)] = candidateType;
                    if (!StringUtil.IsNullOrWhiteSpace(candidateType.ReflectionFullName) &&
                        !string.Equals(candidateType.ReflectionFullName, candidateType.FullName, StringComparison.Ordinal))
                    {
                        typeByAssemblyAndName[string.Concat(assemblyTypeCacheKeyPrefix, candidateType.ReflectionFullName)] = candidateType;
                    }

                    // The types the application can't load have no instances there (dynamic duck typing never sees them).
                    if (!IsAliasCandidateType(candidateType) ||
                        applicationTypeLayouts?.LayoutNeedsMissingAssembly(candidateType) == true)
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
        /// <param name="runtimeDuplicateKeys">Receives the canonical mappings that spell already registered runtime types, by key, with the key of that registration.</param>
        /// <returns>The resulting runtime registration set.</returns>
        private static IReadOnlyList<DuckTypeAotRuntimeRegistration> BuildRuntimeRegistrations(
            IReadOnlyList<DuckTypeAotMapping> canonicalMappings,
            IReadOnlyList<DuckTypeAotTypeReference> genericTypeRoots,
            TargetTypeIndex targetTypeIndex,
            IDictionary<string, string> runtimeDuplicateKeys)
        {
            var registrations = new List<DuckTypeAotRuntimeRegistration>();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var canonicalKeys = canonicalMappings.Select(mapping => mapping.Key).ToHashSet(StringComparer.Ordinal);
            var keysByRuntimeIdentity = new Dictionary<(DuckTypeAotMappingMode Mode, Type ProxyType, Type TargetType), string>();
            var aliasPlansByCanonicalTargetKey = new Dictionary<string, CanonicalTargetAliasPlan>(StringComparer.Ordinal);
            var orderedCanonicalMappings = canonicalMappings.OrderBy(item => item.Key, StringComparer.Ordinal).ToList();

            // Canonical mappings first, so they take precedence over aliases. Different spellings of the same runtime types
            // (unqualified generic arguments, forwarded assemblies...) share a registration: registering both would conflict.
            var registeredCanonicalMappings = new List<DuckTypeAotMapping>();
            foreach (var mapping in orderedCanonicalMappings)
            {
                if (!seenKeys.Add(mapping.Key))
                {
                    continue;
                }

                if (IsRuntimeDuplicate(mapping, out var originalKey))
                {
                    runtimeDuplicateKeys[mapping.Key] = originalKey!;
                    continue;
                }

                registrations.Add(new DuckTypeAotRuntimeRegistration(mapping, mapping.Key, DuckTypeAotRuntimeRegistrationKind.Canonical));
                registeredCanonicalMappings.Add(mapping);
            }

            foreach (var mapping in registeredCanonicalMappings)
            {
                if (mapping.Source == DuckTypeAotMappingSource.CallTarget)
                {
                    // Created for the static type only (System.Object would get an alias per type of every input assembly).
                    continue;
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
                    AddAlias(
                        new DuckTypeAotMapping(
                            mapping.ProxyTypeName,
                            mapping.ProxyAssemblyName,
                            nullableAlias.TypeName,
                            nullableAlias.AssemblyName,
                            mapping.Mode,
                            mapping.Source),
                        mapping,
                        DuckTypeAotRuntimeRegistrationKind.NullableAlias);
                }

                foreach (var aliasTarget in aliasPlan.AssignableTargets)
                {
                    AddAlias(
                        new DuckTypeAotMapping(
                            mapping.ProxyTypeName,
                            mapping.ProxyAssemblyName,
                            aliasTarget.TypeName,
                            aliasTarget.AssemblyName,
                            mapping.Mode,
                            mapping.Source),
                        mapping,
                        DuckTypeAotRuntimeRegistrationKind.AssignableAlias);
                }
            }

            return registrations;

            void AddAlias(DuckTypeAotMapping aliasMapping, DuckTypeAotMapping canonicalMapping, DuckTypeAotRuntimeRegistrationKind kind)
            {
                if (canonicalKeys.Contains(aliasMapping.Key) ||
                    !seenKeys.Add(aliasMapping.Key) ||
                    IsRuntimeDuplicate(aliasMapping, out _))
                {
                    return;
                }

                registrations.Add(new DuckTypeAotRuntimeRegistration(aliasMapping, canonicalMapping.Key, kind));
            }

            bool IsRuntimeDuplicate(DuckTypeAotMapping mapping, out string? originalKey)
            {
                originalKey = null;
                if (runtimeTypeResolutionAssemblyPathsByName is null ||
                    !runtimeTypeResolutionAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath) ||
                    !runtimeTypeResolutionAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) ||
                    !TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) ||
                    !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) ||
                    proxyRuntimeType is null ||
                    targetRuntimeType is null)
                {
                    return false;
                }

                var identity = (mapping.Mode, proxyRuntimeType, targetRuntimeType);
                if (keysByRuntimeIdentity.TryGetValue(identity, out originalKey))
                {
                    return true;
                }

                keysByRuntimeIdentity[identity] = mapping.Key;
                return false;
            }
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
                StringUtil.IsNullOrWhiteSpace(candidateType.FullName))
            {
                return false;
            }

            if (candidateType.IsInterface || candidateType.IsAbstract || candidateType.GenericParameters.Count > 0 || candidateType.IsGlobalModuleType)
            {
                return false;
            }

            // Types no instance (boxed, or of a reference type) can have, which a registry can't reference either (an ldtoken of
            // System.__Canon is invalid IL).
            if (candidateType.Namespace == "System" &&
                candidateType.Name.String is "__Canon" or "Void" or "TypedReference" or "ArgIterator" or "RuntimeArgumentHandle")
            {
                return false;
            }

            // The non-public types of the core library are aliases too, like dynamic duck typing creates a proxy for each of them.
            // They differ between runtimes (NativeAOT's aren't CoreCLR's): each registration is isolated, so one that can't be
            // made there doesn't prevent the others, and the instances of the types a registry doesn't register are served by
            // the proxy of a core library type they derive from, at runtime (see HasRuntimeInternalSubtypes).
            return !candidateType.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, "System.Runtime.CompilerServices.IsByRefLikeAttribute", StringComparison.Ordinal));
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
            var assemblyPaths = runtimeTypeResolutionAssemblyPathsByName;
            if (assemblyPaths is null ||
                !assemblyPaths.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var canonicalRuntimeTargetType) ||
                canonicalRuntimeTargetType is null)
            {
                return false;
            }

            var resolvedAliases = new List<TargetAliasTargetInfo>();
            var seenAliasKeys = new HashSet<string>(StringComparer.Ordinal);

            // The array types assignable to a mapped array type (array covariance) are served by its proxy at runtime (see
            // DuckTypeAotEngine), not by aliases: they can be of any assembly, or not referenceable by a registry.
            if (canonicalRuntimeTargetType.IsArray)
            {
                return false;
            }

            // An array instance of a mapped type that isn't an array type (object, System.Array, an interface arrays implement):
            // dynamic duck typing creates the proxy of its array type, which binds what reflection finds on array types, the same
            // for all of them. A representative array type gets that proxy, which the runtime uses for the other ones.
            if (mapping.Mode == DuckTypeAotMappingMode.Forward && GetArrayViewAliasTarget(canonicalRuntimeTargetType) is { } arrayViewTarget)
            {
                AddResolvedAlias(arrayViewTarget.AssemblyName, arrayViewTarget.TypeName);
            }

            // Task<VoidTaskResult>: the type of Task.CompletedTask and of the tasks of async methods returning Task, and the base
            // type of their boxes (served by its proxy at runtime). No input names it.
            if (mapping.Mode == DuckTypeAotMappingMode.Forward && GetVoidTaskResultTaskAliasTarget(canonicalRuntimeTargetType) is { } voidTaskResultTaskTarget)
            {
                AddResolvedAlias(voidTaskResultTaskTarget.AssemblyName, voidTaskResultTaskTarget.TypeName);
            }

            foreach (var candidateTarget in targetTypeIndex.AliasCandidateTargets)
            {
                if ((mapping.Mode == DuckTypeAotMappingMode.Reverse && candidateTarget.Type.IsValueType) ||
                    !TryResolveCandidate(candidateTarget, out var candidateTypeName, out var candidateRuntimeType) ||
                    !canonicalRuntimeTargetType.IsAssignableFrom(candidateRuntimeType) ||
                    candidateRuntimeType == canonicalRuntimeTargetType)
                {
                    continue;
                }

                AddResolvedAlias(candidateTarget.AssemblyName, candidateTypeName);
            }

            foreach (var genericTypeRoot in genericTypeRoots)
            {
                if (StringUtil.IsNullOrWhiteSpace(genericTypeRoot.TypeName) ||
                    !DuckTypeAotNameHelpers.IsClosedGenericTypeName(genericTypeRoot.TypeName) ||
                    !assemblyPaths.TryGetValue(genericTypeRoot.AssemblyName, out var genericTypeRootAssemblyPath) ||
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

            bool TryResolveCandidate(TargetTypeIndexEntry candidateTarget, out string candidateTypeName, out Type candidateRuntimeType)
            {
                candidateTypeName = GetRuntimeTypeName(candidateTarget.Type);
                candidateRuntimeType = null!;
                if (StringUtil.IsNullOrWhiteSpace(candidateTypeName) ||
                    !assemblyPaths.TryGetValue(candidateTarget.AssemblyName, out var candidateAssemblyPath) ||
                    !TryResolveRuntimeType(candidateTarget.AssemblyName, candidateAssemblyPath, candidateTypeName, out var resolvedType) ||
                    resolvedType is null)
                {
                    return false;
                }

                candidateRuntimeType = resolvedType;
                return true;
            }
        }

        /// <summary>
        /// Gets Task&lt;VoidTaskResult&gt; as an alias of a core library target type it derives from or implements (e.g. Task).
        /// </summary>
        /// <param name="targetType">The mapped target type.</param>
        /// <returns>Task&lt;VoidTaskResult&gt;, or null when the target type isn't a core library type assignable from it.</returns>
        private static TargetAliasTargetInfo? GetVoidTaskResultTaskAliasTarget(Type targetType)
        {
            var coreLibrary = typeof(object).Assembly;
            if (targetType.Assembly != coreLibrary ||
                coreLibrary.GetType("System.Threading.Tasks.VoidTaskResult") is not { } voidTaskResultType ||
                coreLibrary.GetName().Name is not { } coreLibraryName)
            {
                return null;
            }

            var voidTaskResultTaskType = typeof(Task<>).MakeGenericType(voidTaskResultType);
            return voidTaskResultTaskType != targetType && targetType.IsAssignableFrom(voidTaskResultTaskType) && voidTaskResultTaskType.FullName is { } typeName
                       ? new TargetAliasTargetInfo(coreLibraryName, typeName)
                       : null;
        }

        /// <summary>
        /// Gets the representative array type whose proxy serves the array instances of a mapped type that isn't an array type:
        /// object[] (or T[] for a generic collection interface of T), when the mapped type is assignable from it.
        /// </summary>
        /// <param name="targetType">The mapped target type.</param>
        /// <returns>The representative array type, or null when no array type is assignable to the target.</returns>
        private static TargetAliasTargetInfo? GetArrayViewAliasTarget(Type targetType)
        {
            if (targetType.IsValueType || targetType.IsArray || targetType.ContainsGenericParameters)
            {
                return null;
            }

            var representative = targetType.IsGenericType && targetType.GetGenericArguments() is { Length: 1 } arguments && !arguments[0].IsByRef && !arguments[0].IsPointer
                                     ? arguments[0].MakeArrayType()
                                     : typeof(object).MakeArrayType();
            if (!targetType.IsAssignableFrom(representative) && targetType.IsAssignableFrom(typeof(object[])))
            {
                representative = typeof(object[]);
            }

            if (!targetType.IsAssignableFrom(representative) ||
                GetRuntimeTypeDefinition(representative.GetElementType()!) is not { } elementDefinition ||
                elementDefinition.Assembly.GetName().Name is not { } elementAssemblyName ||
                representative.FullName is not { } representativeName)
            {
                return null;
            }

            // Closed generic element types live in the assembly of their definition (their arguments are assembly qualified).
            return new TargetAliasTargetInfo(elementAssemblyName, representativeName);
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
                if (StringUtil.IsNullOrWhiteSpace(typeName))
                {
                    return;
                }

                var assemblyName = ResolveTypeDefAssemblyName(type);
                if (StringUtil.IsNullOrWhiteSpace(assemblyName))
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
                                          .Where(item => !StringUtil.IsNullOrWhiteSpace(item.TypeName))
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
            if (nullableUnderlyingType is null || StringUtil.IsNullOrWhiteSpace(nullableUnderlyingType.FullName))
            {
                return false;
            }

            aliasTarget = new NullableAliasTargetInfo(
                nullableUnderlyingType.FullName!,
                DuckTypeAotNameHelpers.NormalizeAssemblyName(nullableUnderlyingType.Assembly.GetName().Name ?? string.Empty));
            return true;
        }

        /// <summary>
        /// Gets what dynamic duck typing does with the proxy of a forward mapping for an instance of a reverse proxy: it creates
        /// the proxy for the reverse proxy type it creates for the reverse mapping, which the registry generates instead.
        /// </summary>
        /// <param name="forwardMappings">The forward mappings of the proxy type the generated reverse proxy type is assignable to.</param>
        /// <param name="reverseMapping">The reverse mapping.</param>
        /// <param name="aliasMapping">The registration of the forward proxy for the generated reverse proxy type.</param>
        /// <param name="proxyRuntimeType">The runtime proxy type of the forward mappings.</param>
        /// <param name="generatedReverseType">The generated reverse proxy type.</param>
        /// <param name="exceptionType">The exception type dynamic duck typing throws, when it fails.</param>
        /// <param name="exceptionMessage">The exception message dynamic duck typing throws, naming the generated reverse proxy type.</param>
        /// <returns>The outcome in dynamic duck typing, or <see cref="DynamicCreationOutcome.Unavailable"/> when it can't be asked.</returns>
        private static DynamicCreationOutcome GetDynamicGeneratedReverseAliasOutcome(
            IReadOnlyList<DuckTypeAotMapping> forwardMappings,
            DuckTypeAotMapping reverseMapping,
            DuckTypeAotMapping aliasMapping,
            Type proxyRuntimeType,
            TypeDef generatedReverseType,
            out Type? exceptionType,
            out string? exceptionMessage)
        {
            exceptionType = null;
            exceptionMessage = null;
            if (_currentExecutionContext is not { } context)
            {
                return DynamicCreationOutcome.Unavailable;
            }

            if (forwardMappings.Any(mapping => IsCheckedAgainstMetadataOnly(mapping.Key)) ||
                IsCheckedAgainstMetadataOnly(reverseMapping.Key) ||
                !context.GeneratedTypeRuntimeTypes.TryGetValue(generatedReverseType, out var dynamicReverseProxyType))
            {
                // Remembered for the emission of the registration, which asks again.
                context.CacheFailureProbe(aliasMapping.Key, DynamicCreationOutcome.Unavailable, exceptionType: null, exceptionMessage: null, context.Warnings.Count);
                return DynamicCreationOutcome.Unavailable;
            }

            // The forward proxy types of the reverse proxy types dynamic duck typing creates differ only by the type of the instance
            // field and the attributes of ToString: their creation is shared like other forward proxy types of the same shape,
            // unless the reverse proxy type has [DuckInclude] methods (inherited from its contract) the proxy implements too.
            var shapeKey = !dynamicReverseProxyType.IsValueType && DuckType.GetForwardProxyShapeKeyForAot(dynamicReverseProxyType) is Type
                               ? (object)dynamicReverseProxyType
                               : Tuple.Create("generated reverse proxy", dynamicReverseProxyType.IsValueType, dynamicReverseProxyType.GetMethod("ToString", Type.EmptyTypes)?.Attributes);
            var outcome = GetDynamicCreationOutcome(aliasMapping, proxyRuntimeType, dynamicReverseProxyType, out exceptionType, out exceptionMessage, shapeKey);
            if (outcome == DynamicCreationOutcome.Failed && dynamicReverseProxyType.FullName is { Length: > 0 } dynamicReverseProxyTypeName)
            {
                exceptionMessage = exceptionMessage?.Replace(dynamicReverseProxyTypeName, generatedReverseType.FullName);
                if (context.DynamicFailureInnerExceptions.TryGetValue(aliasMapping.Key, out var innerException))
                {
                    context.DynamicFailureInnerExceptions[aliasMapping.Key] = innerException.Select(level => new KeyValuePair<string, string>(level.Key, level.Value.Replace(dynamicReverseProxyTypeName, generatedReverseType.FullName))).ToArray();
                }
            }

            return outcome;
        }

        /// <summary>
        /// What the runtime can't load in a generated registry (see ValidateGeneratedRegistry).
        /// </summary>
        private sealed class RegistryValidation
        {
            /// <summary>
            /// Gets the load failures of the generated proxy types, by registration key.
            /// </summary>
            internal Dictionary<string, string> UnloadableProxyTypes { get; } = new(StringComparer.Ordinal);
        }

        /// <summary>
        /// Which types of the target assemblies can't be loaded and laid out with the assemblies of the application: a base
        /// type, an interface or the type of an instance field (of the type, or of a value type it embeds) comes from another
        /// assembly. No instance of them exists in the application (NativeAOT can't allocate them) and the NativeAOT compiler
        /// fails to compile a reflectable method whose signature has one of them (the registry's proxy factories are reflectable).
        /// </summary>
        private sealed class ApplicationTypeLayouts
        {
            private readonly ISet<string> _applicationAssemblyNames;
            private readonly Dictionary<TypeDef, bool>[] _needsMissingAssembly =
            [
                new(ReferenceIdentityComparer<TypeDef>.Instance),
                new(ReferenceIdentityComparer<TypeDef>.Instance),
                new(ReferenceIdentityComparer<TypeDef>.Instance),
            ];

            internal ApplicationTypeLayouts(ISet<string> applicationAssemblyNames)
            {
                _applicationAssemblyNames = applicationAssemblyNames;
            }

            /// <summary>
            /// What the runtime needs of a type.
            /// </summary>
            private enum Need
            {
                /// <summary>
                /// Its base types: the type of a reference field (e.g. to know it's a reference type).
                /// </summary>
                Resolution,

                /// <summary>
                /// Its base types and interfaces, loaded.
                /// </summary>
                Load,

                /// <summary>
                /// Loaded, and the types of its instance fields (and of its base types'): to allocate it or embed it.
                /// </summary>
                Layout,
            }

            /// <summary>
            /// Determines whether loading or laying out a type needs an assembly the application doesn't have. A type dnlib
            /// can't resolve isn't missing: only the assemblies the application doesn't have are.
            /// </summary>
            /// <param name="type">The type.</param>
            /// <returns>true when the type can't be laid out in the application; otherwise, false.</returns>
            internal bool LayoutNeedsMissingAssembly(TypeDef type) => NeedsMissingAssembly(type, Need.Layout);

            private bool NeedsMissingAssembly(TypeDef type, Need need)
            {
                var results = _needsMissingAssembly[(int)need];
                if (results.TryGetValue(type, out var result))
                {
                    return result;
                }

                // Value types can't embed themselves and types can't derive from themselves: a cycle is through an invalid
                // type, which adds no assembly.
                results[type] = false;
                result = (type.BaseType is { } baseType && NeedsMissingAssembly(baseType, need)) ||
                         (need >= Need.Load && type.Interfaces.Any(implementation => implementation.Interface is { } interfaceType && NeedsMissingAssembly(interfaceType, Need.Load))) ||
                         (need == Need.Layout && type.Fields.Any(field => !field.IsStatic && field.FieldSig?.Type is { } fieldType && NeedsMissingAssembly(fieldType, Need.Resolution)));
                results[type] = result;
                return result;
            }

            /// <summary>
            /// Determines whether a type of a signature, or of its type arguments or element types, needs an assembly the
            /// application doesn't have (value types are always laid out).
            /// </summary>
            private bool NeedsMissingAssembly(TypeSig signature, Need need)
            {
                for (var current = signature; current is not null; current = current.Next)
                {
                    switch (current)
                    {
                        case CorLibTypeSig:
                            return false;
                        case TypeDefOrRefSig typeSignature:
                            return NeedsMissingAssembly(typeSignature.TypeDefOrRef, need);
                        case GenericInstSig genericSignature:
                            return NeedsMissingAssembly(genericSignature.GenericType.TypeDefOrRef, need) ||
                                   genericSignature.GenericArguments.Any(argument => NeedsMissingAssembly(argument, Need.Resolution));
                    }
                }

                // Arrays, pointers and by-refs of the types above; generic parameters and function pointers.
                return false;
            }

            private bool NeedsMissingAssembly(ITypeDefOrRef type, Need need)
            {
                if (type is TypeSpec { TypeSig: { } signature })
                {
                    // E.g. a generic base type, with its type arguments.
                    return NeedsMissingAssembly(signature, need);
                }

                if (!IsApplicationAssembly(type.DefinitionAssembly))
                {
                    return true;
                }

                if (type.ResolveTypeDef() is not { } definition)
                {
                    return false;
                }

                if (!IsApplicationAssembly(definition.Module?.Assembly))
                {
                    // Forwarded to an assembly the application doesn't have.
                    return true;
                }

                return NeedsMissingAssembly(definition, definition.IsValueType ? Need.Layout : need);
            }

            private bool IsApplicationAssembly(IAssembly? assembly)
                => assembly?.Name is not { } name ||
                   StringUtil.IsNullOrEmpty(name) ||
                   string.Equals(name, "System.Private.CoreLib", StringComparison.OrdinalIgnoreCase) ||
                   _applicationAssemblyNames.Contains(name);
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
    }
}
