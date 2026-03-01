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
        /// Defines the duck reverse method attribute type name constant.
        /// </summary>
        private const string DuckReverseMethodAttributeTypeName = "Datadog.Trace.DuckTyping.DuckReverseMethodAttribute";

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
            /// Represents duck chain to proxy.
            /// </summary>
            DuckChainToProxy,

            /// <summary>
            /// Represents type conversion.
            /// </summary>
            TypeConversion
        }

        /// <summary>
        /// Emits emit.
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
            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.StrongNameKeyFile)) evaluates to true.
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
        /// Resolves resolve assembly mvid.
        /// </summary>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ResolveAssemblyMvid(string assemblyPath)
        {
            using var module = ModuleDefMD.Load(assemblyPath);
            return module.Mvid?.ToString("D") ?? string.Empty;
        }

        /// <summary>
        /// Emits emit mapping.
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
            // Branch: take this path when (DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.ProxyTypeName) || evaluates to true.
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
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName);
            }

            // Branch: take this path when (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule)) evaluates to true.
            if (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
            }

            // Branch: take this path when (!targetModulesByAssemblyName.TryGetValue(mapping.TargetAssemblyName, out var targetModule)) evaluates to true.
            if (!targetModulesByAssemblyName.TryGetValue(mapping.TargetAssemblyName, out var targetModule))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target assembly '{mapping.TargetAssemblyName}' was not loaded.");
            }

            // Branch: take this path when (!TryResolveType(proxyModule, mapping.ProxyTypeName, out var proxyType)) evaluates to true.
            if (!TryResolveType(proxyModule, mapping.ProxyTypeName, out var proxyType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy type '{mapping.ProxyTypeName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            // Branch: take this path when (!TryResolveType(targetModule, mapping.TargetTypeName, out var targetType)) evaluates to true.
            if (!TryResolveType(targetModule, mapping.TargetTypeName, out var targetType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            // Branch: take this path when (proxyType.IsValueType) evaluates to true.
            if (proxyType.IsValueType)
            {
                // Branch: take this path when (isReverseMapping) evaluates to true.
                if (isReverseMapping)
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                        StatusCodeUnsupportedProxyKind,
                        $"Reverse proxy type '{mapping.ProxyTypeName}' is not supported when the proxy definition is a value type.");
                }

                return EmitStructCopyMapping(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType);
            }

            // Branch: take this path when (!proxyType.IsInterface && !proxyType.IsClass) evaluates to true.
            if (!proxyType.IsInterface && !proxyType.IsClass)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnsupportedProxyKind,
                    $"Proxy type '{mapping.ProxyTypeName}' is not supported. Only interface, class, and DuckCopy struct proxies are emitted in this phase.");
            }

            var isInterfaceProxy = proxyType.IsInterface;
            // Branch: take this path when (!TryCollectForwardBindings(mapping, proxyType, targetType, isInterfaceProxy, out var bindings, out var failure)) evaluates to true.
            if (!TryCollectForwardBindings(mapping, proxyType, targetType, isInterfaceProxy, out var bindings, out var failure))
            {
                return failure!;
            }

            IMethod baseCtorToCall = importedMembers.ObjectCtor;
            // Branch: take this path when (!isInterfaceProxy) evaluates to true.
            if (!isInterfaceProxy)
            {
                var baseConstructor = FindSupportedProxyBaseConstructor(proxyType);
                // Branch: take this path when (baseConstructor is null) evaluates to true.
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
            var generatedType = new TypeDefUser(
                GeneratedProxyNamespace,
                generatedTypeName,
                isInterfaceProxy ? moduleDef.CorLibTypes.Object.TypeDefOrRef : moduleDef.Import(proxyType))
            {
                Attributes = TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed
            };
            // Branch: take this path when (isInterfaceProxy) evaluates to true.
            if (isInterfaceProxy)
            {
                generatedType.Interfaces.Add(new InterfaceImplUser(moduleDef.Import(proxyType)));
            }

            generatedType.Interfaces.Add(new InterfaceImplUser(importedMembers.IDuckTypeType));
            moduleDef.Types.Add(generatedType);

            var importedTargetType = moduleDef.Import(targetType);
            var importedTargetTypeSig = moduleDef.Import(targetType.ToTypeSig());
            var targetField = new FieldDefUser("_instance", new FieldSig(importedTargetTypeSig), FieldAttributes.Private | FieldAttributes.InitOnly);
            generatedType.Fields.Add(targetField);

            var generatedConstructor = new MethodDefUser(
                ".ctor",
                MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            generatedConstructor.Body = new CilBody();
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseCtorToCall));
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            generatedConstructor.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            generatedConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(targetField));
            generatedConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedType.Methods.Add(generatedConstructor);

            EmitIDuckTypeImplementation(moduleDef, generatedType, importedTargetType, targetField, importedMembers, targetType.IsValueType);

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
                // Branch dispatch: select the execution path based on (binding.Kind).
                switch (binding.Kind)
                {
                    case ForwardBindingKind.Method:
                        // Branch: handles the case ForwardBindingKind.Method switch case.
                    {
                        var targetMethod = binding.TargetMethod!;
                        var methodBinding = binding.MethodBinding!.Value;
                        var byRefWriteBacks = new List<ByRefWriteBackPlan>();
                        // Branch: take this path when (!targetMethod.IsStatic) evaluates to true.
                        if (!targetMethod.IsStatic)
                        {
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                            generatedMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                        }

                        for (var parameterIndex = 0; parameterIndex < proxyMethod.MethodSig.Params.Count; parameterIndex++)
                        {
                            var parameterBinding = methodBinding.ParameterBindings[parameterIndex];
                            var proxyParameter = generatedMethod.Parameters[parameterIndex + 1];
                            // Branch: take this path when (parameterBinding.IsByRef && parameterBinding.UseLocalForByRef) evaluates to true.
                            if (parameterBinding.IsByRef && parameterBinding.UseLocalForByRef)
                            {
                                var targetElementTypeSig = moduleDef.Import(parameterBinding.TargetByRefElementTypeSig!);
                                var targetByRefLocal = new Local(targetElementTypeSig);
                                generatedMethod.Body.Variables.Add(targetByRefLocal);
                                generatedMethod.Body.InitLocals = true;

                                // Branch: take this path when (!parameterBinding.IsOut) evaluates to true.
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
                                // Branch: fallback path when earlier branch conditions evaluate to false.
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxyParameter));
                                // Branch: take this path when (!parameterBinding.IsByRef) evaluates to true.
                                if (!parameterBinding.IsByRef)
                                {
                                    EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'");
                                }
                            }
                        }

                        var importedTargetMethod = moduleDef.Import(targetMethod);
                        var targetMethodToCall = CreateMethodCallTarget(moduleDef, importedTargetMethod, generatedMethod, methodBinding.ClosedGenericMethodArguments);
                        // Branch: take this path when (!targetMethod.IsStatic && targetType.IsValueType && (targetMethod.IsVirtual || targetMethod.DeclaringType.IsInterface)) evaluates to true.
                        if (!targetMethod.IsStatic && targetType.IsValueType && (targetMethod.IsVirtual || targetMethod.DeclaringType.IsInterface))
                        {
                            generatedMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                            generatedMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(targetMethodToCall));
                        }
                        else
                        {
                            // Branch: fallback path when earlier branch conditions evaluate to false.
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
                        // Branch: handles the case ForwardBindingKind.FieldGet switch case.
                    {
                        var fieldBinding = binding.FieldBinding!.Value;
                        var importedTargetMemberField = moduleDef.Import(binding.TargetField!);
                        // Branch: take this path when (binding.TargetField!.IsStatic) evaluates to true.
                        if (binding.TargetField!.IsStatic)
                        {
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(importedTargetMemberField));
                        }
                        else
                        {
                            // Branch: fallback path when earlier branch conditions evaluate to false.
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                            generatedMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(importedTargetMemberField));
                        }

                        EmitMethodReturnConversion(moduleDef, generatedMethod.Body, fieldBinding.ReturnConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");

                        break;
                    }

                    case ForwardBindingKind.FieldSet:
                        // Branch: handles the case ForwardBindingKind.FieldSet switch case.
                    {
                        var fieldBinding = binding.FieldBinding!.Value;
                        var importedTargetMemberField = moduleDef.Import(binding.TargetField!);
                        // Branch: take this path when (binding.TargetField!.IsStatic) evaluates to true.
                        if (binding.TargetField!.IsStatic)
                        {
                            generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                            EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");

                            generatedMethod.Body.Instructions.Add(OpCodes.Stsfld.ToInstruction(importedTargetMemberField));
                        }
                        else
                        {
                            // Branch: fallback path when earlier branch conditions evaluate to false.
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                            generatedMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                            generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                            EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");

                            generatedMethod.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(importedTargetMemberField));
                        }

                        break;
                    }

                    default:
                        // Branch: fallback switch case when no explicit case label matches.
                        throw new InvalidOperationException($"Unsupported forward binding kind '{binding.Kind}'.");
                }

                generatedMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(generatedMethod);
            }

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            activatorMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(generatedConstructor));
            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(moduleDef.Import(proxyType)));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            initializeMethod.Body.Instructions.Add(OpCodes.Ldftn.ToInstruction(activatorMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.FuncObjectObjectCtor));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(isReverseMapping ? importedMembers.RegisterAotReverseProxyMethod : importedMembers.RegisterAotProxyMethod));

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                moduleDef.Assembly?.Name?.String ?? string.Empty,
                generatedType.FullName);
        }

        /// <summary>
        /// Emits emit closed generic mapping.
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
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName)
        {
            // Branch: take this path when (!proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath)) evaluates to true.
            if (!proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
            }

            // Branch: take this path when (!targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath)) evaluates to true.
            if (!targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target assembly '{mapping.TargetAssemblyName}' was not loaded.");
            }

            // Branch: take this path when (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType)) evaluates to true.
            if (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy closed generic type '{mapping.ProxyTypeName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            // Branch: take this path when (!TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType)) evaluates to true.
            if (!TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target closed generic type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            // Branch: take this path when (!proxyRuntimeType!.IsAssignableFrom(targetRuntimeType)) evaluates to true.
            if (!proxyRuntimeType!.IsAssignableFrom(targetRuntimeType))
            {
                var detail = $"Closed generic mapping requires duck adaptation that is not emitted yet. proxy='{mapping.ProxyTypeName}', target='{mapping.TargetTypeName}'.";
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedClosedGenericMapping,
                    StatusCodeUnsupportedClosedGenericMapping,
                    detail);
            }

            var resolvedProxyRuntimeType = proxyRuntimeType!;
            var resolvedTargetRuntimeType = targetRuntimeType!;

            var importedProxyType = moduleDef.Import(resolvedProxyRuntimeType) as ITypeDefOrRef
                ?? throw new InvalidOperationException($"Unable to import closed generic proxy type '{mapping.ProxyTypeName}'.");
            var importedTargetType = moduleDef.Import(resolvedTargetRuntimeType) as ITypeDefOrRef
                ?? throw new InvalidOperationException($"Unable to import closed generic target type '{mapping.TargetTypeName}'.");

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            EmitClosedGenericDirectCastActivation(activatorMethod.Body, importedProxyType, importedTargetType, resolvedProxyRuntimeType, resolvedTargetRuntimeType);
            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            initializeMethod.Body.Instructions.Add(OpCodes.Ldftn.ToInstruction(activatorMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.FuncObjectObjectCtor));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(isReverseMapping ? importedMembers.RegisterAotReverseProxyMethod : importedMembers.RegisterAotProxyMethod));

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                mapping.ProxyAssemblyName,
                mapping.ProxyTypeName);
        }

        /// <summary>
        /// Emits emit closed generic direct cast activation.
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

            // Branch: take this path when (targetRuntimeType.IsValueType) evaluates to true.
            if (targetRuntimeType.IsValueType)
            {
                body.Instructions.Add(OpCodes.Unbox_Any.ToInstruction(importedTargetType));

                // Branch: take this path when (proxyRuntimeType.IsValueType) evaluates to true.
                if (proxyRuntimeType.IsValueType)
                {
                    // Branch: take this path when (proxyRuntimeType != targetRuntimeType) evaluates to true.
                    if (proxyRuntimeType != targetRuntimeType)
                    {
                        throw new InvalidOperationException(
                            $"Closed generic mapping cannot cast value type '{targetRuntimeType.FullName}' to '{proxyRuntimeType.FullName}'.");
                    }

                    body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
                    return;
                }

                body.Instructions.Add(OpCodes.Box.ToInstruction(importedTargetType));
                body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedProxyType));
                return;
            }

            body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedTargetType));
            // Branch: take this path when (proxyRuntimeType.IsValueType) evaluates to true.
            if (proxyRuntimeType.IsValueType)
            {
                throw new InvalidOperationException(
                    $"Closed generic mapping cannot cast reference type '{targetRuntimeType.FullName}' to value type '{proxyRuntimeType.FullName}'.");
            }

            // Branch: take this path when (proxyRuntimeType != targetRuntimeType) evaluates to true.
            if (proxyRuntimeType != targetRuntimeType)
            {
                body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedProxyType));
            }
        }

        /// <summary>
        /// Attempts to try resolve runtime type.
        /// </summary>
        /// <param name="assemblyName">The assembly name value.</param>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <param name="typeName">The type name value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryResolveRuntimeType(string assemblyName, string assemblyPath, string typeName, out Type? runtimeType)
        {
            runtimeType = null;
            try
            {
                runtimeType = Type.GetType(typeName, throwOnError: false);
                // Branch: take this path when (runtimeType is not null) evaluates to true.
                if (runtimeType is not null)
                {
                    return true;
                }

                Assembly? candidateAssembly = null;
                foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var loadedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(loadedAssembly.GetName().Name ?? string.Empty);
                    // Branch: take this path when (string.Equals(loadedAssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase)) evaluates to true.
                    if (string.Equals(loadedAssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        candidateAssembly = loadedAssembly;
                        break;
                    }
                }

                candidateAssembly ??= Assembly.LoadFrom(assemblyPath);
                runtimeType = candidateAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                // Branch: take this path when (runtimeType is not null) evaluates to true.
                if (runtimeType is not null)
                {
                    return true;
                }

                var candidateAssemblyName = candidateAssembly.GetName().Name;
                // Branch: take this path when (!string.IsNullOrWhiteSpace(candidateAssemblyName)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(candidateAssemblyName))
                {
                    runtimeType = Type.GetType($"{typeName}, {candidateAssemblyName}", throwOnError: false);
                    // Branch: take this path when (runtimeType is not null) evaluates to true.
                    if (runtimeType is not null)
                    {
                        return true;
                    }
                }

                // Branch: take this path when (!string.IsNullOrWhiteSpace(candidateAssembly.FullName)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(candidateAssembly.FullName))
                {
                    runtimeType = Type.GetType($"{typeName}, {candidateAssembly.FullName}", throwOnError: false);
                }
            }
            catch
            {
                // Branch: handles any exception that reaches this handler.
            }

            return false;
        }

        /// <summary>
        /// Emits emit struct copy mapping.
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
            TypeDef targetType)
        {
            // Branch: take this path when (!TryCollectStructCopyBindings(mapping, proxyType, targetType, out var bindings, out var failure)) evaluates to true.
            if (!TryCollectStructCopyBindings(mapping, proxyType, targetType, out var bindings, out var failure))
            {
                return failure!;
            }

            var importedTargetType = moduleDef.Import(targetType) as ITypeDefOrRef
                ?? throw new InvalidOperationException($"Unable to import target type '{targetType.FullName}'.");
            var importedProxyType = moduleDef.Import(proxyType) as ITypeDefOrRef
                ?? throw new InvalidOperationException($"Unable to import proxy type '{proxyType.FullName}'.");

            var importedTargetTypeSig = moduleDef.Import(targetType.ToTypeSig());
            var importedProxyTypeSig = moduleDef.Import(proxyType.ToTypeSig());

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.InitLocals = true;

            var targetLocal = new Local(importedTargetTypeSig);
            var proxyLocal = new Local(importedProxyTypeSig);
            activatorMethod.Body.Variables.Add(targetLocal);
            activatorMethod.Body.Variables.Add(proxyLocal);

            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            activatorMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(importedTargetType));
            activatorMethod.Body.Instructions.Add(OpCodes.Stloc.ToInstruction(targetLocal));

            activatorMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(proxyLocal));
            activatorMethod.Body.Instructions.Add(OpCodes.Initobj.ToInstruction(importedProxyType));

            foreach (var binding in bindings!)
            {
                var importedProxyField = moduleDef.Import(binding.ProxyField);
                activatorMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(proxyLocal));

                // Branch: take this path when (binding.SourceKind == StructCopySourceKind.Property) evaluates to true.
                if (binding.SourceKind == StructCopySourceKind.Property)
                {
                    var sourceProperty = binding.SourceProperty!;
                    var sourceGetter = sourceProperty.GetMethod!;
                    var importedSourceGetter = moduleDef.Import(sourceGetter);

                    // Branch: take this path when (!sourceGetter.IsStatic) evaluates to true.
                    if (!sourceGetter.IsStatic)
                    {
                        activatorMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc).ToInstruction(targetLocal));
                    }

                    // Branch: take this path when (!sourceGetter.IsStatic && targetType.IsValueType && (sourceGetter.IsVirtual || sourceGetter.DeclaringType.IsInterface)) evaluates to true.
                    if (!sourceGetter.IsStatic && targetType.IsValueType && (sourceGetter.IsVirtual || sourceGetter.DeclaringType.IsInterface))
                    {
                        activatorMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                        activatorMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedSourceGetter));
                    }
                    else
                    {
                        // Branch: fallback path when earlier branch conditions evaluate to false.
                        var callOpcode = sourceGetter.IsStatic ? OpCodes.Call : (sourceGetter.IsVirtual || sourceGetter.DeclaringType.IsInterface ? OpCodes.Callvirt : OpCodes.Call);
                        activatorMethod.Body.Instructions.Add(callOpcode.ToInstruction(importedSourceGetter));
                    }
                }
                else
                {
                    // Branch: fallback path when earlier branch conditions evaluate to false.
                    var sourceField = binding.SourceField!;
                    var importedSourceField = moduleDef.Import(sourceField);
                    // Branch: take this path when (sourceField.IsStatic) evaluates to true.
                    if (sourceField.IsStatic)
                    {
                        activatorMethod.Body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(importedSourceField));
                    }
                    else
                    {
                        // Branch: fallback path when earlier branch conditions evaluate to false.
                        activatorMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc).ToInstruction(targetLocal));
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
            activatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            initializeMethod.Body.Instructions.Add(OpCodes.Ldftn.ToInstruction(activatorMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.FuncObjectObjectCtor));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.RegisterAotProxyMethod));

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                mapping.ProxyAssemblyName,
                proxyType.FullName);
        }

        /// <summary>
        /// Attempts to try collect struct copy bindings.
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
            out IReadOnlyList<StructCopyFieldBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var collectedBindings = new List<StructCopyFieldBinding>();
            bindings = collectedBindings;
            failure = null;

            foreach (var proxyField in proxyStructType.Fields)
            {
                // Branch: take this path when (proxyField.IsStatic || proxyField.IsInitOnly || !proxyField.IsPublic) evaluates to true.
                if (proxyField.IsStatic || proxyField.IsInitOnly || !proxyField.IsPublic)
                {
                    continue;
                }

                // Branch: take this path when (proxyField.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, "Datadog.Trace.DuckTyping.DuckIgnoreAttribute", StringComparison.Ordinal))) evaluates to true.
                if (proxyField.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, "Datadog.Trace.DuckTyping.DuckIgnoreAttribute", StringComparison.Ordinal)))
                {
                    continue;
                }

                // Branch: take this path when (!TryResolveStructCopyFieldBinding(mapping, targetType, proxyField, out var binding, out failure)) evaluates to true.
                if (!TryResolveStructCopyFieldBinding(mapping, targetType, proxyField, out var binding, out failure))
                {
                    return false;
                }

                collectedBindings.Add(binding);
            }

            // Branch: take this path when (collectedBindings.Count == 0 && proxyStructType.Properties.Count > 0) evaluates to true.
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
        /// Attempts to try resolve struct copy field binding.
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
            out StructCopyFieldBinding binding,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            binding = default;
            failure = null;

            var hasFieldOnlyAttribute = false;
            var allowFieldFallback = false;
            foreach (var attribute in proxyField.CustomAttributes)
            {
                // Branch: take this path when (!IsDuckAttribute(attribute)) evaluates to true.
                if (!IsDuckAttribute(attribute))
                {
                    continue;
                }

                var kind = ResolveDuckKind(attribute);
                // Branch dispatch: select the execution path based on (kind).
                switch (kind)
                {
                    case DuckKindField:
                        // Branch: handles the case DuckKindField switch case.
                        hasFieldOnlyAttribute = true;
                        allowFieldFallback = true;
                        break;
                    case DuckKindPropertyOrField:
                        // Branch: handles the case DuckKindPropertyOrField switch case.
                        allowFieldFallback = true;
                        break;
                }

                // Branch: take this path when (hasFieldOnlyAttribute) evaluates to true.
                if (hasFieldOnlyAttribute)
                {
                    break;
                }
            }

            var candidateNames = TryGetDuckAttributeNames(proxyField.CustomAttributes, out var configuredNames)
                                     ? configuredNames
                                     : new[] { proxyField.Name.String ?? proxyField.Name.ToString() };

            // Branch: take this path when (!hasFieldOnlyAttribute && evaluates to true.
            if (!hasFieldOnlyAttribute &&
                TryFindStructCopyTargetProperty(targetType, candidateNames, out var targetProperty))
            {
                // Branch: take this path when (!TryCreateReturnConversion(proxyField.FieldSig.Type, targetProperty!.PropertySig.RetType, out var returnConversion)) evaluates to true.
                if (!TryCreateReturnConversion(proxyField.FieldSig.Type, targetProperty!.PropertySig.RetType, out var returnConversion))
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

            // Branch: take this path when (hasFieldOnlyAttribute || allowFieldFallback) evaluates to true.
            if (hasFieldOnlyAttribute || allowFieldFallback)
            {
                // Branch: take this path when (TryFindStructCopyTargetField(targetType, candidateNames, out var targetField)) evaluates to true.
                if (TryFindStructCopyTargetField(targetType, candidateNames, out var targetField))
                {
                    // Branch: take this path when (!TryCreateReturnConversion(proxyField.FieldSig.Type, targetField!.FieldSig.Type, out var returnConversion)) evaluates to true.
                    if (!TryCreateReturnConversion(proxyField.FieldSig.Type, targetField!.FieldSig.Type, out var returnConversion))
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
        /// Attempts to try find struct copy target property.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="candidateNames">The candidate names value.</param>
        /// <param name="targetProperty">The target property value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryFindStructCopyTargetProperty(TypeDef targetType, IReadOnlyList<string> candidateNames, out PropertyDef? targetProperty)
        {
            foreach (var candidateName in candidateNames)
            {
                var current = targetType;
                while (current is not null)
                {
                    foreach (var property in current.Properties)
                    {
                        // Branch: take this path when (!string.Equals(property.Name, candidateName, StringComparison.Ordinal)) evaluates to true.
                        if (!string.Equals(property.Name, candidateName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        // Branch: take this path when (property.GetMethod is null || property.GetMethod.MethodSig.Params.Count != 0) evaluates to true.
                        if (property.GetMethod is null || property.GetMethod.MethodSig.Params.Count != 0)
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
        /// Attempts to try find struct copy target field.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="candidateNames">The candidate names value.</param>
        /// <param name="targetField">The target field value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryFindStructCopyTargetField(TypeDef targetType, IReadOnlyList<string> candidateNames, out FieldDef? targetField)
        {
            foreach (var candidateName in candidateNames)
            {
                var current = targetType;
                while (current is not null)
                {
                    foreach (var field in current.Fields)
                    {
                        // Branch: take this path when (string.Equals(field.Name, candidateName, StringComparison.Ordinal)) evaluates to true.
                        if (string.Equals(field.Name, candidateName, StringComparison.Ordinal))
                        {
                            targetField = field;
                            return true;
                        }
                    }

                    current = current.BaseType?.ResolveTypeDef();
                }
            }

            targetField = null;
            return false;
        }

        /// <summary>
        /// Attempts to try create return conversion.
        /// </summary>
        /// <param name="proxyReturnType">The proxy return type value.</param>
        /// <param name="targetReturnType">The target return type value.</param>
        /// <param name="returnConversion">The return conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateReturnConversion(TypeSig proxyReturnType, TypeSig targetReturnType, out MethodReturnConversion returnConversion)
        {
            // Branch: take this path when (AreTypesEquivalent(proxyReturnType, targetReturnType)) evaluates to true.
            if (AreTypesEquivalent(proxyReturnType, targetReturnType))
            {
                returnConversion = MethodReturnConversion.None();
                return true;
            }

            // Branch: take this path when (TryGetValueWithTypeArgument(proxyReturnType, out var proxyReturnValueWithTypeArgument) && AreTypesEquivalent(proxyReturnValueWithTypeArgument!, targetReturnType)) evaluates to true.
            if (TryGetValueWithTypeArgument(proxyReturnType, out var proxyReturnValueWithTypeArgument) && AreTypesEquivalent(proxyReturnValueWithTypeArgument!, targetReturnType))
            {
                returnConversion = MethodReturnConversion.WrapValueWithType(proxyReturnType, proxyReturnValueWithTypeArgument!);
                return true;
            }

            // Branch: take this path when (IsDuckChainingRequired(targetReturnType, proxyReturnType)) evaluates to true.
            if (IsDuckChainingRequired(targetReturnType, proxyReturnType))
            {
                returnConversion = MethodReturnConversion.DuckChainToProxy(proxyReturnType, targetReturnType);
                return true;
            }

            // Branch: take this path when (CanUseTypeConversion(targetReturnType, proxyReturnType)) evaluates to true.
            if (CanUseTypeConversion(targetReturnType, proxyReturnType))
            {
                returnConversion = MethodReturnConversion.TypeConversion(targetReturnType, proxyReturnType);
                return true;
            }

            returnConversion = default;
            return false;
        }

        /// <summary>
        /// Executes copy method generic parameters.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="sourceMethod">The source method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        private static void CopyMethodGenericParameters(ModuleDef moduleDef, MethodDef sourceMethod, MethodDef targetMethod)
        {
            // Branch: take this path when (sourceMethod.GenericParameters.Count == 0) evaluates to true.
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
        /// Creates create method call target.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="importedTargetMethod">The imported target method value.</param>
        /// <param name="generatedMethod">The generated method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethod CreateMethodCallTarget(
            ModuleDef moduleDef,
            IMethodDefOrRef importedTargetMethod,
            MethodDef generatedMethod,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments)
        {
            // Branch: take this path when (closedGenericMethodArguments is not null && closedGenericMethodArguments.Count > 0) evaluates to true.
            if (closedGenericMethodArguments is not null && closedGenericMethodArguments.Count > 0)
            {
                var closedArguments = new List<TypeSig>(closedGenericMethodArguments.Count);
                for (var i = 0; i < closedGenericMethodArguments.Count; i++)
                {
                    closedArguments.Add(moduleDef.Import(closedGenericMethodArguments[i]));
                }

                var methodSpecWithClosedArguments = new MethodSpecUser(importedTargetMethod, new GenericInstMethodSig(closedArguments));
                return moduleDef.UpdateRowId(methodSpecWithClosedArguments);
            }

            // Branch: take this path when (generatedMethod.MethodSig.GenParamCount == 0) evaluates to true.
            if (generatedMethod.MethodSig.GenParamCount == 0)
            {
                return importedTargetMethod;
            }

            var genericArguments = new List<TypeSig>((int)generatedMethod.MethodSig.GenParamCount);
            for (var genericParameterIndex = 0; genericParameterIndex < generatedMethod.MethodSig.GenParamCount; genericParameterIndex++)
            {
                genericArguments.Add(new GenericMVar((uint)genericParameterIndex));
            }

            var methodSpec = new MethodSpecUser(importedTargetMethod, new GenericInstMethodSig(genericArguments));
            return moduleDef.UpdateRowId(methodSpec);
        }

        /// <summary>
        /// Emits emit method return conversion.
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
            // Branch: take this path when (conversion.Kind == MethodReturnConversionKind.WrapValueWithType) evaluates to true.
            if (conversion.Kind == MethodReturnConversionKind.WrapValueWithType)
            {
                var importedTargetReturnType = ResolveImportedTypeForTypeToken(moduleDef, conversion.InnerTypeSig!, context);
                methodBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetReturnType));
                methodBody.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));

                var createMethodRef = CreateValueWithTypeCreateMethodRef(
                    moduleDef,
                    conversion.WrapperTypeSig!,
                    conversion.InnerTypeSig!);
                methodBody.Instructions.Add(OpCodes.Call.ToInstruction(createMethodRef));
                return;
            }

            // Branch: take this path when (conversion.Kind == MethodReturnConversionKind.TypeConversion) evaluates to true.
            if (conversion.Kind == MethodReturnConversionKind.TypeConversion)
            {
                EmitTypeConversion(moduleDef, methodBody, conversion.WrapperTypeSig!, conversion.InnerTypeSig!, context);
                return;
            }

            // Branch: take this path when (conversion.Kind == MethodReturnConversionKind.DuckChainToProxy) evaluates to true.
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
        /// Emits emit method argument conversion.
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
            // Branch dispatch: select the execution path based on (conversion.Kind).
            switch (conversion.Kind)
            {
                case MethodArgumentConversionKind.None:
                    // Branch: handles the case MethodArgumentConversionKind.None switch case.
                    return;
                case MethodArgumentConversionKind.UnwrapValueWithType:
                    // Branch: handles the case MethodArgumentConversionKind.UnwrapValueWithType switch case.
                {
                    var valueFieldRef = CreateValueWithTypeValueFieldRef(moduleDef, conversion.WrapperTypeSig!, conversion.InnerTypeSig!);
                    methodBody.Instructions.Add(OpCodes.Ldfld.ToInstruction(valueFieldRef));
                    return;
                }

                case MethodArgumentConversionKind.ExtractDuckTypeInstance:
                    // Branch: handles the case MethodArgumentConversionKind.ExtractDuckTypeInstance switch case.
                    methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                    methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                    EmitObjectToExpectedTypeConversion(moduleDef, methodBody, conversion.InnerTypeSig!, context);
                    return;
                case MethodArgumentConversionKind.TypeConversion:
                    // Branch: handles the case MethodArgumentConversionKind.TypeConversion switch case.
                    EmitTypeConversion(moduleDef, methodBody, conversion.WrapperTypeSig!, conversion.InnerTypeSig!, context);
                    return;
                default:
                    // Branch: fallback switch case when no explicit case label matches.
                    throw new InvalidOperationException($"Unsupported method argument conversion '{conversion.Kind}'.");
            }
        }

        /// <summary>
        /// Emits emit load by ref value.
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
        /// Emits emit store by ref value.
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
        /// Executes can use type conversion.
        /// </summary>
        /// <param name="actualTypeSig">The actual type sig value.</param>
        /// <param name="expectedTypeSig">The expected type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool CanUseTypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
        {
            // Branch: take this path when (actualTypeSig.ElementType == ElementType.ByRef || expectedTypeSig.ElementType == ElementType.ByRef) evaluates to true.
            if (actualTypeSig.ElementType == ElementType.ByRef || expectedTypeSig.ElementType == ElementType.ByRef)
            {
                return false;
            }

            // Branch: take this path when (actualTypeSig.IsGenericParameter || expectedTypeSig.IsGenericParameter) evaluates to true.
            if (actualTypeSig.IsGenericParameter || expectedTypeSig.IsGenericParameter)
            {
                return actualTypeSig.IsGenericParameter && expectedTypeSig.IsGenericParameter && AreTypesEquivalent(actualTypeSig, expectedTypeSig);
            }

            var actualRuntimeType = TryResolveRuntimeType(actualTypeSig);
            var expectedRuntimeType = TryResolveRuntimeType(expectedTypeSig);
            // Branch: take this path when (actualRuntimeType is not null && expectedRuntimeType is not null) evaluates to true.
            if (actualRuntimeType is not null && expectedRuntimeType is not null)
            {
                return CanUseTypeConversion(actualRuntimeType, expectedRuntimeType);
            }

            var actualUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(actualTypeSig);
            var expectedUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(expectedTypeSig);
            // Branch: take this path when (AreTypesEquivalent(actualUnderlyingTypeSig, expectedUnderlyingTypeSig)) evaluates to true.
            if (AreTypesEquivalent(actualUnderlyingTypeSig, expectedUnderlyingTypeSig))
            {
                return true;
            }

            // Branch: take this path when (actualUnderlyingTypeSig.IsValueType) evaluates to true.
            if (actualUnderlyingTypeSig.IsValueType)
            {
                // Branch: take this path when (expectedUnderlyingTypeSig.IsValueType) evaluates to true.
                if (expectedUnderlyingTypeSig.IsValueType)
                {
                    return false;
                }

                return IsObjectTypeSig(expectedUnderlyingTypeSig)
                    || IsTypeAssignableFrom(expectedUnderlyingTypeSig, actualUnderlyingTypeSig);
            }

            // Branch: take this path when (expectedUnderlyingTypeSig.IsValueType) evaluates to true.
            if (expectedUnderlyingTypeSig.IsValueType)
            {
                return IsObjectTypeSig(actualUnderlyingTypeSig)
                    || IsTypeAssignableFrom(actualUnderlyingTypeSig, expectedUnderlyingTypeSig);
            }

            return true;
        }

        /// <summary>
        /// Executes can use type conversion.
        /// </summary>
        /// <param name="actualType">The actual type value.</param>
        /// <param name="expectedType">The expected type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool CanUseTypeConversion(Type actualType, Type expectedType)
        {
            var actualUnderlyingType = actualType.IsEnum ? Enum.GetUnderlyingType(actualType) : actualType;
            var expectedUnderlyingType = expectedType.IsEnum ? Enum.GetUnderlyingType(expectedType) : expectedType;

            // Branch: take this path when (actualUnderlyingType == expectedUnderlyingType) evaluates to true.
            if (actualUnderlyingType == expectedUnderlyingType)
            {
                return true;
            }

            // Branch: take this path when (actualUnderlyingType.IsValueType) evaluates to true.
            if (actualUnderlyingType.IsValueType)
            {
                // Branch: take this path when (expectedUnderlyingType.IsValueType) evaluates to true.
                if (expectedUnderlyingType.IsValueType)
                {
                    return false;
                }

                return expectedUnderlyingType == typeof(object) || expectedUnderlyingType.IsAssignableFrom(actualUnderlyingType);
            }

            // Branch: take this path when (expectedUnderlyingType.IsValueType) evaluates to true.
            if (expectedUnderlyingType.IsValueType)
            {
                return actualUnderlyingType == typeof(object) || actualUnderlyingType.IsAssignableFrom(expectedUnderlyingType);
            }

            return true;
        }

        /// <summary>
        /// Emits emit type conversion.
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
            // Branch: take this path when (actualTypeSig.IsGenericParameter && expectedTypeSig.IsGenericParameter) evaluates to true.
            if (actualTypeSig.IsGenericParameter && expectedTypeSig.IsGenericParameter)
            {
                return;
            }

            var actualUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(actualTypeSig);
            var expectedUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(expectedTypeSig);
            // Branch: take this path when (AreTypesEquivalent(actualUnderlyingTypeSig, expectedUnderlyingTypeSig)) evaluates to true.
            if (AreTypesEquivalent(actualUnderlyingTypeSig, expectedUnderlyingTypeSig))
            {
                return;
            }

            // Branch: take this path when (actualUnderlyingTypeSig.IsValueType) evaluates to true.
            if (actualUnderlyingTypeSig.IsValueType)
            {
                // Branch: take this path when (expectedUnderlyingTypeSig.IsValueType) evaluates to true.
                if (expectedUnderlyingTypeSig.IsValueType)
                {
                    throw new InvalidOperationException($"Unsupported value-type conversion from '{actualTypeSig.FullName}' to '{expectedTypeSig.FullName}' in {context}.");
                }

                var importedActualType = ResolveImportedTypeForTypeToken(moduleDef, actualTypeSig, context);
                methodBody.Instructions.Add(OpCodes.Box.ToInstruction(importedActualType));
                // Branch: take this path when (!IsObjectTypeSig(expectedUnderlyingTypeSig)) evaluates to true.
                if (!IsObjectTypeSig(expectedUnderlyingTypeSig))
                {
                    var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedUnderlyingTypeSig, context);
                    methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
                }

                return;
            }

            // Branch: take this path when (expectedUnderlyingTypeSig.IsValueType) evaluates to true.
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

            // Branch: take this path when (!IsObjectTypeSig(expectedUnderlyingTypeSig)) evaluates to true.
            if (!IsObjectTypeSig(expectedUnderlyingTypeSig))
            {
                var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedUnderlyingTypeSig, context);
                methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
            }
        }

        /// <summary>
        /// Gets get underlying type for type conversion.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static TypeSig GetUnderlyingTypeForTypeConversion(TypeSig typeSig)
        {
            var typeDef = typeSig.ToTypeDefOrRef()?.ResolveTypeDef();
            // Branch: take this path when (typeDef?.IsEnum != true) evaluates to true.
            if (typeDef?.IsEnum != true)
            {
                return typeSig;
            }

            foreach (var field in typeDef.Fields)
            {
                // Branch: take this path when (field.IsSpecialName && string.Equals(field.Name, "value__", StringComparison.Ordinal)) evaluates to true.
                if (field.IsSpecialName && string.Equals(field.Name, "value__", StringComparison.Ordinal))
                {
                    return field.FieldSig.Type;
                }
            }

            return typeSig;
        }

        /// <summary>
        /// Attempts to try resolve runtime type.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static Type? TryResolveRuntimeType(TypeSig typeSig)
        {
            // Branch: take this path when (typeSig.IsGenericParameter) evaluates to true.
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
        /// Attempts to try resolve runtime type from type def or ref.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static Type? TryResolveRuntimeTypeFromTypeDefOrRef(TypeSig typeSig)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            // Branch: take this path when (typeDefOrRef is null) evaluates to true.
            if (typeDefOrRef is null)
            {
                return null;
            }

            var reflectionName = typeDefOrRef.ReflectionFullName;
            // Branch: take this path when (string.IsNullOrWhiteSpace(reflectionName)) evaluates to true.
            if (string.IsNullOrWhiteSpace(reflectionName))
            {
                reflectionName = typeDefOrRef.FullName;
            }

            // Branch: take this path when (string.IsNullOrWhiteSpace(reflectionName)) evaluates to true.
            if (string.IsNullOrWhiteSpace(reflectionName))
            {
                return null;
            }

            reflectionName = reflectionName.Replace('/', '+');
            var assemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(typeDefOrRef.DefinitionAssembly?.Name.String ?? string.Empty);
            // Branch: take this path when (!string.IsNullOrWhiteSpace(assemblyName)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(assemblyName))
            {
                var assemblyQualifiedName = $"{reflectionName}, {assemblyName}";
                var resolvedFromAssembly = Type.GetType(assemblyQualifiedName, throwOnError: false);
                // Branch: take this path when (resolvedFromAssembly is not null) evaluates to true.
                if (resolvedFromAssembly is not null)
                {
                    return resolvedFromAssembly;
                }
            }

            return Type.GetType(reflectionName, throwOnError: false);
        }

        /// <summary>
        /// Determines whether is object type sig.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsObjectTypeSig(TypeSig typeSig)
        {
            // Branch: take this path when (typeSig.ElementType == ElementType.Object) evaluates to true.
            if (typeSig.ElementType == ElementType.Object)
            {
                return true;
            }

            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            // Branch: take this path when (typeDefOrRef is null) evaluates to true.
            if (typeDefOrRef is null)
            {
                return false;
            }

            return string.Equals(typeDefOrRef.FullName, "System.Object", StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether is type assignable from.
        /// </summary>
        /// <param name="candidateBaseTypeSig">The candidate base type sig value.</param>
        /// <param name="derivedTypeSig">The derived type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsTypeAssignableFrom(TypeSig candidateBaseTypeSig, TypeSig derivedTypeSig)
        {
            var candidateBaseType = candidateBaseTypeSig.ToTypeDefOrRef();
            var derivedType = derivedTypeSig.ToTypeDefOrRef();
            // Branch: take this path when (candidateBaseType is null || derivedType is null) evaluates to true.
            if (candidateBaseType is null || derivedType is null)
            {
                return false;
            }

            return IsAssignableFrom(candidateBaseType, derivedType);
        }

        /// <summary>
        /// Emits emit i duck type implementation.
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
            // Branch: take this path when (generatedType.FindMethod("get_Instance") is null) evaluates to true.
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
                // Branch: take this path when (targetIsValueType) evaluates to true.
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

            // Branch: take this path when (generatedType.FindMethod("get_Type") is null) evaluates to true.
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

            // Branch: take this path when (generatedType.FindMethod("GetInternalDuckTypedInstance") is null) evaluates to true.
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

            // Branch: take this path when (generatedType.FindMethod("ToString") is null) evaluates to true.
            if (generatedType.FindMethod("ToString") is null)
            {
                var toStringMethod = new MethodDefUser(
                    "ToString",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.String),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot);
                toStringMethod.Body = new CilBody();
                // Branch: take this path when (targetIsValueType) evaluates to true.
                if (targetIsValueType)
                {
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldflda.ToInstruction(targetField));
                    toStringMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.ObjectToStringMethod));
                }
                else
                {
                    // Branch: fallback path when earlier branch conditions evaluate to false.
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
        /// Gets get interface method attributes.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static MethodAttributes GetInterfaceMethodAttributes(MethodDef proxyMethod)
        {
            var attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;
            // Branch: take this path when (proxyMethod.IsSpecialName) evaluates to true.
            if (proxyMethod.IsSpecialName)
            {
                attributes |= MethodAttributes.SpecialName;
            }

            // Branch: take this path when (proxyMethod.IsRuntimeSpecialName) evaluates to true.
            if (proxyMethod.IsRuntimeSpecialName)
            {
                attributes |= MethodAttributes.RTSpecialName;
            }

            return attributes;
        }

        /// <summary>
        /// Gets get class override method attributes.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static MethodAttributes GetClassOverrideMethodAttributes(MethodDef proxyMethod)
        {
            var memberAccess = proxyMethod.Attributes & MethodAttributes.MemberAccessMask;
            // Branch: take this path when (memberAccess == 0 || memberAccess == MethodAttributes.Private) evaluates to true.
            if (memberAccess == 0 || memberAccess == MethodAttributes.Private)
            {
                memberAccess = MethodAttributes.Public;
            }

            var attributes = memberAccess | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.ReuseSlot;
            // Branch: take this path when (proxyMethod.IsSpecialName) evaluates to true.
            if (proxyMethod.IsSpecialName)
            {
                attributes |= MethodAttributes.SpecialName;
            }

            // Branch: take this path when (proxyMethod.IsRuntimeSpecialName) evaluates to true.
            if (proxyMethod.IsRuntimeSpecialName)
            {
                attributes |= MethodAttributes.RTSpecialName;
            }

            return attributes;
        }

        /// <summary>
        /// Attempts to try collect forward bindings.
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
            bool isInterfaceProxy,
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            // Branch: take this path when (isInterfaceProxy) evaluates to true.
            if (isInterfaceProxy)
            {
                return TryCollectForwardInterfaceBindings(mapping, proxyType, targetType, out bindings, out failure);
            }

            return TryCollectForwardClassBindings(mapping, proxyType, targetType, out bindings, out failure);
        }

        /// <summary>
        /// Attempts to try collect forward interface bindings.
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
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var collectedBindings = new List<ForwardBinding>();
            bindings = collectedBindings;
            failure = null;

            var proxyMethods = GetInterfaceMethods(proxyInterfaceType);
            foreach (var proxyMethod in proxyMethods)
            {
                // Branch: take this path when (proxyMethod.IsStatic) evaluates to true.
                if (proxyMethod.IsStatic)
                {
                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        $"Proxy method '{proxyMethod.FullName}' is static. Static interface members are not emitted in this phase.");
                    return false;
                }

                // Branch: take this path when (!TryResolveForwardBinding(mapping, targetType, proxyMethod, out var binding, out failure)) evaluates to true.
                if (!TryResolveForwardBinding(mapping, targetType, proxyMethod, out var binding, out failure))
                {
                    return false;
                }

                collectedBindings.Add(binding);
            }

            return true;
        }

        /// <summary>
        /// Attempts to try collect forward class bindings.
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
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var collectedBindings = new List<ForwardBinding>();
            bindings = collectedBindings;
            failure = null;

            var proxyMethods = GetClassProxyMethods(proxyClassType);
            // Branch: take this path when (proxyMethods.Count == 0) evaluates to true.
            if (proxyMethods.Count == 0)
            {
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnsupportedProxyKind,
                    $"Proxy class '{mapping.ProxyTypeName}' does not expose overridable public/protected instance methods.");
                return false;
            }

            foreach (var proxyMethod in proxyMethods)
            {
                // Branch: take this path when (!TryResolveForwardBinding(mapping, targetType, proxyMethod, out var binding, out failure)) evaluates to true.
                if (!TryResolveForwardBinding(mapping, targetType, proxyMethod, out var binding, out failure))
                {
                    return false;
                }

                collectedBindings.Add(binding);
            }

            return true;
        }

        /// <summary>
        /// Gets get interface methods.
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
                // Branch: take this path when (!visitedTypes.Add(current.FullName)) evaluates to true.
                if (!visitedTypes.Add(current.FullName))
                {
                    continue;
                }

                foreach (var method in current.Methods)
                {
                    // Branch: take this path when (method.IsConstructor || method.IsStatic) evaluates to true.
                    if (method.IsConstructor || method.IsStatic)
                    {
                        continue;
                    }

                    var key = $"{method.Name}::{method.MethodSig}";
                    // Branch: take this path when (visitedMethods.Add(key)) evaluates to true.
                    if (visitedMethods.Add(key))
                    {
                        results.Add(method);
                    }
                }

                foreach (var interfaceImpl in current.Interfaces)
                {
                    var resolvedInterface = interfaceImpl.Interface.ResolveTypeDef();
                    // Branch: take this path when (resolvedInterface is not null) evaluates to true.
                    if (resolvedInterface is not null)
                    {
                        stack.Push(resolvedInterface);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets get class proxy methods.
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
                    // Branch: take this path when (!IsSupportedClassProxyMethod(method)) evaluates to true.
                    if (!IsSupportedClassProxyMethod(method))
                    {
                        continue;
                    }

                    var key = $"{method.Name}::{method.MethodSig}";
                    // Branch: take this path when (visitedMethodKeys.Add(key)) evaluates to true.
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
        /// Determines whether is supported class proxy method.
        /// </summary>
        /// <param name="method">The method value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsSupportedClassProxyMethod(MethodDef method)
        {
            // Branch: take this path when (method.IsConstructor || method.IsStatic || !method.IsVirtual || method.IsFinal) evaluates to true.
            if (method.IsConstructor || method.IsStatic || !method.IsVirtual || method.IsFinal)
            {
                return false;
            }

            return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
        }

        /// <summary>
        /// Executes find supported proxy base constructor.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static MethodDef? FindSupportedProxyBaseConstructor(TypeDef proxyType)
        {
            foreach (var constructor in proxyType.Methods)
            {
                // Branch: take this path when (!constructor.IsConstructor || constructor.IsStatic || constructor.MethodSig.Params.Count != 0) evaluates to true.
                if (!constructor.IsConstructor || constructor.IsStatic || constructor.MethodSig.Params.Count != 0)
                {
                    continue;
                }

                // Branch: take this path when (constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly) evaluates to true.
                if (constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly)
                {
                    return constructor;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to try resolve forward binding.
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
            MethodDef proxyMethod,
            out ForwardBinding binding,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            binding = default;
            failure = null;

            var fieldResolutionMode = GetFieldResolutionMode(proxyMethod);
            var fieldOnly = fieldResolutionMode == FieldResolutionMode.FieldOnly;
            var allowFieldFallback = fieldResolutionMode != FieldResolutionMode.Disabled;
            MethodCompatibilityFailure? firstMethodFailure = null;
            // Branch: take this path when (!TryResolveForwardClosedGenericMethodArguments(targetType, proxyMethod, out var closedGenericMethodArguments, out var closedGenericMethodArgumentsFailureReason)) evaluates to true.
            if (!TryResolveForwardClosedGenericMethodArguments(targetType, proxyMethod, out var closedGenericMethodArguments, out var closedGenericMethodArgumentsFailureReason))
            {
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                    StatusCodeIncompatibleSignature,
                    closedGenericMethodArgumentsFailureReason ?? $"Unable to resolve generic type arguments for proxy method '{proxyMethod.FullName}'.");
                return false;
            }

            // Branch: take this path when (!fieldOnly) evaluates to true.
            if (!fieldOnly)
            {
                foreach (var targetMethod in FindForwardTargetMethodCandidates(mapping, targetType, proxyMethod, closedGenericMethodArguments))
                {
                    // Branch: take this path when (TryCreateForwardMethodBinding(proxyMethod, targetMethod, closedGenericMethodArguments, out var methodBinding, out var methodFailure)) evaluates to true.
                    if (TryCreateForwardMethodBinding(proxyMethod, targetMethod, closedGenericMethodArguments, out var methodBinding, out var methodFailure))
                    {
                        binding = ForwardBinding.ForMethod(proxyMethod, targetMethod, methodBinding);
                        return true;
                    }

                    firstMethodFailure ??= methodFailure;
                }
            }

            // Branch: take this path when (allowFieldFallback) evaluates to true.
            if (allowFieldFallback)
            {
                // Branch: take this path when (!TryGetFieldAccessorKind(proxyMethod, out var fieldAccessorKind)) evaluates to true.
                if (!TryGetFieldAccessorKind(proxyMethod, out var fieldAccessorKind))
                {
                    // Branch: take this path when (fieldOnly) evaluates to true.
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
                    // Branch: fallback path when earlier branch conditions evaluate to false.
                    // Branch: take this path when (TryFindForwardTargetField(targetType, proxyMethod, fieldAccessorKind, out var targetField, out var fieldBinding, out var fieldFailureReason)) evaluates to true.
                    if (TryFindForwardTargetField(targetType, proxyMethod, fieldAccessorKind, out var targetField, out var fieldBinding, out var fieldFailureReason))
                    {
                        binding = fieldAccessorKind == FieldAccessorKind.Getter
                                      ? ForwardBinding.ForFieldGet(proxyMethod, targetField!, fieldBinding)
                                      : ForwardBinding.ForFieldSet(proxyMethod, targetField!, fieldBinding);
                        return true;
                    }

                    // Branch: take this path when (!string.IsNullOrWhiteSpace(fieldFailureReason)) evaluates to true.
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

            // Branch: take this path when (firstMethodFailure is not null) evaluates to true.
            if (firstMethodFailure is not null)
            {
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                    StatusCodeIncompatibleSignature,
                    firstMethodFailure.Value.Detail);
                return false;
            }

            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                StatusCodeMissingMethod,
                $"Target member for proxy method '{proxyMethod.FullName}' was not found.");
            return false;
        }

        /// <summary>
        /// Executes find forward target method candidates.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<MethodDef> FindForwardTargetMethodCandidates(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            MethodDef proxyMethod,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments)
        {
            _ = TryGetForwardExplicitInterfaceTypeNames(
                proxyMethod,
                out var explicitInterfaceTypeNames,
                out var useRelaxedNameComparison);
            var expectedGenericArity = closedGenericMethodArguments?.Count ?? (int)proxyMethod.MethodSig.GenParamCount;

            // Branch: take this path when (mapping.Mode == DuckTypeAotMappingMode.Reverse) evaluates to true.
            if (mapping.Mode == DuckTypeAotMappingMode.Reverse)
            {
                var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);
                foreach (var candidate in FindDefaultTargetMethodCandidates(
                             targetType,
                             proxyMethod,
                             explicitInterfaceTypeNames,
                             useRelaxedNameComparison,
                             expectedGenericArity))
                {
                    var candidateKey = GetMethodCandidateKey(candidate);
                    // Branch: take this path when (emittedCandidates.Add(candidateKey)) evaluates to true.
                    if (emittedCandidates.Add(candidateKey))
                    {
                        yield return candidate;
                    }
                }

                foreach (var reverseCandidate in FindReverseTargetMethodCandidates(targetType, proxyMethod))
                {
                    // Branch: take this path when (reverseCandidate.MethodSig.GenParamCount != expectedGenericArity || evaluates to true.
                    if (reverseCandidate.MethodSig.GenParamCount != expectedGenericArity ||
                        reverseCandidate.MethodSig.Params.Count != proxyMethod.MethodSig.Params.Count)
                    {
                        continue;
                    }

                    var reverseCandidateKey = GetMethodCandidateKey(reverseCandidate);
                    // Branch: take this path when (emittedCandidates.Add(reverseCandidateKey)) evaluates to true.
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
                         expectedGenericArity))
            {
                yield return candidate;
            }
        }

        /// <summary>
        /// Executes find default target method candidates.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="explicitInterfaceTypeNames">The explicit interface type names value.</param>
        /// <param name="useRelaxedNameComparison">The use relaxed name comparison value.</param>
        /// <param name="expectedGenericArity">The expected generic arity value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<MethodDef> FindDefaultTargetMethodCandidates(
            TypeDef targetType,
            MethodDef proxyMethod,
            IReadOnlyList<string> explicitInterfaceTypeNames,
            bool useRelaxedNameComparison,
            int expectedGenericArity)
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
                        // Branch: take this path when (!IsForwardTargetMethodNameMatch( evaluates to true.
                        if (!IsForwardTargetMethodNameMatch(
                                candidateMethodActualName,
                                candidateMethodName,
                                explicitInterfaceTypeNames,
                                useRelaxedNameComparison))
                        {
                            continue;
                        }

                        // Branch: take this path when (candidate.MethodSig.GenParamCount != expectedGenericArity || evaluates to true.
                        if (candidate.MethodSig.GenParamCount != expectedGenericArity ||
                            candidate.MethodSig.Params.Count != proxyMethod.MethodSig.Params.Count)
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
        /// Gets get method candidate key.
        /// </summary>
        /// <param name="candidate">The candidate value.</param>
        /// <returns>The resulting string value.</returns>
        private static string GetMethodCandidateKey(MethodDef candidate)
        {
            return $"{candidate.DeclaringType.FullName}::{candidate.Name}::{candidate.MethodSig}";
        }

        /// <summary>
        /// Executes find reverse target method candidates.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<MethodDef> FindReverseTargetMethodCandidates(TypeDef targetType, MethodDef proxyMethod)
        {
            var proxyMethodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();
            var proxyParameterTypeNames = proxyMethod.MethodSig.Params
                                              .Select(GetTypeComparisonNames)
                                              .ToArray();
            var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);

            var current = targetType;
            while (current is not null)
            {
                foreach (var method in current.Methods)
                {
                    // Branch: take this path when (method.IsConstructor || method.IsStatic) evaluates to true.
                    if (method.IsConstructor || method.IsStatic)
                    {
                        continue;
                    }

                    foreach (var reverseAttribute in method.CustomAttributes.Where(IsReverseMethodAttribute))
                    {
                        // Branch: take this path when (!IsReverseCandidateMatch(proxyMethodName, proxyParameterTypeNames, reverseAttribute, method.Name.String ?? method.Name.ToString())) evaluates to true.
                        if (!IsReverseCandidateMatch(proxyMethodName, proxyParameterTypeNames, reverseAttribute, method.Name.String ?? method.Name.ToString()))
                        {
                            continue;
                        }

                        var candidateKey = $"{method.DeclaringType.FullName}::{method.Name}::{method.MethodSig}";
                        // Branch: take this path when (emittedCandidates.Add(candidateKey)) evaluates to true.
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
                        // Branch: take this path when (property.GetMethod is not null && IsReverseCandidateMatch(proxyMethodName, proxyParameterTypeNames, reverseAttribute, "get_" + property.Name)) evaluates to true.
                        if (property.GetMethod is not null && IsReverseCandidateMatch(proxyMethodName, proxyParameterTypeNames, reverseAttribute, "get_" + property.Name))
                        {
                            var candidateKey = $"{property.GetMethod.DeclaringType.FullName}::{property.GetMethod.Name}::{property.GetMethod.MethodSig}";
                            // Branch: take this path when (emittedCandidates.Add(candidateKey)) evaluates to true.
                            if (emittedCandidates.Add(candidateKey))
                            {
                                yield return property.GetMethod;
                            }
                        }

                        // Branch: take this path when (property.SetMethod is not null && IsReverseCandidateMatch(proxyMethodName, proxyParameterTypeNames, reverseAttribute, "set_" + property.Name)) evaluates to true.
                        if (property.SetMethod is not null && IsReverseCandidateMatch(proxyMethodName, proxyParameterTypeNames, reverseAttribute, "set_" + property.Name))
                        {
                            var candidateKey = $"{property.SetMethod.DeclaringType.FullName}::{property.SetMethod.Name}::{property.SetMethod.MethodSig}";
                            // Branch: take this path when (emittedCandidates.Add(candidateKey)) evaluates to true.
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
        /// Determines whether is forward target method name match.
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
            // Branch: take this path when (string.Equals(candidateMethodName, requestedMethodName, StringComparison.Ordinal)) evaluates to true.
            if (string.Equals(candidateMethodName, requestedMethodName, StringComparison.Ordinal))
            {
                return true;
            }

            // Branch: take this path when (useRelaxedNameComparison && evaluates to true.
            if (useRelaxedNameComparison &&
                candidateMethodName.EndsWith("." + requestedMethodName, StringComparison.Ordinal))
            {
                return true;
            }

            for (var i = 0; i < explicitInterfaceTypeNames.Count; i++)
            {
                var explicitInterfaceTypeName = explicitInterfaceTypeNames[i];
                // Branch: take this path when (string.IsNullOrWhiteSpace(explicitInterfaceTypeName)) evaluates to true.
                if (string.IsNullOrWhiteSpace(explicitInterfaceTypeName))
                {
                    continue;
                }

                var normalizedInterfaceTypeName = explicitInterfaceTypeName.Replace("+", ".");
                // Branch: take this path when (string.Equals(candidateMethodName, $"{normalizedInterfaceTypeName}.{requestedMethodName}", StringComparison.Ordinal)) evaluates to true.
                if (string.Equals(candidateMethodName, $"{normalizedInterfaceTypeName}.{requestedMethodName}", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to try get forward explicit interface type names.
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
            // Branch: take this path when (TryGetDeclaringProperty(proxyMethod, out var declaringProperty)) evaluates to true.
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
                    // Branch: take this path when (!IsDuckAttribute(customAttribute)) evaluates to true.
                    if (!IsDuckAttribute(customAttribute))
                    {
                        continue;
                    }

                    foreach (var namedArgument in customAttribute.NamedArguments)
                    {
                        // Branch: take this path when (!string.Equals(namedArgument.Name.String, "ExplicitInterfaceTypeName", StringComparison.Ordinal)) evaluates to true.
                        if (!string.Equals(namedArgument.Name.String, "ExplicitInterfaceTypeName", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        // Branch: take this path when (!TryGetStringArgument(namedArgument.Argument.Value, out var configuredName)) evaluates to true.
                        if (!TryGetStringArgument(namedArgument.Argument.Value, out var configuredName))
                        {
                            continue;
                        }

                        foreach (var candidateName in SplitDuckNames(configuredName!))
                        {
                            // Branch: take this path when (string.Equals(candidateName, "*", StringComparison.Ordinal)) evaluates to true.
                            if (string.Equals(candidateName, "*", StringComparison.Ordinal))
                            {
                                useRelaxed = true;
                                continue;
                            }

                            // Branch: take this path when (!string.IsNullOrWhiteSpace(candidateName)) evaluates to true.
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
        /// Attempts to try create forward method binding.
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
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            out ForwardMethodBindingInfo binding,
            out MethodCompatibilityFailure? failure)
        {
            // Branch: take this path when (proxyMethod.MethodSig.Params.Count != targetMethod.MethodSig.Params.Count) evaluates to true.
            if (proxyMethod.MethodSig.Params.Count != targetMethod.MethodSig.Params.Count)
            {
                failure = new MethodCompatibilityFailure(
                    $"Parameter count mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                binding = default;
                return false;
            }

            // Branch: take this path when (closedGenericMethodArguments is null) evaluates to true.
            if (closedGenericMethodArguments is null)
            {
                // Branch: take this path when (proxyMethod.MethodSig.GenParamCount != targetMethod.MethodSig.GenParamCount) evaluates to true.
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
                // Branch: fallback path when earlier branch conditions evaluate to false.
                // Branch: take this path when (proxyMethod.MethodSig.GenParamCount != 0 || targetMethod.MethodSig.GenParamCount != closedGenericMethodArguments.Count) evaluates to true.
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
                // Branch: take this path when (!TryCreateForwardMethodParameterBinding(proxyMethod, targetMethod, closedGenericMethodArguments, parameterIndex, out var parameterBinding, out failure)) evaluates to true.
                if (!TryCreateForwardMethodParameterBinding(proxyMethod, targetMethod, closedGenericMethodArguments, parameterIndex, out var parameterBinding, out failure))
                {
                    binding = default;
                    return false;
                }

                parameterBindings[parameterIndex] = parameterBinding;
            }

            var targetReturnType = SubstituteMethodGenericTypeArguments(targetMethod.MethodSig.RetType, closedGenericMethodArguments);
            // Branch: take this path when (!TryCreateReturnConversion(proxyMethod.MethodSig.RetType, targetReturnType, out var returnConversion)) evaluates to true.
            if (!TryCreateReturnConversion(proxyMethod.MethodSig.RetType, targetReturnType, out var returnConversion))
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
        /// Attempts to try create forward method parameter binding.
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
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            int parameterIndex,
            out MethodParameterBinding parameterBinding,
            out MethodCompatibilityFailure? failure)
        {
            var proxyParameterType = proxyMethod.MethodSig.Params[parameterIndex];
            var targetParameterType = SubstituteMethodGenericTypeArguments(targetMethod.MethodSig.Params[parameterIndex], closedGenericMethodArguments);

            var proxyIsByRef = proxyParameterType.ElementType == ElementType.ByRef;
            var targetIsByRef = targetParameterType.ElementType == ElementType.ByRef;
            // Branch: take this path when (proxyIsByRef != targetIsByRef) evaluates to true.
            if (proxyIsByRef != targetIsByRef)
            {
                failure = new MethodCompatibilityFailure(
                    $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                parameterBinding = default;
                return false;
            }

            // Branch: take this path when (!proxyIsByRef) evaluates to true.
            if (!proxyIsByRef)
            {
                // Branch: take this path when (!TryCreateMethodArgumentConversion(proxyParameterType, targetParameterType, out var argumentConversion)) evaluates to true.
                if (!TryCreateMethodArgumentConversion(proxyParameterType, targetParameterType, out var argumentConversion))
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
            // Branch: take this path when (proxyIsOut != targetIsOut || proxyIsIn != targetIsIn) evaluates to true.
            if (proxyIsOut != targetIsOut || proxyIsIn != targetIsIn)
            {
                failure = new MethodCompatibilityFailure(
                    $"Parameter direction mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                parameterBinding = default;
                return false;
            }

            // Branch: take this path when (!TryGetByRefElementType(proxyParameterType, out var proxyByRefElementTypeSig) || evaluates to true.
            if (!TryGetByRefElementType(proxyParameterType, out var proxyByRefElementTypeSig) ||
                !TryGetByRefElementType(targetParameterType, out var targetByRefElementTypeSig))
            {
                failure = new MethodCompatibilityFailure(
                    $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                parameterBinding = default;
                return false;
            }

            // Branch: take this path when (AreTypesEquivalent(proxyParameterType, targetParameterType)) evaluates to true.
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
            // Branch: take this path when (proxyIsOut) evaluates to true.
            if (proxyIsOut)
            {
                preCallConversion = MethodArgumentConversion.None();
            }
            else if (!TryCreateMethodArgumentConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, out preCallConversion))
            {
                // Branch: take this path when (!TryCreateMethodArgumentConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, out preCallConversion)) evaluates to true.
                failure = new MethodCompatibilityFailure(
                    $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                parameterBinding = default;
                return false;
            }

            // Branch: take this path when (!TryCreateByRefPostCallConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, out var postCallConversion)) evaluates to true.
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
        /// Attempts to try create method argument conversion.
        /// </summary>
        /// <param name="proxyParameterType">The proxy parameter type value.</param>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <param name="argumentConversion">The argument conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateMethodArgumentConversion(TypeSig proxyParameterType, TypeSig targetParameterType, out MethodArgumentConversion argumentConversion)
        {
            // Branch: take this path when (AreTypesEquivalent(proxyParameterType, targetParameterType)) evaluates to true.
            if (AreTypesEquivalent(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.None();
                return true;
            }

            // Branch: take this path when (TryGetValueWithTypeArgument(proxyParameterType, out var proxyValueWithTypeArgument) && AreTypesEquivalent(proxyValueWithTypeArgument!, targetParameterType)) evaluates to true.
            if (TryGetValueWithTypeArgument(proxyParameterType, out var proxyValueWithTypeArgument) && AreTypesEquivalent(proxyValueWithTypeArgument!, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.UnwrapValueWithType(proxyParameterType, proxyValueWithTypeArgument!);
                return true;
            }

            // Branch: take this path when (IsDuckChainingRequired(targetParameterType, proxyParameterType)) evaluates to true.
            if (IsDuckChainingRequired(targetParameterType, proxyParameterType))
            {
                argumentConversion = MethodArgumentConversion.ExtractDuckTypeInstance(proxyParameterType, targetParameterType);
                return true;
            }

            // Branch: take this path when (CanUseTypeConversion(proxyParameterType, targetParameterType)) evaluates to true.
            if (CanUseTypeConversion(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.TypeConversion(proxyParameterType, targetParameterType);
                return true;
            }

            argumentConversion = default;
            return false;
        }

        /// <summary>
        /// Attempts to try create by ref post call conversion.
        /// </summary>
        /// <param name="proxyParameterElementType">The proxy parameter element type value.</param>
        /// <param name="targetParameterElementType">The target parameter element type value.</param>
        /// <param name="returnConversion">The return conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateByRefPostCallConversion(TypeSig proxyParameterElementType, TypeSig targetParameterElementType, out MethodReturnConversion returnConversion)
        {
            // Branch: take this path when (TryCreateReturnConversion(proxyParameterElementType, targetParameterElementType, out returnConversion)) evaluates to true.
            if (TryCreateReturnConversion(proxyParameterElementType, targetParameterElementType, out returnConversion))
            {
                return true;
            }

            returnConversion = default;
            return false;
        }

        /// <summary>
        /// Attempts to try get method parameter direction.
        /// </summary>
        /// <param name="method">The method value.</param>
        /// <param name="parameterIndex">The parameter index value.</param>
        /// <param name="direction">The direction value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetMethodParameterDirection(MethodDef method, int parameterIndex, out ParameterDirection direction)
        {
            foreach (var parameter in method.Parameters)
            {
                // Branch: take this path when (parameter.MethodSigIndex != parameterIndex) evaluates to true.
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
        /// Attempts to try get by ref element type.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="elementType">The element type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetByRefElementType(TypeSig typeSig, out TypeSig? elementType)
        {
            // Branch: take this path when (typeSig is ByRefSig byRefSig) evaluates to true.
            if (typeSig is ByRefSig byRefSig)
            {
                elementType = byRefSig.Next;
                return true;
            }

            elementType = null;
            return false;
        }

        /// <summary>
        /// Executes substitute method generic type arguments.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static TypeSig SubstituteMethodGenericTypeArguments(TypeSig typeSig, IReadOnlyList<TypeSig>? closedGenericMethodArguments)
        {
            // Branch: take this path when (closedGenericMethodArguments is null || closedGenericMethodArguments.Count == 0) evaluates to true.
            if (closedGenericMethodArguments is null || closedGenericMethodArguments.Count == 0)
            {
                return typeSig;
            }

            // Branch: take this path when (typeSig is GenericMVar methodGenericParameter && evaluates to true.
            if (typeSig is GenericMVar methodGenericParameter &&
                methodGenericParameter.Number < closedGenericMethodArguments.Count)
            {
                return closedGenericMethodArguments[(int)methodGenericParameter.Number];
            }

            // Branch: take this path when (typeSig is ByRefSig byRefSig) evaluates to true.
            if (typeSig is ByRefSig byRefSig)
            {
                return new ByRefSig(SubstituteMethodGenericTypeArguments(byRefSig.Next, closedGenericMethodArguments));
            }

            // Branch: take this path when (typeSig is SZArraySig szArraySig) evaluates to true.
            if (typeSig is SZArraySig szArraySig)
            {
                return new SZArraySig(SubstituteMethodGenericTypeArguments(szArraySig.Next, closedGenericMethodArguments));
            }

            // Branch: take this path when (typeSig is GenericInstSig genericInstSig) evaluates to true.
            if (typeSig is GenericInstSig genericInstSig)
            {
                var genericArguments = new List<TypeSig>(genericInstSig.GenericArguments.Count);
                for (var i = 0; i < genericInstSig.GenericArguments.Count; i++)
                {
                    genericArguments.Add(SubstituteMethodGenericTypeArguments(genericInstSig.GenericArguments[i], closedGenericMethodArguments));
                }

                return new GenericInstSig(genericInstSig.GenericType, genericArguments);
            }

            return typeSig;
        }

        /// <summary>
        /// Attempts to try get value with type argument.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="valueArgument">The value argument value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetValueWithTypeArgument(TypeSig typeSig, out TypeSig? valueArgument)
        {
            valueArgument = null;
            // Branch: take this path when (typeSig is not GenericInstSig genericInstSig || genericInstSig.GenericArguments.Count != 1) evaluates to true.
            if (typeSig is not GenericInstSig genericInstSig || genericInstSig.GenericArguments.Count != 1)
            {
                return false;
            }

            var genericType = genericInstSig.GenericType?.TypeDefOrRef;
            // Branch: take this path when (genericType is null) evaluates to true.
            if (genericType is null)
            {
                return false;
            }

            // Branch: take this path when (!string.Equals(genericType.FullName, typeof(ValueWithType<>).FullName, StringComparison.Ordinal)) evaluates to true.
            if (!string.Equals(genericType.FullName, typeof(ValueWithType<>).FullName, StringComparison.Ordinal))
            {
                return false;
            }

            valueArgument = genericInstSig.GenericArguments[0];
            return true;
        }

        /// <summary>
        /// Creates create value with type value field ref.
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
        /// Creates create value with type create method ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
        /// <param name="innerTypeSig">The inner type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateValueWithTypeCreateMethodRef(ModuleDef moduleDef, TypeSig wrapperTypeSig, TypeSig innerTypeSig)
        {
            var importedWrapperTypeSig = moduleDef.Import(wrapperTypeSig);
            // Branch: take this path when (importedWrapperTypeSig is not GenericInstSig wrapperGenericInst) evaluates to true.
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
        /// Creates create duck type create cache create method ref.
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
        /// Resolves resolve imported type for type token.
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
        /// Emits emit object to expected type conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="body">The body value.</param>
        /// <param name="expectedTypeSig">The expected type sig value.</param>
        /// <param name="context">The context value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void EmitObjectToExpectedTypeConversion(ModuleDef moduleDef, CilBody body, TypeSig expectedTypeSig, string context)
        {
            // Branch: take this path when (expectedTypeSig.ElementType == ElementType.Object) evaluates to true.
            if (expectedTypeSig.ElementType == ElementType.Object)
            {
                return;
            }

            var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedTypeSig, context);
            // Branch: take this path when (expectedTypeSig.ToTypeDefOrRef()?.ResolveTypeDef()?.IsValueType == true) evaluates to true.
            if (expectedTypeSig.ToTypeDefOrRef()?.ResolveTypeDef()?.IsValueType == true)
            {
                body.Instructions.Add(OpCodes.Unbox_Any.ToInstruction(importedExpectedType));
                return;
            }

            body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
        }

        /// <summary>
        /// Emits emit duck chain to proxy conversion.
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
            // Branch: take this path when (targetTypeSig.ToTypeDefOrRef()?.ResolveTypeDef()?.IsValueType == true) evaluates to true.
            if (targetTypeSig.ToTypeDefOrRef()?.ResolveTypeDef()?.IsValueType == true)
            {
                var importedTargetTypeForBox = ResolveImportedTypeForTypeToken(moduleDef, targetTypeSig, context);
                body.Instructions.Add(OpCodes.Box.ToInstruction(importedTargetTypeForBox));
            }

            // Branch: take this path when (TryGetNullableElementType(proxyTypeSig, out var nullableProxyElementType)) evaluates to true.
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
        /// Creates create nullable ctor ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="nullableTypeSig">The nullable type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateNullableCtorRef(ModuleDef moduleDef, TypeSig nullableTypeSig)
        {
            // Branch: take this path when (!TryGetNullableElementType(nullableTypeSig, out _)) evaluates to true.
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
        /// Attempts to try find forward target field.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="accessorKind">The accessor kind value.</param>
        /// <param name="targetField">The target field value.</param>
        /// <param name="fieldBinding">The field binding value.</param>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryFindForwardTargetField(
            TypeDef targetType,
            MethodDef proxyMethod,
            FieldAccessorKind accessorKind,
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
                        // Branch: take this path when (!string.Equals(candidate.Name, candidateFieldName, StringComparison.Ordinal)) evaluates to true.
                        if (!string.Equals(candidate.Name, candidateFieldName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        // Branch: take this path when (!AreFieldAccessorSignatureCompatible(proxyMethod, candidate, accessorKind, out var candidateFieldBinding, out failureReason)) evaluates to true.
                        if (!AreFieldAccessorSignatureCompatible(proxyMethod, candidate, accessorKind, out var candidateFieldBinding, out failureReason))
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
        /// Gets get forward target field names.
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
                    // Branch: take this path when (string.IsNullOrWhiteSpace(name)) evaluates to true.
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    // Branch: take this path when (visitedNames.Add(name)) evaluates to true.
                    if (visitedNames.Add(name))
                    {
                        fieldNames.Add(name);
                    }
                }
            }

            // Branch: take this path when (TryGetDuckAttributeNames(proxyMethod.CustomAttributes, out var methodAttributeNames)) evaluates to true.
            if (TryGetDuckAttributeNames(proxyMethod.CustomAttributes, out var methodAttributeNames))
            {
                AddNames(methodAttributeNames);
            }

            // Branch: take this path when (TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames)) evaluates to true.
            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames))
            {
                AddNames(propertyAttributeNames);
            }

            // Branch: take this path when (TryGetAccessorPropertyName(proxyMethod.Name.String ?? proxyMethod.Name.ToString(), out var propertyName)) evaluates to true.
            if (TryGetAccessorPropertyName(proxyMethod.Name.String ?? proxyMethod.Name.ToString(), out var propertyName))
            {
                AddNames(new[] { propertyName! });
            }

            return fieldNames;
        }

        /// <summary>
        /// Attempts to try get field accessor kind.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="accessorKind">The accessor kind value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetFieldAccessorKind(MethodDef proxyMethod, out FieldAccessorKind accessorKind)
        {
            accessorKind = default;
            var methodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();

            // Branch: take this path when (methodName.StartsWith("get_", StringComparison.Ordinal) && proxyMethod.MethodSig.Params.Count == 0) evaluates to true.
            if (methodName.StartsWith("get_", StringComparison.Ordinal) && proxyMethod.MethodSig.Params.Count == 0)
            {
                accessorKind = FieldAccessorKind.Getter;
                return true;
            }

            // Branch: take this path when (methodName.StartsWith("set_", StringComparison.Ordinal) && proxyMethod.MethodSig.Params.Count == 1 && proxyMethod.MethodSig.RetType.ElementType == ElementType.Void) evaluates to true.
            if (methodName.StartsWith("set_", StringComparison.Ordinal) && proxyMethod.MethodSig.Params.Count == 1 && proxyMethod.MethodSig.RetType.ElementType == ElementType.Void)
            {
                accessorKind = FieldAccessorKind.Setter;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to try get accessor property name.
        /// </summary>
        /// <param name="methodName">The method name value.</param>
        /// <param name="propertyName">The property name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetAccessorPropertyName(string methodName, out string? propertyName)
        {
            propertyName = null;
            // Branch: take this path when (methodName.StartsWith("get_", StringComparison.Ordinal) || methodName.StartsWith("set_", StringComparison.Ordinal)) evaluates to true.
            if (methodName.StartsWith("get_", StringComparison.Ordinal) || methodName.StartsWith("set_", StringComparison.Ordinal))
            {
                propertyName = methodName.Substring(4);
                return !string.IsNullOrWhiteSpace(propertyName);
            }

            return false;
        }

        /// <summary>
        /// Executes are field accessor signature compatible.
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
            out ForwardFieldBindingInfo fieldBinding,
            out string? failureReason)
        {
            fieldBinding = ForwardFieldBindingInfo.None();
            failureReason = null;
            // Branch dispatch: select the execution path based on (accessorKind).
            switch (accessorKind)
            {
                case FieldAccessorKind.Getter:
                    // Branch: handles the case FieldAccessorKind.Getter switch case.
                {
                    // Branch: take this path when (TryCreateReturnConversion(proxyMethod.MethodSig.RetType, targetField.FieldSig.Type, out var returnConversion)) evaluates to true.
                    if (TryCreateReturnConversion(proxyMethod.MethodSig.RetType, targetField.FieldSig.Type, out var returnConversion))
                    {
                        fieldBinding = returnConversion.Kind switch
                        {
                            MethodReturnConversionKind.None => ForwardFieldBindingInfo.None(),
                            MethodReturnConversionKind.WrapValueWithType => ForwardFieldBindingInfo.WrapValueWithType(returnConversion.WrapperTypeSig!, returnConversion.InnerTypeSig!),
                            MethodReturnConversionKind.DuckChainToProxy => ForwardFieldBindingInfo.DuckChainToProxy(returnConversion.WrapperTypeSig!, returnConversion.InnerTypeSig!),
                            MethodReturnConversionKind.TypeConversion => ForwardFieldBindingInfo.ReturnTypeConversion(returnConversion.WrapperTypeSig!, returnConversion.InnerTypeSig!),
                            _ => ForwardFieldBindingInfo.None()
                        };
                        return true;
                    }

                    failureReason = $"Return type mismatch between proxy method '{proxyMethod.FullName}' and target field '{targetField.FullName}'.";
                    return false;
                }

                case FieldAccessorKind.Setter:
                    // Branch: handles the case FieldAccessorKind.Setter switch case.
                {
                    // Branch: take this path when (targetField.IsLiteral || targetField.IsInitOnly) evaluates to true.
                    if (targetField.IsLiteral || targetField.IsInitOnly)
                    {
                        failureReason = $"Target field '{targetField.FullName}' is readonly and cannot be set by proxy method '{proxyMethod.FullName}'.";
                        return false;
                    }

                    var proxyParameterType = proxyMethod.MethodSig.Params[0];
                    // Branch: take this path when (TryCreateMethodArgumentConversion(proxyParameterType, targetField.FieldSig.Type, out var argumentConversion)) evaluates to true.
                    if (TryCreateMethodArgumentConversion(proxyParameterType, targetField.FieldSig.Type, out var argumentConversion))
                    {
                        fieldBinding = argumentConversion.Kind switch
                        {
                            MethodArgumentConversionKind.None => ForwardFieldBindingInfo.None(),
                            MethodArgumentConversionKind.UnwrapValueWithType => ForwardFieldBindingInfo.UnwrapValueWithType(argumentConversion.WrapperTypeSig!, argumentConversion.InnerTypeSig!),
                            MethodArgumentConversionKind.ExtractDuckTypeInstance => ForwardFieldBindingInfo.ExtractDuckTypeInstance(argumentConversion.WrapperTypeSig!, argumentConversion.InnerTypeSig!),
                            MethodArgumentConversionKind.TypeConversion => ForwardFieldBindingInfo.TypeConversion(argumentConversion.WrapperTypeSig!, argumentConversion.InnerTypeSig!),
                            _ => ForwardFieldBindingInfo.None()
                        };
                        return true;
                    }

                    failureReason = $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target field '{targetField.FullName}'.";
                    return false;
                }

                default:
                    // Branch: fallback switch case when no explicit case label matches.
                    failureReason = $"Proxy method '{proxyMethod.FullName}' does not map to a supported field accessor.";
                    return false;
            }
        }

        /// <summary>
        /// Gets get field resolution mode.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static FieldResolutionMode GetFieldResolutionMode(MethodDef proxyMethod)
        {
            var mode = FieldResolutionMode.Disabled;
            foreach (var duckAttribute in EnumerateDuckAttributes(proxyMethod))
            {
                var duckKind = ResolveDuckKind(duckAttribute);
                // Branch dispatch: select the execution path based on (duckKind).
                switch (duckKind)
                {
                    case DuckKindField:
                        // Branch: handles the case DuckKindField switch case.
                        return FieldResolutionMode.FieldOnly;
                    case DuckKindPropertyOrField:
                        // Branch: handles the case DuckKindPropertyOrField switch case.
                        mode = FieldResolutionMode.AllowFallback;
                        break;
                }
            }

            return mode;
        }

        /// <summary>
        /// Executes enumerate duck attributes.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<CustomAttribute> EnumerateDuckAttributes(MethodDef proxyMethod)
        {
            foreach (var attribute in proxyMethod.CustomAttributes)
            {
                // Branch: take this path when (IsDuckAttribute(attribute)) evaluates to true.
                if (IsDuckAttribute(attribute))
                {
                    yield return attribute;
                }
            }

            // Branch: take this path when (TryGetDeclaringProperty(proxyMethod, out var declaringProperty)) evaluates to true.
            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty))
            {
                foreach (var attribute in declaringProperty!.CustomAttributes)
                {
                    // Branch: take this path when (IsDuckAttribute(attribute)) evaluates to true.
                    if (IsDuckAttribute(attribute))
                    {
                        yield return attribute;
                    }
                }
            }
        }

        /// <summary>
        /// Resolves resolve duck kind.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>The computed numeric value.</returns>
        private static int ResolveDuckKind(CustomAttribute customAttribute)
        {
            var fullName = customAttribute.TypeFullName;
            // Branch: take this path when (string.Equals(fullName, DuckFieldAttributeTypeName, StringComparison.Ordinal)) evaluates to true.
            if (string.Equals(fullName, DuckFieldAttributeTypeName, StringComparison.Ordinal))
            {
                return DuckKindField;
            }

            // Branch: take this path when (string.Equals(fullName, DuckPropertyOrFieldAttributeTypeName, StringComparison.Ordinal)) evaluates to true.
            if (string.Equals(fullName, DuckPropertyOrFieldAttributeTypeName, StringComparison.Ordinal))
            {
                return DuckKindPropertyOrField;
            }

            foreach (var namedArgument in customAttribute.NamedArguments)
            {
                // Branch: take this path when (!string.Equals(namedArgument.Name.String, "Kind", StringComparison.Ordinal)) evaluates to true.
                if (!string.Equals(namedArgument.Name.String, "Kind", StringComparison.Ordinal))
                {
                    continue;
                }

                // Branch: take this path when (TryGetIntArgument(namedArgument.Argument.Value, out var kind)) evaluates to true.
                if (TryGetIntArgument(namedArgument.Argument.Value, out var kind))
                {
                    return kind;
                }
            }

            return DuckKindProperty;
        }

        /// <summary>
        /// Gets get forward target method names.
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
                    // Branch: take this path when (string.IsNullOrWhiteSpace(name)) evaluates to true.
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    // Branch: take this path when (visitedNames.Add(name)) evaluates to true.
                    if (visitedNames.Add(name))
                    {
                        methodNames.Add(name);
                    }
                }
            }

            // Branch: take this path when (TryGetDuckAttributeNames(proxyMethod.CustomAttributes, out var methodAttributeNames)) evaluates to true.
            if (TryGetDuckAttributeNames(proxyMethod.CustomAttributes, out var methodAttributeNames))
            {
                // Branch: take this path when (proxyMethod.IsSpecialName && TryGetAccessorPrefix(proxyMethod.Name, out var accessorPrefix)) evaluates to true.
                if (proxyMethod.IsSpecialName && TryGetAccessorPrefix(proxyMethod.Name, out var accessorPrefix))
                {
                    AddNames(methodAttributeNames.Select(name => $"{accessorPrefix}{name}"));
                }
                else
                {
                    // Branch: fallback path when earlier branch conditions evaluate to false.
                    AddNames(methodAttributeNames);
                }
            }

            // Branch: take this path when (proxyMethod.IsSpecialName && TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames) && TryGetAccessorPrefix(proxyMethod.Name, out var propertyAccessorPrefix)) evaluates to true.
            if (proxyMethod.IsSpecialName && TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames) && TryGetAccessorPrefix(proxyMethod.Name, out var propertyAccessorPrefix))
            {
                AddNames(propertyAttributeNames.Select(name => $"{propertyAccessorPrefix}{name}"));
            }

            AddNames(new[] { proxyMethod.Name.String ?? proxyMethod.Name.ToString() });
            return methodNames;
        }

        /// <summary>
        /// Attempts to try get declaring property.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="propertyDef">The property def value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetDeclaringProperty(MethodDef proxyMethod, out PropertyDef? propertyDef)
        {
            propertyDef = null;
            var declaringType = proxyMethod.DeclaringType;
            // Branch: take this path when (declaringType is null) evaluates to true.
            if (declaringType is null)
            {
                return false;
            }

            foreach (var property in declaringType.Properties)
            {
                // Branch: take this path when (property.GetMethod == proxyMethod || property.SetMethod == proxyMethod || property.OtherMethods.Contains(proxyMethod)) evaluates to true.
                if (property.GetMethod == proxyMethod || property.SetMethod == proxyMethod || property.OtherMethods.Contains(proxyMethod))
                {
                    propertyDef = property;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to try resolve forward closed generic method arguments.
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
            // Branch: take this path when (!TryGetDuckGenericParameterTypeNames(proxyMethod, out var genericParameterTypeNames)) evaluates to true.
            if (!TryGetDuckGenericParameterTypeNames(proxyMethod, out var genericParameterTypeNames))
            {
                return true;
            }

            var resolvedTypeSigs = new List<TypeSig>(genericParameterTypeNames.Count);
            foreach (var genericParameterTypeName in genericParameterTypeNames)
            {
                // Branch: take this path when (!TryResolveRuntimeTypeByName(genericParameterTypeName, out var runtimeType)) evaluates to true.
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
        /// Attempts to try get duck generic parameter type names.
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
                    // Branch: take this path when (!string.Equals(namedArgument.Name.String, "GenericParameterTypeNames", StringComparison.Ordinal)) evaluates to true.
                    if (!string.Equals(namedArgument.Name.String, "GenericParameterTypeNames", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Branch: take this path when (TryGetStringArrayArgument(namedArgument.Argument.Value, out var argumentNames)) evaluates to true.
                    if (TryGetStringArrayArgument(namedArgument.Argument.Value, out var argumentNames))
                    {
                        names.AddRange(argumentNames);
                    }
                }
            }

            return names.Count > 0;
        }

        /// <summary>
        /// Attempts to try resolve runtime type by name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryResolveRuntimeTypeByName(string typeName, out Type? runtimeType)
        {
            runtimeType = Type.GetType(typeName, throwOnError: false);
            // Branch: take this path when (runtimeType is not null) evaluates to true.
            if (runtimeType is not null)
            {
                return true;
            }

            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                runtimeType = loadedAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                // Branch: take this path when (runtimeType is not null) evaluates to true.
                if (runtimeType is not null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether is reverse method attribute.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsReverseMethodAttribute(CustomAttribute customAttribute)
        {
            return string.Equals(customAttribute.TypeFullName, DuckReverseMethodAttributeTypeName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether is reverse candidate match.
        /// </summary>
        /// <param name="proxyMethodName">The proxy method name value.</param>
        /// <param name="proxyParameterTypeNames">The proxy parameter type names value.</param>
        /// <param name="reverseAttribute">The reverse attribute value.</param>
        /// <param name="targetMethodName">The target method name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsReverseCandidateMatch(
            string proxyMethodName,
            IReadOnlyList<HashSet<string>> proxyParameterTypeNames,
            CustomAttribute reverseAttribute,
            string targetMethodName)
        {
            // Branch: take this path when (TryGetAccessorPrefix(proxyMethodName, out var proxyAccessorPrefix) && evaluates to true.
            if (TryGetAccessorPrefix(proxyMethodName, out var proxyAccessorPrefix) &&
                TryGetAccessorPrefix(targetMethodName, out var targetAccessorPrefix) &&
                !string.Equals(proxyAccessorPrefix, targetAccessorPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var mappedName = targetMethodName;
            // Branch: take this path when (TryGetDuckAttributeName(reverseAttribute, out var explicitMappedName)) evaluates to true.
            if (TryGetDuckAttributeName(reverseAttribute, out var explicitMappedName))
            {
                // Branch: take this path when (proxyMethodName.StartsWith("get_", StringComparison.Ordinal) || evaluates to true.
                if (proxyMethodName.StartsWith("get_", StringComparison.Ordinal) ||
                    proxyMethodName.StartsWith("set_", StringComparison.Ordinal))
                {
                    mappedName = proxyMethodName.Substring(0, 4) + explicitMappedName;
                }
                else
                {
                    // Branch: fallback path when earlier branch conditions evaluate to false.
                    mappedName = explicitMappedName!;
                }
            }

            // Branch: take this path when (!string.Equals(proxyMethodName, mappedName, StringComparison.Ordinal)) evaluates to true.
            if (!string.Equals(proxyMethodName, mappedName, StringComparison.Ordinal))
            {
                return false;
            }

            // Branch: take this path when (!TryGetDuckAttributeParameterTypeNames(reverseAttribute, out var configuredParameterTypeNames)) evaluates to true.
            if (!TryGetDuckAttributeParameterTypeNames(reverseAttribute, out var configuredParameterTypeNames))
            {
                return true;
            }

            // Branch: take this path when (configuredParameterTypeNames.Count != proxyParameterTypeNames.Count) evaluates to true.
            if (configuredParameterTypeNames.Count != proxyParameterTypeNames.Count)
            {
                return false;
            }

            for (var i = 0; i < configuredParameterTypeNames.Count; i++)
            {
                // Branch: take this path when (!proxyParameterTypeNames[i].Contains(configuredParameterTypeNames[i])) evaluates to true.
                if (!proxyParameterTypeNames[i].Contains(configuredParameterTypeNames[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Attempts to try get duck attribute name.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <param name="configuredName">The configured name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetDuckAttributeName(CustomAttribute customAttribute, out string? configuredName)
        {
            foreach (var namedArgument in customAttribute.NamedArguments)
            {
                // Branch: take this path when (!string.Equals(namedArgument.Name.String, "Name", StringComparison.Ordinal)) evaluates to true.
                if (!string.Equals(namedArgument.Name.String, "Name", StringComparison.Ordinal))
                {
                    continue;
                }

                // Branch: take this path when (TryGetStringArgument(namedArgument.Argument.Value, out configuredName)) evaluates to true.
                if (TryGetStringArgument(namedArgument.Argument.Value, out configuredName))
                {
                    return true;
                }
            }

            configuredName = null;
            return false;
        }

        /// <summary>
        /// Attempts to try get duck attribute parameter type names.
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
                // Branch: take this path when (!string.Equals(namedArgument.Name.String, "ParameterTypeNames", StringComparison.Ordinal)) evaluates to true.
                if (!string.Equals(namedArgument.Name.String, "ParameterTypeNames", StringComparison.Ordinal))
                {
                    continue;
                }

                // Branch: take this path when (TryGetStringArrayArgument(namedArgument.Argument.Value, out var configuredNames)) evaluates to true.
                if (TryGetStringArrayArgument(namedArgument.Argument.Value, out var configuredNames))
                {
                    names.AddRange(configuredNames);
                }
            }

            return names.Count > 0;
        }

        /// <summary>
        /// Gets get type comparison names.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static HashSet<string> GetTypeComparisonNames(TypeSig typeSig)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var normalizedType = typeSig;
            // Branch: take this path when (normalizedType.ElementType == ElementType.ByRef && normalizedType is ByRefSig byRefSig) evaluates to true.
            if (normalizedType.ElementType == ElementType.ByRef && normalizedType is ByRefSig byRefSig)
            {
                normalizedType = byRefSig.Next;
            }

            var fullName = normalizedType.FullName;
            // Branch: take this path when (!string.IsNullOrWhiteSpace(fullName)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                names.Add(fullName);
            }

            var typeName = normalizedType.TypeName;
            // Branch: take this path when (!string.IsNullOrWhiteSpace(typeName)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(typeName))
            {
                names.Add(typeName);
            }

            var runtimeType = TryResolveRuntimeType(normalizedType);
            // Branch: take this path when (runtimeType is not null) evaluates to true.
            if (runtimeType is not null)
            {
                names.Add(runtimeType.Name);
                // Branch: take this path when (!string.IsNullOrWhiteSpace(runtimeType.FullName)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(runtimeType.FullName))
                {
                    names.Add(runtimeType.FullName);
                    var assemblyName = runtimeType.Assembly.GetName().Name;
                    // Branch: take this path when (!string.IsNullOrWhiteSpace(assemblyName)) evaluates to true.
                    if (!string.IsNullOrWhiteSpace(assemblyName))
                    {
                        names.Add($"{runtimeType.FullName}, {assemblyName}");
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// Attempts to try get duck attribute names.
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
                // Branch: take this path when (!IsDuckAttribute(customAttribute)) evaluates to true.
                if (!IsDuckAttribute(customAttribute))
                {
                    continue;
                }

                foreach (var namedArgument in customAttribute.NamedArguments)
                {
                    // Branch: take this path when (!string.Equals(namedArgument.Name.String, "Name", StringComparison.Ordinal)) evaluates to true.
                    if (!string.Equals(namedArgument.Name.String, "Name", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Branch: take this path when (TryGetStringArgument(namedArgument.Argument.Value, out var configuredName)) evaluates to true.
                    if (TryGetStringArgument(namedArgument.Argument.Value, out var configuredName))
                    {
                        foreach (var name in SplitDuckNames(configuredName!))
                        {
                            // Branch: take this path when (!string.IsNullOrWhiteSpace(name)) evaluates to true.
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
        /// Determines whether is duck attribute.
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
        /// Attempts to try get string argument.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="text">The text value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetStringArgument(object? value, out string? text)
        {
            // Branch dispatch: select the execution path based on (value).
            switch (value)
            {
                case UTF8String utf8:
                    // Branch: handles the case UTF8String utf8 switch case.
                    text = utf8.String;
                    return !string.IsNullOrWhiteSpace(text);
                case string stringValue:
                    // Branch: handles the case string stringValue switch case.
                    text = stringValue;
                    return !string.IsNullOrWhiteSpace(text);
                default:
                    // Branch: fallback switch case when no explicit case label matches.
                    text = null;
                    return false;
            }
        }

        /// <summary>
        /// Attempts to try get string array argument.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="values">The values value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetStringArrayArgument(object? value, out IReadOnlyList<string> values)
        {
            var parsedValues = new List<string>();
            values = parsedValues;
            // Branch dispatch: select the execution path based on (value).
            switch (value)
            {
                case IList<CAArgument> caArguments:
                    // Branch: handles the case IList<CAArgument> caArguments switch case.
                    foreach (var caArgument in caArguments)
                    {
                        // Branch: take this path when (TryGetStringArgument(caArgument.Value, out var text)) evaluates to true.
                        if (TryGetStringArgument(caArgument.Value, out var text))
                        {
                            parsedValues.Add(text!.Trim());
                        }
                    }

                    break;
                case string[] stringArray:
                    // Branch: handles the case string[] stringArray switch case.
                    for (var i = 0; i < stringArray.Length; i++)
                    {
                        var valueText = stringArray[i]?.Trim();
                        // Branch: take this path when (!string.IsNullOrWhiteSpace(valueText)) evaluates to true.
                        if (!string.IsNullOrWhiteSpace(valueText))
                        {
                            parsedValues.Add(valueText!);
                        }
                    }

                    break;
                case object[] objectArray:
                    // Branch: handles the case object[] objectArray switch case.
                    for (var i = 0; i < objectArray.Length; i++)
                    {
                        // Branch: take this path when (TryGetStringArgument(objectArray[i], out var text)) evaluates to true.
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
        /// Attempts to try get int argument.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="intValue">The int value value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetIntArgument(object? value, out int intValue)
        {
            // Branch dispatch: select the execution path based on (value).
            switch (value)
            {
                case int int32Value:
                    // Branch: handles the case int int32Value switch case.
                    intValue = int32Value;
                    return true;
                case short int16Value:
                    // Branch: handles the case short int16Value switch case.
                    intValue = int16Value;
                    return true;
                case byte byteValue:
                    // Branch: handles the case byte byteValue switch case.
                    intValue = byteValue;
                    return true;
                case sbyte sbyteValue:
                    // Branch: handles the case sbyte sbyteValue switch case.
                    intValue = sbyteValue;
                    return true;
                default:
                    // Branch: fallback switch case when no explicit case label matches.
                    intValue = default;
                    return false;
            }
        }

        /// <summary>
        /// Executes split duck names.
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
        /// Attempts to try get accessor prefix.
        /// </summary>
        /// <param name="methodName">The method name value.</param>
        /// <param name="prefix">The prefix value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetAccessorPrefix(string methodName, out string prefix)
        {
            var separatorIndex = methodName.IndexOf('_');
            // Branch: take this path when (separatorIndex <= 0) evaluates to true.
            if (separatorIndex <= 0)
            {
                prefix = string.Empty;
                return false;
            }

            prefix = methodName.Substring(0, separatorIndex + 1);
            return true;
        }

        /// <summary>
        /// Executes are methods signature compatible.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool AreMethodsSignatureCompatible(MethodDef proxyMethod, MethodDef targetMethod)
        {
            // Branch: take this path when (!AreTypesEquivalent(proxyMethod.MethodSig.RetType, targetMethod.MethodSig.RetType)) evaluates to true.
            if (!AreTypesEquivalent(proxyMethod.MethodSig.RetType, targetMethod.MethodSig.RetType))
            {
                return false;
            }

            for (var i = 0; i < proxyMethod.MethodSig.Params.Count; i++)
            {
                // Branch: take this path when (!AreTypesEquivalent(proxyMethod.MethodSig.Params[i], targetMethod.MethodSig.Params[i])) evaluates to true.
                if (!AreTypesEquivalent(proxyMethod.MethodSig.Params[i], targetMethod.MethodSig.Params[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Executes are types equivalent.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool AreTypesEquivalent(TypeSig proxyType, TypeSig targetType)
        {
            return string.Equals(proxyType.FullName, targetType.FullName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether is duck chaining required.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static bool IsDuckChainingRequired(TypeSig targetType, TypeSig proxyType)
        {
            // Branch: take this path when (proxyType.ContainsGenericParameter || targetType.ContainsGenericParameter) evaluates to true.
            if (proxyType.ContainsGenericParameter || targetType.ContainsGenericParameter)
            {
                return false;
            }

            // Branch: take this path when (proxyType.ElementType == ElementType.ByRef || targetType.ElementType == ElementType.ByRef) evaluates to true.
            if (proxyType.ElementType == ElementType.ByRef || targetType.ElementType == ElementType.ByRef)
            {
                return false;
            }

            // Branch: take this path when (AreTypesEquivalent(proxyType, targetType)) evaluates to true.
            if (AreTypesEquivalent(proxyType, targetType))
            {
                return false;
            }

            // Branch: take this path when (!TryGetDuckChainingProxyType(proxyType, out var proxyTypeForCache)) evaluates to true.
            if (!TryGetDuckChainingProxyType(proxyType, out var proxyTypeForCache))
            {
                return false;
            }

            var proxyTypeDefOrRef = proxyTypeForCache.ToTypeDefOrRef();
            var targetTypeDefOrRef = targetType.ToTypeDefOrRef();
            // Branch: take this path when (proxyTypeDefOrRef is null || targetTypeDefOrRef is null) evaluates to true.
            if (proxyTypeDefOrRef is null || targetTypeDefOrRef is null)
            {
                return false;
            }

            // Branch: take this path when (IsAssignableFrom(proxyTypeDefOrRef, targetTypeDefOrRef)) evaluates to true.
            if (IsAssignableFrom(proxyTypeDefOrRef, targetTypeDefOrRef))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to try get duck chaining proxy type.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="proxyTypeForCache">The proxy type for cache value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static bool TryGetDuckChainingProxyType(TypeSig proxyType, out TypeSig proxyTypeForCache)
        {
            // Branch: take this path when (TryGetNullableElementType(proxyType, out var nullableInnerType) && IsDuckProxyCandidate(nullableInnerType!)) evaluates to true.
            if (TryGetNullableElementType(proxyType, out var nullableInnerType) && IsDuckProxyCandidate(nullableInnerType!))
            {
                proxyTypeForCache = nullableInnerType!;
                return true;
            }

            // Branch: take this path when (IsDuckProxyCandidate(proxyType)) evaluates to true.
            if (IsDuckProxyCandidate(proxyType))
            {
                proxyTypeForCache = proxyType;
                return true;
            }

            proxyTypeForCache = null!;
            return false;
        }

        /// <summary>
        /// Determines whether is duck proxy candidate.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsDuckProxyCandidate(TypeSig typeSig)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            // Branch: take this path when (typeDefOrRef is null) evaluates to true.
            if (typeDefOrRef is null)
            {
                return false;
            }

            var typeDef = typeDefOrRef.ResolveTypeDef();
            // Branch: take this path when (typeDef is null) evaluates to true.
            if (typeDef is null)
            {
                return false;
            }

            // Branch: take this path when (typeDef.IsInterface) evaluates to true.
            if (typeDef.IsInterface)
            {
                return true;
            }

            // Branch: take this path when (typeDef.IsClass) evaluates to true.
            if (typeDef.IsClass)
            {
                return typeSig.DefinitionAssembly?.IsCorLib() != true;
            }

            // Branch: take this path when (typeDef.IsValueType) evaluates to true.
            if (typeDef.IsValueType)
            {
                return IsDuckCopyValueType(typeDef);
            }

            return false;
        }

        /// <summary>
        /// Determines whether is duck copy value type.
        /// </summary>
        /// <param name="typeDef">The type def value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsDuckCopyValueType(TypeDef typeDef)
        {
            foreach (var customAttribute in typeDef.CustomAttributes)
            {
                // Branch: take this path when (string.Equals(customAttribute.TypeFullName, "Datadog.Trace.DuckTyping.DuckCopyAttribute", StringComparison.Ordinal)) evaluates to true.
                if (string.Equals(customAttribute.TypeFullName, "Datadog.Trace.DuckTyping.DuckCopyAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to try get nullable element type.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="elementType">The element type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetNullableElementType(TypeSig typeSig, out TypeSig? elementType)
        {
            elementType = null;
            // Branch: take this path when (typeSig is not GenericInstSig genericInstSig || genericInstSig.GenericArguments.Count != 1) evaluates to true.
            if (typeSig is not GenericInstSig genericInstSig || genericInstSig.GenericArguments.Count != 1)
            {
                return false;
            }

            var genericType = genericInstSig.GenericType?.TypeDefOrRef;
            // Branch: take this path when (genericType is null || !string.Equals(genericType.FullName, "System.Nullable`1", StringComparison.Ordinal)) evaluates to true.
            if (genericType is null || !string.Equals(genericType.FullName, "System.Nullable`1", StringComparison.Ordinal))
            {
                return false;
            }

            elementType = genericInstSig.GenericArguments[0];
            return true;
        }

        /// <summary>
        /// Determines whether is assignable from.
        /// </summary>
        /// <param name="candidateBaseType">The candidate base type value.</param>
        /// <param name="derivedType">The derived type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsAssignableFrom(ITypeDefOrRef candidateBaseType, ITypeDefOrRef derivedType)
        {
            // Branch: take this path when (string.Equals(candidateBaseType.FullName, derivedType.FullName, StringComparison.Ordinal)) evaluates to true.
            if (string.Equals(candidateBaseType.FullName, derivedType.FullName, StringComparison.Ordinal))
            {
                return true;
            }

            var derivedTypeDef = derivedType.ResolveTypeDef();
            // Branch: take this path when (derivedTypeDef is null) evaluates to true.
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
                // Branch: take this path when (!visitedTypes.Add(current.FullName)) evaluates to true.
                if (!visitedTypes.Add(current.FullName))
                {
                    continue;
                }

                // Branch: take this path when (string.Equals(current.FullName, candidateBaseType.FullName, StringComparison.Ordinal)) evaluates to true.
                if (string.Equals(current.FullName, candidateBaseType.FullName, StringComparison.Ordinal))
                {
                    return true;
                }

                var baseType = current.BaseType?.ResolveTypeDef();
                // Branch: take this path when (baseType is not null) evaluates to true.
                if (baseType is not null)
                {
                    typesToInspect.Push(baseType);
                }

                foreach (var interfaceImpl in current.Interfaces)
                {
                    // Branch: take this path when (string.Equals(interfaceImpl.Interface.FullName, candidateBaseType.FullName, StringComparison.Ordinal)) evaluates to true.
                    if (string.Equals(interfaceImpl.Interface.FullName, candidateBaseType.FullName, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    var resolvedInterface = interfaceImpl.Interface.ResolveTypeDef();
                    // Branch: take this path when (resolvedInterface is not null) evaluates to true.
                    if (resolvedInterface is not null)
                    {
                        typesToInspect.Push(resolvedInterface);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to try resolve type.
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
                // Branch: take this path when (!string.IsNullOrWhiteSpace(assemblyName)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            foreach (var assemblyName in mappingResolutionResult.TargetAssemblyPathsByName.Keys)
            {
                // Branch: take this path when (!string.IsNullOrWhiteSpace(assemblyName)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            var generatedAssemblyName = assemblyDef.Name?.String;
            foreach (var assemblyName in assemblyNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                // Branch: take this path when (string.Equals(assemblyName, generatedAssemblyName, StringComparison.OrdinalIgnoreCase)) evaluates to true.
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
        /// Executes load modules.
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
            // Branch: take this path when (assemblyReferences.TryGetValue(assemblyName.Name ?? string.Empty, out var assemblyRef)) evaluates to true.
            if (assemblyReferences.TryGetValue(assemblyName.Name ?? string.Empty, out var assemblyRef))
            {
                return assemblyRef;
            }

            assemblyRef = moduleDef.UpdateRowId(new AssemblyRefUser(assemblyName));
            assemblyReferences[assemblyName.Name ?? string.Empty] = assemblyRef;
            return assemblyRef;
        }

        /// <summary>
        /// Computes compute deterministic mvid.
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
        /// Computes compute stable short hash.
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
            /// Executes for property.
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
            /// Executes for field.
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
            /// Executes for method.
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
            /// Executes for field get.
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
            /// Executes for field set.
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
            /// Executes for standard.
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
            /// Executes for by ref direct.
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
            /// Executes for by ref with local.
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
            /// Executes none.
            /// </summary>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo None()
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.None());
            }

            /// <summary>
            /// Executes unwrap value with type.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo UnwrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.UnwrapValueWithType(wrapperTypeSig, innerTypeSig), MethodReturnConversion.None());
            }

            /// <summary>
            /// Executes wrap value with type.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo WrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.WrapValueWithType(wrapperTypeSig, innerTypeSig));
            }

            /// <summary>
            /// Executes extract duck type instance.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.ExtractDuckTypeInstance(wrapperTypeSig, innerTypeSig), MethodReturnConversion.None());
            }

            /// <summary>
            /// Executes duck chain to proxy.
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
            /// Executes return type conversion.
            /// </summary>
            /// <param name="actualTypeSig">The actual type sig value.</param>
            /// <param name="expectedTypeSig">The expected type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static ForwardFieldBindingInfo ReturnTypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.TypeConversion(actualTypeSig, expectedTypeSig));
            }

            /// <summary>
            /// Executes type conversion.
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
            private MethodArgumentConversion(MethodArgumentConversionKind kind, TypeSig? wrapperTypeSig, TypeSig? innerTypeSig)
            {
                Kind = kind;
                WrapperTypeSig = wrapperTypeSig;
                InnerTypeSig = innerTypeSig;
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
            /// Executes none.
            /// </summary>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion None()
            {
                return new MethodArgumentConversion(MethodArgumentConversionKind.None, wrapperTypeSig: null, innerTypeSig: null);
            }

            /// <summary>
            /// Executes unwrap value with type.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion UnwrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(MethodArgumentConversionKind.UnwrapValueWithType, wrapperTypeSig, innerTypeSig);
            }

            /// <summary>
            /// Executes extract duck type instance.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(MethodArgumentConversionKind.ExtractDuckTypeInstance, wrapperTypeSig, innerTypeSig);
            }

            /// <summary>
            /// Executes type conversion.
            /// </summary>
            /// <param name="actualTypeSig">The actual type sig value.</param>
            /// <param name="expectedTypeSig">The expected type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion TypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new MethodArgumentConversion(MethodArgumentConversionKind.TypeConversion, actualTypeSig, expectedTypeSig);
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
            /// Executes none.
            /// </summary>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion None()
            {
                return new MethodReturnConversion(MethodReturnConversionKind.None, wrapperTypeSig: null, innerTypeSig: null);
            }

            /// <summary>
            /// Executes wrap value with type.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion WrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.WrapValueWithType, wrapperTypeSig, innerTypeSig);
            }

            /// <summary>
            /// Executes duck chain to proxy.
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
            /// Executes type conversion.
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
                // Branch: take this path when (getTypeFromHandleMethod is null) evaluates to true.
                if (getTypeFromHandleMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve Type.GetTypeFromHandle(RuntimeTypeHandle).");
                }

                var funcObjectObjectCtor = typeof(Func<object, object>).GetConstructor(new[] { typeof(object), typeof(IntPtr) });
                // Branch: take this path when (funcObjectObjectCtor is null) evaluates to true.
                if (funcObjectObjectCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve Func<object, object> constructor.");
                }

                var registerAotProxyMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotProxy),
                    new[] { typeof(Type), typeof(Type), typeof(Type), typeof(Func<object, object>) });
                // Branch: take this path when (registerAotProxyMethod is null) evaluates to true.
                if (registerAotProxyMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotProxy(Type, Type, Type, Func<object, object>).");
                }

                var registerAotReverseProxyMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotReverseProxy),
                    new[] { typeof(Type), typeof(Type), typeof(Type), typeof(Func<object, object>) });
                // Branch: take this path when (registerAotReverseProxyMethod is null) evaluates to true.
                if (registerAotReverseProxyMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotReverseProxy(Type, Type, Type, Func<object, object>).");
                }

                var enableAotModeMethod = typeof(DuckType).GetMethod(nameof(DuckType.EnableAotMode), Type.EmptyTypes);
                // Branch: take this path when (enableAotModeMethod is null) evaluates to true.
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
                // Branch: take this path when (validateAotRegistryContractMethod is null) evaluates to true.
                if (validateAotRegistryContractMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.ValidateAotRegistryContract(string, string, string, string, string).");
                }

                var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes);
                // Branch: take this path when (objectCtor is null) evaluates to true.
                if (objectCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve object constructor.");
                }

                var objectToStringMethod = typeof(object).GetMethod(nameof(object.ToString), Type.EmptyTypes);
                // Branch: take this path when (objectToStringMethod is null) evaluates to true.
                if (objectToStringMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve object.ToString().");
                }

                var iDuckTypeType = moduleDef.Import(typeof(IDuckType));
                // Branch: take this path when (iDuckTypeType is null) evaluates to true.
                if (iDuckTypeType is null)
                {
                    throw new InvalidOperationException("Unable to import IDuckType.");
                }

                var iDuckTypeInstanceGetter = typeof(IDuckType).GetProperty(nameof(IDuckType.Instance), BindingFlags.Instance | BindingFlags.Public)?.GetMethod;
                // Branch: take this path when (iDuckTypeInstanceGetter is null) evaluates to true.
                if (iDuckTypeInstanceGetter is null)
                {
                    throw new InvalidOperationException("Unable to resolve IDuckType.Instance getter.");
                }

                var ignoresAccessChecksToCtor = typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute).GetConstructor(new[] { typeof(string) });
                // Branch: take this path when (ignoresAccessChecksToCtor is null) evaluates to true.
                if (ignoresAccessChecksToCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve IgnoresAccessChecksToAttribute(string).");
                }

                GetTypeFromHandleMethod = moduleDef.Import(getTypeFromHandleMethod);
                FuncObjectObjectCtor = moduleDef.Import(funcObjectObjectCtor);
                RegisterAotProxyMethod = moduleDef.Import(registerAotProxyMethod);
                RegisterAotReverseProxyMethod = moduleDef.Import(registerAotReverseProxyMethod);
                EnableAotModeMethod = moduleDef.Import(enableAotModeMethod);
                ValidateAotRegistryContractMethod = moduleDef.Import(validateAotRegistryContractMethod);
                ObjectCtor = moduleDef.Import(objectCtor);
                ObjectToStringMethod = moduleDef.Import(objectToStringMethod);
                IDuckTypeType = iDuckTypeType;
                IDuckTypeInstanceGetter = moduleDef.Import(iDuckTypeInstanceGetter);
                var importedIgnoresAccessChecksToCtor = moduleDef.Import(ignoresAccessChecksToCtor) as ICustomAttributeType;
                // Branch: take this path when (importedIgnoresAccessChecksToCtor is null) evaluates to true.
                if (importedIgnoresAccessChecksToCtor is null)
                {
                    throw new InvalidOperationException("Unable to import IgnoresAccessChecksToAttribute(string).");
                }

                IgnoresAccessChecksToAttributeCtor = importedIgnoresAccessChecksToCtor;
            }

            /// <summary>
            /// Gets get type from handle method.
            /// </summary>
            /// <value>The get type from handle method value.</value>
            internal IMethod GetTypeFromHandleMethod { get; }

            /// <summary>
            /// Gets func object object ctor.
            /// </summary>
            /// <value>The func object object ctor value.</value>
            internal IMethod FuncObjectObjectCtor { get; }

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
