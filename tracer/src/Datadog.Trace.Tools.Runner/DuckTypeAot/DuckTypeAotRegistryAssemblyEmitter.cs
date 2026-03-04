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
            var generatedAssemblyName = options.AssemblyName ?? Path.GetFileNameWithoutExtension(artifactPaths.OutputAssemblyPath);
            var deterministicMvid = ComputeDeterministicMvid(generatedAssemblyName, mappingResolutionResult.Mappings);
            var generatedAssemblyVersion = new Version(1, 0, 0, 0);
            var generatedAssemblyFullName = new AssemblyName(generatedAssemblyName) { Version = generatedAssemblyVersion }.FullName ?? generatedAssemblyName;

            var assemblyDef = new AssemblyDefUser(generatedAssemblyName, generatedAssemblyVersion);
            var moduleDef = new ModuleDefUser(Path.GetFileName(artifactPaths.OutputAssemblyPath), deterministicMvid)
            {
                Kind = ModuleKind.Dll
            };
            assemblyDef.Modules.Add(moduleDef);

            var importedMembers = new ImportedMembers(moduleDef);
            var assemblyReferences = new Dictionary<string, AssemblyRef>(StringComparer.OrdinalIgnoreCase);
            var mappingResults = new Dictionary<string, DuckTypeAotMappingEmissionResult>(StringComparer.Ordinal);

            foreach (var proxyAssemblyPath in mappingResolutionResult.ProxyAssemblyPathsByName.Values.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                AddAssemblyReference(moduleDef, assemblyReferences, proxyAssemblyPath);
            }

            foreach (var targetAssemblyPath in mappingResolutionResult.TargetAssemblyPathsByName.Values.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                AddAssemblyReference(moduleDef, assemblyReferences, targetAssemblyPath);
            }

            var datadogTraceAssemblyPath = typeof(Datadog.Trace.Tracer).Assembly.Location;
            var datadogTraceAssemblyVersion = typeof(Datadog.Trace.Tracer).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
            var datadogTraceAssemblyMvid = ResolveAssemblyMvid(datadogTraceAssemblyPath);
            _ = AddAssemblyReference(moduleDef, assemblyReferences, datadogTraceAssemblyPath);
            AddIgnoresAccessChecksToAttributes(assemblyDef, moduleDef, importedMembers.IgnoresAccessChecksToAttributeCtor, mappingResolutionResult);

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

            var proxyModulesByAssemblyName = LoadModules(mappingResolutionResult.ProxyAssemblyPathsByName);
            var targetModulesByAssemblyName = LoadModules(mappingResolutionResult.TargetAssemblyPathsByName);
            try
            {
                var sortedMappings = mappingResolutionResult.Mappings.OrderBy(mapping => mapping.Key, StringComparer.Ordinal).ToList();
                for (var i = 0; i < sortedMappings.Count; i++)
                {
                    var mapping = sortedMappings[i];
                    mappingResults[mapping.Key] = EmitMapping(
                        moduleDef,
                        bootstrapType,
                        initializeMethod,
                        importedMembers,
                        mapping,
                        i + 1,
                        proxyModulesByAssemblyName,
                        targetModulesByAssemblyName,
                        mappingResolutionResult.ProxyAssemblyPathsByName,
                        mappingResolutionResult.TargetAssemblyPathsByName);
                }
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

            return new DuckTypeAotRegistryEmissionResult(registryInfo, mappingResults);
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
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                return false;
            }

            if (string.Equals(scenarioId, ParityScenarioIdRt2, StringComparison.Ordinal))
            {
                return string.Equals(status, DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature, StringComparison.Ordinal);
            }

            if (string.Equals(scenarioId, ParityScenarioIdE39, StringComparison.Ordinal) ||
                string.Equals(scenarioId, ParityScenarioIdE40, StringComparison.Ordinal))
            {
                return string.Equals(status, DuckTypeAotCompatibilityStatuses.MissingTargetMethod, StringComparison.Ordinal);
            }

            if (string.Equals(scenarioId, ParityScenarioIdE42, StringComparison.Ordinal))
            {
                return string.Equals(status, DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind, StringComparison.Ordinal);
            }

            return false;
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
        /// <returns>true when a failure registration was emitted; otherwise, false.</returns>
        private static bool TryEmitKnownFailureRegistration(
            ModuleDef moduleDef,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            TypeDef proxyType,
            ITypeDefOrRef targetType,
            DuckTypeAotMappingEmissionResult failure,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName)
        {
            ITypeDefOrRef? exceptionType = null;
            if (TryResolveDynamicFailureExceptionType(
                    moduleDef,
                    mapping,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    out var dynamicFailureExceptionType))
            {
                exceptionType = moduleDef.Import(dynamicFailureExceptionType!) as ITypeDefOrRef;
            }

            if (exceptionType is null &&
                !TryResolveFailureExceptionType(moduleDef, failure, out exceptionType))
            {
                return false;
            }

            var importedProxyType = moduleDef.Import(proxyType) as ITypeDefOrRef
                                    ?? throw new InvalidOperationException($"Unable to import proxy type '{proxyType.FullName}' for failure registration.");
            var importedTargetType = moduleDef.Import(targetType) as ITypeDefOrRef
                                     ?? throw new InvalidOperationException($"Unable to import target type '{targetType.FullName}' for failure registration.");

            EmitFailureRegistration(
                initializeMethod,
                importedMembers,
                mapping.Mode,
                importedProxyType,
                importedTargetType,
                exceptionType!);

            return true;
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
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMappingMode mode,
            ITypeDefOrRef proxyType,
            ITypeDefOrRef targetType,
            ITypeDefOrRef exceptionType)
        {
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(proxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(targetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(exceptionType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(
                OpCodes.Call.ToInstruction(
                    mode == DuckTypeAotMappingMode.Reverse
                        ? importedMembers.RegisterAotReverseProxyFailureMethod
                        : importedMembers.RegisterAotProxyFailureMethod));
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
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            out Type? exceptionType)
        {
            exceptionType = null;
            if (moduleDef is null ||
                mapping is null ||
                DynamicForwardDryRunFactory is null ||
                DynamicReverseDryRunFactory is null ||
                CreateTypeResultCanCreateMethod is null ||
                CreateTypeResultProxyTypeProperty is null)
            {
                return false;
            }

            if (!proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath) ||
                !targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath))
            {
                return false;
            }

            if (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) ||
                proxyRuntimeType is null ||
                targetRuntimeType is null)
            {
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
                return false;
            }

            return TryExtractDynamicFailureExceptionType(dryRunResult, out exceptionType);
        }

        /// <summary>
        /// Attempts to extract the captured failure exception type from DuckType.CreateTypeResult.
        /// </summary>
        /// <param name="dryRunResult">The dry run result value.</param>
        /// <param name="exceptionType">The exception type value.</param>
        /// <returns>true when a failure exception type was extracted; otherwise, false.</returns>
        private static bool TryExtractDynamicFailureExceptionType(object? dryRunResult, out Type? exceptionType)
        {
            exceptionType = null;
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
                return true;
            }
            catch (Exception ex)
            {
                exceptionType = ex.GetType();
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
        private static bool TryResolveFailureExceptionType(
            ModuleDef moduleDef,
            DuckTypeAotMappingEmissionResult failure,
            out ITypeDefOrRef? exceptionType)
        {
            exceptionType = null;
            var detail = failure.Detail ?? string.Empty;

            if (string.Equals(failure.DiagnosticCode, StatusCodeUnsupportedProxyKind, StringComparison.Ordinal) &&
                detail.IndexOf("Reverse proxy type", StringComparison.Ordinal) >= 0)
            {
                exceptionType = moduleDef.Import(typeof(DuckTypeReverseProxyBaseIsStructException)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodePropertyCantBeWritten, StringComparison.Ordinal))
            {
                exceptionType = moduleDef.Import(typeof(DuckTypePropertyCantBeWrittenException)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeCustomAttributeNamedArguments, StringComparison.Ordinal))
            {
                exceptionType = moduleDef.Import(typeof(DuckTypeCustomAttributeHasNamedArgumentsException)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (detail.IndexOf("cannot be abstract or interface", StringComparison.Ordinal) >= 0)
            {
                exceptionType = moduleDef.Import(typeof(DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (detail.IndexOf("marked with [DuckReverseMethod]", StringComparison.Ordinal) >= 0)
            {
                exceptionType = detail.IndexOf("Proxy property", StringComparison.Ordinal) >= 0
                                    ? moduleDef.Import(typeof(DuckTypeIncorrectReversePropertyUsageException)) as ITypeDefOrRef
                                    : moduleDef.Import(typeof(DuckTypeIncorrectReverseMethodUsageException)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (detail.IndexOf("belongs to value type", StringComparison.Ordinal) >= 0)
            {
                exceptionType = moduleDef.Import(typeof(DuckTypeStructMembersCannotBeChangedException)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (detail.IndexOf("does not expose any writable public fields", StringComparison.Ordinal) >= 0)
            {
                exceptionType = moduleDef.Import(typeof(DuckTypeDuckCopyStructDoesNotContainsAnyField)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (detail.IndexOf("Ambiguous target method match", StringComparison.Ordinal) >= 0)
            {
                exceptionType = moduleDef.Import(typeof(DuckTypeTargetMethodAmbiguousMatchException)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeMissingMethod, StringComparison.Ordinal))
            {
                exceptionType = moduleDef.Import(typeof(DuckTypeTargetMethodNotFoundException)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeFieldIsReadonly, StringComparison.Ordinal) ||
                (string.Equals(failure.DiagnosticCode, StatusCodeIncompatibleSignature, StringComparison.Ordinal) &&
                 IsReadonlyFieldFailure(failure.Detail ?? string.Empty)))
            {
                exceptionType = moduleDef.Import(typeof(DuckTypeFieldIsReadonlyException)) as ITypeDefOrRef;
                return exceptionType is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeIncompatibleSignature, StringComparison.Ordinal))
            {
                if (IsReturnTypeFailure(detail))
                {
                    exceptionType = moduleDef.Import(typeof(DuckTypeProxyAndTargetMethodReturnTypeMismatchException)) as ITypeDefOrRef;
                    return exceptionType is not null;
                }

                if (IsInvalidTypeConversionFailure(detail))
                {
                    exceptionType = moduleDef.Import(typeof(DuckTypeInvalidTypeConversionException)) as ITypeDefOrRef;
                    return exceptionType is not null;
                }

                if (IsParameterSignatureFailure(detail))
                {
                    exceptionType = moduleDef.Import(typeof(DuckTypeProxyAndTargetMethodParameterSignatureMismatchException)) as ITypeDefOrRef;
                    return exceptionType is not null;
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
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName)
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
                    targetAssemblyPathsByName);
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
                        initializeMethod,
                        importedMembers,
                        mapping,
                        proxyType,
                        targetType,
                        reverseValueTypeFailure,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName);

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
                        initializeMethod,
                        importedMembers,
                        mapping,
                        proxyType,
                        targetType,
                        structCopyResult,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName);
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
                    initializeMethod,
                    importedMembers,
                    mapping,
                    proxyType,
                    targetType,
                    reverseProxyImplementorFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName);
                return reverseProxyImplementorFailure;
            }

            var isInterfaceProxy = proxyType.IsInterface;
            var isDuckAsClassInterface = isInterfaceProxy && HasDuckAsClassAttribute(proxyType);
            // This branch controls default allocation behavior for all interface mappings:
            // struct by default (parity/perf), class only when explicitly requested via [DuckAsClass].
            var emitInterfaceStructProxy = isInterfaceProxy && !isDuckAsClassInterface;
            if (!TryCollectForwardBindings(mapping, proxyType, targetType, closedGenericTargetTypeArguments: null, isInterfaceProxy, out var bindings, out var failure))
            {
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    proxyType,
                    targetType,
                    failure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName);
                return failure!;
            }

            IMethod baseCtorToCall = importedMembers.ObjectCtor;
            // Class-based proxy contracts keep inheritance semantics by requiring a callable parameterless base ctor.
            if (!isInterfaceProxy)
            {
                var baseConstructor = FindSupportedProxyBaseConstructor(proxyType);
                if (baseConstructor is null)
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedProxyConstructor,
                        StatusCodeUnsupportedProxyConstructor,
                        $"Proxy class '{mapping.ProxyTypeName}' must provide a public/protected parameterless constructor.");
                }

                baseCtorToCall = moduleDef.Import(baseConstructor);
            }

            var generatedTypeName = $"DuckTypeProxy_{mappingIndex:D4}_{ComputeStableShortHash(mapping.Key)}";
            var generatedParentType = emitInterfaceStructProxy ? moduleDef.CorLibTypes.GetTypeRef("System", "ValueType") : (isInterfaceProxy ? moduleDef.CorLibTypes.Object.TypeDefOrRef : moduleDef.Import(proxyType));
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
                generatedType.Interfaces.Add(new InterfaceImplUser(moduleDef.Import(proxyType)));
            }

            generatedType.Interfaces.Add(new InterfaceImplUser(importedMembers.IDuckTypeType));
            moduleDef.Types.Add(generatedType);

            if (isReverseMapping &&
                !TryApplyReverseTargetCustomAttributes(moduleDef, generatedType, targetType, mapping, out var reverseCustomAttributeFailure))
            {
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    proxyType,
                    targetType,
                    reverseCustomAttributeFailure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName);
                return reverseCustomAttributeFailure!;
            }

            var importedTargetType = moduleDef.Import(targetType);
            var importedTargetTypeSig = moduleDef.Import(targetType.ToTypeSig());
            var targetField = new FieldDefUser("_instance", new FieldSig(importedTargetTypeSig), FieldAttributes.Private | FieldAttributes.InitOnly);
            generatedType.Fields.Add(targetField);

            var generatedConstructor = new MethodDefUser(
                ".ctor",
                MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            generatedConstructor.Body = new CilBody();
            // Struct proxies skip base constructor chaining by runtime design; class proxies must call base .ctor.
            if (!emitInterfaceStructProxy)
            {
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseCtorToCall));
            }

            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(targetField));
            generatedConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedType.Methods.Add(generatedConstructor);

            EmitIDuckTypeImplementation(moduleDef, generatedType, importedTargetType, targetField, importedMembers, targetType.IsValueType);
            var generatedInterfaceProperties = new Dictionary<string, PropertyDef>(StringComparer.Ordinal);

            foreach (var binding in bindings!)
            {
                var proxyMethod = binding.ProxyMethod;
                var importedProxyMethod = moduleDef.Import(proxyMethod);

                var generatedMethod = new MethodDefUser(
                    proxyMethod.Name,
                    importedProxyMethod.MethodSig,
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    isInterfaceProxy ? GetInterfaceMethodAttributes(proxyMethod) : GetClassOverrideMethodAttributes(proxyMethod));

                CopyMethodGenericParameters(moduleDef, proxyMethod, generatedMethod);
                generatedMethod.Body = new CilBody();
                switch (binding.Kind)
                {
                    case ForwardBindingKind.Method:
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
                                var targetElementTypeSig = moduleDef.Import(parameterBinding.TargetByRefElementTypeSig!);
                                var targetByRefLocal = new Local(targetElementTypeSig);
                                generatedMethod.Body.Variables.Add(targetByRefLocal);
                                generatedMethod.Body.InitLocals = true;

                                if (!parameterBinding.IsOut)
                                {
                                    generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxyParameter));
                                    EmitLoadByRefValue(moduleDef, generatedMethod.Body, parameterBinding.ProxyByRefElementTypeSig!, $"proxy parameter '{proxyMethod.FullName}'");
                                    EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'");
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
                                    EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'");
                                }
                            }
                        }

                        var importedTargetMethod = moduleDef.Import(targetMethod);
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

                        break;
                    }

                    case ForwardBindingKind.FieldGet:
                    {
                        var fieldBinding = binding.FieldBinding!.Value;
                        var importedTargetMemberField = moduleDef.Import(binding.TargetField!);
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

                        break;
                    }

                    case ForwardBindingKind.FieldSet:
                    {
                        var fieldBinding = binding.FieldBinding!.Value;
                        var importedTargetMemberField = moduleDef.Import(binding.TargetField!);
                        if (binding.TargetField!.IsStatic)
                        {
                            generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                            EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");

                            generatedMethod.Body.Instructions.Add(OpCodes.Stsfld.ToInstruction(importedTargetMemberField));
                        }
                        else
                        {
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                            generatedMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                            generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                            EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");

                            generatedMethod.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(importedTargetMemberField));
                        }

                        break;
                    }

                    default:
                        throw new InvalidOperationException($"Unsupported forward binding kind '{binding.Kind}'.");
                }

                generatedMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(generatedMethod);
                EnsureInterfacePropertyMetadata(moduleDef, generatedType, proxyMethod, generatedMethod, generatedInterfaceProperties);
            }

            var importedProxyTypeSig = moduleDef.Import(proxyType.ToTypeSig());
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
                MethodSig.CreateStatic(importedProxyTypeSig, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            // Bootstrap registration stays object-based while preserving a typed activator method for normal execution paths.
            registrationActivatorMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(moduleDef.Import(proxyType)));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(registrationActivatorMethod));
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
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
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
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName)
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
                        initializeMethod,
                        importedMembers,
                        mapping,
                        proxyType,
                        importedTargetType,
                        reverseValueTypeFailure,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName);

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
                        initializeMethod,
                        importedMembers,
                        mapping,
                        proxyType,
                        importedTargetType,
                        structCopyResult,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName);
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
                    initializeMethod,
                    importedMembers,
                    mapping,
                    proxyType,
                    importedTargetType,
                    reverseProxyImplementorFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName);
                return reverseProxyImplementorFailure;
            }

            var isInterfaceProxy = proxyType.IsInterface;
            var isDuckAsClassInterface = isInterfaceProxy && HasDuckAsClassAttribute(proxyType);
            // This branch controls default allocation behavior for all interface mappings:
            // struct by default (parity/perf), class only when explicitly requested via [DuckAsClass].
            var emitInterfaceStructProxy = isInterfaceProxy && !isDuckAsClassInterface;
            if (!TryCollectForwardBindings(mapping, proxyType, targetType, closedGenericTargetTypeArguments, isInterfaceProxy, out var bindings, out var failure))
            {
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    proxyType,
                    importedTargetType,
                    failure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName);
                return failure!;
            }

            IMethod baseCtorToCall = importedMembers.ObjectCtor;
            // Class-based proxy contracts keep inheritance semantics by requiring a callable parameterless base ctor.
            if (!isInterfaceProxy)
            {
                var baseConstructor = FindSupportedProxyBaseConstructor(proxyType);
                if (baseConstructor is null)
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedProxyConstructor,
                        StatusCodeUnsupportedProxyConstructor,
                        $"Proxy class '{mapping.ProxyTypeName}' must provide a public/protected parameterless constructor.");
                }

                baseCtorToCall = moduleDef.Import(baseConstructor);
            }

            var generatedTypeName = $"DuckTypeProxy_{mappingIndex:D4}_{ComputeStableShortHash(mapping.Key)}";
            var generatedParentType = emitInterfaceStructProxy ? moduleDef.CorLibTypes.GetTypeRef("System", "ValueType") : (isInterfaceProxy ? moduleDef.CorLibTypes.Object.TypeDefOrRef : moduleDef.Import(proxyType));
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
                generatedType.Interfaces.Add(new InterfaceImplUser(moduleDef.Import(proxyType)));
            }

            generatedType.Interfaces.Add(new InterfaceImplUser(importedMembers.IDuckTypeType));
            moduleDef.Types.Add(generatedType);

            if (isReverseMapping &&
                !TryApplyReverseTargetCustomAttributes(moduleDef, generatedType, targetType, mapping, out var reverseCustomAttributeFailure))
            {
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    proxyType,
                    importedTargetType,
                    reverseCustomAttributeFailure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName);
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

            // Struct proxies skip base constructor chaining by runtime design; class proxies must call base .ctor.
            if (!emitInterfaceStructProxy)
            {
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseCtorToCall));
            }

            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(targetField));
            generatedConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedType.Methods.Add(generatedConstructor);

            EmitIDuckTypeImplementation(moduleDef, generatedType, importedTargetType, targetField, importedMembers, targetIsValueType);
            var generatedInterfaceProperties = new Dictionary<string, PropertyDef>(StringComparer.Ordinal);

            foreach (var binding in bindings!)
            {
                var proxyMethod = binding.ProxyMethod;
                var importedProxyMethod = moduleDef.Import(proxyMethod);

                var generatedMethod = new MethodDefUser(
                    proxyMethod.Name,
                    importedProxyMethod.MethodSig,
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    isInterfaceProxy ? GetInterfaceMethodAttributes(proxyMethod) : GetClassOverrideMethodAttributes(proxyMethod));

                CopyMethodGenericParameters(moduleDef, proxyMethod, generatedMethod);
                generatedMethod.Body = new CilBody();
                switch (binding.Kind)
                {
                    case ForwardBindingKind.Method:
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
                                var targetElementTypeSig = moduleDef.Import(parameterBinding.TargetByRefElementTypeSig!);
                                var targetByRefLocal = new Local(targetElementTypeSig);
                                generatedMethod.Body.Variables.Add(targetByRefLocal);
                                generatedMethod.Body.InitLocals = true;

                                if (!parameterBinding.IsOut)
                                {
                                    generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxyParameter));
                                    EmitLoadByRefValue(moduleDef, generatedMethod.Body, parameterBinding.ProxyByRefElementTypeSig!, $"proxy parameter '{proxyMethod.FullName}'");
                                    EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'");
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
                                    EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'");
                                }
                            }
                        }

                        var importedTargetMethod = moduleDef.Import(targetMethod);
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

                        break;
                    }

                    case ForwardBindingKind.FieldGet:
                    {
                        var fieldBinding = binding.FieldBinding!.Value;
                        var importedTargetMemberField = moduleDef.Import(binding.TargetField!);
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

                        break;
                    }

                    case ForwardBindingKind.FieldSet:
                    {
                        var fieldBinding = binding.FieldBinding!.Value;
                        var importedTargetMemberField = moduleDef.Import(binding.TargetField!);
                        if (binding.TargetField!.IsStatic)
                        {
                            generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                            EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");
                            generatedMethod.Body.Instructions.Add(OpCodes.Stsfld.ToInstruction(importedTargetMemberField));
                        }
                        else
                        {
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                            generatedMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                            generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                            EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");
                            generatedMethod.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(importedTargetMemberField));
                        }

                        break;
                    }

                    default:
                        throw new InvalidOperationException($"Unsupported forward binding kind '{binding.Kind}'.");
                }

                generatedMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(generatedMethod);
                EnsureInterfacePropertyMetadata(moduleDef, generatedType, proxyMethod, generatedMethod, generatedInterfaceProperties);
            }

            var importedProxyTypeSig = moduleDef.Import(proxyType.ToTypeSig());
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
                MethodSig.CreateStatic(importedProxyTypeSig, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            // Bootstrap registration stays object-based while preserving a typed activator method for normal execution paths.
            registrationActivatorMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(moduleDef.Import(proxyType)));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(registrationActivatorMethod));
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
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
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
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName)
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
                // Closed generic duck adaptation currently supports non-generic proxy contracts.
                // Closed generic proxy definitions require generic-interface/class emission and stay explicitly unsupported.
                if (proxyRuntimeType.IsGenericType)
                {
                    var detail = $"Closed generic mapping requires duck adaptation that is not emitted yet. proxy='{mapping.ProxyTypeName}', target='{mapping.TargetTypeName}'.";
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedClosedGenericMapping,
                        StatusCodeUnsupportedClosedGenericMapping,
                        detail);
                }

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

                var importedClosedTargetType = moduleDef.Import(closedTargetRuntimeType) as ITypeDefOrRef
                                               ?? throw new InvalidOperationException($"Unable to import closed generic target type '{mapping.TargetTypeName}'.");
                var importedClosedTargetTypeSig = ImportRuntimeTypeSig(moduleDef, closedTargetRuntimeType);

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
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName);
            }

            var resolvedProxyRuntimeType = proxyRuntimeType!;
            var resolvedTargetRuntimeType = targetRuntimeType!;

            var importedProxyType = moduleDef.Import(resolvedProxyRuntimeType) as ITypeDefOrRef
                ?? throw new InvalidOperationException($"Unable to import closed generic proxy type '{mapping.ProxyTypeName}'.");
            var importedTargetType = moduleDef.Import(resolvedTargetRuntimeType) as ITypeDefOrRef
                ?? throw new InvalidOperationException($"Unable to import closed generic target type '{mapping.TargetTypeName}'.");
            var importedTargetTypeSig = moduleDef.Import(importedTargetType.ToTypeSig());

            var importedProxyTypeSig = moduleDef.Import(importedProxyType.ToTypeSig());
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
                MethodSig.CreateStatic(importedProxyTypeSig, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            registrationActivatorMethod.Body.Instructions.Add((resolvedTargetRuntimeType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(registrationActivatorMethod));
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
            runtimeType = null;
            try
            {
                runtimeType = Type.GetType(typeName, throwOnError: false);
                if (runtimeType is not null)
                {
                    return true;
                }

                Assembly? candidateAssembly = null;
                foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var loadedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(loadedAssembly.GetName().Name ?? string.Empty);
                    if (string.Equals(loadedAssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        candidateAssembly = loadedAssembly;
                        break;
                    }
                }

                candidateAssembly ??= Assembly.LoadFrom(assemblyPath);
                runtimeType = candidateAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (runtimeType is not null)
                {
                    return true;
                }

                var candidateAssemblyName = candidateAssembly.GetName().Name;
                if (!string.IsNullOrWhiteSpace(candidateAssemblyName))
                {
                    runtimeType = Type.GetType($"{typeName}, {candidateAssemblyName}", throwOnError: false);
                    if (runtimeType is not null)
                    {
                        return true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(candidateAssembly.FullName))
                {
                    runtimeType = Type.GetType($"{typeName}, {candidateAssembly.FullName}", throwOnError: false);
                }
            }
            catch
            {
                // Type probing is best-effort; failures are handled by returning false to the caller.
            }

            return false;
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
            if (!TryCollectStructCopyBindings(mapping, proxyType, targetType, closedGenericTargetTypeArguments, out var bindings, out var failure))
            {
                return failure!;
            }

            var importedTargetType = importedTargetTypeOverride ??
                                     (moduleDef.Import(targetType) as ITypeDefOrRef
                                      ?? throw new InvalidOperationException($"Unable to import target type '{targetType.FullName}'."));
            var importedProxyType = moduleDef.Import(proxyType) as ITypeDefOrRef
                ?? throw new InvalidOperationException($"Unable to import proxy type '{proxyType.FullName}'.");

            var importedTargetTypeSig = importedTargetTypeSigOverride ?? moduleDef.Import(targetType.ToTypeSig());
            var importedProxyTypeSig = moduleDef.Import(proxyType.ToTypeSig());
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
                var importedProxyField = moduleDef.Import(binding.ProxyField);
                activatorMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(proxyLocal));

                if (binding.SourceKind == StructCopySourceKind.Property)
                {
                    var sourceProperty = binding.SourceProperty!;
                    var sourceGetter = sourceProperty.GetMethod!;
                    var importedSourceGetter = moduleDef.Import(sourceGetter);
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
                    var importedSourceField = moduleDef.Import(sourceField);
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
                MethodSig.CreateStatic(importedProxyTypeSig, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            registrationActivatorMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(registrationActivatorMethod));
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
            binding = default;
            failure = null;

            var hasFieldOnlyAttribute = false;
            var allowFieldFallback = false;
            var allowPrivateBaseMembers = IsFallbackToBaseTypesEnabled(proxyField.CustomAttributes);
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
                TryFindStructCopyTargetProperty(targetType, candidateNames, allowPrivateBaseMembers, out var targetProperty))
            {
                var targetPropertyType = SubstituteTypeAndMethodGenericTypeArguments(targetProperty!.PropertySig.RetType, closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
                if (!TryCreateReturnConversion(proxyField.FieldSig.Type, targetPropertyType, out var returnConversion))
                {
                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        $"Return type mismatch between proxy struct field '{proxyField.FullName}' and target property '{targetProperty.FullName}'.");
                    return false;
                }

                binding = StructCopyFieldBinding.ForProperty(proxyField, targetProperty, returnConversion);
                return true;
            }

            if (hasFieldOnlyAttribute || allowFieldFallback)
            {
                if (TryFindStructCopyTargetField(targetType, candidateNames, allowPrivateBaseMembers, out var targetField))
                {
                    var targetFieldType = SubstituteTypeAndMethodGenericTypeArguments(targetField!.FieldSig.Type, closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
                    if (!TryCreateReturnConversion(proxyField.FieldSig.Type, targetFieldType, out var returnConversion))
                    {
                        failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                            mapping,
                            DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                            StatusCodeIncompatibleSignature,
                            $"Return type mismatch between proxy struct field '{proxyField.FullName}' and target field '{targetField.FullName}'.");
                        return false;
                    }

                    binding = StructCopyFieldBinding.ForField(proxyField, targetField, returnConversion);
                    return true;
                }
            }

            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                StatusCodeMissingMethod,
                $"Target member for proxy struct field '{proxyField.FullName}' was not found.");
            return false;
        }

        /// <summary>
        /// Attempts to find struct copy target property.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="candidateNames">The candidate names value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <param name="targetProperty">The target property value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryFindStructCopyTargetProperty(TypeDef targetType, IReadOnlyList<string> candidateNames, bool allowPrivateBaseMembers, out PropertyDef? targetProperty)
        {
            foreach (var candidateName in candidateNames)
            {
                var current = targetType;
                while (current is not null)
                {
                    foreach (var property in current.Properties)
                    {
                        if (!string.Equals(property.Name, candidateName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (property.GetMethod is null || property.GetMethod.MethodSig.Params.Count != 0)
                        {
                            continue;
                        }

                        if (!allowPrivateBaseMembers &&
                            !ReferenceEquals(current, targetType) &&
                            property.GetMethod.IsPrivate)
                        {
                            continue;
                        }

                        targetProperty = property;
                        return true;
                    }

                    current = current.BaseType?.ResolveTypeDef();
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
        /// <param name="targetField">The target field value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryFindStructCopyTargetField(TypeDef targetType, IReadOnlyList<string> candidateNames, bool allowPrivateBaseMembers, out FieldDef? targetField)
        {
            foreach (var candidateName in candidateNames)
            {
                var current = targetType;
                while (current is not null)
                {
                    foreach (var field in current.Fields)
                    {
                        if (!string.Equals(field.Name, candidateName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (!allowPrivateBaseMembers &&
                            !ReferenceEquals(current, targetType) &&
                            field.IsPrivate)
                        {
                            continue;
                        }

                        targetField = field;
                        return true;
                    }

                    current = current.BaseType?.ResolveTypeDef();
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
                return true;
            }

            if (TryGetValueWithTypeArgument(proxyReturnType, out var proxyReturnValueWithTypeArgument))
            {
                var proxyInnerReturnType = proxyReturnValueWithTypeArgument!;
                if (AreTypesEquivalent(proxyInnerReturnType, targetReturnType))
                {
                    // ValueWithType<TProxy>: direct wrap when target returns TProxy.
                    returnConversion = MethodReturnConversion.WrapValueWithType(proxyReturnType, targetReturnType);
                    return true;
                }

                if (IsDuckChainingRequiredForMapping(targetReturnType, proxyInnerReturnType))
                {
                    // ValueWithType<TProxy>: dynamic mode supports duck-chaining before wrapping.
                    returnConversion = MethodReturnConversion.WrapValueWithTypeAfterDuckChainToProxy(proxyReturnType, targetReturnType);
                    return true;
                }

                if (CanUseTypeConversion(targetReturnType, proxyInnerReturnType))
                {
                    // ValueWithType<TProxy>: dynamic mode supports type conversion before wrapping.
                    returnConversion = MethodReturnConversion.WrapValueWithTypeAfterTypeConversion(proxyReturnType, targetReturnType);
                    return true;
                }

                returnConversion = default;
                return false;
            }

            if (IsDuckChainingRequiredForMapping(targetReturnType, proxyReturnType))
            {
                returnConversion = MethodReturnConversion.DuckChainToProxy(proxyReturnType, targetReturnType);
                return true;
            }

            if (CanUseTypeConversion(targetReturnType, proxyReturnType))
            {
                returnConversion = MethodReturnConversion.TypeConversion(targetReturnType, proxyReturnType);
                return true;
            }

            returnConversion = default;
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
                    var importedConstraint = moduleDef.Import(constraint.Constraint) as ITypeDefOrRef
                        ?? throw new InvalidOperationException($"Unable to import generic parameter constraint '{constraint.Constraint?.FullName}' for '{sourceMethod.FullName}'.");
                    copiedGenericParameter.GenericParamConstraints.Add(new GenericParamConstraintUser(importedConstraint));
                }

                targetMethod.GenericParameters.Add(copiedGenericParameter);
            }
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
                    closedArguments.Add(moduleDef.Import(closedGenericMethodArguments[i]));
                }

                var methodSpecWithClosedArguments = new MethodSpecUser(effectiveTargetMethod, new GenericInstMethodSig(closedArguments));
                return moduleDef.UpdateRowId(methodSpecWithClosedArguments);
            }

            if (generatedMethod.MethodSig.GenParamCount == 0)
            {
                return effectiveTargetMethod;
            }

            var genericArguments = new List<TypeSig>((int)generatedMethod.MethodSig.GenParamCount);
            for (var genericParameterIndex = 0; genericParameterIndex < generatedMethod.MethodSig.GenParamCount; genericParameterIndex++)
            {
                genericArguments.Add(new GenericMVar((uint)genericParameterIndex));
            }

            var methodSpec = new MethodSpecUser(effectiveTargetMethod, new GenericInstMethodSig(genericArguments));
            return moduleDef.UpdateRowId(methodSpec);
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
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitMethodArgumentConversion(
            ModuleDef moduleDef,
            CilBody methodBody,
            MethodArgumentConversion conversion,
            ImportedMembers importedMembers,
            string context)
        {
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
            if (typeSig.IsGenericParameter)
            {
                return null;
            }

            return typeSig.ElementType switch
            {
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
                var assemblyQualifiedName = $"{reflectionName}, {assemblyName}";
                var resolvedFromAssembly = Type.GetType(assemblyQualifiedName, throwOnError: false);
                if (resolvedFromAssembly is not null)
                {
                    return resolvedFromAssembly;
                }
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
            IDictionary<string, PropertyDef> generatedPropertiesByKey)
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
                importedPropertySig = CreateImportedPropertySig(moduleDef, proxyProperty.PropertySig);
                propertyName = proxyProperty.Name;
                propertyKey = proxyProperty.FullName;
            }
            else
            {
                // Fallback keeps metadata usable for contracts that declare accessor methods without property rows.
                if (!TryInferPropertyMetadataFromAccessor(moduleDef, proxyMethod, out propertyName, out importedPropertySig, out propertyKey))
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

            var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
            var typesToInspect = new Stack<TypeDef>();
            typesToInspect.Push(declaringType);

            while (typesToInspect.Count > 0)
            {
                var currentType = typesToInspect.Pop();
                // Graph walk includes base types and interfaces so inherited property metadata is preserved in emission.
                if (!visitedTypes.Add(currentType.FullName))
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
                var importedReturnType = moduleDef.Import(accessorMethod.MethodSig.RetType);
                var importedParameterTypes = accessorMethod.MethodSig.Params.Select(moduleDef.Import).ToArray();
                propertySig = new PropertySig(hasThis: true, importedReturnType, importedParameterTypes);
                propertyKey = $"{accessorMethod.DeclaringType?.FullName ?? string.Empty}::{propertyName}::{propertySig}";
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
            var valueType = moduleDef.Import(accessorMethod.MethodSig.Params[accessorMethod.MethodSig.Params.Count - 1]);
            var importedIndexParameters = accessorMethod.MethodSig.Params
                                            .Take(accessorMethod.MethodSig.Params.Count - 1)
                                            .Select(moduleDef.Import)
                                            .ToArray();
            propertySig = new PropertySig(hasThis: true, valueType, importedIndexParameters);
            propertyKey = $"{accessorMethod.DeclaringType?.FullName ?? string.Empty}::{propertyName}::{propertySig}";
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

            return string.Equals(left.FullName, right.FullName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Creates an imported property signature for the generated module.
        /// </summary>
        /// <param name="moduleDef">The destination module definition.</param>
        /// <param name="sourcePropertySig">The source property signature.</param>
        /// <returns>The imported property signature.</returns>
        private static PropertySig CreateImportedPropertySig(ModuleDef moduleDef, PropertySig sourcePropertySig)
        {
            var importedReturnType = moduleDef.Import(sourcePropertySig.RetType);
            if (sourcePropertySig.Params.Count == 0)
            {
                return new PropertySig(hasThis: sourcePropertySig.HasThis, importedReturnType);
            }

            var importedParameterTypes = new TypeSig[sourcePropertySig.Params.Count];
            for (var index = 0; index < sourcePropertySig.Params.Count; index++)
            {
                importedParameterTypes[index] = moduleDef.Import(sourcePropertySig.Params[index]);
            }

            return new PropertySig(hasThis: sourcePropertySig.HasThis, importedReturnType, importedParameterTypes);
        }

        /// <summary>
        /// Gets interface method attributes.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static MethodAttributes GetInterfaceMethodAttributes(MethodDef proxyMethod)
        {
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
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            bool isInterfaceProxy,
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            if (isInterfaceProxy)
            {
                return TryCollectForwardInterfaceBindings(mapping, proxyType, targetType, closedGenericTargetTypeArguments, out bindings, out failure);
            }

            return TryCollectForwardClassBindings(mapping, proxyType, targetType, closedGenericTargetTypeArguments, out bindings, out failure);
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
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
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

                if (!TryResolveForwardBinding(mapping, targetType, closedGenericTargetTypeArguments, proxyMethod, out var binding, out failure))
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
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
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
                if (!TryResolveForwardBinding(mapping, targetType, closedGenericTargetTypeArguments, proxyMethod, out var binding, out failure))
                {
                    // Reverse proxies may intentionally leave concrete base virtual members unbound.
                    // In this case the generated type inherits the original implementation unchanged.
                    if (mapping.Mode == DuckTypeAotMappingMode.Reverse &&
                        failure?.Status == DuckTypeAotCompatibilityStatuses.MissingTargetMethod &&
                        !proxyMethod.IsAbstract)
                    {
                        continue;
                    }

                    // Ignore inherited open-generic abstract rows when a concrete override already exists
                    // in the proxied class hierarchy. Dynamic reverse ducktyping binds through the concrete
                    // override and does not require an extra mapping for the inherited generic definition.
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
            var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);

            foreach (var method in targetType.Methods)
            {
                if (method.IsConstructor || method.IsStatic || !method.CustomAttributes.Any(IsReverseMethodAttribute))
                {
                    continue;
                }

                var key = GetMethodCandidateKey(method);
                if (emittedCandidates.Add(key))
                {
                    yield return method;
                }
            }

            foreach (var property in targetType.Properties)
            {
                if (!property.CustomAttributes.Any(IsReverseMethodAttribute))
                {
                    continue;
                }

                if (property.GetMethod is not null)
                {
                    var getterKey = GetMethodCandidateKey(property.GetMethod);
                    if (emittedCandidates.Add(getterKey))
                    {
                        yield return property.GetMethod;
                    }
                }

                if (property.SetMethod is not null)
                {
                    var setterKey = GetMethodCandidateKey(property.SetMethod);
                    if (emittedCandidates.Add(setterKey))
                    {
                        yield return property.SetMethod;
                    }
                }
            }
        }

        /// <summary>
        /// Gets interface methods.
        /// </summary>
        /// <param name="interfaceType">The interface type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<MethodDef> GetInterfaceMethods(TypeDef interfaceType)
        {
            var results = new List<MethodDef>();
            var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
            var visitedMethods = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<TypeDef>();
            stack.Push(interfaceType);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!visitedTypes.Add(current.FullName))
                {
                    continue;
                }

                foreach (var method in current.Methods)
                {
                    if (method.IsConstructor || method.IsStatic)
                    {
                        continue;
                    }

                    if (IsDuckIgnoreMethod(method))
                    {
                        continue;
                    }

                    var key = $"{method.Name}::{method.MethodSig}";
                    if (visitedMethods.Add(key))
                    {
                        results.Add(method);
                    }
                }

                foreach (var interfaceImpl in current.Interfaces)
                {
                    var resolvedInterface = interfaceImpl.Interface.ResolveTypeDef();
                    if (resolvedInterface is not null)
                    {
                        stack.Push(resolvedInterface);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets class proxy methods.
        /// </summary>
        /// <param name="proxyClassType">The proxy class type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<MethodDef> GetClassProxyMethods(TypeDef proxyClassType)
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

        /// <summary>
        /// Appends target methods explicitly marked with DuckInclude into the proxy method list.
        /// </summary>
        /// <param name="proxyMethods">The proxy methods value.</param>
        /// <param name="targetType">The target type value.</param>
        private static void AppendDuckIncludeTargetMethods(List<MethodDef> proxyMethods, TypeDef targetType)
        {
            var visitedMethodKeys = new HashSet<string>(proxyMethods.Select(method => $"{method.Name}::{method.MethodSig}"), StringComparer.Ordinal);
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

                    var key = $"{method.Name}::{method.MethodSig}";
                    if (visitedMethodKeys.Add(key))
                    {
                        proxyMethods.Add(method);
                    }
                }

                current = current.BaseType?.ResolveTypeDef();
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

            if (!method.IsPublic && !method.IsFamily && !method.IsFamilyOrAssembly)
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
            foreach (var constructor in proxyType.Methods)
            {
                if (!constructor.IsConstructor || constructor.IsStatic || constructor.MethodSig.Params.Count != 0)
                {
                    continue;
                }

                if (constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly)
                {
                    return constructor;
                }
            }

            return null;
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
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            MethodDef proxyMethod,
            out ForwardBinding binding,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            binding = default;
            failure = null;

            var fieldResolutionMode = GetFieldResolutionMode(proxyMethod);
            var fieldOnly = fieldResolutionMode == FieldResolutionMode.FieldOnly;
            var allowFieldFallback = fieldResolutionMode != FieldResolutionMode.Disabled;
            var allowPrivateBaseMembers = IsFallbackToBaseTypesEnabled(proxyMethod);
            var isReverseMapping = mapping.Mode == DuckTypeAotMappingMode.Reverse;
            // Dynamic ducktyping only applies FallbackToBaseTypes semantics to property/field binding paths.
            // In AOT, property bindings are represented as accessor methods, so keep private-base fallback
            // enabled only for accessor method resolution to preserve runtime parity.
            var allowPrivateBaseMethodCandidates = allowPrivateBaseMembers && IsPropertyAccessorMethod(proxyMethod);
            if (!isReverseMapping &&
                TryGetForwardReverseUsageFailure(proxyMethod, out var reverseUsageFailureDetail))
            {
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                    StatusCodeIncompatibleSignature,
                    reverseUsageFailureDetail!);
                return false;
            }

            MethodCompatibilityFailure? firstMethodFailure = null;
            if (!TryResolveForwardClosedGenericMethodArguments(targetType, proxyMethod, out var closedGenericMethodArguments, out var closedGenericMethodArgumentsFailureReason))
            {
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                    StatusCodeIncompatibleSignature,
                    closedGenericMethodArgumentsFailureReason ?? $"Unable to resolve generic type arguments for proxy method '{proxyMethod.FullName}'.");
                return false;
            }

            if (!fieldOnly)
            {
                var hasSuccessfulMethodBinding = false;
                var successfulMethodBinding = default(ForwardBinding);
                MethodDef? successfulTargetMethod = null;
                var visitedMethodCandidates = new HashSet<string>(StringComparer.Ordinal);
                foreach (var targetMethod in FindForwardTargetMethodCandidates(mapping, targetType, proxyMethod, closedGenericMethodArguments, allowPrivateBaseMethodCandidates))
                {
                    if (!visitedMethodCandidates.Add(GetMethodCandidateKey(targetMethod)))
                    {
                        continue;
                    }

                    if (TryCreateForwardMethodBinding(proxyMethod, targetMethod, closedGenericTargetTypeArguments, closedGenericMethodArguments, isReverseMapping, out var methodBinding, out var methodFailure))
                    {
                        if (TryGetStructMemberMutationFailureDetail(proxyMethod, targetMethod, out var structMutationFailureDetail))
                        {
                            firstMethodFailure ??= new MethodCompatibilityFailure(structMutationFailureDetail!);
                            continue;
                        }

                        if (hasSuccessfulMethodBinding)
                        {
                            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                                mapping,
                                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                                StatusCodeIncompatibleSignature,
                                $"Ambiguous target method match for proxy method '{proxyMethod.FullName}' between '{successfulTargetMethod!.FullName}' and '{targetMethod.FullName}'.");
                            return false;
                        }

                        successfulMethodBinding = ForwardBinding.ForMethod(proxyMethod, targetMethod, methodBinding);
                        successfulTargetMethod = targetMethod;
                        hasSuccessfulMethodBinding = true;
                        continue;
                    }

                    firstMethodFailure ??= methodFailure;
                }

                if (hasSuccessfulMethodBinding)
                {
                    binding = successfulMethodBinding;
                    return true;
                }
            }

            if (allowFieldFallback)
            {
                if (!TryGetFieldAccessorKind(proxyMethod, out var fieldAccessorKind))
                {
                    if (fieldOnly)
                    {
                        failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                            mapping,
                            DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                            StatusCodeIncompatibleSignature,
                            $"Proxy member '{proxyMethod.FullName}' uses DuckField semantics but is not a supported property accessor.");
                        return false;
                    }
                }
                else
                {
                    if (TryFindForwardTargetField(targetType, proxyMethod, fieldAccessorKind, allowPrivateBaseMembers, closedGenericTargetTypeArguments, isReverseMapping, out var targetField, out var fieldBinding, out var fieldFailureReason))
                    {
                        binding = fieldAccessorKind == FieldAccessorKind.Getter
                                      ? ForwardBinding.ForFieldGet(proxyMethod, targetField!, fieldBinding)
                                      : ForwardBinding.ForFieldSet(proxyMethod, targetField!, fieldBinding);
                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(fieldFailureReason))
                    {
                        failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                            mapping,
                            DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                            StatusCodeIncompatibleSignature,
                            fieldFailureReason ?? $"Field binding for proxy method '{proxyMethod.FullName}' is not compatible.");
                        return false;
                    }
                }
            }

            if (firstMethodFailure is not null)
            {
                failure = CreateFailureResult(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                    StatusCodeIncompatibleSignature,
                    firstMethodFailure.Value.Detail);
                return false;
            }

            if (TryResolvePropertyCantBeWrittenFailure(targetType, proxyMethod, allowPrivateBaseMembers, out var propertyCantBeWrittenDetail))
            {
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                    StatusCodePropertyCantBeWritten,
                    propertyCantBeWrittenDetail!);
                return false;
            }

            failure = CreateFailureResult(
                mapping,
                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                StatusCodeMissingMethod,
                $"Target member for proxy method '{proxyMethod.FullName}' was not found.");
            return false;
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
            detail = null;
            var proxyMethodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();
            if (!proxyMethodName.StartsWith("set_", StringComparison.Ordinal) ||
                proxyMethod.MethodSig.Params.Count != 1 ||
                proxyMethod.MethodSig.RetType.ElementType != ElementType.Void)
            {
                return false;
            }

            var candidatePropertyNames = GetForwardTargetMethodNames(proxyMethod)
                                        .Select(methodName => ExtractSetterPropertyName(methodName))
                                        .Where(propertyName => !string.IsNullOrWhiteSpace(propertyName))
                                        .Distinct(StringComparer.Ordinal)
                                        .ToArray();
            if (candidatePropertyNames.Length == 0)
            {
                return false;
            }

            var current = targetType;
            while (current is not null)
            {
                foreach (var property in current.Properties)
                {
                    var propertyName = property.Name.String ?? property.Name.ToString();
                    if (!candidatePropertyNames.Contains(propertyName, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    if (!allowPrivateBaseMembers &&
                        !ReferenceEquals(current, targetType) &&
                        IsPropertyPrivate(property))
                    {
                        continue;
                    }

                    if (property.SetMethod is null)
                    {
                        detail = $"Target property '{property.FullName}' can't be written for proxy method '{proxyMethod.FullName}'.";
                        return true;
                    }
                }

                current = current.BaseType?.ResolveTypeDef();
            }

            return false;
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

        /// <summary>
        /// Finds forward-mapping target method candidates for a proxy method.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<MethodDef> FindForwardTargetMethodCandidates(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            MethodDef proxyMethod,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            bool allowPrivateBaseMembers)
        {
            _ = TryGetForwardExplicitInterfaceTypeNames(
                proxyMethod,
                out var explicitInterfaceTypeNames,
                out var useRelaxedNameComparison);
            _ = TryGetForwardParameterTypeNames(proxyMethod, out var configuredParameterTypeNames);
            var expectedGenericArity = closedGenericMethodArguments?.Count ?? (int)proxyMethod.MethodSig.GenParamCount;

            if (mapping.Mode == DuckTypeAotMappingMode.Reverse)
            {
                if (!HasReverseMethodAttributes(targetType))
                {
                    foreach (var candidate in FindDefaultTargetMethodCandidates(
                                 targetType,
                                 proxyMethod,
                                 explicitInterfaceTypeNames,
                                 useRelaxedNameComparison,
                                 expectedGenericArity,
                                 configuredParameterTypeNames,
                                 allowPrivateBaseMembers))
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
                        yield return reverseCandidate;
                    }
                }

                yield break;
            }

            foreach (var candidate in FindDefaultTargetMethodCandidates(
                         targetType,
                         proxyMethod,
                         explicitInterfaceTypeNames,
                         useRelaxedNameComparison,
                         expectedGenericArity,
                         configuredParameterTypeNames,
                         allowPrivateBaseMembers))
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
            var current = targetType;
            while (current is not null)
            {
                foreach (var method in current.Methods)
                {
                    if (method.CustomAttributes.Any(IsReverseMethodAttribute))
                    {
                        return true;
                    }
                }

                foreach (var property in current.Properties)
                {
                    if (property.CustomAttributes.Any(IsReverseMethodAttribute))
                    {
                        return true;
                    }
                }

                current = current.BaseType?.ResolveTypeDef();
            }

            return false;
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
        private static IEnumerable<MethodDef> FindDefaultTargetMethodCandidates(
            TypeDef targetType,
            MethodDef proxyMethod,
            IReadOnlyList<string> explicitInterfaceTypeNames,
            bool useRelaxedNameComparison,
            int expectedGenericArity,
            IReadOnlyList<string> configuredParameterTypeNames,
            bool allowPrivateBaseMembers)
        {
            var candidateMethodNames = GetForwardTargetMethodNames(proxyMethod);
            foreach (var candidateMethodName in candidateMethodNames)
            {
                var current = targetType;
                while (current is not null)
                {
                    foreach (var candidate in current.Methods)
                    {
                        var candidateMethodActualName = candidate.Name.String ?? candidate.Name.ToString();
                        // Skip candidates whose method name does not match forward-mapping naming rules.
                        if (!IsForwardTargetMethodNameMatch(
                                candidateMethodActualName,
                                candidateMethodName,
                                explicitInterfaceTypeNames,
                                useRelaxedNameComparison))
                        {
                            continue;
                        }

                        // Signature shape prefilter: generic arity and parameter count must match proxy method.
                        if (candidate.MethodSig.GenParamCount != expectedGenericArity ||
                            candidate.MethodSig.Params.Count != proxyMethod.MethodSig.Params.Count)
                        {
                            continue;
                        }

                        if (configuredParameterTypeNames.Count > 0 &&
                            !IsForwardCandidateParameterTypeNameMatch(candidate, configuredParameterTypeNames))
                        {
                            continue;
                        }

                        if (!allowPrivateBaseMembers &&
                            !ReferenceEquals(current, targetType) &&
                            candidate.IsPrivate)
                        {
                            continue;
                        }

                        yield return candidate;
                    }

                    current = current.BaseType?.ResolveTypeDef();
                }
            }
        }

        /// <summary>
        /// Determines whether candidate method parameters match configured [Duck(ParameterTypeNames=...)] values.
        /// </summary>
        /// <param name="candidate">The candidate value.</param>
        /// <param name="configuredParameterTypeNames">The configured parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsForwardCandidateParameterTypeNameMatch(MethodDef candidate, IReadOnlyList<string> configuredParameterTypeNames)
        {
            if (configuredParameterTypeNames.Count != candidate.MethodSig.Params.Count)
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

                var candidateNames = GetTypeComparisonNames(candidate.MethodSig.Params[i]);
                if (!candidateNames.Contains(configuredName))
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
            return $"{candidate.DeclaringType.FullName}::{candidate.Name}::{candidate.MethodSig}";
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

            var current = targetType;
            while (current is not null)
            {
                foreach (var method in current.Methods)
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

                foreach (var property in current.Properties)
                {
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
            bool useRelaxedNameComparison)
        {
            if (string.Equals(candidateMethodName, requestedMethodName, StringComparison.Ordinal))
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
                if (string.Equals(candidateMethodName, $"{normalizedInterfaceTypeName}.{requestedMethodName}", StringComparison.Ordinal))
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
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            bool isReverseMapping,
            out ForwardMethodBindingInfo binding,
            out MethodCompatibilityFailure? failure)
        {
            if (proxyMethod.MethodSig.Params.Count != targetMethod.MethodSig.Params.Count)
            {
                failure = new MethodCompatibilityFailure(
                    $"Parameter count mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                binding = default;
                return false;
            }

            if (closedGenericMethodArguments is null)
            {
                if (proxyMethod.MethodSig.GenParamCount != targetMethod.MethodSig.GenParamCount)
                {
                    failure = new MethodCompatibilityFailure(
                        $"Generic arity mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    binding = default;
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
                    return false;
                }
            }

            var parameterBindings = new MethodParameterBinding[proxyMethod.MethodSig.Params.Count];
            for (var parameterIndex = 0; parameterIndex < proxyMethod.MethodSig.Params.Count; parameterIndex++)
            {
                if (!TryCreateForwardMethodParameterBinding(proxyMethod, targetMethod, closedGenericTargetTypeArguments, closedGenericMethodArguments, parameterIndex, isReverseMapping, out var parameterBinding, out failure))
                {
                    binding = default;
                    return false;
                }

                parameterBindings[parameterIndex] = parameterBinding;
            }

            var targetReturnType = SubstituteTypeAndMethodGenericTypeArguments(targetMethod.MethodSig.RetType, closedGenericTargetTypeArguments, closedGenericMethodArguments);
            if (!TryCreateReturnConversion(proxyMethod.MethodSig.RetType, targetReturnType, isReverseMapping, out var returnConversion))
            {
                failure = new MethodCompatibilityFailure(
                    $"Return type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                binding = default;
                return false;
            }

            binding = new ForwardMethodBindingInfo(parameterBindings, returnConversion, closedGenericMethodArguments);
            failure = null;
            return true;
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
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            int parameterIndex,
            bool isReverseMapping,
            out MethodParameterBinding parameterBinding,
            out MethodCompatibilityFailure? failure)
        {
            var proxyParameterType = proxyMethod.MethodSig.Params[parameterIndex];
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

            _ = TryGetMethodParameterDirection(proxyMethod, parameterIndex, out var proxyParameterDirection);
            _ = TryGetMethodParameterDirection(targetMethod, parameterIndex, out var targetParameterDirection);
            var proxyIsOut = proxyParameterDirection.IsOut;
            var targetIsOut = targetParameterDirection.IsOut;
            var proxyIsIn = proxyParameterDirection.IsIn;
            var targetIsIn = targetParameterDirection.IsIn;
            if (proxyIsOut != targetIsOut || proxyIsIn != targetIsIn)
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

            if (!MatchesDynamicMethodParameterSelectionRule(proxyByRefElementTypeSig!, targetByRefElementTypeSig!))
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

            if (!TryCreateByRefPostCallConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, out var postCallConversion))
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
            if (AreTypesEquivalent(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.None();
                return true;
            }

            if (TryGetValueWithTypeArgument(proxyParameterType, out var proxyValueWithTypeArgument))
            {
                // ValueWithType<T>: unwrap first, then reuse normal argument conversion rules for T -> target.
                if (!TryCreateMethodArgumentConversion(proxyValueWithTypeArgument!, targetParameterType, isReverseMapping, enforceMethodSelectionRules, out argumentConversion))
                {
                    argumentConversion = default;
                    return false;
                }

                argumentConversion = argumentConversion.WithValueWithTypeUnwrap(proxyParameterType, proxyValueWithTypeArgument!);
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

                return true;
            }

            if (enforceMethodSelectionRules &&
                !MatchesDynamicMethodParameterSelectionRule(proxyParameterType, targetParameterType))
            {
                argumentConversion = default;
                return false;
            }

            if (CanUseTypeConversion(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.TypeConversion(proxyParameterType, targetParameterType);
                return true;
            }

            argumentConversion = default;
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
            if (proxyRuntimeType is null || targetRuntimeType is null)
            {
                // Keep unresolved signatures permissive to avoid rejecting analyzable metadata-only cases.
                return true;
            }

            if (targetRuntimeType.IsGenericParameter)
            {
                return true;
            }

            // Dynamic selector requires exact matches for non-enum value-type parameters.
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
        /// <param name="returnConversion">The return conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateByRefPostCallConversion(TypeSig proxyParameterElementType, TypeSig targetParameterElementType, out MethodReturnConversion returnConversion)
        {
            if (TryCreateReturnConversion(proxyParameterElementType, targetParameterElementType, out returnConversion))
            {
                return true;
            }

            returnConversion = default;
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

            if (typeSig is GenericVar typeGenericParameter &&
                closedGenericTypeArguments is not null &&
                typeGenericParameter.Number < closedGenericTypeArguments.Count)
            {
                return closedGenericTypeArguments[(int)typeGenericParameter.Number];
            }

            // Replace method generic parameter with the corresponding closed generic argument when available.
            if (typeSig is GenericMVar methodGenericParameter &&
                closedGenericMethodArguments is not null &&
                methodGenericParameter.Number < closedGenericMethodArguments.Count)
            {
                return closedGenericMethodArguments[(int)methodGenericParameter.Number];
            }

            if (typeSig is ByRefSig byRefSig)
            {
                return new ByRefSig(SubstituteTypeAndMethodGenericTypeArguments(byRefSig.Next, closedGenericTypeArguments, closedGenericMethodArguments));
            }

            if (typeSig is SZArraySig szArraySig)
            {
                return new SZArraySig(SubstituteTypeAndMethodGenericTypeArguments(szArraySig.Next, closedGenericTypeArguments, closedGenericMethodArguments));
            }

            if (typeSig is GenericInstSig genericInstSig)
            {
                var genericArguments = new List<TypeSig>(genericInstSig.GenericArguments.Count);
                for (var i = 0; i < genericInstSig.GenericArguments.Count; i++)
                {
                    genericArguments.Add(SubstituteTypeAndMethodGenericTypeArguments(genericInstSig.GenericArguments[i], closedGenericTypeArguments, closedGenericMethodArguments));
                }

                return new GenericInstSig(genericInstSig.GenericType, genericArguments);
            }

            return typeSig;
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
            var importedWrapperTypeSig = moduleDef.Import(wrapperTypeSig);
            var typeSpec = moduleDef.UpdateRowId(new TypeSpecUser(importedWrapperTypeSig));
            var fieldRef = new MemberRefUser(moduleDef, "Value", new FieldSig(new GenericVar(0)), typeSpec);
            return moduleDef.UpdateRowId(fieldRef);
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
            var importedWrapperTypeSig = moduleDef.Import(wrapperTypeSig);
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
            return moduleDef.UpdateRowId(methodRef);
        }

        /// <summary>
        /// Creates duck type create cache create method ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="proxyTypeSig">The proxy type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateDuckTypeCreateCacheCreateMethodRef(ModuleDef moduleDef, TypeSig proxyTypeSig)
        {
            var importedProxyTypeSig = moduleDef.Import(proxyTypeSig);
            var importedCreateCacheOpenType = moduleDef.Import(typeof(DuckType.CreateCache<>)) as ITypeDefOrRef
                ?? throw new InvalidOperationException("Unable to import DuckType.CreateCache<> type.");

            var importedCreateCacheOpenTypeSig = importedCreateCacheOpenType.ToTypeSig() as ClassOrValueTypeSig
                ?? throw new InvalidOperationException("Unable to resolve DuckType.CreateCache<> signature.");

            var createCacheClosedTypeSig = new GenericInstSig(importedCreateCacheOpenTypeSig, importedProxyTypeSig);
            var createCacheClosedTypeSpec = moduleDef.UpdateRowId(new TypeSpecUser(createCacheClosedTypeSig));
            var createMethodSig = MethodSig.CreateStatic(new GenericVar(0), moduleDef.CorLibTypes.Object);
            var createMethodRef = new MemberRefUser(moduleDef, "Create", createMethodSig, createCacheClosedTypeSpec);
            return moduleDef.UpdateRowId(createMethodRef);
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
            return moduleDef.Import(typeDefOrRef) as ITypeDefOrRef
                ?? throw new InvalidOperationException($"Unable to import type token for {context}.");
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
            var importedTargetTypeSig = moduleDef.Import(targetTypeSig);
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
                var nullableResultLocal = new Local(moduleDef.Import(proxyTypeSig));
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

            var importedNullableTypeSig = moduleDef.Import(nullableTypeSig);
            var nullableTypeSpec = moduleDef.UpdateRowId(new TypeSpecUser(importedNullableTypeSig));
            var ctorSig = MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, new GenericVar(0));
            var ctorRef = new MemberRefUser(moduleDef, ".ctor", ctorSig, nullableTypeSpec);
            return moduleDef.UpdateRowId(ctorRef);
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
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            bool isReverseMapping,
            out FieldDef? targetField,
            out ForwardFieldBindingInfo fieldBinding,
            out string? failureReason)
        {
            targetField = null;
            fieldBinding = default;
            failureReason = null;
            var candidateFieldNames = GetForwardTargetFieldNames(proxyMethod);

            foreach (var candidateFieldName in candidateFieldNames)
            {
                var current = targetType;
                while (current is not null)
                {
                    foreach (var candidate in current.Fields)
                    {
                        if (!string.Equals(candidate.Name, candidateFieldName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (!allowPrivateBaseMembers &&
                            !ReferenceEquals(current, targetType) &&
                            candidate.IsPrivate)
                        {
                            continue;
                        }

                        if (!AreFieldAccessorSignatureCompatible(proxyMethod, candidate, accessorKind, closedGenericTargetTypeArguments, isReverseMapping, out var candidateFieldBinding, out failureReason))
                        {
                            continue;
                        }

                        targetField = candidate;
                        fieldBinding = candidateFieldBinding;
                        return true;
                    }

                    current = current.BaseType?.ResolveTypeDef();
                }
            }

            return false;
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
            }

            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames))
            {
                AddNames(propertyAttributeNames);
            }

            if (TryGetAccessorPropertyName(proxyMethod.Name.String ?? proxyMethod.Name.ToString(), out var propertyName))
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
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            bool isReverseMapping,
            out ForwardFieldBindingInfo fieldBinding,
            out string? failureReason)
        {
            fieldBinding = ForwardFieldBindingInfo.None();
            failureReason = null;
            var targetFieldType = SubstituteTypeAndMethodGenericTypeArguments(targetField.FieldSig.Type, closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
            switch (accessorKind)
            {
                case FieldAccessorKind.Getter:
                {
                    if (TryCreateReturnConversion(proxyMethod.MethodSig.RetType, targetFieldType, out var returnConversion))
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

                    var proxyParameterType = proxyMethod.MethodSig.Params[0];
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
            }

            if (proxyMethod.IsSpecialName && TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames) && TryGetAccessorPrefix(proxyMethod.Name, out var propertyAccessorPrefix))
            {
                AddNames(propertyAttributeNames.Select(name => $"{propertyAccessorPrefix}{name}"));
            }

            AddNames(new[] { proxyMethod.Name.String ?? proxyMethod.Name.ToString() });
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
            MethodDef proxyMethod,
            out IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            out string? failureReason)
        {
            closedGenericMethodArguments = null;
            failureReason = null;
            if (!TryGetDuckGenericParameterTypeNames(proxyMethod, out var genericParameterTypeNames))
            {
                return true;
            }

            var resolvedTypeSigs = new List<TypeSig>(genericParameterTypeNames.Count);
            foreach (var genericParameterTypeName in genericParameterTypeNames)
            {
                if (!TryResolveRuntimeTypeByName(genericParameterTypeName, out var runtimeType))
                {
                    failureReason =
                        $"Generic parameter type '{genericParameterTypeName}' for proxy method '{proxyMethod.FullName}' could not be resolved.";
                    return false;
                }

                resolvedTypeSigs.Add(targetType.Module.Import(runtimeType!).ToTypeSig());
            }

            closedGenericMethodArguments = resolvedTypeSigs;
            return true;
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
            runtimeType = Type.GetType(typeName, throwOnError: false);
            if (runtimeType is not null)
            {
                return true;
            }

            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                runtimeType = loadedAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (runtimeType is not null)
                {
                    return true;
                }
            }

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

            if (proxyType is ByRefSig proxyByRef && targetType is ByRefSig targetByRef)
            {
                return AreTypesEquivalent(proxyByRef.Next, targetByRef.Next);
            }

            if (proxyType is GenericInstSig proxyGenericInst && targetType is GenericInstSig targetGenericInst)
            {
                var proxyGenericTypeName = proxyGenericInst.GenericType.TypeDefOrRef?.FullName ?? proxyGenericInst.GenericType.FullName;
                var targetGenericTypeName = targetGenericInst.GenericType.TypeDefOrRef?.FullName ?? targetGenericInst.GenericType.FullName;
                if (!string.Equals(proxyGenericTypeName, targetGenericTypeName, StringComparison.Ordinal))
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

            return string.Equals(proxyType.FullName, targetType.FullName, StringComparison.Ordinal);
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
            failure = null;
            foreach (var customAttribute in targetType.CustomAttributes)
            {
                if (ShouldSkipReverseCopiedCustomAttribute(customAttribute))
                {
                    continue;
                }

                if (customAttribute.NamedArguments.Count > 0)
                {
                    failure = CreateFailureResult(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeCustomAttributeNamedArguments,
                        $"Custom attribute '{customAttribute.TypeFullName}' on target type '{targetType.FullName}' contains named arguments and is not supported.");
                    return false;
                }

                if (!TryCloneCustomAttribute(moduleDef, customAttribute, out var clonedAttribute))
                {
                    failure = CreateFailureResult(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        $"Unable to copy custom attribute '{customAttribute.TypeFullName}' from target type '{targetType.FullName}'.");
                    return false;
                }

                generatedType.CustomAttributes.Add(clonedAttribute!);
            }

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
            if (moduleDef.Import(sourceAttribute.Constructor) is not ICustomAttributeType importedCtor)
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
            type = module.Find(typeName, isReflectionName: true)
                ?? module.Find(typeName, isReflectionName: false)
                ?? module.GetTypes().FirstOrDefault(candidate =>
                    string.Equals(candidate.ReflectionFullName, typeName, StringComparison.Ordinal) ||
                    string.Equals(candidate.FullName, typeName, StringComparison.Ordinal))!;

            return type is not null;
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
            DuckTypeAotMappingResolutionResult mappingResolutionResult)
        {
            var assemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
        /// Loads dnlib modules for all supplied assembly paths.
        /// </summary>
        /// <param name="assemblyPathsByName">The assembly paths by name value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyDictionary<string, ModuleDefMD> LoadModules(IReadOnlyDictionary<string, string> assemblyPathsByName)
        {
            var modulesByAssemblyName = new Dictionary<string, ModuleDefMD>(StringComparer.OrdinalIgnoreCase);
            foreach (var (assemblyName, assemblyPath) in assemblyPathsByName)
            {
                modulesByAssemblyName[assemblyName] = ModuleDefMD.Load(assemblyPath);
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
                IReadOnlyList<TypeSig>? closedGenericMethodArguments)
            {
                ParameterBindings = parameterBindings;
                ReturnConversion = returnConversion;
                ClosedGenericMethodArguments = closedGenericMethodArguments;
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

                var registerAotProxyMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotProxy),
                    new[] { typeof(Type), typeof(Type), typeof(Type), typeof(RuntimeMethodHandle) });
                if (registerAotProxyMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotProxy(Type, Type, Type, RuntimeMethodHandle).");
                }

                var registerAotReverseProxyMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotReverseProxy),
                    new[] { typeof(Type), typeof(Type), typeof(Type), typeof(RuntimeMethodHandle) });
                if (registerAotReverseProxyMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotReverseProxy(Type, Type, Type, RuntimeMethodHandle).");
                }

                var registerAotProxyFailureMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotProxyFailure),
                    new[] { typeof(Type), typeof(Type), typeof(Type) });
                if (registerAotProxyFailureMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotProxyFailure(Type, Type, Type).");
                }

                var registerAotReverseProxyFailureMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotReverseProxyFailure),
                    new[] { typeof(Type), typeof(Type), typeof(Type) });
                if (registerAotReverseProxyFailureMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotReverseProxyFailure(Type, Type, Type).");
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

                GetTypeFromHandleMethod = moduleDef.Import(getTypeFromHandleMethod);
                RegisterAotProxyMethod = moduleDef.Import(registerAotProxyMethod);
                RegisterAotReverseProxyMethod = moduleDef.Import(registerAotReverseProxyMethod);
                RegisterAotProxyFailureMethod = moduleDef.Import(registerAotProxyFailureMethod);
                RegisterAotReverseProxyFailureMethod = moduleDef.Import(registerAotReverseProxyFailureMethod);
                EnableAotModeMethod = moduleDef.Import(enableAotModeMethod);
                ValidateAotRegistryContractMethod = moduleDef.Import(validateAotRegistryContractMethod);
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
    }
}
