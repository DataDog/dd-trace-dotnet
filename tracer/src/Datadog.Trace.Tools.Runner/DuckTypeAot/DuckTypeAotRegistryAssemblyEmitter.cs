// <copyright file="DuckTypeAotRegistryAssemblyEmitter.cs" company="Datadog">
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
using System.Runtime.InteropServices;
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.Loader;
#endif
using System.Security.Cryptography;
using System.Text;
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
        /// The most emission passes: each one leaves out what the previous one generated but the runtime can't load.
        /// </summary>
        private const int MaxEmissionPasses = 3;

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
        /// Defines the status code reverse custom attribute named arguments constant.
        /// </summary>
        private const string StatusCodeCustomAttributeNamedArguments = "DTAOT0214";

        /// <summary>
        /// Defines the status code for proxy types Reflection.Emit can't create (sealed base type, abstract member left without
        /// implementation by [DuckIgnore]).
        /// </summary>
        private const string StatusCodeUnloadableProxyType = "DTAOT0215";

        /// <summary>
        /// Diagnostic code of the warning for a compatible mapping that targets, or whose proxy binds, a non-public type or
        /// member of the core library (see DuckTypeAotMappingEmissionResult.RuntimeSpecific).
        /// </summary>
        private const string StatusCodeRuntimeSpecific = "DTAOT0216";

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
        internal static DuckTypeAotRegistryEmissionResult Emit(
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
            _currentProfile = DuckTypeAotGenerateProcessor.IsProfilingEnabled() ? new EmitterProfile() : null;
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
            // generates, which forward proxies of reverse proxies bind (see EmitGeneratedReverseTargetAliases).
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
                var targetTypeIndex = BuildTargetTypeIndex(targetModulesByAssemblyName, queriedTargetTypeKeys);
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.BuildTargetTypeIndexSeconds += seconds);
                phaseStopwatch = StartProfilePhase();
                _currentExecutionContext = new EmitterExecutionContext(targetTypeIndex, dynamicOracleCache);
                var runtimeDuplicateKeys = new Dictionary<string, string>(StringComparer.Ordinal);
                runtimeRegistrations.AddRange(BuildRuntimeRegistrations(
                    mappingResolutionResult.Mappings,
                    mappingResolutionResult.GenericTypeRoots,
                    targetTypeIndex,
                    runtimeDuplicateKeys));
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.BuildRuntimeRegistrationsSeconds += seconds);
                var generatedReverseTargets = new List<KeyValuePair<DuckTypeAotMapping, TypeDef>>();
                var registrationResults = new Dictionary<string, DuckTypeAotMappingEmissionResult>(StringComparer.Ordinal);

                phaseStopwatch = StartProfilePhase();
                for (var i = 0; i < runtimeRegistrations.Count; i++)
                {
                    var registrationMethod = GetBootstrapRegistrationMethod(moduleDef, bootstrapType, bootstrapRegistrationMethods, i);

                    var runtimeRegistration = runtimeRegistrations[i];
                    var mapping = runtimeRegistration.Mapping;
                    var emitMappingStopwatch = StartProfilePhase();
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

                    if (mapping.Mode == DuckTypeAotMappingMode.Reverse &&
                        emissionResult.Status == DuckTypeAotCompatibilityStatuses.Compatible &&
                        moduleDef.Find(emissionResult.GeneratedProxyTypeName, isReflectionName: false) is { } generatedReverseType)
                    {
                        generatedReverseTargets.Add(new KeyValuePair<DuckTypeAotMapping, TypeDef>(mapping, generatedReverseType));
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

                aliasFailures.AddRange(EmitGeneratedReverseTargetAliases(
                    moduleDef,
                    bootstrapType,
                    bootstrapRegistrationMethods,
                    importedMembers,
                    mappingResolutionResult,
                    mappingResults,
                    generatedReverseTargets,
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
        /// Returns the bootstrap method of a registration. Each registration has its own method, called under a handler for type
        /// load failures (see EmitBootstrapRegistrationChunks).
        /// </summary>
        /// <param name="module">The registry module.</param>
        /// <param name="bootstrap">The bootstrap type.</param>
        /// <param name="registrationMethods">The registration methods, by registration index.</param>
        /// <param name="registrationIndex">The index of the registration.</param>
        /// <returns>The method receiving the registration.</returns>
        private static MethodDef GetBootstrapRegistrationMethod(ModuleDef module, TypeDef bootstrap, IList<MethodDef> registrationMethods, int registrationIndex)
        {
            while (registrationMethods.Count <= registrationIndex)
            {
                var method = new MethodDefUser(
                    $"RegisterMapping_{registrationMethods.Count + 1:D5}",
                    MethodSig.CreateStatic(module.CorLibTypes.Void),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig)
                {
                    Body = new CilBody()
                };
                bootstrap.Methods.Add(method);
                registrationMethods.Add(method);
            }

            return registrationMethods[registrationIndex];
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
        /// Registers the forward mappings for the reverse proxy types the registry generates: dynamic duck typing creates a forward
        /// proxy for the runtime type of the instance, and a reverse proxy is an instance of its generated type, which derives from
        /// (or implements) the mapped target.
        /// </summary>
        /// <param name="module">The generated registry module.</param>
        /// <param name="bootstrap">The bootstrap type.</param>
        /// <param name="chunks">The bootstrap registration methods.</param>
        /// <param name="members">The imported members.</param>
        /// <param name="resolution">The mapping resolution result.</param>
        /// <param name="results">The emission results of the mappings, flagged when a proxy for a generated reverse proxy type is
        /// bound from metadata.</param>
        /// <param name="reverseTargets">The generated reverse proxy types, by reverse mapping.</param>
        /// <param name="proxyModules">The proxy modules by assembly name.</param>
        /// <param name="runtimeRegistrations">The runtime registrations, completed with the ones emitted here.</param>
        /// <param name="emissionWarnings">The emission warnings.</param>
        /// <returns>The failures only the registry has (dynamic duck typing creates those proxies), by canonical mapping key.</returns>
        private static IReadOnlyList<KeyValuePair<string, DuckTypeAotMappingEmissionResult>> EmitGeneratedReverseTargetAliases(
            ModuleDef module,
            TypeDef bootstrap,
            IList<MethodDef> chunks,
            ImportedMembers members,
            DuckTypeAotMappingResolutionResult resolution,
            IDictionary<string, DuckTypeAotMappingEmissionResult> results,
            IReadOnlyList<KeyValuePair<DuckTypeAotMapping, TypeDef>> reverseTargets,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModules,
            IList<DuckTypeAotRuntimeRegistration> runtimeRegistrations,
            ICollection<string> emissionWarnings)
        {
            var aliasFailures = new List<KeyValuePair<string, DuckTypeAotMappingEmissionResult>>();
            var registrationIndex = runtimeRegistrations.Count;
            var context = _currentExecutionContext;

            // The reverse proxy type dynamic duck typing creates for each pair, which the generated one stands for.
            foreach (var reverseTarget in reverseTargets)
            {
                if (context is not null && context.DynamicReverseProxyTypes.TryGetValue(reverseTarget.Key.Key, out var dynamicReverseProxyType))
                {
                    context.GeneratedTypeRuntimeTypes[reverseTarget.Value] = dynamicReverseProxyType;
                    context.RuntimeTypeGeneratedTypes[dynamicReverseProxyType] = reverseTarget.Value;
                }
            }

            // The forward mappings whose target a generated reverse proxy type is assignable to (a contract it derives from or
            // implements, IDuckType, object...), grouped by proxy type and generated reverse proxy type: one registration each.
            // A failure dynamic duck typing has too is replayed for the generated reverse proxy types as well.
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
                    if (reverseTargets.Count > 0)
                    {
                        emissionWarnings.Add($"Mapping '{mapping.Key}' gets no registration for the generated reverse proxy types: its types couldn't be loaded.");
                    }

                    continue;
                }

                foreach (var reverseTarget in reverseTargets)
                {
                    var reversePath = resolution.ProxyAssemblyPathsByName[reverseTarget.Key.ProxyAssemblyName];
                    if (!TryResolveRuntimeType(reverseTarget.Key.ProxyAssemblyName, reversePath, reverseTarget.Key.ProxyTypeName, out var reverseContract) ||
                        reverseContract is null ||
                        !targetType.IsAssignableFrom(context?.GeneratedTypeRuntimeTypes.TryGetValue(reverseTarget.Value, out var dynamicReverseProxyType) == true ? dynamicReverseProxyType : reverseContract))
                    {
                        continue;
                    }

                    // Other spellings of the same proxy type, and other mapped targets of the generated reverse proxy type, share
                    // its registration.
                    var groupKey = Tuple.Create(proxyType, reverseTarget.Value);
                    if (!groups.TryGetValue(groupKey, out var groupMappings))
                    {
                        groupMappings = [];
                        groups[groupKey] = groupMappings;
                        orderedGroups.Add(Tuple.Create(proxyType, reverseTarget.Value, reverseTarget));
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

                // What dynamic duck typing does with the reverse proxy type it creates: when it can't create the proxy, the
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
                // generated reverse proxy type (its implementations of the contract, its IDuckType members, its ToString...), and
                // IDuckType.Type is the generated reverse proxy type.
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
                    // The mappings serve the generated reverse proxy type with a proxy bound from metadata: they say so.
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

                emissionWarnings.Add($"The registry can't create the proxy of mapping '{mapping.Key}' for the generated reverse proxy type '{generatedReverseType.FullName}': {aliasResult.Detail}");
                var aliasFailureDetail = checkedAgainstMetadataOnly
                                             ? $"The registry can't create the proxy of the generated reverse proxy type of '{reverseMapping.ProxyTypeName}', an instance of the mapped target, and dynamic duck typing couldn't be asked whether it can: {aliasResult.Detail}"
                                             : $"The registry can't create the proxy of the generated reverse proxy type of '{reverseMapping.ProxyTypeName}', an instance of the mapped target, like dynamic duck typing does: {aliasResult.Detail}";
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
        /// Emits the proxy of a forward mapping for a reverse proxy type the registry generates, bound to the members of that type.
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
        /// Emits a known failure registration when an incompatible mapping should preserve dynamic exception behavior in AOT mode.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetTypeDefinition">The target type definition.</param>
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
            TypeDef targetTypeDefinition,
            ITypeDefOrRef targetType,
            DuckTypeAotMappingEmissionResult failure,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string>? emissionWarnings = null,
            ITypeDefOrRef? closedProxyType = null,
            IReadOnlyList<KeyValuePair<string, string>>? predictedInnerException = null)
        {
            var phaseStopwatch = StartProfilePhase();
            // Dynamic duck typing in the generator can't read the duck attributes standalone contracts declare themselves.
            var dynamicEngineApplies = !UsesMetadataOnlyDuckAttributes(mapping, proxyType, targetTypeDefinition);
            if (!TryResolveFailureReplay(
                    moduleDef,
                    mapping,
                    failure,
                    dynamicEngineApplies,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    out var resolvedExceptionTypeName,
                    out var resolvedFailureMessage,
                    out var fromDynamicEngine))
            {
                return false;
            }

            if (fromDynamicEngine && _currentExecutionContext?.UnverifiedDynamicFailures.Contains(mapping.Key) != true)
            {
                _currentExecutionContext?.MarkDynamicFailureReplayed(mapping.Key);
            }

            // Closed generic mappings are looked up with their closed proxy type, not the generic type definition.
            var importedProxyType = closedProxyType ?? ImportTypeDefOrRefCached(moduleDef, proxyType, $"failure registration proxy type '{proxyType.FullName}'");
            var importedTargetType = ImportTypeDefOrRefCached(moduleDef, targetType, $"failure registration target type '{targetType.FullName}'");
            var innerException = fromDynamicEngine
                                     ? _currentExecutionContext?.DynamicFailureInnerExceptions.TryGetValue(mapping.Key, out var dynamicInnerException) == true ? dynamicInnerException : null
                                     : predictedInnerException;

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
                GetFailureReplayDetail(mapping, importedProxyType, importedTargetType, resolvedFailureMessage ?? failure.Detail),
                innerException);

            if (emissionWarnings is not null)
            {
                var diagnosticCode = StringUtil.IsNullOrWhiteSpace(failure.DiagnosticCode) ? "n/a" : failure.DiagnosticCode!;
                var detail = StringUtil.IsNullOrWhiteSpace(failure.Detail) ? "No additional details." : failure.Detail!;
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
            bool dynamicEngineApplies,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            out string failureTypeName,
            out string failureMessage,
            out bool fromDynamicEngine)
        {
            failureTypeName = string.Empty;
            failureMessage = failure.Detail ?? string.Empty;
            fromDynamicEngine = false;

            // The dynamic engine's own failure first: it gives the exception type and message dynamic duck typing throws,
            // in its order (e.g. properties before methods). The classifiers below only cover the mappings dynamic duck typing
            // creates (failures of the AOT emitter), or can't evaluate in the generator.
            if (dynamicEngineApplies &&
                TryResolveDynamicFailureExceptionType(
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
                fromDynamicEngine = true;
                if (_currentProfile is not null)
                {
                    _currentProfile.FailureClassifierFallbackCount++;
                }

                return true;
            }

            if (TryResolveStaticExactFailureTypeName(failure, out var staticFailureTypeName))
            {
                failureTypeName = staticFailureTypeName!;
                if (_currentProfile is not null)
                {
                    _currentProfile.FailureClassifierFastPathCount++;
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
        /// Emits the mapping of an array proxy type: the failure dynamic duck typing has with it (an array type isn't a proxy
        /// definition it can implement), or else an unsupported proxy kind.
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <param name="bootstrapType">The bootstrap type.</param>
        /// <param name="initializeMethod">The registration method.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="mapping">The mapping.</param>
        /// <param name="mappingIndex">The registration index.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name.</param>
        /// <returns>The emission result.</returns>
        private static DuckTypeAotMappingEmissionResult EmitArrayProxyTypeMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName)
        {
            var unsupported = DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                StatusCodeUnsupportedProxyKind,
                $"Proxy type '{mapping.ProxyTypeName}' is an array type, which the registry can't generate a proxy of.");
            if (!proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath) ||
                !targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) ||
                !TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) ||
                proxyRuntimeType is null ||
                targetRuntimeType is null ||
                GetDynamicCreationOutcome(mapping, proxyRuntimeType, targetRuntimeType, out var exceptionType, out var exceptionMessage) != DynamicCreationOutcome.Failed ||
                exceptionType is null)
            {
                return unsupported;
            }

            var importedProxyType = ImportRuntimeTypeCached(moduleDef, proxyRuntimeType, $"array proxy type '{mapping.ProxyTypeName}'");
            var importedTargetType = ImportRuntimeTypeCached(moduleDef, targetRuntimeType, $"target type '{mapping.TargetTypeName}'");
            EmitFailureRegistration(
                moduleDef,
                bootstrapType,
                initializeMethod,
                importedMembers,
                mapping.Mode,
                mappingIndex,
                importedProxyType,
                importedTargetType,
                exceptionType.FullName ?? exceptionType.Name,
                GetFailureReplayDetail(mapping, importedProxyType, importedTargetType, exceptionMessage),
                _currentExecutionContext?.DynamicFailureInnerExceptions.TryGetValue(mapping.Key, out var innerException) == true ? innerException : null);
            _currentExecutionContext?.MarkDynamicFailureReplayed(mapping.Key);
            return DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                StatusCodeUnsupportedProxyKind,
                exceptionMessage ?? unsupported.Detail!);
        }

        /// <summary>
        /// Gets the message replayed by a failure registration. Known DuckType exceptions use it as their whole message,
        /// so it falls back to describing the mapping instead of replaying an empty message.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="detail">The resolved failure detail value.</param>
        /// <returns>The failure message to replay.</returns>
        private static string GetFailureReplayDetail(DuckTypeAotMapping mapping, ITypeDefOrRef proxyType, ITypeDefOrRef targetType, string? detail)
        {
            if (!StringUtil.IsNullOrWhiteSpace(detail))
            {
                return detail!;
            }

            return mapping.Mode == DuckTypeAotMappingMode.Reverse
                       ? $"The AOT reverse proxy deriving from '{proxyType.FullName}' cannot be created for delegation type '{targetType.FullName}'."
                       : $"The AOT proxy for '{proxyType.FullName}' cannot be created for target type '{targetType.FullName}'.";
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
            string detail,
            IReadOnlyList<KeyValuePair<string, string>>? innerException = null)
        {
            var failureThrowerMethod = GetOrCreateFailureThrowerMethod(moduleDef, bootstrapType, importedMembers, mappingIndex, failureTypeName, detail, innerException);
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(proxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(targetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            initializeMethod.Body.Instructions.Add(OpCodes.Ldftn.ToInstruction(failureThrowerMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.FuncExceptionCtor));
            initializeMethod.Body.Instructions.Add(
                OpCodes.Call.ToInstruction(
                    mode == DuckTypeAotMappingMode.Reverse
                        ? importedMembers.RegisterAotReverseProxyFailureMethod
                        : importedMembers.RegisterAotProxyFailureMethod));
        }

        /// <summary>
        /// Emits the cast of the instance an activator receives (its first argument) to the target type. An instance of another
        /// type throws the InvalidCastException dynamic duck typing's activator throws (CoreCLR's cast, which names both types,
        /// where NativeAOT's doesn't).
        /// </summary>
        /// <param name="body">The activator body.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="targetIsValueType">Whether the target type is a value type.</param>
        private static void EmitActivatorInstanceCast(CilBody body, ImportedMembers importedMembers, ITypeDefOrRef targetType, bool targetIsValueType, OpCode? loadInstance = null)
        {
            var load = loadInstance ?? OpCodes.Ldarg_0;
            var cast = load.ToInstruction();
            body.Instructions.Add(load.ToInstruction());
            body.Instructions.Add(OpCodes.Isinst.ToInstruction(targetType));
            body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(cast));
            body.Instructions.Add(load.ToInstruction());
            body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(cast));
            body.Instructions.Add(load.ToInstruction());
            body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(targetType));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.ThrowActivatorInvalidCastMethod));
            body.Instructions.Add(cast);
            body.Instructions.Add((targetIsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(targetType));
        }

        /// <summary>
        /// Emits the typed activator of a registration: a CreateProxyInstance&lt;TProxy&gt; (the delegate type of dynamic duck
        /// typing's activators, which CreateInstance&lt;TProxy&gt; calls directly) to a generated static method, bound to the object
        /// activator (its first argument), which object-based creation calls (see DuckType.CreateTypeResult).
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <param name="bootstrapType">The bootstrap type.</param>
        /// <param name="body">The registration method body.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="mappingIndex">The registration index.</param>
        /// <param name="proxyTypeSig">The proxy definition type (TProxy).</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="targetIsValueType">Whether the target type is a value type.</param>
        /// <param name="activatorMethod">The activator creating the proxy of a target instance.</param>
        /// <param name="objectActivatorMethod">The object activator.</param>
        private static void EmitTypedActivatorDelegate(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            CilBody body,
            ImportedMembers importedMembers,
            int mappingIndex,
            TypeSig proxyTypeSig,
            ITypeDefOrRef targetType,
            bool targetIsValueType,
            MethodDef activatorMethod,
            MethodDef objectActivatorMethod)
        {
            // TProxy ActivateTypedProxy_N(Func<object?, object?> objectActivator, object instance)
            var typedActivatorMethod = new MethodDefUser(
                $"ActivateTypedProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(proxyTypeSig, importedMembers.FuncObjectObjectTypeSig, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            typedActivatorMethod.Body = new CilBody();
            EmitActivatorInstanceCast(typedActivatorMethod.Body, importedMembers, targetType, targetIsValueType, OpCodes.Ldarg_1);
            typedActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            typedActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(typedActivatorMethod);

            var typedActivatorType = new TypeSpecUser(new GenericInstSig((ClassSig)importedMembers.CreateProxyInstanceType.ToTypeSig(), proxyTypeSig));
            EmitFuncObjectObjectDelegate(body, importedMembers, objectActivatorMethod);
            body.Instructions.Add(OpCodes.Ldftn.ToInstruction(typedActivatorMethod));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(new MemberRefUser(moduleDef, ".ctor", MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.IntPtr), typedActivatorType)));
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

        private static MethodDef GetOrCreateFailureThrowerMethod(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            ImportedMembers importedMembers,
            int mappingIndex,
            string failureTypeName,
            string detail,
            IReadOnlyList<KeyValuePair<string, string>>? innerException = null)
        {
            detail ??= string.Empty;
            var failureThrowerCacheKey = string.Concat(
                failureTypeName,
                "::",
                detail,
                "::",
                innerException is null ? string.Empty : string.Join("::", innerException.Select(level => level.Key + "::" + level.Value)));
            if (_currentExecutionContext?.TryGetFailureThrowerMethod(failureThrowerCacheKey, out var cachedFailureThrowerMethod) == true)
            {
                return cachedFailureThrowerMethod;
            }

            var failureThrowerMethod = EmitFailureThrowerMethod(moduleDef, bootstrapType, importedMembers, mappingIndex, failureTypeName, detail, innerException);
            _currentExecutionContext?.CacheFailureThrowerMethod(failureThrowerCacheKey, failureThrowerMethod);
            return failureThrowerMethod;
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
            string detail,
            IReadOnlyList<KeyValuePair<string, string>>? innerException)
        {
            var method = new MethodDefUser(
                $"CreateFailure_{mappingIndex:D4}",
                MethodSig.CreateStatic(RuntimeImporter(moduleDef).Import(typeof(Exception)).ToTypeSig()),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            method.Body = new CilBody();
            method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(failureTypeName));
            method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(detail ?? string.Empty));
            if (innerException is { Count: > 0 } && TryEmitExceptionChain(moduleDef, method.Body, innerException, 0))
            {
                // The exceptions dynamic duck typing's wraps, created with their types (the ones of the core library).
                method.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.DuckTypeAotRegisteredFailureCreateWithInnerExceptionInstanceMethod));
            }
            else if (innerException is { Count: > 0 })
            {
                method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(innerException[0].Key));
                method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(innerException[0].Value));
                method.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.DuckTypeAotRegisteredFailureCreateWithInnerExceptionMethod));
            }
            else
            {
                method.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.DuckTypeAotRegisteredFailureCreateMethod));
            }

            method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(method);
            return method;
        }

        /// <summary>
        /// Emits the creation of a chain of wrapped exceptions (each wraps the next one), as far as their types are public types
        /// of the core library with a constructor taking the message (and the inner exception, for the ones that wrap another).
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <param name="body">The method body.</param>
        /// <param name="exceptions">The types and messages of the exceptions, from the outermost.</param>
        /// <param name="index">The index of the exception to create.</param>
        /// <returns>true if the exception was created; false if its type can't be created this way (nothing is emitted).</returns>
        private static bool TryEmitExceptionChain(ModuleDef moduleDef, CilBody body, IReadOnlyList<KeyValuePair<string, string>> exceptions, int index)
        {
            if (typeof(object).Assembly.GetType(exceptions[index].Key, throwOnError: false) is not { IsPublic: true } exceptionType ||
                !typeof(Exception).IsAssignableFrom(exceptionType) ||
                exceptionType.GetConstructor([typeof(string)]) is not { } messageConstructor)
            {
                return false;
            }

            body.Instructions.Add(OpCodes.Ldstr.ToInstruction(exceptions[index].Value));
            if (index + 1 < exceptions.Count &&
                exceptionType.GetConstructor([typeof(string), typeof(Exception)]) is { } wrappingConstructor &&
                TryEmitExceptionChain(moduleDef, body, exceptions, index + 1))
            {
                body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(wrappingConstructor)));
            }
            else
            {
                body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(messageConstructor)));
            }

            return true;
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
        /// Determines whether a duck typing attribute is one dynamic duck typing reads by type, so only the Datadog.Trace one.
        /// [DuckAsClass] is read by name, from any assembly (e.g. Datadog.Trace.Manual declares one).
        /// </summary>
        /// <param name="attributeTypeName">The full name of the attribute type.</param>
        /// <returns>true if dynamic duck typing reads the attribute by type; otherwise, false.</returns>
        private static bool IsDuckAttributeReadByType(string? attributeTypeName)
        {
            return attributeTypeName is DuckAttributeTypeName or DuckFieldAttributeTypeName or DuckPropertyOrFieldAttributeTypeName or DuckReverseMethodAttributeTypeName
                       or DuckCopyAttributeTypeName or DuckIgnoreAttributeTypeName or DuckIncludeAttributeTypeName;
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
        /// Attempts to resolve the exception dynamic duck typing throws for a mapping (see <see cref="GetDynamicCreationOutcome"/>).
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="failure">The AOT failure of the mapping, if any.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="exceptionType">The resolved exception type value.</param>
        /// <param name="exceptionMessage">The resolved exception message value.</param>
        /// <returns>true when dynamic duck typing fails to create the proxy; otherwise, false.</returns>
        private static bool TryResolveDynamicFailureExceptionType(
            ModuleDef moduleDef,
            DuckTypeAotMapping mapping,
            DuckTypeAotMappingEmissionResult failure,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            out Type? exceptionType,
            out string? exceptionMessage)
            => GetDynamicCreationOutcome(mapping, proxyAssemblyPathsByName, targetAssemblyPathsByName, out exceptionType, out exceptionMessage) == DynamicCreationOutcome.Failed;

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
                    // type's name: one AssemblyName rejects (e.g. "__StaticArrayInitTypeSize=3") fails in every process.
                    case FileLoadException fileLoadException when fileLoadException.FileName?.StartsWith("Datadog.DuckType", StringComparison.Ordinal) == true:
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

            if (string.Equals(failure.DiagnosticCode, StatusCodeUnloadableProxyType, StringComparison.Ordinal))
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

            if (detail.IndexOf("belongs to value type", StringComparison.Ordinal) >= 0 ||
                detail.StartsWith("Modifying struct members is not supported.", StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypeStructMembersCannotBeChangedException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("does not expose any writable public fields", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeDuckCopyStructDoesNotContainsAnyField).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeMissingMethod, StringComparison.Ordinal) &&
                detail.StartsWith("The property or field '", StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypePropertyOrFieldNotFoundException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("Ambiguous target method match", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeTargetMethodAmbiguousMatchException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeIncompatibleSignature, StringComparison.Ordinal) &&
                IsReadonlyFieldFailure(failure.Detail ?? string.Empty))
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
        /// <param name="generatedReverseTarget">The generated reverse proxy type the proxy is created for, instead of the target.</param>
        /// <returns>The result produced by this operation.</returns>
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
            ICollection<string> emissionWarnings,
            TypeDef? generatedReverseTarget = null)
        {
            var isReverseMapping = mapping.Mode == DuckTypeAotMappingMode.Reverse;

            // Array targets ("System.Int32[]", recorded like any other runtime type) and array delegation types of reverse
            // proxies have no type definition: they're bound through their runtime type, with the members of System.Array, which
            // is what dynamic duck typing sees.
            // An array proxy type (e.g. a mapping recorded for the duck chaining of an array member): dynamic duck typing can't
            // create a proxy of it, and the registry replays its failure.
            if (DuckTypeAotNameHelpers.IsArrayTypeName(mapping.ProxyTypeName))
            {
                return EmitArrayProxyTypeMapping(moduleDef, bootstrapType, initializeMethod, importedMembers, mapping, mappingIndex, proxyAssemblyPathsByName, targetAssemblyPathsByName);
            }

            if (DuckTypeAotNameHelpers.IsArrayTypeName(mapping.TargetTypeName))
            {
                return EmitArrayTargetMapping(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyModulesByAssemblyName,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
            }

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
                    emissionWarnings,
                    generatedReverseTarget);
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
                // A generated reverse proxy type deriving from (or implementing) the mapped target is the instance instead.
                generatedReverseTarget ?? ImportTypeDefOrRefCached(moduleDef, targetType, $"target type '{targetType.FullName}'"),
                generatedReverseTarget?.ToTypeSig() ?? ImportTypeSigCached(moduleDef, targetType.ToTypeSig(), $"target type signature '{targetType.FullName}'"),
                generatedReverseTarget?.IsValueType ?? targetType.IsValueType,
                closedGenericTargetTypeArguments: null,
                importedProxyContractType: null,
                importedProxyContractTypeSig: null,
                closedGenericProxyTypeArguments: null,
                proxyAssemblyPathsByName,
                targetAssemblyPathsByName,
                emissionWarnings);
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
            // Decided from the types of the mapping, before its members are bound (the member selection reads it).
            _ = UsesMetadataOnlyDuckAttributes(mapping, proxyType, targetType);

            // Value-type proxy definitions are DuckCopy projections and must follow the dedicated struct-copy emitter.
            if (proxyType.IsValueType)
            {
                if (isReverseMapping)
                {
                    var reverseValueTypeFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
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
                        importedTargetType,
                        reverseValueTypeFailure,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings,
                        importedProxyContractType);

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
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    importedTargetType,
                    importedTargetTypeSig,
                    targetIsValueType,
                    closedGenericTargetTypeArguments,
                    importedProxyContractType,
                    importedProxyContractTypeSig);

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
                        importedTargetType,
                        structCopyResult,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings,
                        importedProxyContractType);
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

            // An array delegation type is bound against the System.Array definition, which is abstract: array types aren't.
            if (isReverseMapping && (targetType.IsInterface || targetType.IsAbstract) && !DuckTypeAotNameHelpers.IsArrayTypeName(mapping.TargetTypeName))
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
                    importedTargetType,
                    reverseProxyImplementorFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
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
                    targetType,
                    importedTargetType,
                    failure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
                return failure!;
            }

            StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
            RecordRuntimeSpecificBindings(mapping, targetType, bindings.Select(binding => (IMemberDef?)binding.TargetMethod ?? binding.TargetField));

            IMethod? baseCtorToCall = importedMembers.ObjectCtor;
            // Match dynamic proxies: call a parameterless base constructor when one exists.
            if (!isInterfaceProxy)
            {
                var baseConstructor = FindSupportedProxyBaseConstructor(proxyType);
                // Dynamic proxies skip the base call when no parameterless constructor exists.
                baseCtorToCall = baseConstructor is null
                                     ? null
                                     : ImportMethodCached(moduleDef, baseConstructor, $"base constructor for proxy '{proxyType.FullName}'");
            }

            // Keep successful metadata plans subject to the dynamic engine's current validation contract.
            // This catches binder ambiguity and unsafe open generic signatures before IL is emitted.
            var dynamicValidationFailure = ValidateMappingWithDynamicEngine(moduleDef, mapping, proxyType, targetType, proxyAssemblyPathsByName, targetAssemblyPathsByName, out var evaluatedByDynamicEngine);
            if (dynamicValidationFailure is not null)
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
                    importedTargetType,
                    dynamicValidationFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
                return dynamicValidationFailure;
            }

            // When dynamic duck typing can't tell (see above), and the generator can't load the proxy type it generates either (see
            // ValidateGeneratedRegistry, which lets the runtime decide), detect from metadata the proxy types that can't be loaded.
            if (!evaluatedByDynamicEngine &&
                !CanLoadMappingTypes(mapping) &&
                TryGetUnloadableProxyTypeFailure(mapping, proxyType, isInterfaceProxy, out var unloadableProxyTypeFailure, out var unloadableProxyTypeReason))
            {
                emissionWarnings.Add($"Proxy type for mapping '{mapping.Key}' can't be created: {unloadableProxyTypeReason}.");
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    importedTargetType,
                    unloadableProxyTypeFailure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
                return unloadableProxyTypeFailure!;
            }

            // A proxy type a previous pass generated but the runtime can't load (see ValidateGeneratedRegistry).
            var registrationKey = string.Concat(mapping.Key, "|", importedTargetType.FullName);
            if (unloadableGeneratedProxyTypes?.TryGetValue(registrationKey, out var loadFailure) == true)
            {
                emissionWarnings.Add($"Proxy type for mapping '{mapping.Key}' (target '{importedTargetType.FullName}') can't be loaded: {loadFailure}");
                var unloadableFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnloadableProxyType,
                    CreateDuckTypeCreationFailureMessage(importedTargetType.ReflectionFullName, mapping.ProxyTypeName));
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    importedTargetType,
                    unloadableFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType,
                    predictedInnerException: [new KeyValuePair<string, string>(typeof(TypeLoadException).FullName!, loadFailure!)]);
                return unloadableFailure;
            }

            var generatedTypeName = $"DuckTypeProxy_{mappingIndex:D4}_{ComputeStableShortHash(mapping.Key)}";
            var resolvedProxyContractType = importedProxyContractType ?? ImportTypeDefOrRefCached(moduleDef, proxyType, $"proxy contract type '{proxyType.FullName}'");
            var resolvedProxyContractTypeSig = importedProxyContractTypeSig ?? ImportTypeSigCached(moduleDef, proxyType.ToTypeSig(), $"proxy type signature '{proxyType.FullName}'");
            if (!isInterfaceProxy &&
                baseCtorToCall?.DeclaringType is ITypeDefOrRef baseConstructorDeclaringType &&
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
                    targetType,
                    importedTargetType,
                    reverseCustomAttributeFailure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
                return reverseCustomAttributeFailure!;
            }

            // The proxy of an array type also serves the other array types, and the proxy of a type of the core library its
            // non-public types (see DuckTypeAotEngine.TryGetFallbackResult): it receives the runtime type, which it reports as
            // IDuckType.Type like the proxy dynamic duck typing creates for that type. A proxy of an array type binds the members
            // of System.Array, and stores the instance as one.
            var isArrayTarget = !isReverseMapping && IsArrayTargetMapping(mapping);
            var servesFallbackTypes = !isReverseMapping && (isArrayTarget ? !IsSystemArrayTargetMapping(mapping) : HasRuntimeInternalSubtypes(targetType));
            var systemArrayType = moduleDef.CorLibTypes.GetTypeRef("System", "Array");
            var instanceSig = isArrayTarget ? new ClassSig(systemArrayType) : importedTargetTypeSig;
            var targetField = new FieldDefUser("_currentInstance", new FieldSig(instanceSig), FieldAttributes.Private | FieldAttributes.InitOnly);
            generatedType.Fields.Add(targetField);
            FieldDef? reportedTargetTypeField = null;
            if (servesFallbackTypes)
            {
                reportedTargetTypeField = new FieldDefUser("_targetType", new FieldSig(importedMembers.SystemTypeSig), FieldAttributes.Private | FieldAttributes.InitOnly);
                generatedType.Fields.Add(reportedTargetTypeField);
            }

            var generatedConstructorSig = reportedTargetTypeField is null
                                              ? MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, instanceSig)
                                              : MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, instanceSig, importedMembers.SystemTypeSig);
            var generatedConstructor = new MethodDefUser(
                ".ctor",
                generatedConstructorSig,
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            generatedConstructor.Body = new CilBody();

            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(targetField));
            if (reportedTargetTypeField is not null)
            {
                // Null for the registered target type itself.
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_2.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(reportedTargetTypeField));
            }

            // Class proxy base constructors can call virtual members, so store the target first to match dynamic ducktyping.
            if (!emitInterfaceStructProxy && baseCtorToCall is not null)
            {
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseCtorToCall));
            }

            generatedConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedType.Methods.Add(generatedConstructor);

            // The instance can be a reverse proxy type of this registry, whose ToString dynamic duck typing inspects.
            var targetToString = (importedTargetType is TypeDef generatedTargetType ? FindPublicToString(generatedTargetType) : null) ?? FindPublicToString(targetType);
            EmitIDuckTypeImplementation(moduleDef, generatedType, importedTargetType, targetField, importedMembers, targetIsValueType, targetToString, targetType, closedGenericTargetTypeArguments, reportedTargetTypeField);
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

                // Like the methods dynamic duck typing defines, the methods of a proxy have no custom modifiers (e.g. the modreq of
                // an init accessor or an `in` parameter): they override (or implement) the methods with the same name and signature,
                // so a member with custom modifiers isn't implemented.
                var generatedMethodSig = CreateGeneratedProxyMethodSig(moduleDef, proxyMethod, GetProxyMethodTypeArguments(proxyType, targetType, proxyMethod, isInterfaceProxy, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments), interfaceMethodContract);
                var generatedMethod = new MethodDefUser(
                    proxyMethod.Name,
                    WithoutCustomModifiers(generatedMethodSig),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    isInterfaceProxy ? GetInterfaceMethodAttributes(proxyMethod) : GetClassOverrideMethodAttributes(proxyMethod, isReverseMapping));

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
                                GetClassMemberGenericTypeArguments(targetType, targetMethod.DeclaringType, closedGenericTargetTypeArguments));

                            var calledTargetMethod = GetTargetCallOpCode(moduleDef, targetType, targetMethod, targetIsValueType) == OpCodes.Callvirt
                                                         ? GetPortableVirtualCallTarget(targetType, targetMethod)
                                                         : targetMethod;
                            var importedTargetMethod = ImportMethodDefOrRefCached(moduleDef, calledTargetMethod, $"target method '{calledTargetMethod.FullName}'");
                            var targetMethodToCall = CreateMethodCallTarget(
                                moduleDef,
                                importedTargetMethod,
                                ImportMemberDeclaringType(moduleDef, targetType, calledTargetMethod.DeclaringType, closedGenericTargetTypeArguments),
                                generatedMethod,
                                methodBinding.ClosedGenericMethodArguments);
                            if (!targetMethod.IsStatic && targetIsValueType && (targetMethod.IsVirtual || targetMethod.DeclaringType.IsInterface))
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                                generatedMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(targetMethodToCall));
                            }
                            else
                            {
                                generatedMethod.Body.Instructions.Add(GetTargetCallOpCode(moduleDef, targetType, targetMethod, targetIsValueType).ToInstruction(targetMethodToCall));
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
                            var importedTargetMemberField = ImportTargetField(moduleDef, binding.TargetField!, targetType, closedGenericTargetTypeArguments);
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
                            var importedTargetMemberField = ImportTargetField(moduleDef, binding.TargetField!, targetType, closedGenericTargetTypeArguments);
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

                // Like dynamic duck typing, a reverse proxy's property is named after the delegation's property, with the type of the
                // property it implements.
                var implementationProperty = isReverseMapping && binding.Kind == ForwardBindingKind.Method && binding.TargetMethod is { } implementationAccessor
                                                 ? FindPropertyFromAccessor(implementationAccessor)
                                                 : null;
                EnsureInterfacePropertyMetadata(moduleDef, generatedType, proxyMethod, generatedMethod, closedGenericProxyTypeArguments, generatedInterfaceProperties, implementationProperty);
            }

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(resolvedProxyContractTypeSig, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            if (reportedTargetTypeField is not null)
            {
                activatorMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            }

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
            // Bootstrap registration stays object-based while preserving a typed activator method for normal execution paths.
            EmitActivatorInstanceCast(registrationActivatorMethod.Body, importedMembers, importedTargetType, targetIsValueType);
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
            EmitTypedActivatorDelegate(moduleDef, bootstrapType, initializeMethod.Body, importedMembers, mappingIndex, resolvedProxyContractTypeSig, importedTargetType, targetIsValueType, activatorMethod, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(isReverseMapping ? importedMembers.RegisterTypedReverseProxyMethod : importedMembers.RegisterTypedProxyMethod));

            if (reportedTargetTypeField is not null)
            {
                var fallbackActivatorMethod = new MethodDefUser(
                    $"ActivateFallbackProxy_{mappingIndex:D4}",
                    MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object, importedMembers.SystemTypeSig),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
                fallbackActivatorMethod.Body = new CilBody();
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Castclass.ToInstruction(isArrayTarget ? systemArrayType : importedTargetType));
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(generatedConstructor));
                if (emitInterfaceStructProxy)
                {
                    fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(generatedType));
                }

                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                bootstrapType.Methods.Add(fallbackActivatorMethod);
                EmitFallbackRegistration(initializeMethod.Body, importedMembers, resolvedProxyContractType, importedTargetType, generatedType, fallbackActivatorMethod);
            }

            _currentExecutionContext?.GeneratedProxyTypes.Add(new KeyValuePair<string, string>(registrationKey, generatedType.ReflectionFullName));
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
        /// <param name="generatedReverseTarget">The generated reverse proxy type the proxy is created for, instead of the target.</param>
        /// <returns>The result produced by this operation.</returns>
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
            ICollection<string> emissionWarnings,
            TypeDef? generatedReverseTarget = null)
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

            if (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) || proxyRuntimeType is null)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy closed generic type '{mapping.ProxyTypeName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            if (!TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) || targetRuntimeType is null)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target closed generic type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            // Like dynamic duck typing, a proxy is created even when the target already implements the proxy contract
            // (only the generic CreateCache<T>.Create short-circuits that case, in both engines): it honors the [Duck]
            // renames and exposes IDuckType.
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

            // A generated reverse proxy type deriving from (or implementing) the mapped target is the instance of the proxy instead.
            var importedClosedTargetType = generatedReverseTarget ?? ImportRuntimeTypeCached(moduleDef, closedTargetRuntimeType, $"closed generic target type '{mapping.TargetTypeName}'");
            var importedClosedTargetTypeSig = generatedReverseTarget?.ToTypeSig() ?? ImportRuntimeTypeSig(moduleDef, closedTargetRuntimeType);
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
                generatedReverseTarget?.IsValueType ?? closedTargetRuntimeType.IsValueType,
                closedGenericTargetTypeArguments,
                importedClosedProxyType,
                importedClosedProxyTypeSig,
                closedGenericProxyTypeArguments,
                proxyAssemblyPathsByName,
                targetAssemblyPathsByName,
                emissionWarnings);
        }

        /// <summary>
        /// Determines whether the generator can load the proxy and target types of a mapping, so the runtime can tell whether the
        /// proxy type it generates loads (see ValidateGeneratedRegistry).
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <returns>true if both types load in the generator; otherwise, false.</returns>
        private static bool CanLoadMappingTypes(DuckTypeAotMapping mapping)
            => runtimeTypeResolutionAssemblyPathsByName is { } assemblyPaths &&
               assemblyPaths.TryGetValue(DuckTypeAotNameHelpers.NormalizeAssemblyName(mapping.ProxyAssemblyName), out var proxyAssemblyPath) &&
               assemblyPaths.TryGetValue(DuckTypeAotNameHelpers.NormalizeAssemblyName(mapping.TargetAssemblyName), out var targetAssemblyPath) &&
               TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) &&
               TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) &&
               proxyRuntimeType is not null &&
               targetRuntimeType is not null;

        /// <summary>
        /// Detects, from metadata, proxy types that can't be loaded: a sealed base type, a static abstract interface member, or
        /// an abstract member of a forward proxy ignored with [DuckIgnore] that nothing implements (reverse proxies implement
        /// those too). Reflection.Emit fails to create such a type in dynamic duck typing, and a generated one would make the
        /// whole registry fail to load. Only used when the generator can't ask dynamic duck typing itself (types it can't load,
        /// contracts with their own duck attributes).
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The proxy type value (the base type of reverse proxies).</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <param name="failure">The failure to register, with the dynamic duck typing message.</param>
        /// <param name="reason">Why the type can't be created.</param>
        /// <returns>true if the proxy type can't be created; otherwise, false.</returns>
        private static bool TryGetUnloadableProxyTypeFailure(
            DuckTypeAotMapping mapping,
            TypeDef proxyType,
            bool isInterfaceProxy,
            out DuckTypeAotMappingEmissionResult? failure,
            out string? reason)
        {
            failure = null;
            reason = null;
            var hierarchy = GetProxyTypeHierarchy(proxyType, isInterfaceProxy);
            if (!isInterfaceProxy && proxyType.IsSealed)
            {
                reason = $"its base type '{proxyType.FullName}' is sealed";
            }
            else if (isInterfaceProxy && hierarchy.Keys.SelectMany(type => type.Methods).FirstOrDefault(method => method.IsStatic && method.IsAbstract) is { } staticAbstractMethod)
            {
                reason = $"the static abstract member '{staticAbstractMethod.Name}' has no implementation";
            }
            else if (mapping.Mode == DuckTypeAotMappingMode.Forward && TryGetUnimplementedIgnoredMember(hierarchy, isInterfaceProxy, out var ignoredMemberName))
            {
                reason = $"the abstract member '{ignoredMemberName}' is ignored with [DuckIgnore], so it has no implementation";
            }
            else if (mapping.Mode == DuckTypeAotMappingMode.Forward &&
                     _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyType).ImplementedAccessors is { } implementedAccessors &&
                     TryGetUnimplementedAbstractAccessor(proxyType, hierarchy, implementedAccessors, isInterfaceProxy, out var unimplementedAccessor))
            {
                reason = $"the abstract accessor '{unimplementedAccessor!.Name}' isn't one of a property dynamic duck typing implements, so it has no implementation";
            }
            else if (mapping.Mode == DuckTypeAotMappingMode.Forward &&
                     !isInterfaceProxy &&
                     _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyType).ImplementedAccessors.FirstOrDefault(accessor => IsFinalMethodDynamicDuckTypingOverrides(proxyType, accessor)) is { } finalAccessor)
            {
                // E.g. the implementation of an interface member, or a sealed override: the override dynamic duck typing defines
                // for it can't be created.
                reason = $"the accessor '{finalAccessor.Name}' of a property dynamic duck typing implements is final, so it can't be overridden";
            }
            else
            {
                return false;
            }

            // The message of the DuckTypeException dynamic duck typing throws.
            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                StatusCodeUnloadableProxyType,
                CreateDuckTypeCreationFailureMessage(mapping.TargetTypeName, mapping.ProxyTypeName));
            return true;
        }

        /// <summary>
        /// Determines whether the method dynamic duck typing defines for an implemented accessor overrides a final method, which
        /// fails: it has the name and the signature of the accessor, without custom modifiers, and is virtual without NewSlot, so
        /// it overrides the most derived virtual method with that name and exact signature (an accessor with custom modifiers,
        /// e.g. an init setter, has another signature).
        /// </summary>
        /// <param name="proxyType">The class proxy type.</param>
        /// <param name="accessor">The implemented accessor.</param>
        /// <returns>true if the overridden method is final; otherwise, false.</returns>
        private static bool IsFinalMethodDynamicDuckTypingOverrides(TypeDef proxyType, MethodDef accessor)
        {
            if (HasCustomModifiers(accessor.MethodSig))
            {
                return false;
            }

            var accessorArguments = accessor.DeclaringType is { IsInterface: false } accessorDeclaringType ? GetClassMemberGenericTypeArguments(proxyType, accessorDeclaringType, closedRootTypeArguments: null) : null;
            for (var current = proxyType; current is not null; current = current.BaseType?.ResolveTypeDef())
            {
                foreach (var method in current.Methods)
                {
                    if (method.IsVirtual &&
                        !method.IsStatic &&
                        string.Equals(method.Name, accessor.Name, StringComparison.Ordinal) &&
                        !HasCustomModifiers(method.MethodSig) &&
                        AreEffectiveMethodSignaturesEquivalent(method.MethodSig, GetClassMemberGenericTypeArguments(proxyType, current, closedRootTypeArguments: null), accessor.MethodSig, accessorArguments))
                    {
                        return method.IsFinal;
                    }
                }
            }

            return false;

            static bool HasCustomModifiers(MethodSig signature)
                => new[] { signature.RetType }.Concat(signature.Params).Any(ContainsModifier);

            static bool ContainsModifier(TypeSig? type)
                => type switch
                {
                    ModifierSig => true,
                    GenericInstSig genericInstSig => genericInstSig.GenericArguments.Any(ContainsModifier),
                    NonLeafSig nonLeafSig => ContainsModifier(nonLeafSig.Next),
                    _ => false,
                };
        }

        /// <summary>
        /// Finds an abstract accessor of a forward proxy type nothing implements: dynamic duck typing implements the accessors of
        /// the properties it selects (see ProxyTypePlan.BuildImplementedAccessors). In a class hierarchy, the most derived
        /// declaration of a vtable slot (an override, an explicit override, a re-abstraction) decides whether it has an
        /// implementation. In an interface hierarchy, an accessor is also implemented by a default implementation of a derived
        /// interface, or by an implemented accessor with the same name and signature (the runtime binds interface members by
        /// name and signature to the public methods of the proxy type).
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <param name="hierarchy">The types the proxy type is made of, with their generic arguments.</param>
        /// <param name="implementedAccessors">The accessors dynamic duck typing implements.</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <param name="unimplementedAccessor">The abstract accessor nothing implements.</param>
        /// <returns>true if an abstract accessor has no implementation; otherwise, false.</returns>
        private static bool TryGetUnimplementedAbstractAccessor(
            TypeDef proxyType,
            IReadOnlyDictionary<TypeDef, IReadOnlyList<TypeSig>?> hierarchy,
            IReadOnlyCollection<MethodDef> implementedAccessors,
            bool isInterfaceProxy,
            out MethodDef? unimplementedAccessor)
        {
            unimplementedAccessor = null;
            if (isInterfaceProxy)
            {
                var implemented = new HashSet<MethodDef>(implementedAccessors, ReferenceIdentityComparer<MethodDef>.Instance);
                foreach (var method in hierarchy.Keys.SelectMany(type => type.Methods))
                {
                    if (method.IsAbstract)
                    {
                        continue;
                    }

                    foreach (var methodOverride in method.Overrides)
                    {
                        if (methodOverride.MethodDeclaration.ResolveMethodDef() is { } implementedMethod)
                        {
                            implemented.Add(implementedMethod);
                        }
                    }
                }

                foreach (var accessor in hierarchy.Keys.SelectMany(type => type.Methods))
                {
                    if (!accessor.IsAbstract || accessor.IsStatic || !ProxyTypePlan.IsAccessor(accessor) || IsImplemented(accessor))
                    {
                        continue;
                    }

                    // A re-abstraction (an abstract explicit override) needs an implementation of the member it re-abstracts.
                    if (accessor.Overrides.Count > 0 &&
                        accessor.Overrides.All(methodOverride => methodOverride.MethodDeclaration.ResolveMethodDef() is { } reabstracted && IsImplemented(reabstracted)))
                    {
                        continue;
                    }

                    unimplementedAccessor = accessor;
                    return true;
                }

                return false;

                bool IsImplemented(MethodDef accessor)
                    => implemented.Contains(accessor) ||
                       (hierarchy.TryGetValue(accessor.DeclaringType, out var accessorArguments) &&
                        implementedAccessors.Any(
                            other => string.Equals(other.Name, accessor.Name, StringComparison.Ordinal) &&
                                     hierarchy.TryGetValue(other.DeclaringType, out var otherArguments) &&
                                     AreEffectiveMethodSignaturesEquivalent(other.MethodSig, otherArguments, accessor.MethodSig, accessorArguments)));
            }

            // The most derived declaration of each slot: the types are visited from the proxy type to its base types.
            var mostDerivedDeclarations = new Dictionary<MethodDef, MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
            for (var current = proxyType; current is not null; current = current.BaseType?.ResolveTypeDef())
            {
                foreach (var method in current.Methods)
                {
                    if (!method.IsVirtual || method.IsStatic)
                    {
                        continue;
                    }

                    foreach (var slotMethod in GetImplementedVtableSlotMethods(proxyType, method))
                    {
                        if (!mostDerivedDeclarations.ContainsKey(slotMethod))
                        {
                            mostDerivedDeclarations[slotMethod] = method;
                        }
                    }
                }
            }

            var overriddenSlots = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
            var interfaceArguments = new Dictionary<TypeDef, IReadOnlyList<TypeSig>?>();
            foreach (var interfaceEntry in ProxyTypePlan.EnumerateInterfacesWithArguments(proxyType))
            {
                if (!interfaceArguments.ContainsKey(interfaceEntry.InterfaceType))
                {
                    interfaceArguments[interfaceEntry.InterfaceType] = interfaceEntry.GenericArguments;
                }
            }

            foreach (var accessor in implementedAccessors)
            {
                if (accessor.DeclaringType?.IsInterface != true)
                {
                    overriddenSlots.Add(GetVtableSlotMethod(proxyType, accessor));
                    continue;
                }

                // The public method dynamic duck typing defines for an interface accessor overrides a base virtual method with the
                // same name and signature.
                foreach (var method in mostDerivedDeclarations.Values)
                {
                    if (string.Equals(method.Name, accessor.Name, StringComparison.Ordinal) &&
                        AreEffectiveMethodSignaturesEquivalent(
                            method.MethodSig,
                            GetClassMemberGenericTypeArguments(proxyType, method.DeclaringType, closedRootTypeArguments: null),
                            accessor.MethodSig,
                            interfaceArguments.TryGetValue(accessor.DeclaringType, out var accessorInterfaceArguments) ? accessorInterfaceArguments : null))
                    {
                        overriddenSlots.Add(GetVtableSlotMethod(proxyType, method));
                    }
                }
            }

            foreach (var slot in mostDerivedDeclarations)
            {
                if (slot.Value.IsAbstract && ProxyTypePlan.IsAccessor(slot.Value) && !overriddenSlots.Contains(slot.Key))
                {
                    unimplementedAccessor = slot.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the types a proxy type is made of, with their generic arguments as seen from the proxy type: the inherited
        /// interfaces of an interface proxy, the base types of a class proxy.
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <returns>The types, with their generic arguments.</returns>
        private static Dictionary<TypeDef, IReadOnlyList<TypeSig>?> GetProxyTypeHierarchy(TypeDef proxyType, bool isInterfaceProxy)
        {
            var types = new Dictionary<TypeDef, IReadOnlyList<TypeSig>?>();
            var pending = new Stack<KeyValuePair<TypeDef, IReadOnlyList<TypeSig>?>>();
            pending.Push(new KeyValuePair<TypeDef, IReadOnlyList<TypeSig>?>(proxyType, null));
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (types.ContainsKey(current.Key))
                {
                    continue;
                }

                types.Add(current.Key, current.Value);
                var inheritedTypes = isInterfaceProxy
                                         ? current.Key.Interfaces.Select(interfaceImpl => interfaceImpl.Interface)
                                         : current.Key.BaseType is { } baseType ? new[] { baseType } : Array.Empty<ITypeDefOrRef>();
                foreach (var inheritedType in inheritedTypes)
                {
                    if (inheritedType.ResolveTypeDef() is { } inheritedTypeDef)
                    {
                        var inheritedTypeSig = SubstituteTypeAndMethodGenericTypeArguments(inheritedType.ToTypeSig(), current.Value, closedGenericMethodArguments: null);
                        pending.Push(new KeyValuePair<TypeDef, IReadOnlyList<TypeSig>?>(inheritedTypeDef, (inheritedTypeSig as GenericInstSig)?.GenericArguments.ToArray()));
                    }
                }
            }

            return types;
        }

        /// <summary>
        /// Finds an abstract member of a proxy type that is ignored with [DuckIgnore] and that nothing implements: no override
        /// in a derived proxy class, no default implementation in a derived proxy interface, and no other (not ignored) member
        /// with the same name and signature, whose implementation would also implement it.
        /// </summary>
        /// <param name="hierarchy">The types the proxy type is made of, with their generic arguments (see GetProxyTypeHierarchy).</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <param name="memberName">The name of the member.</param>
        /// <returns>true if such a member exists; otherwise, false.</returns>
        private static bool TryGetUnimplementedIgnoredMember(IReadOnlyDictionary<TypeDef, IReadOnlyList<TypeSig>?> hierarchy, bool isInterfaceProxy, out string? memberName)
        {
            memberName = null;
            var methods = hierarchy.Keys.SelectMany(type => type.Methods).ToList();
            // Default implementations of a derived proxy interface (and explicit overrides) implement the members they override.
            var implementedMethods = new HashSet<MethodDef>(methods.SelectMany(method => method.Overrides)
                                                                   .Select(methodOverride => methodOverride.MethodDeclaration.ResolveMethodDef())
                                                                   .OfType<MethodDef>());
            foreach (var method in methods)
            {
                if (!method.IsAbstract || method.IsStatic || !IsDuckIgnoreMethod(method) || implementedMethods.Contains(method))
                {
                    continue;
                }

                var implemented = methods.Any(
                    other => other != method &&
                             !other.IsStatic &&
                             string.Equals(other.Name, method.Name, StringComparison.Ordinal) &&
                             AreEffectiveMethodSignaturesEquivalent(other.MethodSig, hierarchy[other.DeclaringType], method.MethodSig, hierarchy[method.DeclaringType]) &&
                             (isInterfaceProxy ? !IsDuckIgnoreMethod(other) : !other.IsAbstract && other.IsVirtual && !other.IsNewSlot));
                if (!implemented)
                {
                    memberName = method.Name;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Emits a forward mapping whose target is an array type.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingEmissionResult EmitArrayTargetMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string> emissionWarnings)
        {
            if (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule) ||
                !proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
            }

            if ((!targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) &&
                 runtimeTypeResolutionAssemblyPathsByName?.TryGetValue(mapping.TargetAssemblyName, out targetAssemblyPath) != true) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath!, mapping.TargetTypeName, out var arrayRuntimeType) ||
                arrayRuntimeType is null ||
                !arrayRuntimeType.IsArray)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target array type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            var proxyTypeDefinitionName = mapping.ProxyTypeName;
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments = null;
            ITypeDefOrRef? importedClosedProxyType = null;
            TypeSig? importedClosedProxyTypeSig = null;
            if (DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.ProxyTypeName))
            {
                if (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) || proxyRuntimeType is null)
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.MissingProxyType,
                        StatusCodeMissingProxyType,
                        $"Proxy closed generic type '{mapping.ProxyTypeName}' was not found in '{mapping.ProxyAssemblyName}'.");
                }

                proxyTypeDefinitionName = proxyRuntimeType.GetGenericTypeDefinition().FullName!;
                closedGenericProxyTypeArguments = proxyRuntimeType.GetGenericArguments().Select(runtimeType => ImportRuntimeTypeSig(moduleDef, runtimeType)).ToArray();
                importedClosedProxyType = ImportRuntimeTypeCached(moduleDef, proxyRuntimeType, $"closed generic proxy type '{mapping.ProxyTypeName}'");
                importedClosedProxyTypeSig = ImportRuntimeTypeSig(moduleDef, proxyRuntimeType);
            }

            if (!TryResolveType(proxyModule, proxyTypeDefinitionName, out var proxyType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy type '{proxyTypeDefinitionName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            // Reflection on an array type finds the members of System.Array: bind against that definition.
            if (proxyModule.CorLibTypes.GetTypeRef("System", "Array").ResolveTypeDef() is not { } arrayTypeDefinition)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"The System.Array definition needed by target array type '{mapping.TargetTypeName}' could not be resolved.");
            }

            return EmitResolvedTypeMapping(
                moduleDef,
                bootstrapType,
                initializeMethod,
                importedMembers,
                mapping,
                mappingIndex,
                isReverseMapping: mapping.Mode == DuckTypeAotMappingMode.Reverse,
                proxyType,
                arrayTypeDefinition,
                ImportRuntimeTypeCached(moduleDef, arrayRuntimeType, $"array target type '{mapping.TargetTypeName}'"),
                ImportRuntimeTypeSig(moduleDef, arrayRuntimeType),
                targetIsValueType: false,
                closedGenericTargetTypeArguments: null,
                importedClosedProxyType,
                importedClosedProxyTypeSig,
                closedGenericProxyTypeArguments,
                proxyAssemblyPathsByName,
                targetAssemblyPathsByName,
                emissionWarnings);
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
        /// Emits the registration of a proxy's activator for the runtime types the registry can't name (see
        /// DuckTypeAotEngine.TryGetFallbackResult).
        /// </summary>
        /// <param name="body">The registration method body.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="proxyContractType">The proxy definition type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="generatedType">The generated proxy type.</param>
        /// <param name="fallbackActivatorMethod">The activator receiving the instance and the runtime type.</param>
        private static void EmitFallbackRegistration(CilBody body, ImportedMembers importedMembers, ITypeDefOrRef proxyContractType, ITypeDefOrRef targetType, ITypeDefOrRef generatedType, MethodDef fallbackActivatorMethod)
        {
            body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(proxyContractType));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(targetType));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedType));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            body.Instructions.Add(OpCodes.Ldftn.ToInstruction(fallbackActivatorMethod));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.FuncObjectTypeObjectCtor));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.RegisterAotFallbackProxyMethod));
        }

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
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingEmissionResult EmitStructCopyMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            TypeDef proxyType,
            TypeDef targetType,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ITypeDefOrRef? importedTargetTypeOverride = null,
            TypeSig? importedTargetTypeSigOverride = null,
            bool? targetIsValueTypeOverride = null,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments = null,
            ITypeDefOrRef? importedProxyTypeOverride = null,
            TypeSig? importedProxyTypeSigOverride = null)
        {
            // The fields of a closed generic [DuckCopy] struct have the types of its generic arguments, and are referenced on the
            // closed type, not on the generic definition.
            var closedGenericProxyTypeArguments = (importedProxyTypeSigOverride as GenericInstSig)?.GenericArguments.ToArray();
            var planningStopwatch = StartProfilePhase();
            if (!TryCollectStructCopyBindings(mapping, proxyType, targetType, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, out var bindings, out var failure))
            {
                StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
                return failure!;
            }

            StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
            RecordRuntimeSpecificBindings(mapping, targetType, bindings.Select(binding => (IMemberDef?)binding.SourceProperty?.GetMethod ?? binding.SourceField));

            var dynamicValidationFailure = ValidateMappingWithDynamicEngine(moduleDef, mapping, proxyType, targetType, proxyAssemblyPathsByName, targetAssemblyPathsByName, out _);
            if (dynamicValidationFailure is not null)
            {
                return dynamicValidationFailure;
            }

            var importedTargetType = importedTargetTypeOverride ?? ImportTypeDefOrRefCached(moduleDef, targetType, $"resolved mapping target type '{targetType.FullName}'");
            // A closed generic [DuckCopy] struct is registered with its closed type, not the generic type definition.
            var importedProxyType = importedProxyTypeOverride ?? ImportTypeDefOrRefCached(moduleDef, proxyType, $"resolved mapping proxy type '{proxyType.FullName}'");

            var importedTargetTypeSig = importedTargetTypeSigOverride ?? ImportTypeSigCached(moduleDef, targetType.ToTypeSig(), $"resolved mapping target signature '{targetType.FullName}'");
            var importedProxyTypeSig = importedProxyTypeSigOverride ?? ImportTypeSigCached(moduleDef, proxyType.ToTypeSig(), $"resolved mapping proxy signature '{proxyType.FullName}'");
            var targetIsValueType = targetIsValueTypeOverride ?? targetType.IsValueType;

            // Like a proxy, the copy of an array type also serves the other array types (it reads members of System.Array, from
            // the instance as one), and the copy of a type of the core library its non-public types.
            var isArrayTarget = IsArrayTargetMapping(mapping);
            var servesFallbackTypes = isArrayTarget ? !IsSystemArrayTargetMapping(mapping) : mapping.Mode == DuckTypeAotMappingMode.Forward && HasRuntimeInternalSubtypes(targetType);
            var systemArrayType = moduleDef.CorLibTypes.GetTypeRef("System", "Array");
            if (isArrayTarget)
            {
                importedTargetTypeSig = new ClassSig(systemArrayType);
            }

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
                var importedProxyField = closedGenericProxyTypeArguments is null
                                             ? ImportFieldCached(moduleDef, binding.ProxyField, $"struct copy proxy field '{binding.ProxyField.FullName}'")
                                             : ImportTargetField(moduleDef, binding.ProxyField, proxyType, closedGenericProxyTypeArguments);
                activatorMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(proxyLocal));
                EmitReadBindingValue(
                    activatorMethod.Body,
                    activatorMethod,
                    binding,
                    (body, asAddress) => body.Instructions.Add((asAddress ? OpCodes.Ldloca : OpCodes.Ldloc).ToInstruction(targetLocal)));
                activatorMethod.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(importedProxyField));
            }

            activatorMethod.Body.Instructions.Add(OpCodes.Ldloc.ToInstruction(proxyLocal));
            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            // Like dynamic duck typing, the proxy type of the registration (CreateTypeResult.ProxyType) is a struct over the target
            // implementing IDuckType, with a property reading each target member the copy reads; the activator copies them into
            // the [DuckCopy] struct.
            var generatedProxyStruct = new TypeDefUser(
                GeneratedProxyNamespace,
                $"DuckTypeProxy_{mappingIndex:D4}_{ComputeStableShortHash(mapping.Key)}",
                moduleDef.CorLibTypes.GetTypeRef("System", "ValueType"))
            {
                Attributes = TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable
            };
            generatedProxyStruct.Interfaces.Add(new InterfaceImplUser(importedMembers.IDuckTypeType));
            moduleDef.Types.Add(generatedProxyStruct);
            var instanceField = new FieldDefUser("_currentInstance", new FieldSig(importedTargetTypeSig), FieldAttributes.Private | FieldAttributes.InitOnly);
            generatedProxyStruct.Fields.Add(instanceField);
            var proxyStructConstructor = new MethodDefUser(
                ".ctor",
                MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            proxyStructConstructor.Body = new CilBody();
            proxyStructConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            proxyStructConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            proxyStructConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(instanceField));
            proxyStructConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedProxyStruct.Methods.Add(proxyStructConstructor);
            EmitIDuckTypeImplementation(moduleDef, generatedProxyStruct, importedTargetType, instanceField, importedMembers, targetIsValueType, FindPublicToString(targetType), targetType, closedGenericTargetTypeArguments);
            foreach (var binding in bindings!)
            {
                var propertyType = closedGenericProxyTypeArguments is null
                                       ? ImportTypeSigCached(moduleDef, binding.ProxyField.FieldType, $"struct copy proxy field type '{binding.ProxyField.FullName}'")
                                       : ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(binding.ProxyField.FieldType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"struct copy proxy field type '{binding.ProxyField.FullName}'");
                var getter = new MethodDefUser(
                    "get_" + binding.ProxyField.Name,
                    MethodSig.CreateInstance(propertyType),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual);
                getter.Body = new CilBody();
                EmitReadBindingValue(
                    getter.Body,
                    getter,
                    binding,
                    (body, asAddress) =>
                    {
                        body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                        body.Instructions.Add((asAddress ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(instanceField));
                    });
                getter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedProxyStruct.Methods.Add(getter);
                generatedProxyStruct.Properties.Add(new PropertyDefUser(binding.ProxyField.Name, PropertySig.CreateInstance(propertyType)) { GetMethod = getter });
            }

            _currentExecutionContext?.GeneratedProxyTypes.Add(new KeyValuePair<string, string>(string.Concat(mapping.Key, "|", importedTargetType.FullName), generatedProxyStruct.ReflectionFullName));

            var registrationActivatorMethod = new MethodDefUser(
                $"ActivateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            EmitActivatorInstanceCast(registrationActivatorMethod.Body, importedMembers, importedTargetType, targetIsValueType);
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedProxyStruct));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitTypedActivatorDelegate(moduleDef, bootstrapType, initializeMethod.Body, importedMembers, mappingIndex, importedProxyTypeSig, importedTargetType, targetIsValueType, activatorMethod, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.RegisterTypedProxyMethod));

            if (servesFallbackTypes)
            {
                var fallbackActivatorMethod = new MethodDefUser(
                    $"ActivateFallbackProxy_{mappingIndex:D4}",
                    MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object, importedMembers.SystemTypeSig),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
                fallbackActivatorMethod.Body = new CilBody();
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Castclass.ToInstruction(isArrayTarget ? systemArrayType : importedTargetType));
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                bootstrapType.Methods.Add(fallbackActivatorMethod);
                EmitFallbackRegistration(initializeMethod.Body, importedMembers, importedProxyType, importedTargetType, generatedProxyStruct, fallbackActivatorMethod);
            }

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                moduleDef.Assembly?.Name?.String ?? string.Empty,
                generatedProxyStruct.FullName);

            void EmitReadBindingValue(CilBody body, MethodDef callingMethod, StructCopyFieldBinding binding, Action<CilBody, bool> loadTarget)
            {
                if (binding.SourceKind == StructCopySourceKind.Property)
                {
                    var sourceProperty = binding.SourceProperty!;
                    var sourceGetter = sourceProperty.GetMethod!;
                    var calledSourceGetter = GetTargetCallOpCode(moduleDef, targetType, sourceGetter, targetIsValueType) == OpCodes.Callvirt
                                                 ? GetPortableVirtualCallTarget(targetType, sourceGetter)
                                                 : sourceGetter;
                    var importedSourceGetter = ImportMethodDefOrRefCached(moduleDef, calledSourceGetter, $"struct copy source getter '{calledSourceGetter.FullName}'");
                    var sourceGetterToCall = CreateMethodCallTarget(
                        moduleDef,
                        importedSourceGetter,
                        ImportMemberDeclaringType(moduleDef, targetType, calledSourceGetter.DeclaringType, closedGenericTargetTypeArguments),
                        callingMethod,
                        closedGenericMethodArguments: null);

                    if (!sourceGetter.IsStatic)
                    {
                        loadTarget(body, targetIsValueType);
                    }

                    if (!sourceGetter.IsStatic && targetIsValueType && (sourceGetter.IsVirtual || sourceGetter.DeclaringType.IsInterface))
                    {
                        body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                        body.Instructions.Add(OpCodes.Callvirt.ToInstruction(sourceGetterToCall));
                    }
                    else
                    {
                        body.Instructions.Add(GetTargetCallOpCode(moduleDef, targetType, sourceGetter, targetIsValueType).ToInstruction(sourceGetterToCall));
                    }
                }
                else
                {
                    var sourceField = binding.SourceField!;
                    var importedSourceField = ImportTargetField(moduleDef, sourceField, targetType, closedGenericTargetTypeArguments);
                    if (sourceField.IsStatic)
                    {
                        body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(importedSourceField));
                    }
                    else
                    {
                        loadTarget(body, targetIsValueType);
                        body.Instructions.Add(OpCodes.Ldfld.ToInstruction(importedSourceField));
                    }
                }

                EmitMethodReturnConversion(
                    moduleDef,
                    body,
                    binding.ReturnConversion,
                    importedMembers,
                    $"target member for struct field '{binding.ProxyField.FullName}'");
            }
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
                    returnConversion = MethodReturnConversion.WrapValueWithTypeAfterDuckChainToProxy(proxyReturnType, targetReturnType, isReverseMapping);
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
                returnConversion = MethodReturnConversion.DuckChainToProxy(proxyReturnType, targetReturnType, isReverseMapping);
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
                            context,
                            conversion.IsReverseDuckChaining);
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
                        context,
                        conversion.IsReverseDuckChaining);
                    return;
                }

                if (conversion.Kind == MethodReturnConversionKind.ExtractDuckTypeInstance)
                {
                    if (conversion.KeepsNull)
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
                    }
                    else
                    {
                        methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                        methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                    }

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
                methodBody.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(invalidCastExceptionCtor)));
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
                // Imported signatures belong to the generated module, whose resolver may not have the
                // source enum assembly. Use the runtime resolver already used to validate the conversion.
                var runtimeType = TryResolveRuntimeType(typeSig);
                if (runtimeType?.IsEnum == true && typeSig.Module is { } module)
                {
                    return RuntimeImporter(module).Import(Enum.GetUnderlyingType(runtimeType)).ToTypeSig();
                }

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
        /// Emits i duck type implementation.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="generatedType">The generated type value.</param>
        /// <param name="importedTargetType">The imported target type value.</param>
        /// <param name="targetField">The target field value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="targetIsValueType">The target is value type value.</param>
        /// <param name="targetToString">The public ToString() of the target type the proxy's ToString calls, if any.</param>
        /// <param name="targetType">The target type definition.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of a closed generic target type.</param>
        private static void EmitIDuckTypeImplementation(
            ModuleDef moduleDef,
            TypeDef generatedType,
            ITypeDefOrRef importedTargetType,
            FieldDef targetField,
            ImportedMembers importedMembers,
            bool targetIsValueType,
            MethodDef? targetToString,
            TypeDef targetType,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            FieldDef? reportedTargetTypeField = null)
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
                var returnType = OpCodes.Ret.ToInstruction();
                if (reportedTargetTypeField is not null)
                {
                    // The type the proxy is created for, when it isn't the target type of the mapping.
                    getTypeMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    getTypeMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(reportedTargetTypeField));
                    getTypeMethod.Body.Instructions.Add(OpCodes.Dup.ToInstruction());
                    getTypeMethod.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(returnType));
                    getTypeMethod.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
                }

                getTypeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
                getTypeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
                getTypeMethod.Body.Instructions.Add(returnType);
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

            // Like dynamic duck typing, ToString has the attributes of the target's public ToString() and calls it: it overrides
            // object.ToString only when that method overrides it, and it implements IDuckType.ToString when it's virtual.
            const MethodAttributes ToStringAttributes = MethodAttributes.MemberAccessMask | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask | MethodAttributes.CheckAccessOnOverride;
            if (targetToString is { } uncallableToString &&
                (uncallableToString.IsStatic || uncallableToString.MethodSig.GenParamCount > 0) &&
                generatedType.FindMethod("ToString") is null)
            {
                // A static ToString(), or a generic one: the proxy gets a ToString with its attributes, which dynamic duck typing's
                // calls the method found. A static or non-virtual one isn't called (the proxy has no ToString of its own); a virtual
                // generic one implements IDuckType.ToString (or overrides object.ToString), and calling the generic method
                // definition fails like it does for dynamic duck typing's.
                var stubMethod = new MethodDefUser(
                    "ToString",
                    uncallableToString.IsStatic ? MethodSig.CreateStatic(moduleDef.CorLibTypes.String) : MethodSig.CreateInstance(moduleDef.CorLibTypes.String),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    uncallableToString.Attributes & (ToStringAttributes | MethodAttributes.Static));
                stubMethod.Body = new CilBody();
                if (uncallableToString.IsVirtual)
                {
                    // The BadImageFormatException of the runtime (COR_E_BADIMAGEFORMAT), with its message on that platform.
                    stubMethod.Body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(unchecked((int)0x8007000B)));
                    stubMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(RuntimeImporter(moduleDef).Import(typeof(Marshal).GetMethod(nameof(Marshal.GetExceptionForHR), [typeof(int)])!)));
                    stubMethod.Body.Instructions.Add(OpCodes.Throw.ToInstruction());
                }
                else
                {
                    stubMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                    stubMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                }

                generatedType.Methods.Add(stubMethod);
            }
            else if (targetToString is not null && generatedType.FindMethod("ToString") is null)
            {
                var toStringMethod = new MethodDefUser(
                    "ToString",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.String),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    targetToString.Attributes & ToStringAttributes);
                toStringMethod.Body = new CilBody();

                // An override of object.ToString is reached through its slot. Another method (one that hides object.ToString, or an
                // override of such a method) is called directly: one of a type of the registry (a generated reverse proxy) as it
                // is, another on the closed type for a generic declaring type.
                IMethod calledToString;
                if (targetToString.IsVirtual &&
                    !targetToString.IsNewSlot &&
                    GetVtableSlotMethod(targetToString.Module == moduleDef ? targetToString.DeclaringType : targetType, targetToString).DeclaringType is { BaseType: null, IsInterface: false } slotType &&
                    string.Equals(slotType.FullName, "System.Object", StringComparison.Ordinal))
                {
                    calledToString = importedMembers.ObjectToStringMethod;
                }
                else if (targetToString.Module == moduleDef)
                {
                    calledToString = targetToString;
                }
                else
                {
                    calledToString = CreateMethodCallTarget(
                        moduleDef,
                        ImportMethodDefOrRefCached(moduleDef, targetToString, $"ToString of target type '{targetToString.DeclaringType.FullName}'"),
                        ImportMemberDeclaringType(moduleDef, targetType, targetToString.DeclaringType, closedGenericTargetTypeArguments),
                        toStringMethod,
                        closedGenericMethodArguments: null);
                }

                // Value-type targets use constrained callvirt to avoid boxing while preserving virtual dispatch semantics.
                if (targetIsValueType)
                {
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldflda.ToInstruction(targetField));
                    toStringMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(calledToString));
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
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(calledToString));
                }

                toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(toStringMethod);
            }
            else if (targetToString is null &&
                     reportedTargetTypeField is not null &&
                     generatedType.BaseType is { } baseType &&
                     generatedType.FindMethod("ToString") is null)
            {
                // A target type without a public ToString() (an interface): the proxy of the target type itself has no ToString,
                // dynamic duck typing's proxy of a class this proxy serves (see DuckTypeAotEngine.TryGetFallbackResult) has the one
                // of object.ToString (it implements IDuckType.ToString and calls the instance's). The ToString this one doesn't
                // have is the inherited one: object.ToString of the boxed value of a struct (this method doesn't override it).
                var toStringMethod = new MethodDefUser(
                    "ToString",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.String),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot);
                toStringMethod.Body = new CilBody();
                var servedTypeLabel = Instruction.Create(OpCodes.Nop);
                var hasValueLabel = Instruction.Create(OpCodes.Nop);
                toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(reportedTargetTypeField));
                toStringMethod.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(servedTypeLabel));
                toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                if (generatedType.IsValueType)
                {
                    toStringMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(generatedType));
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.ObjectToStringMethod));
                }
                else
                {
                    toStringMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(new MemberRefUser(moduleDef, "ToString", MethodSig.CreateInstance(moduleDef.CorLibTypes.String), baseType)));
                }

                toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                toStringMethod.Body.Instructions.Add(servedTypeLabel);
                toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(targetField));
                toStringMethod.Body.Instructions.Add(OpCodes.Dup.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));
                toStringMethod.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                toStringMethod.Body.Instructions.Add(hasValueLabel);
                toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.ObjectToStringMethod));
                toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(toStringMethod);
            }
        }

        /// <summary>
        /// Finds the public ToString() of a target type, like the Type.GetMethod("ToString", Type.EmptyTypes) dynamic duck typing
        /// defines the proxy's ToString from: the most derived one, generic method definitions too; a static one only when the
        /// type itself declares it; for an interface, only one the interface itself declares.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <returns>The ToString method, or null if the target type has none.</returns>
        private static MethodDef? FindPublicToString(TypeDef targetType)
        {
            for (var current = targetType; current is not null; current = current.IsInterface ? null : current.BaseType?.ResolveTypeDef())
            {
                foreach (var method in current.Methods)
                {
                    if (method.IsPublic &&
                        method.MethodSig.Params.Count == 0 &&
                        string.Equals(method.Name, "ToString", StringComparison.Ordinal) &&
                        (!method.IsStatic || ReferenceEquals(current, targetType)))
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Ensures emitted interface accessors are also represented as property metadata when possible.
        /// </summary>
        /// <param name="moduleDef">The module definition.</param>
        /// <param name="generatedType">The generated type.</param>
        /// <param name="proxyMethod">Source accessor method from the proxy definition.</param>
        /// <param name="generatedMethod">Generated accessor method on the emitted proxy type.</param>
        /// <param name="generatedPropertiesByKey">Per-type cache to avoid duplicating emitted property rows.</param>
        /// <param name="implementationProperty">For a reverse proxy, the delegation's property implementing the accessor.</param>
        private static void EnsureInterfacePropertyMetadata(
            ModuleDef moduleDef,
            TypeDef generatedType,
            MethodDef proxyMethod,
            MethodDef generatedMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IDictionary<string, PropertyDef> generatedPropertiesByKey,
            PropertyDef? implementationProperty = null)
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

                if (implementationProperty is not null)
                {
                    // Like dynamic duck typing's: the name of the delegation's property, the type of the implemented one (without
                    // index parameters).
                    propertyName = implementationProperty.Name;
                    importedPropertySig = PropertySig.CreateInstance(importedPropertySig.RetType);
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
            // Like dynamic duck typing, without NewSlot: a method implementing an Equals, GetHashCode or ToString declared by the
            // proxy interface also overrides the object method.
            var attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final;
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
        /// Gets the attributes of the method overriding a method of a class proxy or reverse proxy, like dynamic duck typing defines
        /// them: final, public (property accessors too), except the override of a protected method in a reverse proxy.
        /// </summary>
        /// <param name="proxyMethod">The overridden method.</param>
        /// <param name="isReverseMapping">Whether the proxy is a reverse proxy.</param>
        /// <returns>The attributes of the override.</returns>
        private static MethodAttributes GetClassOverrideMethodAttributes(MethodDef proxyMethod, bool isReverseMapping)
        {
            var isPropertyAccessor = proxyMethod.IsSpecialName && FindPropertyFromAccessor(proxyMethod) is not null;
            var memberAccess = isReverseMapping && !isPropertyAccessor && proxyMethod.IsFamily ? MethodAttributes.Family : MethodAttributes.Public;
            var attributes = memberAccess | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.ReuseSlot;
            return isPropertyAccessor ? attributes | MethodAttributes.SpecialName : attributes;
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

        /// <summary>
        /// Resolves the declaring class's generic arguments through the proxy's base types.
        /// </summary>
        /// <param name="proxyClassType">The proxy class type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="closedProxyTypeArguments">The closed arguments of the proxy class.</param>
        /// <returns>The arguments that close the method's declaring class.</returns>
        private static IReadOnlyList<TypeSig>? GetClassMethodGenericTypeArguments(
            TypeDef proxyClassType,
            MethodDef proxyMethod,
            IReadOnlyList<TypeSig>? closedProxyTypeArguments)
        {
            return GetClassMemberGenericTypeArguments(proxyClassType, proxyMethod.DeclaringType, closedProxyTypeArguments);
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

        /// <summary>
        /// Gets the vtable slots a virtual method of a class hierarchy implements: the one it uses, and those of the base methods
        /// it explicitly overrides (e.g. a covariant return override).
        /// </summary>
        /// <param name="rootType">The most derived type of the hierarchy.</param>
        /// <param name="method">The virtual method.</param>
        /// <returns>The methods introducing the slots.</returns>
        private static IEnumerable<MethodDef> GetImplementedVtableSlotMethods(TypeDef rootType, MethodDef method)
        {
            yield return GetVtableSlotMethod(rootType, method);
            foreach (var methodOverride in method.Overrides)
            {
                if (methodOverride.MethodDeclaration.ResolveMethodDef() is { DeclaringType.IsInterface: false } overriddenMethod)
                {
                    yield return GetVtableSlotMethod(rootType, overriddenMethod);
                }
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
        /// Determines whether an inherited generic abstract method already has a concrete override.
        /// </summary>
        /// <param name="proxyClassType">The proxy class type.</param>
        /// <param name="proxyMethod">The inherited proxy method.</param>
        /// <returns>true if a concrete override exists.</returns>
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
        /// Gets interface methods.
        /// </summary>
        /// <param name="interfaceType">The interface type value.</param>
        /// <param name="reverse">Whether the methods a reverse proxy implements are requested: like dynamic duck typing, they
        /// include the [DuckIgnore] methods, which a [DuckReverseMethod] method can still implement.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<MethodDef> GetInterfaceMethods(TypeDef interfaceType, bool reverse = false)
        {
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(interfaceType);
            return (reverse ? proxyTypePlan?.ReverseInterfaceMethods : proxyTypePlan?.InterfaceMethods) ?? Array.Empty<MethodDef>();
        }

        /// <summary>
        /// Gets class proxy methods.
        /// </summary>
        /// <param name="proxyClassType">The proxy class type value.</param>
        /// <param name="reverse">Whether the methods a reverse proxy overrides are requested: like dynamic duck typing, they
        /// include the [DuckIgnore] methods, which a [DuckReverseMethod] method can still implement.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<MethodDef> GetClassProxyMethods(TypeDef proxyClassType, bool reverse)
        {
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyClassType);
            return (reverse ? proxyTypePlan?.ReverseClassMethods : proxyTypePlan?.ClassMethods) ?? Array.Empty<MethodDef>();
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
        /// Gets the generic arguments closing the signature of a method the proxy implements: those of the proxy type, or for a
        /// [DuckInclude] method of the target type (or of a type it derives from), those of the target type.
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="proxyMethod">The method the proxy implements.</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <param name="closedGenericProxyTypeArguments">The generic arguments of a closed generic proxy type.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of a closed generic target type.</param>
        /// <returns>The generic arguments of the type declaring the method, or null.</returns>
        private static IReadOnlyList<TypeSig>? GetProxyMethodTypeArguments(
            TypeDef proxyType,
            TypeDef targetType,
            MethodDef proxyMethod,
            bool isInterfaceProxy,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            if (proxyMethod.DeclaringType is { } declaringType &&
                _currentExecutionContext?.GetOrCreateDuckIncludeMethods(targetType).Contains(proxyMethod) == true)
            {
                return GetClassMemberGenericTypeArguments(targetType, declaringType, closedGenericTargetTypeArguments);
            }

            return isInterfaceProxy ? closedGenericProxyTypeArguments : GetClassMethodGenericTypeArguments(proxyType, proxyMethod, closedGenericProxyTypeArguments);
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
        private static bool IsSupportedClassProxyMethod(MethodDef method, bool includeDuckIgnored)
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

            return includeDuckIgnored || !IsDuckIgnoreMethod(method);
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
        /// Determines whether failure detail corresponds to invalid type conversion semantics.
        /// </summary>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true when the detail indicates invalid type conversion; otherwise, false.</returns>
        private static bool IsInvalidTypeConversionFailure(string failureReason)
        {
            return failureReason.IndexOf("Type conversion is not supported", StringComparison.Ordinal) >= 0 ||
                   failureReason.StartsWith("Invalid type conversion from ", StringComparison.Ordinal);
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

        /// <summary>
        /// Extracts property name from a setter method candidate name.
        /// </summary>
        /// <param name="methodName">The method name value.</param>
        /// <returns>The resulting property name, or null when not a setter accessor.</returns>
        private static string? ExtractSetterPropertyName(string methodName)
        {
            const string setterPrefix = "set_";
            if (StringUtil.IsNullOrWhiteSpace(methodName))
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
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
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
                var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reverseCandidate in FindReverseTargetMethodCandidates(targetType, proxyMethod, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments))
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
            // The candidates come from the plan of the target type, which only exists while emitting.
            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
            if (targetTypePlan is null)
            {
                yield break;
            }

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
        }

        /// <summary>
        /// Selects a target method like Type.GetMethod with the proxy's parameter types (its default binder) does, for each name
        /// of the proxy method in order: a method with as many parameters, each of which accepts the proxy's (the same type, a
        /// type it's assignable to, object, or a wider primitive), the most specific one.
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="proxyMethodPlan">The proxy method plan.</param>
        /// <param name="closedGenericProxyTypeArguments">The generic arguments of a closed generic proxy type.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of a closed generic target type.</param>
        /// <param name="allowPrivateBaseMethodCandidates">Whether private methods of base types are candidates.</param>
        /// <param name="selectedMethod">The selected method.</param>
        /// <returns>true if the binder selects a method; false if it selects none, or several (it throws then, which the
        /// candidate scan reports as an ambiguity).</returns>
        private static bool TrySelectWithDefaultBinder(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            ProxyMethodPlan proxyMethodPlan,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            bool allowPrivateBaseMethodCandidates,
            out MethodDef? selectedMethod)
        {
            selectedMethod = null;
            var proxyMethod = proxyMethodPlan.Method;
            var proxyParameterTypes = proxyMethod.MethodSig.Params
                                                 .Select(parameter => SubstituteTypeAndMethodGenericTypeArguments(parameter, closedGenericProxyTypeArguments, closedGenericMethodArguments: null))
                                                 .ToList();
            var nameComparison = proxyMethodPlan.UseIgnoreCaseMemberMatching ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var candidatesOfName in FindForwardTargetMethodCandidates(mapping, targetType, proxyMethodPlan, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, closedGenericMethodArguments: null, allowPrivateBaseMethodCandidates)
                                                .GroupBy(candidate => candidate.NameOrdinal)
                                                .OrderBy(group => group.Key))
            {
                var name = candidatesOfName.Key < proxyMethodPlan.ForwardTargetMethodNames.Count ? proxyMethodPlan.ForwardTargetMethodNames[candidatesOfName.Key] : null;
                MethodDef? best = null;
                IReadOnlyList<TypeSig>? bestParameterTypes = null;
                var ambiguous = false;
                foreach (var candidate in candidatesOfName)
                {
                    var method = candidate.Method;
                    if (name is null ||
                        !string.Equals(method.Name.String, name, nameComparison) ||
                        method.MethodSig.GenParamCount != 0 ||
                        method.MethodSig.Params.Count != proxyParameterTypes.Count)
                    {
                        continue;
                    }

                    var declaringTypeArguments = GetClassMethodGenericTypeArguments(targetType, method, closedGenericTargetTypeArguments);
                    var parameterTypes = method.MethodSig.Params
                                               .Select(parameter => SubstituteTypeAndMethodGenericTypeArguments(parameter, declaringTypeArguments, closedGenericMethodArguments: null))
                                               .ToList();
                    if (!parameterTypes.Select((parameterType, index) => IsDefaultBinderArgumentCompatible(proxyParameterTypes[index], parameterType)).All(compatible => compatible))
                    {
                        continue;
                    }

                    if (best is null)
                    {
                        best = method;
                        bestParameterTypes = parameterTypes;
                        continue;
                    }

                    // The binder's most specific method: by parameter, then by declaring type (the most derived).
                    var specificity = CompareDefaultBinderSpecificity(parameterTypes, bestParameterTypes!, proxyParameterTypes);
                    if (specificity == 0)
                    {
                        specificity = IsAssignableFrom(best.DeclaringType, method.DeclaringType) && !IsSameMetadataType(best.DeclaringType, method.DeclaringType) ? 1 :
                                      IsAssignableFrom(method.DeclaringType, best.DeclaringType) && !IsSameMetadataType(best.DeclaringType, method.DeclaringType) ? -1 : 0;
                    }

                    if (specificity > 0)
                    {
                        best = method;
                        bestParameterTypes = parameterTypes;
                        ambiguous = false;
                    }
                    else if (specificity == 0)
                    {
                        ambiguous = true;
                    }
                }

                if (best is not null)
                {
                    selectedMethod = ambiguous ? null : best;
                    return !ambiguous;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a parameter accepts an argument of a type for the default binder of Type.GetMethod: the same type,
        /// object, a type the argument's type is assignable to, or a wider primitive (e.g. long for int). By-ref parameters need
        /// the same type.
        /// </summary>
        /// <param name="argumentType">The type of the argument (the proxy's parameter).</param>
        /// <param name="parameterType">The type of the parameter.</param>
        /// <returns>true if the binder accepts it; otherwise, false.</returns>
        private static bool IsDefaultBinderArgumentCompatible(TypeSig argumentType, TypeSig parameterType)
        {
            if (AreTypesEquivalent(argumentType, parameterType))
            {
                return true;
            }

            if (argumentType.ElementType == ElementType.ByRef || parameterType.ElementType == ElementType.ByRef ||
                argumentType.IsGenericParameter || parameterType.IsGenericParameter)
            {
                return false;
            }

            return IsObjectTypeSig(parameterType) ||
                   IsTypeAssignableFrom(parameterType, argumentType) ||
                   IsPrimitiveWidening(argumentType.ElementType, parameterType.ElementType);
        }

        /// <summary>
        /// Compares the specificity of two parameter lists for arguments of given types, like the default binder: 1 if the first
        /// is more specific, -1 if the second is, 0 if neither is.
        /// </summary>
        /// <param name="first">The first parameter list.</param>
        /// <param name="second">The second parameter list.</param>
        /// <param name="argumentTypes">The argument types.</param>
        /// <returns>The comparison.</returns>
        private static int CompareDefaultBinderSpecificity(IReadOnlyList<TypeSig> first, IReadOnlyList<TypeSig> second, IReadOnlyList<TypeSig> argumentTypes)
        {
            var firstIsMoreSpecific = false;
            var secondIsMoreSpecific = false;
            for (var i = 0; i < first.Count; i++)
            {
                if (AreTypesEquivalent(first[i], second[i]))
                {
                    continue;
                }

                if (AreTypesEquivalent(first[i], argumentTypes[i]) ||
                    IsObjectTypeSig(second[i]) ||
                    IsTypeAssignableFrom(second[i], first[i]) ||
                    IsPrimitiveWidening(first[i].ElementType, second[i].ElementType))
                {
                    firstIsMoreSpecific = true;
                }
                else if (AreTypesEquivalent(second[i], argumentTypes[i]) ||
                         IsObjectTypeSig(first[i]) ||
                         IsTypeAssignableFrom(first[i], second[i]) ||
                         IsPrimitiveWidening(second[i].ElementType, first[i].ElementType))
                {
                    secondIsMoreSpecific = true;
                }
            }

            return firstIsMoreSpecific == secondIsMoreSpecific ? 0 : firstIsMoreSpecific ? 1 : -1;
        }

        /// <summary>
        /// Determines whether a primitive type widens to another one for the default binder (e.g. int to long, float to double).
        /// </summary>
        /// <param name="source">The source element type.</param>
        /// <param name="target">The target element type.</param>
        /// <returns>true if the source widens to the target; otherwise, false.</returns>
        private static bool IsPrimitiveWidening(ElementType source, ElementType target)
            => source switch
            {
                ElementType.Char => target is ElementType.U2 or ElementType.U4 or ElementType.I4 or ElementType.U8 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.U1 => target is ElementType.Char or ElementType.U2 or ElementType.I2 or ElementType.U4 or ElementType.I4 or ElementType.U8 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.I1 => target is ElementType.I2 or ElementType.I4 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.U2 => target is ElementType.U4 or ElementType.I4 or ElementType.U8 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.I2 => target is ElementType.I4 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.U4 => target is ElementType.U8 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.I4 => target is ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.U8 or ElementType.I8 => target is ElementType.R4 or ElementType.R8,
                ElementType.R4 => target is ElementType.R8,
                _ => false,
            };

        /// <summary>
        /// Gets the failure dynamic duck typing has converting the arguments (then the return value) of a proxy method for the
        /// target method it selects: "Invalid type conversion from {source} to {target}".
        /// </summary>
        /// <param name="proxyMethod">The proxy method.</param>
        /// <param name="targetMethod">The target method.</param>
        /// <param name="closedGenericProxyTypeArguments">The generic arguments of a closed generic proxy type.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of the target method's declaring type.</param>
        /// <returns>The failure detail, or null when every conversion is supported.</returns>
        private static string? GetInvalidTypeConversionDetail(MethodDef proxyMethod, MethodDef targetMethod, IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments, IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            for (var i = 0; i < proxyMethod.MethodSig.Params.Count && i < targetMethod.MethodSig.Params.Count; i++)
            {
                var proxyParameterType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.Params[i], closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                var targetParameterType = SubstituteTypeAndMethodGenericTypeArguments(targetMethod.MethodSig.Params[i], closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
                if (proxyParameterType.ElementType != ElementType.ByRef &&
                    targetParameterType.ElementType != ElementType.ByRef &&
                    !IsDuckChainingRequired(targetParameterType, proxyParameterType) &&
                    !CanUseTypeConversion(proxyParameterType, targetParameterType))
                {
                    return $"Invalid type conversion from {GetRuntimeFullName(proxyParameterType)} to {GetRuntimeFullName(targetParameterType)}";
                }
            }

            var proxyReturnType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
            var targetReturnType = SubstituteTypeAndMethodGenericTypeArguments(targetMethod.MethodSig.RetType, closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
            if (proxyReturnType.ElementType != ElementType.Void &&
                targetReturnType.ElementType != ElementType.Void &&
                !IsDuckChainingRequired(proxyReturnType, targetReturnType) &&
                !CanUseTypeConversion(targetReturnType, proxyReturnType))
            {
                return $"Invalid type conversion from {GetRuntimeFullName(targetReturnType)} to {GetRuntimeFullName(proxyReturnType)}";
            }

            return null;

            static string GetRuntimeFullName(TypeSig type) => TryResolveRuntimeType(type)?.FullName ?? type.ReflectionFullName;
        }

        /// <summary>
        /// Determines whether candidate method parameters match configured [Duck(ParameterTypeNames=...)] values.
        /// </summary>
        /// <param name="candidate">The candidate value.</param>
        /// <param name="configuredParameterTypeNames">The configured parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsForwardCandidateParameterTypeNameMatch(MethodDef candidate, IReadOnlyList<string> configuredParameterTypeNames, IReadOnlyList<TypeSig>? closedGenericTypeArguments)
        {
            if (configuredParameterTypeNames.Count != candidate.MethodSig.Params.Count)
            {
                return false;
            }

            for (var i = 0; i < configuredParameterTypeNames.Count; i++)
            {
                var configuredName = configuredParameterTypeNames[i];
                if (StringUtil.IsNullOrWhiteSpace(configuredName))
                {
                    return false;
                }

                var parameterType = SubstituteTypeAndMethodGenericTypeArguments(candidate.MethodSig.Params[i], closedGenericTypeArguments, closedGenericMethodArguments: null);
                if (!GetTypeComparisonNames(parameterType).Contains(configuredName))
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

        private static string BuildForwardMethodBindingPlanCacheKey(
            ProxyMethodPlan proxyMethodPlan,
            MethodPlan targetMethodPlan,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            bool isReverseMapping,
            bool enforceMethodSelectionRules)
        {
            return string.Concat(
                isReverseMapping ? "reverse" : "forward",
                enforceMethodSelectionRules ? "|strict|" : "|selected|",
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
        private static IEnumerable<MethodDef> FindReverseTargetMethodCandidates(
            TypeDef targetType,
            MethodDef proxyMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
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

                foreach (var reverseAttribute in GetReverseMethodAttributes(method))
                {
                    if (!IsReverseCandidateMatch(proxyMethodName, proxyParameterTypes, proxyParameterTypeNames, reverseAttribute, method.Name.String ?? method.Name.ToString()))
                    {
                        continue;
                    }

                    if (!TryGetDuckAttributeParameterTypeNames(reverseAttribute, out _) &&
                        !MatchesReverseMethodParameterSelection(
                            proxyMethod,
                            method,
                            closedGenericProxyTypeArguments,
                            GetClassMethodGenericTypeArguments(targetType, method, closedGenericTargetTypeArguments)))
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

        private static bool MatchesReverseMethodParameterSelection(
            MethodDef proxyMethod,
            MethodDef implementationMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericImplementationTypeArguments)
        {
            if (proxyMethod.MethodSig.Params.Count != implementationMethod.MethodSig.Params.Count)
            {
                return false;
            }

            for (var index = 0; index < proxyMethod.MethodSig.Params.Count; index++)
            {
                var contractParameter = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.Params[index], closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                var implementationParameter = SubstituteTypeAndMethodGenericTypeArguments(implementationMethod.MethodSig.Params[index], closedGenericImplementationTypeArguments, closedGenericMethodArguments: null);
                if (contractParameter is ByRefSig contractByRef && implementationParameter is ByRefSig implementationByRef)
                {
                    contractParameter = contractByRef.Next;
                    implementationParameter = implementationByRef.Next;
                }

                if (!MatchesDynamicMethodParameterSelectionRule(implementationParameter, contractParameter))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reads reverse method metadata with the attribute inheritance used by dynamic reverse proxies.
        /// </summary>
        /// <param name="method">The implementation method declared on the delegation type.</param>
        /// <returns>The effective reverse attributes, including attributes inherited by virtual overrides.</returns>
        private static IEnumerable<CustomAttribute> GetReverseMethodAttributes(MethodDef method)
        {
            var rootType = method.DeclaringType;
            var currentMethod = method;
            while (currentMethod is not null)
            {
                var attributes = currentMethod.CustomAttributes.Where(IsReverseMethodAttribute).ToArray();
                if (attributes.Length > 0)
                {
                    return attributes;
                }

                if (!currentMethod.IsVirtual || currentMethod.IsNewSlot)
                {
                    break;
                }

                var currentArguments = GetClassMemberGenericTypeArguments(rootType, currentMethod.DeclaringType, closedRootTypeArguments: null);
                var baseType = currentMethod.DeclaringType.BaseType?.ResolveTypeDef();
                MethodDef? baseMethod = null;
                while (baseType is not null && baseMethod is null)
                {
                    var baseArguments = GetClassMemberGenericTypeArguments(rootType, baseType, closedRootTypeArguments: null);
                    baseMethod = baseType.Methods.FirstOrDefault(candidate =>
                        candidate.IsVirtual &&
                        !candidate.IsStatic &&
                        string.Equals(candidate.Name, currentMethod.Name, StringComparison.Ordinal) &&
                        AreEffectiveMethodSignaturesEquivalent(candidate.MethodSig, baseArguments, currentMethod.MethodSig, currentArguments));
                    baseType = baseType.BaseType?.ResolveTypeDef();
                }

                currentMethod = baseMethod;
            }

            return Array.Empty<CustomAttribute>();
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
        /// Determines whether the generator's dynamic duck typing reads an attribute like the application's does: a duck typing
        /// attribute it reads by type, declared by another assembly (standalone contracts can declare their own) or by another
        /// copy of Datadog.Trace, is invisible to it. [DuckAsClass] is read by name, and other attributes (e.g. the [DuckType]
        /// of Datadog.Trace.Manual) don't matter.
        /// </summary>
        /// <param name="attributeType">The attribute type.</param>
        /// <returns>true if dynamic duck typing in the generator reads the attribute as it is meant to; otherwise, false.</returns>
        private static bool IsReadByGeneratorDuckTyping(Type attributeType)
        {
            return attributeType.Assembly == typeof(DuckType).Assembly || !IsDuckAttributeReadByType(attributeType.FullName);
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
        /// Finds the runtime method of a type (including base types and interfaces) defined by a metadata method.
        /// </summary>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <param name="method">The metadata method value.</param>
        /// <returns>The runtime method, or null when it isn't found.</returns>
        private static MethodInfo? FindRuntimeMethod(Type runtimeType, MethodDef method)
        {
            if (method.Module?.Mvid is not { } moduleVersionId)
            {
                return null;
            }

            var metadataToken = unchecked((int)method.MDToken.Raw);
            foreach (var type in EnumerateRuntimeTypeHierarchy(runtimeType))
            {
                foreach (var runtimeMethod in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (runtimeMethod.MetadataToken == metadataToken && runtimeMethod.Module.ModuleVersionId == moduleVersionId)
                    {
                        return runtimeMethod;
                    }
                }
            }

            return null;
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

        /// <summary>
        /// Finds the metadata method of a type hierarchy that defines a runtime method.
        /// </summary>
        /// <param name="type">The type definition value.</param>
        /// <param name="runtimeMethod">The runtime method value.</param>
        /// <returns>The metadata method, or null when it isn't found.</returns>
        private static MethodDef? FindMethodDefInHierarchy(TypeDef type, MethodInfo runtimeMethod)
        {
            var methodDefinition = runtimeMethod.IsGenericMethod && !runtimeMethod.IsGenericMethodDefinition
                                       ? runtimeMethod.GetGenericMethodDefinition()
                                       : runtimeMethod;
            return FindMemberDefInHierarchy(type, methodDefinition, static current => current.Methods);
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
                if (StringUtil.IsNullOrWhiteSpace(explicitInterfaceTypeName))
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

                        // Dynamic duck typing treats ExplicitInterfaceTypeName as a single interface name (no comma
                        // list, no trimming) and only an exact "*" as the wildcard, so mirror that for parity.
                        if (string.Equals(configuredName, "*", StringComparison.Ordinal))
                        {
                            useRelaxed = true;
                        }
                        else
                        {
                            explicitInterfaceTypeNamesList.Add(configuredName!);
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
        /// <param name="enforceMethodSelectionRules">
        /// true when the target method is a candidate of the metadata scan: its parameters must also satisfy the rules dynamic
        /// duck typing uses to select a method; false when dynamic duck typing selected it (Type.GetMethod and its default binder
        /// accept boxing and enum conversions): only a conversion dynamic duck typing can emit is required.
        /// </param>
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
            bool enforceMethodSelectionRules,
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
                    isReverseMapping,
                    enforceMethodSelectionRules);
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
                    if (!TryCreateForwardMethodParameterBinding(proxyMethod, targetMethod, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, closedGenericMethodArguments, parameterIndex, isReverseMapping, enforceMethodSelectionRules, out var parameterBinding, out failure))
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

                // A reverse proxy's property getter extracts the instance of the duck type the delegation's getter returns, where
                // a reverse method creates a reverse proxy of it (DuckType.CreateCache<T>.CreateReverse), like dynamic duck typing.
                if (isReverseMapping &&
                    returnConversion.Kind == MethodReturnConversionKind.DuckChainToProxy &&
                    proxyMethod.IsSpecialName &&
                    proxyMethod.Name.StartsWith("get_", StringComparison.Ordinal) &&
                    FindPropertyFromAccessor(proxyMethod) is not null)
                {
                    returnConversion = MethodReturnConversion.ExtractDuckTypeInstance(targetReturnType, proxyReturnType, keepsNull: false);
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
            bool enforceMethodSelectionRules,
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
                    if (!TryCreateMethodArgumentConversion(proxyParameterType, targetParameterType, isReverseMapping, enforceMethodSelectionRules, out var argumentConversion))
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

                if (enforceMethodSelectionRules &&
                    !MatchesByRefDynamicMethodParameterSelectionRule(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, isReverseMapping))
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
                else if (!TryCreateMethodArgumentConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, isReverseMapping, enforceMethodSelectionRules, out preCallConversion))
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

            // Dynamic duck typing compares a parameter type built on a generic parameter of the proxy method (e.g. IBox<T>) with
            // the parameter type of the target method definition, built on the target method's own generic parameter: they're
            // never the same type, nor assignable, so it duck chains the argument, unless the proxy type is a value type (not
            // [DuckCopy]) or a type of the core library.
            if (!isReverseMapping && IsBuiltOnMethodGenericParameter(proxyParameterType) && IsDuckChainedByDynamicDuckTyping(proxyParameterType))
            {
                argumentConversion = MethodArgumentConversion.ExtractDuckTypeInstance(proxyParameterType, targetParameterType);
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
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
                // Like dynamic duck typing, a reverse proxy creates a proxy of the argument for the delegation, and a forward proxy
                // extracts the instance of the duck type it receives.
                argumentConversion = isReverseMapping
                                         ? MethodArgumentConversion.DuckChainToProxy(targetParameterType, proxyParameterType)
                                         : MethodArgumentConversion.ExtractDuckTypeInstance(proxyParameterType, targetParameterType);

                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            if (enforceMethodSelectionRules &&
                !(isReverseMapping
                      ? MatchesDynamicMethodParameterSelectionRule(targetParameterType, proxyParameterType)
                      : MatchesDynamicMethodParameterSelectionRule(proxyParameterType, targetParameterType)))
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
            return isReverseMapping
                       ? MatchesDynamicMethodParameterSelectionRule(targetParameterElementType, proxyParameterElementType)
                       : MatchesDynamicMethodParameterSelectionRule(proxyParameterElementType, targetParameterElementType);
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

                // Like dynamic duck typing, the value the delegation writes is a duck type whose instance is extracted (a null one
                // throws a NullReferenceException).
                if (IsDuckChainingRequired(proxyParameterElementType, targetParameterElementType))
                {
                    returnConversion = MethodReturnConversion.ExtractDuckTypeInstance(targetParameterElementType, proxyParameterElementType, keepsNull: false);
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
        /// <param name="reverse">Whether to call the reverse factory.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateDuckTypeCreateCacheCreateMethodRef(ModuleDef moduleDef, TypeSig proxyTypeSig, bool reverse = false)
        {
            var cacheKey = BuildTypeSigCacheKey(proxyTypeSig) + (reverse ? "::reverse" : "::forward");
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
            var createMethodRef = new MemberRefUser(moduleDef, reverse ? "CreateReverse" : "Create", createMethodSig, createCacheClosedTypeSpec);
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

        /// <summary>
        /// Gets the message of the DuckTypeException dynamic duck typing throws when Reflection.Emit can't create a proxy type.
        /// </summary>
        /// <param name="targetTypeName">The reflection name of the target type.</param>
        /// <param name="proxyTypeName">The reflection name of the proxy type.</param>
        /// <returns>The message.</returns>
        private static string CreateDuckTypeCreationFailureMessage(string targetTypeName, string proxyTypeName)
            => $"{CreatingDuckTypeFailurePrefix}'{DuckTypeAotNameHelpers.ToTypeToStringName(targetTypeName)}' using proxy: '{DuckTypeAotNameHelpers.ToTypeToStringName(proxyTypeName)}'";

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
            var context = $"optional parameter '{parameterIndex.ToString(CultureInfo.InvariantCulture)}' of method '{targetMethod.FullName}'";

            // Optional by-ref parameters (e.g. `in int value = 5`) need a storage location, not a null managed pointer.
            // Roslyn wraps the by-ref of `in` parameters of virtual and interface methods in modreq(InAttribute), so look
            // through modifiers; the call itself keeps the target method's original signature.
            if (parameterType.RemoveModifiers() is ByRefSig byRefSig)
            {
                var elementType = byRefSig.Next;
                var valueLocal = new Local(ImportTypeSigCached(moduleDef, elementType, context));
                body.Variables.Add(valueLocal);
                if (TryEmitOptionalParameterValue(moduleDef, body, targetMethod, parameterIndex, elementType))
                {
                    body.Instructions.Add(OpCodes.Stloc.ToInstruction(valueLocal));
                }
                else
                {
                    body.Instructions.Add(OpCodes.Ldloca.ToInstruction(valueLocal));
                    body.Instructions.Add(OpCodes.Initobj.ToInstruction(ResolveImportedTypeForTypeToken(moduleDef, elementType, context)));
                }

                body.Instructions.Add(OpCodes.Ldloca.ToInstruction(valueLocal));
                return;
            }

            if (!TryEmitOptionalParameterValue(moduleDef, body, targetMethod, parameterIndex, parameterType))
            {
                EmitDefaultValue(moduleDef, body, parameterType, context);
            }
        }

        /// <summary>
        /// Emits the default value of an omitted optional parameter. Returns false when the value is default(T), which
        /// the caller materializes because it depends on how the value is passed.
        /// </summary>
        private static bool TryEmitOptionalParameterValue(
            ModuleDef moduleDef,
            CilBody body,
            MethodDef targetMethod,
            int parameterIndex,
            TypeSig valueType)
        {
            if (!TryGetOptionalParameterConstant(targetMethod, parameterIndex, out var constantValue))
            {
                // Like the C# compiler and dynamic duck typing, an omitted [IUnknownConstant] or [IDispatchConstant] object
                // parameter receives a wrapper of null.
#pragma warning disable CS0618 // The wrappers of VARIANT marshalling are obsolete, but still what these defaults are
                if (valueType.ElementType == ElementType.Object &&
                    targetMethod.Parameters.FirstOrDefault(parameter => parameter.MethodSigIndex == parameterIndex)?.ParamDef?.CustomAttributes is { } parameterAttributes &&
                    (parameterAttributes.IsDefined("System.Runtime.CompilerServices.IUnknownConstantAttribute") ? typeof(UnknownWrapper) :
                     parameterAttributes.IsDefined("System.Runtime.CompilerServices.IDispatchConstantAttribute") ? typeof(DispatchWrapper) : null) is { } wrapperType)
#pragma warning restore CS0618
                {
                    body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                    body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(wrapperType.GetConstructor([typeof(object)])!)));
                    return true;
                }

                // Like the C# compiler and dynamic duck typing, an omitted [Optional] object parameter without a default
                // value receives Type.Missing.
                if (valueType.ElementType == ElementType.Object)
                {
                    body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(ImportTypeMissingField(moduleDef)));
                    return true;
                }

                return false;
            }

            if (constantValue is null)
            {
                return false;
            }

            if (TryGetNullableElementType(valueType, out var nullableElementType))
            {
                // The metadata constant of a Nullable<T> parameter is the T value; wrap it instead of passing a raw T.
                if (!TryEmitConstantValue(moduleDef, body, nullableElementType!, constantValue))
                {
                    return false;
                }

                body.Instructions.Add(OpCodes.Newobj.ToInstruction(CreateNullableCtorRef(moduleDef, valueType)));
                return true;
            }

            return TryEmitConstantValue(moduleDef, body, valueType, constantValue);
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

            if (parameterType.ElementType == ElementType.Object)
            {
                TypeSig? boxedType = constantValue switch
                {
                    bool => moduleDef.CorLibTypes.Boolean,
                    char => moduleDef.CorLibTypes.Char,
                    sbyte => moduleDef.CorLibTypes.SByte,
                    byte => moduleDef.CorLibTypes.Byte,
                    short => moduleDef.CorLibTypes.Int16,
                    ushort => moduleDef.CorLibTypes.UInt16,
                    int => moduleDef.CorLibTypes.Int32,
                    uint => moduleDef.CorLibTypes.UInt32,
                    long => moduleDef.CorLibTypes.Int64,
                    ulong => moduleDef.CorLibTypes.UInt64,
                    float => moduleDef.CorLibTypes.Single,
                    double => moduleDef.CorLibTypes.Double,
                    decimal => new ValueTypeSig(moduleDef.CorLibTypes.GetTypeRef("System", "Decimal")),
                    DateTime => new ValueTypeSig(moduleDef.CorLibTypes.GetTypeRef("System", "DateTime")),
                    _ => null
                };
                if (boxedType is not null && TryEmitConstantValue(moduleDef, body, boxedType, constantValue))
                {
                    body.Instructions.Add(OpCodes.Box.ToInstruction(ResolveImportedTypeForTypeToken(moduleDef, boxedType, "boxed optional constant")));
                    return true;
                }
            }

            var underlyingType = GetUnderlyingTypeForTypeConversion(parameterType);

            // The constant of a native integer parameter (nint, nuint) is a 32-bit or 64-bit integer.
            if (underlyingType.ElementType is ElementType.I or ElementType.U && constantValue is sbyte or byte or short or ushort or int or uint or long or ulong)
            {
                body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(constantValue is ulong or uint or ushort or byte ? unchecked((long)Convert.ToUInt64(constantValue, CultureInfo.InvariantCulture)) : Convert.ToInt64(constantValue, CultureInfo.InvariantCulture)));
                body.Instructions.Add((underlyingType.ElementType == ElementType.I ? OpCodes.Conv_I : OpCodes.Conv_U).ToInstruction());
                return true;
            }

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
                // Widen explicitly: dnlib's sbyte and byte ToInstruction overloads only accept short-form opcodes.
                case char charValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)charValue));
                    return true;
                case sbyte sbyteValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)sbyteValue));
                    return true;
                case byte byteValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)byteValue));
                    return true;
                case short shortValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)shortValue));
                    return true;
                case ushort ushortValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)ushortValue));
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
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(decimalCtor)));
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
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(dateTimeCtor)));
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
            // Generic parameters can be instantiated with value types, so only known reference types can use ldnull.
            if (!typeSig.IsValueType && typeSig.RemoveModifiers() is not GenericSig)
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
        /// <param name="reverse">Whether to create a reverse proxy when the value is not already a forward proxy.</param>
        private static void EmitDuckChainToProxyConversion(
            ModuleDef moduleDef,
            CilBody body,
            TypeSig proxyTypeSig,
            TypeSig targetTypeSig,
            string context,
            bool reverse = false)
        {
            // Same shapes as dynamic duck typing (MethodIlHelper.AddIlToDuckChain): a value-type target is passed unboxed to
            // CreateCache<TProxy>.CreateFrom<TTarget>, so the proxy is created for the static type (e.g. Nullable<T> itself,
            // not the boxed T), and a null Nullable<T> creates no proxy.
            if (!reverse && targetTypeSig.IsValueType)
            {
                body.Instructions.Add(OpCodes.Call.ToInstruction(CreateDuckTypeCreateCacheCreateFromMethodRef(moduleDef, proxyTypeSig, targetTypeSig)));
                return;
            }

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
                var createCacheCreateMethodRef = CreateDuckTypeCreateCacheCreateMethodRef(moduleDef, nullableProxyElementType!, reverse);
                body.Instructions.Add(OpCodes.Call.ToInstruction(createCacheCreateMethodRef));
                body.Instructions.Add(OpCodes.Newobj.ToInstruction(nullableCtor));
                body.Instructions.Add(OpCodes.Stloc.ToInstruction(nullableResultLocal));

                body.Instructions.Add(endLabel);
                body.Instructions.Add(OpCodes.Ldloc.ToInstruction(nullableResultLocal));
                return;
            }

            var createMethodRef = CreateDuckTypeCreateCacheCreateMethodRef(moduleDef, proxyTypeSig, reverse);
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
                    if (StringUtil.IsNullOrWhiteSpace(name))
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
                return !StringUtil.IsNullOrWhiteSpace(propertyName);
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
                    if (StringUtil.IsNullOrWhiteSpace(name))
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
                !StringUtil.IsNullOrWhiteSpace(explicitMappedName))
            {
                mappedNames.Clear();
                foreach (var normalizedName in SplitDuckNames(explicitMappedName!))
                {
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
        /// Gets the instruction that calls a target member (other than a virtual member of a value type, called through a
        /// constrained callvirt) like dynamic duck typing does: callvirt for a public or generic instance method of a reference
        /// type (a NullReferenceException for a null instance, virtual dispatch), and otherwise the exact method, without a null
        /// check, which dynamic duck typing calls through its function pointer (calli). An abstract method, and a member of a
        /// reverse proxy type the registry generates (bound through the method it overrides, see FindMemberDefInHierarchy, on an
        /// instance of exactly that type), are called with callvirt.
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <param name="targetType">The target type of the proxy.</param>
        /// <param name="targetMethod">The target method or accessor.</param>
        /// <param name="targetIsValueType">Whether the target type is a value type.</param>
        /// <returns>The call instruction.</returns>
        private static OpCode GetTargetCallOpCode(ModuleDef moduleDef, TypeDef targetType, MethodDef targetMethod, bool targetIsValueType)
        {
            if (targetMethod.IsStatic || targetIsValueType)
            {
                return OpCodes.Call;
            }

            return targetMethod.IsPublic || targetMethod.HasGenericParameters || targetMethod.IsAbstract || targetType.Module == moduleDef
                       ? OpCodes.Callvirt
                       : OpCodes.Call;
        }

        /// <summary>
        /// Gets the method a virtual call to a public override declared by a non-public type of the core library goes through:
        /// the overridden public method of the closest public base type. The runtime's non-public types differ between runtimes
        /// (NativeAOT's don't declare the overrides CoreCLR's do): a virtual call through the public method dispatches to the
        /// same override on both.
        /// </summary>
        /// <param name="targetType">The target type of the proxy.</param>
        /// <param name="targetMethod">The target method or accessor, called with callvirt.</param>
        /// <returns>The method to reference in the call.</returns>
        private static MethodDef GetPortableVirtualCallTarget(TypeDef targetType, MethodDef targetMethod)
        {
            if (!targetMethod.IsVirtual ||
                targetMethod.DeclaringType is not { } declaringType ||
                !IsCoreLibraryAssemblyName(declaringType.Module?.Assembly?.Name?.String) ||
                IsVisibleTypeDefinition(declaringType))
            {
                return targetMethod;
            }

            var slotMethod = targetMethod;
            var visitedMethods = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
            while (visitedMethods.Add(slotMethod) && FindOverriddenBaseMethod(targetType, slotMethod) is { } overriddenMethod)
            {
                if (overriddenMethod.IsPublic && IsVisibleTypeDefinition(overriddenMethod.DeclaringType))
                {
                    return overriddenMethod;
                }

                slotMethod = overriddenMethod;
            }

            return targetMethod;
        }

        /// <summary>
        /// Records why a mapping is specific to the generator's runtime (see DuckTypeAotMappingEmissionResult.RuntimeSpecific):
        /// its target type, or a member its proxy binds, is a type or member of the core library that isn't public.
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="boundMembers">The target members the proxy binds.</param>
        private static void RecordRuntimeSpecificBindings(DuckTypeAotMapping mapping, TypeDef targetType, IEnumerable<IMemberDef?> boundMembers)
        {
            if (_currentExecutionContext is null)
            {
                return;
            }

            string? reason = null;
            if (IsCoreLibraryAssemblyName(targetType.Module?.Assembly?.Name?.String) && !IsVisibleTypeDefinition(targetType))
            {
                reason = $"the target type '{targetType.FullName}'";
            }
            else if (boundMembers.FirstOrDefault(member => member is not null && IsRuntimeSpecificMember(targetType, member)) is { } member)
            {
                reason = $"the member '{member.FullName}'";
            }

            if (reason is not null)
            {
                _currentExecutionContext.RuntimeSpecificMappings[mapping.Key] = reason;
            }
            else
            {
                _currentExecutionContext.RuntimeSpecificMappings.Remove(mapping.Key);
            }
        }

        /// <summary>
        /// Determines whether a bound member is a member of the core library that isn't public (a public override of a non-public
        /// type is called through the public method it overrides, see GetPortableVirtualCallTarget).
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <param name="member">The bound member.</param>
        /// <returns>true if other runtimes may not have the member; otherwise, false.</returns>
        private static bool IsRuntimeSpecificMember(TypeDef targetType, IMemberDef member)
        {
            if (member.DeclaringType is not { } declaringType || !IsCoreLibraryAssemblyName(declaringType.Module?.Assembly?.Name?.String))
            {
                return false;
            }

            return member switch
            {
                MethodDef method => !(method.IsPublic && (IsVisibleTypeDefinition(declaringType) || (method.IsVirtual && GetPortableVirtualCallTarget(targetType, method) != method))),
                FieldDef field => !(field.IsPublic && IsVisibleTypeDefinition(declaringType)),
                _ => false,
            };
        }

        /// <summary>
        /// Determines whether a type definition is public, and nested in public types.
        /// </summary>
        /// <param name="type">The type definition.</param>
        /// <returns>true if the type definition is visible outside its assembly; otherwise, false.</returns>
        private static bool IsVisibleTypeDefinition(TypeDef type)
            => type.DeclaringType is { } declaringType ? type.IsNestedPublic && IsVisibleTypeDefinition(declaringType) : type.IsPublic;

        /// <summary>
        /// Determines whether a type is built on a generic parameter of a method (e.g. IBox&lt;T&gt; or T[] in a generic method),
        /// without being one.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>true if the type is built on a generic parameter of a method; otherwise, false.</returns>
        private static bool IsBuiltOnMethodGenericParameter(TypeSig type)
        {
            return !type.IsGenericParameter && ContainsMethodGenericParameter(type);

            static bool ContainsMethodGenericParameter(TypeSig? candidate)
                => candidate switch
                {
                    GenericMVar => true,
                    GenericInstSig genericInstSig => genericInstSig.GenericArguments.Any(ContainsMethodGenericParameter),
                    NonLeafSig nonLeafSig => ContainsMethodGenericParameter(nonLeafSig.Next),
                    _ => false,
                };
        }

        /// <summary>
        /// Gets what dynamic duck typing's NeedsDuckChaining decides for a proxy type it finds neither equal nor assignable to the
        /// target type: it duck chains a [DuckCopy] struct, or a reference type that isn't a type of the core library (an array
        /// is of the module of its element type, a generic parameter of the module of its method).
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <returns>true if dynamic duck typing duck chains it; otherwise, false.</returns>
        private static bool IsDuckChainedByDynamicDuckTyping(TypeSig proxyType)
        {
            var elementType = proxyType;
            var isArray = false;
            while (elementType is SZArraySig or ArraySig)
            {
                isArray = true;
                elementType = elementType.Next;
            }

            if (elementType is GenericSig)
            {
                return true;
            }

            var definition = (elementType as GenericInstSig)?.GenericType?.TypeDefOrRef ?? elementType.ToTypeDefOrRef();
            if (definition is null)
            {
                return false;
            }

            var definitionType = definition.ResolveTypeDef();
            if (!isArray && definitionType?.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, DuckCopyAttributeTypeName, StringComparison.Ordinal)) == true)
            {
                return true;
            }

            return (isArray || definitionType?.IsValueType != true) && !IsCoreLibraryType(definition, definitionType);

            // Dynamic duck typing compares the module of the runtime type with the core library's: a type a contract references
            // through a reference assembly (e.g. List<T> through System.Collections) is a type of the core library at runtime.
            static bool IsCoreLibraryType(ITypeDefOrRef definition, TypeDef? definitionType)
            {
                if (definitionType?.Module?.Assembly?.Name?.String is { } definitionAssemblyName)
                {
                    return IsCoreLibraryAssemblyName(definitionAssemblyName);
                }

                if (TryResolveRuntimeType(definition.ToTypeSig()) is { } runtimeType)
                {
                    return runtimeType.Module == typeof(string).Module;
                }

                return IsCoreLibraryAssemblyName(DuckTypeAotNameHelpers.NormalizeAssemblyName(definition.DefinitionAssembly?.Name?.String ?? string.Empty)) ||
                       IsFrameworkImplementationType(definition);
            }
        }

        /// <summary>
        /// Determines whether duck chaining required.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsDuckChainingRequired(TypeSig targetType, TypeSig proxyType)
        {
            // Like dynamic duck typing, a generic parameter isn't duck chained, but a type built on one (e.g. IBox<T> in a generic
            // method) is decided like any other type.
            if (proxyType.IsGenericParameter || targetType.IsGenericParameter)
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

            // Same decision as dynamic duck typing whenever the runtime types can be loaded: it doesn't chain CoreLib types
            // or assignable types (including generic instantiations), whatever their shape. The metadata rules below only
            // approximate it for types the generator can't load.
            if (TryResolveRuntimeType(proxyType) is { } proxyRuntimeType &&
                TryResolveRuntimeType(targetType) is { } targetRuntimeType &&
                AreDuckCopyAttributesBoundToGenerator(proxyRuntimeType))
            {
                return DuckType.NeedsDuckChainingForAot(targetRuntimeType, proxyRuntimeType);
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
        /// Determines whether dynamic duck typing reads the [DuckCopy] attributes of a runtime proxy type (and of the type
        /// wrapped by Nullable) by identity, i.e. they come from the Datadog.Trace the generator runs.
        /// </summary>
        /// <param name="proxyRuntimeType">The runtime proxy type.</param>
        /// <returns>true if its attributes are bound to the generator's Datadog.Trace; otherwise, false.</returns>
        private static bool AreDuckCopyAttributesBoundToGenerator(Type proxyRuntimeType)
        {
            bool IsBound(Type type) => type.CustomAttributes.All(attribute => IsReadByGeneratorDuckTyping(attribute.AttributeType));

            try
            {
                return IsBound(proxyRuntimeType) && (Nullable.GetUnderlyingType(proxyRuntimeType) is not { } underlyingType || IsBound(underlyingType));
            }
            catch (Exception)
            {
                // Attributes that can't be loaded: use the metadata rules.
                return false;
            }
        }

        /// <summary>
        /// Attempts to get duck chaining proxy type.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="proxyTypeForCache">The proxy type for cache value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
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

            // Like dynamic duck typing, CoreLib types are never duck chained.
            if (typeDef.IsInterface || typeDef.IsClass)
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
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
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
            if (StringUtil.IsNullOrWhiteSpace(runtimeAssemblyPath) ||
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
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            foreach (var assemblyName in mappingResolutionResult.TargetAssemblyPathsByName.Keys)
            {
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            foreach (var assemblyName in requiredAccessCheckAssemblyNames)
            {
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            // Every other assembly the registry references: a non-public type can also be a generic argument of an imported
            // type (dynamic duck typing makes those visible as well).
            foreach (var typeRef in new MemberFinder().FindAll(moduleDef).TypeRefs.Keys)
            {
                var assemblyName = typeRef.DefinitionAssembly?.Name?.String;
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName!);
                }
            }

            // The registry itself too: like dynamic duck typing, the forward proxy of a generated reverse proxy can bind its private
            // members (e.g. [DuckField(Name = "_currentInstance")]).
            if (assemblyDef.Name?.String is { Length: > 0 } generatedAssemblyName)
            {
                _ = assemblyNames.Add(generatedAssemblyName);
            }

            foreach (var assemblyName in assemblyNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
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
            /// <param name="isReverseDuckChaining">Whether chaining calls the reverse factory.</param>
            private MethodReturnConversion(MethodReturnConversionKind kind, TypeSig? wrapperTypeSig, TypeSig? innerTypeSig, bool isReverseDuckChaining = false, bool keepsNull = true)
            {
                Kind = kind;
                WrapperTypeSig = wrapperTypeSig;
                InnerTypeSig = innerTypeSig;
                IsReverseDuckChaining = isReverseDuckChaining;
                KeepsNull = keepsNull;
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
            /// Gets whether chaining calls the reverse factory.
            /// </summary>
            internal bool IsReverseDuckChaining { get; }

            /// <summary>
            /// Gets whether extracting the instance of a null duck type gives null (otherwise, like dynamic duck typing's reverse
            /// proxies, it throws a NullReferenceException).
            /// </summary>
            internal bool KeepsNull { get; }

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
            /// <param name="reverse">Whether chaining calls the reverse factory.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion WrapValueWithTypeAfterDuckChainToProxy(TypeSig wrapperTypeSig, TypeSig sourceTypeSig, bool reverse = false)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.WrapValueWithTypeAfterDuckChainToProxy, wrapperTypeSig, sourceTypeSig, reverse);
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
            /// <param name="reverse">Whether chaining calls the reverse factory.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion DuckChainToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig, bool reverse = false)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.DuckChainToProxy, wrapperTypeSig, innerTypeSig, reverse);
            }

            /// <summary>
            /// Creates a conversion that extracts IDuckType.Instance.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig, bool keepsNull = true)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.ExtractDuckTypeInstance, wrapperTypeSig, innerTypeSig, keepsNull: keepsNull);
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
            public override TypeRef? Map(Type source)
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
                                  ? new TypeRefUser(_moduleDef, string.Empty, name, Map(declaringType))
                                  : new TypeRefUser(_moduleDef, source.Namespace ?? string.Empty, name, _datadogTraceAssemblyRef);
                    typeRef = _moduleDef.UpdateRowId(typeRef);
                    _typeRefs[source] = typeRef;
                }

                return typeRef;
            }

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

                var registerAotFallbackProxyMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotFallbackProxy),
                    new[] { typeof(Type), typeof(Type), typeof(Type), typeof(Func<object?, Type, object?>) });
                if (registerAotFallbackProxyMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotFallbackProxy(Type, Type, Type, Func<object?, Type, object?>).");
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
                    nameof(DuckType.RegisterAotProxyFailureFactory),
                    new[] { typeof(Type), typeof(Type), typeof(Func<Exception>) });
                if (registerAotProxyFailureMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotProxyFailureFactory(Type, Type, Func<Exception>).");
                }

                var registerAotReverseProxyFailureMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotReverseProxyFailureFactory),
                    new[] { typeof(Type), typeof(Type), typeof(Func<Exception>) });
                if (registerAotReverseProxyFailureMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotReverseProxyFailureFactory(Type, Type, Func<Exception>).");
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
                RegisterAotFallbackProxyMethod = importer.Import(registerAotFallbackProxyMethod);
                SystemTypeSig = importer.ImportAsTypeSig(typeof(Type));
                RegisterAotReverseProxyMethod = importer.Import(registerAotReverseProxyMethod);
                RegisterAotProxyFailureMethod = importer.Import(registerAotProxyFailureMethod);
                RegisterAotReverseProxyFailureMethod = importer.Import(registerAotReverseProxyFailureMethod);
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

        private sealed class EmitterExecutionContext
        {
            private readonly Dictionary<ModuleDef, IReadOnlyDictionary<string, TypeDef>> _typeLookupsByModule = new();
            private readonly Dictionary<string, RuntimeTypeResolutionCacheEntry> _runtimeTypesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, AssemblyResolutionCacheEntry> _preferredRuntimeAssembliesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, AssemblyResolutionCacheEntry> _resolvedRuntimeAssembliesByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, RuntimeTypeResolutionCacheEntry> _typesByName = new(StringComparer.Ordinal);
            private readonly HashSet<string> _dynamicFailureReplayKeys = new(StringComparer.Ordinal);
            private readonly Dictionary<string, IReadOnlyList<KeyValuePair<MethodInfo, MethodInfo>>?> _dynamicReverseSelections = new(StringComparer.Ordinal);
            private readonly DynamicOracleCache _dynamicOracleCache;
            private readonly HashSet<string> _appliedFailureProbeKeys = new(StringComparer.Ordinal);
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
        private sealed class DynamicOracleCache
        {
            internal Dictionary<string, FailureProbeCacheEntry> FailureProbes { get; } = new(StringComparer.Ordinal);

            internal HashSet<Tuple<Type, object>> CreatableForwardProxyShapes { get; } = new();
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
                        if (!StringUtil.IsNullOrWhiteSpace(simpleName))
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
                            !GetReverseMethodAttributes(method).Any())
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
            }

            internal MethodDef Method { get; }

            internal string MethodName { get; }

            internal bool IsInherited { get; }
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

        internal sealed class InterfaceTraversalEntry
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
                var implementedAccessors = BuildImplementedAccessors(proxyType);
                ImplementedAccessors = implementedAccessors;
                var interfaceMethods = BuildInterfaceMethods(proxyType, includeDuckIgnored: false, implementedAccessors);
                var reverseInterfaceMethods = BuildInterfaceMethods(proxyType, includeDuckIgnored: true, implementedAccessors: null);
                InterfaceMethods = interfaceMethods.Methods;
                ReverseInterfaceMethods = reverseInterfaceMethods.Methods;
                var interfaceMethodContracts = new Dictionary<MethodDef, InterfaceMethodContract>(ReferenceIdentityComparer<MethodDef>.Instance);
                foreach (var contract in reverseInterfaceMethods.ContractsByMethod.Concat(interfaceMethods.ContractsByMethod))
                {
                    interfaceMethodContracts[contract.Key] = contract.Value;
                }

                _interfaceMethodContractsByMethod = interfaceMethodContracts;
                ClassMethods = BuildClassMethods(proxyType, includeDuckIgnored: false, implementedAccessors);
                ReverseClassMethods = BuildClassMethods(proxyType, includeDuckIgnored: true, implementedAccessors: null);
                SupportedBaseConstructor = BuildSupportedBaseConstructor(proxyType);
                _propertiesByAccessorMethod = BuildAccessorPropertyMap(proxyType);
            }

            internal IReadOnlyList<MethodDef> InterfaceMethods { get; }

            internal IReadOnlyList<MethodDef> ReverseInterfaceMethods { get; }

            /// <summary>
            /// Gets the property accessors a forward proxy implements (see BuildImplementedAccessors).
            /// </summary>
            internal IReadOnlyCollection<MethodDef> ImplementedAccessors { get; }

            internal IReadOnlyList<MethodDef> ClassMethods { get; }

            internal IReadOnlyList<MethodDef> ReverseClassMethods { get; }

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

            private static InterfaceMethodCollection BuildInterfaceMethods(TypeDef interfaceType, bool includeDuckIgnored, HashSet<MethodDef>? implementedAccessors)
            {
                var results = new List<MethodDef>();
                var contractsByMethod = new Dictionary<MethodDef, InterfaceMethodContract>(ReferenceIdentityComparer<MethodDef>.Instance);
                var visitedMethods = new HashSet<string>(StringComparer.Ordinal);

                // A property of a generic interface implemented twice (IGen<int>, IGen<string>) is selected once, by name, for the
                // first instance (see BuildImplementedAccessors): its other accessors have no implementation, like in dynamic duck
                // typing's proxy type.
                var addedImplementedAccessors = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);

                // The proxy interface, then its interfaces like Type.GetInterfaces() lists them: of two methods with the same
                // signature, dynamic duck typing implements the first one.
                var interfaces = new[] { new InterfaceTraversalEntry(interfaceType, interfaceType, interfaceType.ToTypeSig(), isProxyType: true, genericArguments: null) }
                    .Concat(EnumerateInterfacesWithArguments(interfaceType));
                foreach (var current in interfaces)
                {
                    var currentType = current.InterfaceType;
                    if (!IsDuckTypeInfrastructureInterface(currentType))
                    {
                        foreach (var method in currentType.Methods)
                        {
                            // Like dynamic duck typing, private (C# 8 helper) and final interface members aren't implemented, nor are
                            // the non-public members of the inherited interfaces (it lists their methods with GetMethods()).
                            if (method.IsConstructor || method.IsStatic || method.IsPrivate || method.IsFinal || (!current.IsProxyType && !method.IsPublic) ||
                                (!includeDuckIgnored && IsDuckIgnoreMethod(method)) ||
                                (implementedAccessors is not null && IsAccessor(method) && (!implementedAccessors.Contains(method) || !addedImplementedAccessors.Add(method))))
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
                }

                return new InterfaceMethodCollection(results, contractsByMethod);
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

            private static IReadOnlyList<MethodDef> BuildClassMethods(TypeDef proxyClassType, bool includeDuckIgnored, HashSet<MethodDef>? implementedAccessors)
            {
                var results = new List<MethodDef>();
                var visitedMethodKeys = new HashSet<string>(StringComparer.Ordinal);
                var visitedSlots = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                var implementedAccessorIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
                var current = proxyClassType;

                while (current is not null)
                {
                    foreach (var method in current.Methods)
                    {
                        // Like Type.GetMethods(), only the most derived method of a vtable slot counts, even when dynamic duck typing
                        // doesn't implement it (e.g. a sealed override hides the virtual method it overrides).
                        if (method.IsVirtual && !method.IsStatic && !visitedSlots.Add(GetVtableSlotMethod(proxyClassType, method)))
                        {
                            continue;
                        }

                        // Dynamic duck typing binds and implements both accessors of the properties it selects, whatever their
                        // access, even a final one (then it fails to create the proxy type, see TryGetUnloadableProxyTypeFailure).
                        var isImplementedAccessor = implementedAccessors is not null && IsAccessor(method) && implementedAccessors.Contains(method);
                        if (isImplementedAccessor
                                ? method.IsStatic || (!includeDuckIgnored && IsDuckIgnoreMethod(method))
                                : !IsSupportedClassProxyMethod(method, includeDuckIgnored) || (implementedAccessors is not null && IsAccessor(method)))
                        {
                            continue;
                        }

                        var methodTypeArguments = GetClassMethodGenericTypeArguments(proxyClassType, method, null);
                        var key = string.Concat(method.Name, "::", BuildEffectiveMethodSignatureKey(method.MethodSig, methodTypeArguments));
                        if (visitedMethodKeys.Add(key))
                        {
                            if (isImplementedAccessor)
                            {
                                implementedAccessorIndexes[key] = results.Count;
                            }

                            results.Add(method);
                        }
                        else if (isImplementedAccessor && implementedAccessorIndexes.TryGetValue(key, out var index))
                        {
                            // Two properties dynamic duck typing implements (e.g. `new virtual T Value` over `virtual T Value`, both
                            // closed with the same type) get two methods with the same name and signature: the runtime overrides
                            // the most derived method of that signature with the one defined last, for the base type's property.
                            results[index] = method;
                        }
                    }

                    current = current.BaseType?.ResolveTypeDef();
                }

                if (implementedAccessors is not null)
                {
                    // The accessors of the interface properties dynamic duck typing implements too (see BuildImplementedAccessors): the
                    // public method it defines for each one overrides a base virtual method with the same name and signature. A
                    // property of a generic interface implemented twice (IGen<int>, IGen<string>) is selected once, by name, for the
                    // first instance Type.GetInterfaces() lists.
                    var appendedInterfaceAccessors = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                    foreach (var interfaceEntry in EnumerateInterfacesWithArguments(proxyClassType))
                    {
                        foreach (var method in interfaceEntry.InterfaceType.Methods)
                        {
                            if (!implementedAccessors.Contains(method) || !appendedInterfaceAccessors.Add(method))
                            {
                                continue;
                            }

                            var resolvedMethod = CreateInterfaceMethodForTraversal(method, interfaceEntry.GenericArguments);
                            if (visitedMethodKeys.Add(string.Concat(resolvedMethod.Name, "::", BuildEffectiveMethodSignatureKey(resolvedMethod.MethodSig, closedGenericTargetTypeArguments: null))))
                            {
                                results.Add(resolvedMethod);
                            }
                        }
                    }
                }

                return results;
            }

            /// <summary>
            /// Enumerates the interfaces a type implements like Type.GetInterfaces() lists them (the interface map the runtime
            /// builds): those of the base type first, then each interface the type declares followed by its own interfaces, depth
            /// first in declaration order, skipping the ones already listed. Each one comes with its generic arguments as seen from
            /// the type: a generic interface implemented twice (IGen&lt;int&gt;, IGen&lt;string&gt;) is listed twice.
            /// </summary>
            /// <param name="type">The type.</param>
            /// <returns>The interfaces, with their generic arguments.</returns>
            internal static IEnumerable<InterfaceTraversalEntry> EnumerateInterfacesWithArguments(TypeDef type)
            {
                var hierarchy = new List<TypeDef>();
                for (var current = type; current is not null; current = current.BaseType?.ResolveTypeDef())
                {
                    hierarchy.Add(current);
                }

                var visitedKeys = new HashSet<string>(StringComparer.Ordinal);
                var pending = new Stack<InterfaceTraversalEntry>();
                for (var i = hierarchy.Count - 1; i >= 0; i--)
                {
                    var current = hierarchy[i];
                    PushInterfaces(current, GetClassMemberGenericTypeArguments(type, current, closedRootTypeArguments: null));
                    while (pending.Count > 0)
                    {
                        // The same interface reached through references of other assemblies (e.g. a facade) is the same: the key
                        // doesn't include the assemblies of the generic arguments.
                        var entry = pending.Pop();
                        var key = entry.GenericArguments is null
                                      ? entry.InterfaceType.FullName
                                      : string.Concat(entry.InterfaceType.FullName, "::", string.Join("|", entry.GenericArguments.Select(argument => argument.FullName)));
                        if (!visitedKeys.Add(key))
                        {
                            continue;
                        }

                        yield return entry;
                        PushInterfaces(entry.InterfaceType, entry.GenericArguments);
                    }
                }

                void PushInterfaces(TypeDef declaringType, IReadOnlyList<TypeSig>? typeArguments)
                {
                    // Pushed in reverse, so the interfaces are visited in declaration order.
                    foreach (var interfaceImpl in declaringType.Interfaces.Reverse())
                    {
                        if (ResolveInterfaceTypeDefForTraversal(declaringType, interfaceImpl.Interface) is { } interfaceType)
                        {
                            var interfaceTypeSig = interfaceImpl.Interface.ToTypeSig();
                            pending.Push(new InterfaceTraversalEntry(
                                interfaceType,
                                interfaceImpl.Interface,
                                interfaceTypeSig,
                                isProxyType: false,
                                ResolveInterfaceGenericArguments(interfaceTypeSig, typeArguments)));
                        }
                    }
                }
            }

            /// <summary>
            /// Gets the property accessors a forward proxy implements, like dynamic duck typing: those of the properties
            /// Type.GetProperties() returns for the proxy type, then those of its interfaces whose name isn't taken yet. It doesn't
            /// implement event accessors.
            /// </summary>
            /// <param name="proxyType">The proxy type.</param>
            /// <returns>The accessors.</returns>
            private static HashSet<MethodDef> BuildImplementedAccessors(TypeDef proxyType)
            {
                var accessors = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                var selectedNames = new HashSet<string>(StringComparer.Ordinal);
                if (proxyType.IsInterface)
                {
                    // All the public properties of the interface itself.
                    foreach (var property in proxyType.Properties)
                    {
                        if (IsPublicProperty(property))
                        {
                            selectedNames.Add(property.Name);
                            AddAccessors(property);
                        }
                    }
                }
                else
                {
                    // Type.GetProperties() of a class, from the most derived type: a property is hidden by a more derived one
                    // using its vtable slot (an override), or with the same name and signature (not substituted: a property of
                    // a generic base type isn't hidden by one of the closed type). Inherited properties whose accessors are all
                    // private don't count. Dynamic duck typing implements the virtual ones.
                    var usedSlots = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                    var signatures = new HashSet<string>(StringComparer.Ordinal);
                    for (var current = proxyType; current is not null; current = current.BaseType?.ResolveTypeDef())
                    {
                        foreach (var property in current.Properties)
                        {
                            if (!ReferenceEquals(current, proxyType) && property.GetMethod?.IsPrivate != false && property.SetMethod?.IsPrivate != false)
                            {
                                continue;
                            }

                            var slotAccessor = property.GetMethod is { IsPublic: true } publicGetter ? publicGetter : property.SetMethod is { IsPublic: true } publicSetter ? publicSetter : null;
                            if (slotAccessor is { IsVirtual: true } && !usedSlots.Add(GetVtableSlotMethod(proxyType, slotAccessor)))
                            {
                                continue;
                            }

                            var signature = property.PropertySig;
                            if (!signatures.Add(string.Concat(property.Name, "|", signature?.HasThis == true ? "instance" : "static", "|", string.Join(",", signature is null ? [] : new[] { signature.RetType }.Concat(signature.Params).Select(GetRawSignatureTypeName)))))
                            {
                                continue;
                            }

                            if (IsPublicProperty(property) && (property.GetMethod?.IsVirtual == true || property.SetMethod?.IsVirtual == true))
                            {
                                selectedNames.Add(property.Name);
                                AddAccessors(property, inherited: !ReferenceEquals(current, proxyType));
                            }
                        }
                    }
                }

                // Then the properties of the interfaces, in Type.GetInterfaces() order, whose name isn't taken (AddInterfaceProperties).
                foreach (var interfaceType in EnumerateInterfacesForTraversal(proxyType))
                {
                    foreach (var property in interfaceType.Properties)
                    {
                        if (IsPublicProperty(property) && selectedNames.Add(property.Name))
                        {
                            AddAccessors(property);
                        }
                    }
                }

                return accessors;

                static bool IsPublicProperty(PropertyDef property) => property.GetMethod?.IsPublic == true || property.SetMethod?.IsPublic == true;

                // Like the signature comparison of reflection: generic parameters by position, the other types by name (whatever
                // the assembly reference spelling them).
                static string GetRawSignatureTypeName(TypeSig? type) => type switch
                {
                    null => string.Empty,
                    GenericSig genericSig => string.Concat(genericSig.IsMethodVar ? "!!" : "!", genericSig.Number.ToString(CultureInfo.InvariantCulture)),
                    GenericInstSig genericInstSig => string.Concat(genericInstSig.GenericType?.FullName, "<", string.Join(",", genericInstSig.GenericArguments.Select(GetRawSignatureTypeName)), ">"),
                    ArraySig arraySig => string.Concat(GetRawSignatureTypeName(arraySig.Next), "[", arraySig.Rank.ToString(CultureInfo.InvariantCulture), "]"),
                    ModifierSig modifierSig => string.Concat(GetRawSignatureTypeName(modifierSig.Next), modifierSig is CModReqdSig ? " modreq(" : " modopt(", modifierSig.Modifier?.FullName, ")"),
                    NonLeafSig nonLeafSig => string.Concat(nonLeafSig.ElementType.ToString(), "(", GetRawSignatureTypeName(nonLeafSig.Next), ")"),
                    _ => type.FullName,
                };

                // Reflection doesn't see the private accessors of an inherited property (it can't be read or written through them).
                void AddAccessors(PropertyDef property, bool inherited = false)
                {
                    if (property.GetMethod is { } getter && !(inherited && getter.IsPrivate))
                    {
                        accessors.Add(getter);
                    }

                    if (property.SetMethod is { } setter && !(inherited && setter.IsPrivate))
                    {
                        accessors.Add(setter);
                    }
                }
            }

            /// <summary>
            /// Enumerates the interface types a type implements like Type.GetInterfaces() lists them, each generic interface
            /// once (see EnumerateInterfacesWithArguments).
            /// </summary>
            /// <param name="type">The type.</param>
            /// <returns>The interfaces.</returns>
            private static IEnumerable<TypeDef> EnumerateInterfacesForTraversal(TypeDef type)
            {
                var visitedTypes = new HashSet<TypeDef>();
                foreach (var entry in EnumerateInterfacesWithArguments(type))
                {
                    if (visitedTypes.Add(entry.InterfaceType))
                    {
                        yield return entry.InterfaceType;
                    }
                }
            }

            internal static bool IsAccessor(MethodDef method)
                => method.IsSpecialName && (method.IsGetter || method.IsSetter || method.IsAddOn || method.IsRemoveOn || method.IsFire);

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
                    if (StringUtil.IsNullOrWhiteSpace(propertyName))
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
