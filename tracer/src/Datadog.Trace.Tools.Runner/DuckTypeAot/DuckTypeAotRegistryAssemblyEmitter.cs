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
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal static class DuckTypeAotRegistryAssemblyEmitter
    {
        private const string BootstrapNamespace = "Datadog.Trace.DuckTyping.Generated";
        private const string BootstrapTypeName = "DuckTypeAotRegistryBootstrap";
        private const string BootstrapInitializeMethodName = "Initialize";
        private const string GeneratedProxyNamespace = "Datadog.Trace.DuckTyping.Generated.Proxies";
        private const string AotContractSchemaVersion = "1";
        private const string StatusCodeUnsupportedProxyKind = "DTAOT0202";
        private const string StatusCodeMissingProxyType = "DTAOT0204";
        private const string StatusCodeMissingTargetType = "DTAOT0205";
        private const string StatusCodeMissingMethod = "DTAOT0207";
        private const string StatusCodeIncompatibleSignature = "DTAOT0209";
        private const string StatusCodeUnsupportedProxyConstructor = "DTAOT0210";
        private const string StatusCodeUnsupportedClosedGenericMapping = "DTAOT0211";
        private const string DuckAttributeTypeName = "Datadog.Trace.DuckTyping.DuckAttribute";
        private const string DuckFieldAttributeTypeName = "Datadog.Trace.DuckTyping.DuckFieldAttribute";
        private const string DuckPropertyOrFieldAttributeTypeName = "Datadog.Trace.DuckTyping.DuckPropertyOrFieldAttribute";
        private const string DuckReverseMethodAttributeTypeName = "Datadog.Trace.DuckTyping.DuckReverseMethodAttribute";
        private const int DuckKindProperty = 0;
        private const int DuckKindField = 1;
        private const int DuckKindPropertyOrField = 2;

        private enum ForwardBindingKind
        {
            Method,
            FieldGet,
            FieldSet
        }

        private enum FieldAccessorKind
        {
            Getter,
            Setter
        }

        private enum StructCopySourceKind
        {
            Property,
            Field
        }

        private enum FieldResolutionMode
        {
            Disabled,
            AllowFallback,
            FieldOnly
        }

        private enum MethodArgumentConversionKind
        {
            None,
            UnwrapValueWithType,
            ExtractDuckTypeInstance,
            TypeConversion
        }

        private enum MethodReturnConversionKind
        {
            None,
            WrapValueWithType,
            DuckChainToProxy,
            TypeConversion
        }

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
                        targetModulesByAssemblyName);
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

            moduleDef.Write(artifactPaths.OutputAssemblyPath);

            var registryInfo = new DuckTypeAotRegistryAssemblyInfo(
                generatedAssemblyName,
                bootstrapType.FullName,
                Path.GetFullPath(artifactPaths.OutputAssemblyPath),
                deterministicMvid);

            return new DuckTypeAotRegistryEmissionResult(registryInfo, mappingResults);
        }

        private static string ResolveAssemblyMvid(string assemblyPath)
        {
            using var module = ModuleDefMD.Load(assemblyPath);
            return module.Mvid?.ToString("D") ?? string.Empty;
        }

        private static DuckTypeAotMappingEmissionResult EmitMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName)
        {
            var isReverseMapping = mapping.Mode == DuckTypeAotMappingMode.Reverse;
            if (DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.ProxyTypeName) ||
                DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.TargetTypeName))
            {
                var closedGenericDetail = $"Closed generic mappings are not yet emitted in this phase. proxy='{mapping.ProxyTypeName}', target='{mapping.TargetTypeName}'.";
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedClosedGenericMapping,
                    StatusCodeUnsupportedClosedGenericMapping,
                    closedGenericDetail);
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

            if (!TryResolveType(targetModule, mapping.TargetTypeName, out var targetType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            if (proxyType.IsValueType)
            {
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

            if (!proxyType.IsInterface && !proxyType.IsClass)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnsupportedProxyKind,
                    $"Proxy type '{mapping.ProxyTypeName}' is not supported. Only interface, class, and DuckCopy struct proxies are emitted in this phase.");
            }

            var isInterfaceProxy = proxyType.IsInterface;
            if (!TryCollectForwardBindings(mapping, proxyType, targetType, isInterfaceProxy, out var bindings, out var failure))
            {
                return failure!;
            }

            IMethod baseCtorToCall = importedMembers.ObjectCtor;
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
            var generatedType = new TypeDefUser(
                GeneratedProxyNamespace,
                generatedTypeName,
                isInterfaceProxy ? moduleDef.CorLibTypes.Object.TypeDefOrRef : moduleDef.Import(proxyType))
            {
                Attributes = TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed
            };
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
                        var targetMethodToCall = CreateMethodCallTarget(moduleDef, importedTargetMethod, generatedMethod);
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

                if (binding.SourceKind == StructCopySourceKind.Property)
                {
                    var sourceProperty = binding.SourceProperty!;
                    var sourceGetter = sourceProperty.GetMethod!;
                    var importedSourceGetter = moduleDef.Import(sourceGetter);

                    if (!sourceGetter.IsStatic)
                    {
                        activatorMethod.Body.Instructions.Add((targetType.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc).ToInstruction(targetLocal));
                    }

                    if (!sourceGetter.IsStatic && targetType.IsValueType && (sourceGetter.IsVirtual || sourceGetter.DeclaringType.IsInterface))
                    {
                        activatorMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                        activatorMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedSourceGetter));
                    }
                    else
                    {
                        var callOpcode = sourceGetter.IsStatic ? OpCodes.Call : (sourceGetter.IsVirtual || sourceGetter.DeclaringType.IsInterface ? OpCodes.Callvirt : OpCodes.Call);
                        activatorMethod.Body.Instructions.Add(callOpcode.ToInstruction(importedSourceGetter));
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
                if (proxyField.IsStatic || proxyField.IsInitOnly || !proxyField.IsPublic)
                {
                    continue;
                }

                if (proxyField.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, "Datadog.Trace.DuckTyping.DuckIgnoreAttribute", StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!TryResolveStructCopyFieldBinding(mapping, targetType, proxyField, out var binding, out failure))
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

            if (!hasFieldOnlyAttribute &&
                TryFindStructCopyTargetProperty(targetType, candidateNames, out var targetProperty))
            {
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

            if (hasFieldOnlyAttribute || allowFieldFallback)
            {
                if (TryFindStructCopyTargetField(targetType, candidateNames, out var targetField))
                {
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

        private static bool TryFindStructCopyTargetProperty(TypeDef targetType, IReadOnlyList<string> candidateNames, out PropertyDef? targetProperty)
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

                        targetProperty = property;
                        return true;
                    }

                    current = current.BaseType?.ResolveTypeDef();
                }
            }

            targetProperty = null;
            return false;
        }

        private static bool TryFindStructCopyTargetField(TypeDef targetType, IReadOnlyList<string> candidateNames, out FieldDef? targetField)
        {
            foreach (var candidateName in candidateNames)
            {
                var current = targetType;
                while (current is not null)
                {
                    foreach (var field in current.Fields)
                    {
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

        private static bool TryCreateReturnConversion(TypeSig proxyReturnType, TypeSig targetReturnType, out MethodReturnConversion returnConversion)
        {
            if (AreTypesEquivalent(proxyReturnType, targetReturnType))
            {
                returnConversion = MethodReturnConversion.None();
                return true;
            }

            if (TryGetValueWithTypeArgument(proxyReturnType, out var proxyReturnValueWithTypeArgument) && AreTypesEquivalent(proxyReturnValueWithTypeArgument!, targetReturnType))
            {
                returnConversion = MethodReturnConversion.WrapValueWithType(proxyReturnType, proxyReturnValueWithTypeArgument!);
                return true;
            }

            if (IsDuckChainingRequired(targetReturnType, proxyReturnType))
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

        private static IMethod CreateMethodCallTarget(ModuleDef moduleDef, IMethodDefOrRef importedTargetMethod, MethodDef generatedMethod)
        {
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

        private static void EmitMethodReturnConversion(
            ModuleDef moduleDef,
            CilBody methodBody,
            MethodReturnConversion conversion,
            ImportedMembers importedMembers,
            string context)
        {
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

        private static void EmitMethodArgumentConversion(
            ModuleDef moduleDef,
            CilBody methodBody,
            MethodArgumentConversion conversion,
            ImportedMembers importedMembers,
            string context)
        {
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
                    methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                    methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                    EmitObjectToExpectedTypeConversion(moduleDef, methodBody, conversion.InnerTypeSig!, context);
                    return;
                case MethodArgumentConversionKind.TypeConversion:
                    EmitTypeConversion(moduleDef, methodBody, conversion.WrapperTypeSig!, conversion.InnerTypeSig!, context);
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported method argument conversion '{conversion.Kind}'.");
            }
        }

        private static void EmitLoadByRefValue(ModuleDef moduleDef, CilBody methodBody, TypeSig valueTypeSig, string context)
        {
            var importedValueType = ResolveImportedTypeForTypeToken(moduleDef, valueTypeSig, context);
            methodBody.Instructions.Add(OpCodes.Ldobj.ToInstruction(importedValueType));
        }

        private static void EmitStoreByRefValue(ModuleDef moduleDef, CilBody methodBody, TypeSig valueTypeSig, string context)
        {
            var importedValueType = ResolveImportedTypeForTypeToken(moduleDef, valueTypeSig, context);
            methodBody.Instructions.Add(OpCodes.Stobj.ToInstruction(importedValueType));
        }

        private static bool CanUseTypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
        {
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

        private static void EmitIDuckTypeImplementation(
            ModuleDef moduleDef,
            TypeDef generatedType,
            ITypeDefOrRef importedTargetType,
            FieldDef targetField,
            ImportedMembers importedMembers,
            bool targetIsValueType)
        {
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

            if (generatedType.FindMethod("ToString") is null)
            {
                var toStringMethod = new MethodDefUser(
                    "ToString",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.String),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot);
                toStringMethod.Body = new CilBody();
                if (targetIsValueType)
                {
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldflda.ToInstruction(targetField));
                    toStringMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.ObjectToStringMethod));
                }
                else
                {
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

        private static MethodAttributes GetInterfaceMethodAttributes(MethodDef proxyMethod)
        {
            var attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;
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

        private static bool TryCollectForwardBindings(
            DuckTypeAotMapping mapping,
            TypeDef proxyType,
            TypeDef targetType,
            bool isInterfaceProxy,
            out IReadOnlyList<ForwardBinding> bindings,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            if (isInterfaceProxy)
            {
                return TryCollectForwardInterfaceBindings(mapping, proxyType, targetType, out bindings, out failure);
            }

            return TryCollectForwardClassBindings(mapping, proxyType, targetType, out bindings, out failure);
        }

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
                if (proxyMethod.IsStatic)
                {
                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                        StatusCodeIncompatibleSignature,
                        $"Proxy method '{proxyMethod.FullName}' is static. Static interface members are not emitted in this phase.");
                    return false;
                }

                if (!TryResolveForwardBinding(mapping, targetType, proxyMethod, out var binding, out failure))
                {
                    return false;
                }

                collectedBindings.Add(binding);
            }

            return true;
        }

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
                if (!TryResolveForwardBinding(mapping, targetType, proxyMethod, out var binding, out failure))
                {
                    return false;
                }

                collectedBindings.Add(binding);
            }

            return true;
        }

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

        private static bool IsSupportedClassProxyMethod(MethodDef method)
        {
            if (method.IsConstructor || method.IsStatic || !method.IsVirtual || method.IsFinal)
            {
                return false;
            }

            return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
        }

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

            if (!fieldOnly)
            {
                foreach (var targetMethod in FindForwardTargetMethodCandidates(targetType, proxyMethod))
                {
                    if (TryCreateForwardMethodBinding(proxyMethod, targetMethod, out var methodBinding, out var methodFailure))
                    {
                        binding = ForwardBinding.ForMethod(proxyMethod, targetMethod, methodBinding);
                        return true;
                    }

                    firstMethodFailure ??= methodFailure;
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
                    if (TryFindForwardTargetField(targetType, proxyMethod, fieldAccessorKind, out var targetField, out var fieldBinding, out var fieldFailureReason))
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

        private static IEnumerable<MethodDef> FindForwardTargetMethodCandidates(TypeDef targetType, MethodDef proxyMethod)
        {
            var candidateMethodNames = GetForwardTargetMethodNames(proxyMethod);
            foreach (var candidateMethodName in candidateMethodNames)
            {
                var current = targetType;
                while (current is not null)
                {
                    foreach (var candidate in current.Methods)
                    {
                        if (!string.Equals(candidate.Name, candidateMethodName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (candidate.MethodSig.GenParamCount != proxyMethod.MethodSig.GenParamCount || candidate.MethodSig.Params.Count != proxyMethod.MethodSig.Params.Count)
                        {
                            continue;
                        }

                        yield return candidate;
                    }

                    current = current.BaseType?.ResolveTypeDef();
                }
            }
        }

        private static bool TryCreateForwardMethodBinding(
            MethodDef proxyMethod,
            MethodDef targetMethod,
            out ForwardMethodBindingInfo binding,
            out MethodCompatibilityFailure? failure)
        {
            if (proxyMethod.MethodSig.GenParamCount != targetMethod.MethodSig.GenParamCount)
            {
                failure = new MethodCompatibilityFailure(
                    $"Generic arity mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                binding = default;
                return false;
            }

            var parameterBindings = new MethodParameterBinding[proxyMethod.MethodSig.Params.Count];
            for (var parameterIndex = 0; parameterIndex < proxyMethod.MethodSig.Params.Count; parameterIndex++)
            {
                if (!TryCreateForwardMethodParameterBinding(proxyMethod, targetMethod, parameterIndex, out var parameterBinding, out failure))
                {
                    binding = default;
                    return false;
                }

                parameterBindings[parameterIndex] = parameterBinding;
            }

            if (!TryCreateReturnConversion(proxyMethod.MethodSig.RetType, targetMethod.MethodSig.RetType, out var returnConversion))
            {
                failure = new MethodCompatibilityFailure(
                    $"Return type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                binding = default;
                return false;
            }

            binding = new ForwardMethodBindingInfo(parameterBindings, returnConversion);
            failure = null;
            return true;
        }

        private static bool TryCreateForwardMethodParameterBinding(
            MethodDef proxyMethod,
            MethodDef targetMethod,
            int parameterIndex,
            out MethodParameterBinding parameterBinding,
            out MethodCompatibilityFailure? failure)
        {
            var proxyParameterType = proxyMethod.MethodSig.Params[parameterIndex];
            var targetParameterType = targetMethod.MethodSig.Params[parameterIndex];

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
            if (proxyIsOut != targetIsOut || proxyIsIn != targetIsIn)
            {
                failure = new MethodCompatibilityFailure(
                    $"Parameter direction mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                parameterBinding = default;
                return false;
            }

            if (!TryGetByRefElementType(proxyParameterType, out var proxyByRefElementTypeSig) ||
                !TryGetByRefElementType(targetParameterType, out var targetByRefElementTypeSig))
            {
                failure = new MethodCompatibilityFailure(
                    $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
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
            else if (!TryCreateMethodArgumentConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, out preCallConversion))
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

        private static bool TryCreateMethodArgumentConversion(TypeSig proxyParameterType, TypeSig targetParameterType, out MethodArgumentConversion argumentConversion)
        {
            if (AreTypesEquivalent(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.None();
                return true;
            }

            if (TryGetValueWithTypeArgument(proxyParameterType, out var proxyValueWithTypeArgument) && AreTypesEquivalent(proxyValueWithTypeArgument!, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.UnwrapValueWithType(proxyParameterType, proxyValueWithTypeArgument!);
                return true;
            }

            if (IsDuckChainingRequired(targetParameterType, proxyParameterType))
            {
                argumentConversion = MethodArgumentConversion.ExtractDuckTypeInstance(proxyParameterType, targetParameterType);
                return true;
            }

            if (CanUseTypeConversion(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.TypeConversion(proxyParameterType, targetParameterType);
                return true;
            }

            argumentConversion = default;
            return false;
        }

        private static bool TryCreateByRefPostCallConversion(TypeSig proxyParameterElementType, TypeSig targetParameterElementType, out MethodReturnConversion returnConversion)
        {
            if (TryCreateReturnConversion(proxyParameterElementType, targetParameterElementType, out returnConversion))
            {
                return true;
            }

            returnConversion = default;
            return false;
        }

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

        private static IField CreateValueWithTypeValueFieldRef(ModuleDef moduleDef, TypeSig wrapperTypeSig, TypeSig innerTypeSig)
        {
            var importedWrapperTypeSig = moduleDef.Import(wrapperTypeSig);
            var typeSpec = moduleDef.UpdateRowId(new TypeSpecUser(importedWrapperTypeSig));
            var fieldRef = new MemberRefUser(moduleDef, "Value", new FieldSig(new GenericVar(0)), typeSpec);
            return moduleDef.UpdateRowId(fieldRef);
        }

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

        private static ITypeDefOrRef ResolveImportedTypeForTypeToken(ModuleDef moduleDef, TypeSig typeSig, string context)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef()
                            ?? throw new InvalidOperationException($"Unable to resolve type token for {context}.");
            return moduleDef.Import(typeDefOrRef) as ITypeDefOrRef
                ?? throw new InvalidOperationException($"Unable to import type token for {context}.");
        }

        private static void EmitObjectToExpectedTypeConversion(ModuleDef moduleDef, CilBody body, TypeSig expectedTypeSig, string context)
        {
            if (expectedTypeSig.ElementType == ElementType.Object)
            {
                return;
            }

            var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedTypeSig, context);
            if (expectedTypeSig.ToTypeDefOrRef()?.ResolveTypeDef()?.IsValueType == true)
            {
                body.Instructions.Add(OpCodes.Unbox_Any.ToInstruction(importedExpectedType));
                return;
            }

            body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
        }

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
            if (targetTypeSig.ToTypeDefOrRef()?.ResolveTypeDef()?.IsValueType == true)
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
                        if (!string.Equals(candidate.Name, candidateFieldName, StringComparison.Ordinal))
                        {
                            continue;
                        }

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

        private static bool AreFieldAccessorSignatureCompatible(
            MethodDef proxyMethod,
            FieldDef targetField,
            FieldAccessorKind accessorKind,
            out ForwardFieldBindingInfo fieldBinding,
            out string? failureReason)
        {
            fieldBinding = ForwardFieldBindingInfo.None();
            failureReason = null;
            switch (accessorKind)
            {
                case FieldAccessorKind.Getter:
                {
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
                {
                    if (targetField.IsLiteral || targetField.IsInitOnly)
                    {
                        failureReason = $"Target field '{targetField.FullName}' is readonly and cannot be set by proxy method '{proxyMethod.FullName}'.";
                        return false;
                    }

                    var proxyParameterType = proxyMethod.MethodSig.Params[0];
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
                    failureReason = $"Proxy method '{proxyMethod.FullName}' does not map to a supported field accessor.";
                    return false;
            }
        }

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

        private static bool IsDuckAttribute(CustomAttribute customAttribute)
        {
            var fullName = customAttribute.TypeFullName;
            return string.Equals(fullName, DuckAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckFieldAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckPropertyOrFieldAttributeTypeName, StringComparison.Ordinal)
                || string.Equals(fullName, DuckReverseMethodAttributeTypeName, StringComparison.Ordinal);
        }

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

        private static IEnumerable<string> SplitDuckNames(string configuredName)
        {
            return configuredName
                .Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name));
        }

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

        private static bool AreTypesEquivalent(TypeSig proxyType, TypeSig targetType)
        {
            return string.Equals(proxyType.FullName, targetType.FullName, StringComparison.Ordinal);
        }

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

        private static bool IsDuckCopyValueType(TypeDef typeDef)
        {
            foreach (var customAttribute in typeDef.CustomAttributes)
            {
                if (string.Equals(customAttribute.TypeFullName, "Datadog.Trace.DuckTyping.DuckCopyAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

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

        private static bool TryResolveType(ModuleDef module, string typeName, out TypeDef type)
        {
            type = module.Find(typeName, isReflectionName: true)
                ?? module.Find(typeName, isReflectionName: false)
                ?? module.GetTypes().FirstOrDefault(candidate =>
                    string.Equals(candidate.ReflectionFullName, typeName, StringComparison.Ordinal) ||
                    string.Equals(candidate.FullName, typeName, StringComparison.Ordinal))!;

            return type is not null;
        }

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

        private static IReadOnlyDictionary<string, ModuleDefMD> LoadModules(IReadOnlyDictionary<string, string> assemblyPathsByName)
        {
            var modulesByAssemblyName = new Dictionary<string, ModuleDefMD>(StringComparer.OrdinalIgnoreCase);
            foreach (var (assemblyName, assemblyPath) in assemblyPathsByName)
            {
                modulesByAssemblyName[assemblyName] = ModuleDefMD.Load(assemblyPath);
            }

            return modulesByAssemblyName;
        }

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

        private static string ComputeStableShortHash(string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return string.Concat(bytes.Take(4).Select(b => b.ToString("x2")));
        }

        private readonly struct StructCopyFieldBinding
        {
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

            internal FieldDef ProxyField { get; }

            internal StructCopySourceKind SourceKind { get; }

            internal PropertyDef? SourceProperty { get; }

            internal FieldDef? SourceField { get; }

            internal MethodReturnConversion ReturnConversion { get; }

            internal static StructCopyFieldBinding ForProperty(FieldDef proxyField, PropertyDef sourceProperty, MethodReturnConversion returnConversion)
            {
                return new StructCopyFieldBinding(proxyField, StructCopySourceKind.Property, sourceProperty, sourceField: null, returnConversion);
            }

            internal static StructCopyFieldBinding ForField(FieldDef proxyField, FieldDef sourceField, MethodReturnConversion returnConversion)
            {
                return new StructCopyFieldBinding(proxyField, StructCopySourceKind.Field, sourceProperty: null, sourceField, returnConversion);
            }
        }

        private readonly struct ForwardBinding
        {
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

            internal ForwardBindingKind Kind { get; }

            internal MethodDef ProxyMethod { get; }

            internal MethodDef? TargetMethod { get; }

            internal FieldDef? TargetField { get; }

            internal ForwardMethodBindingInfo? MethodBinding { get; }

            internal ForwardFieldBindingInfo? FieldBinding { get; }

            internal static ForwardBinding ForMethod(MethodDef proxyMethod, MethodDef targetMethod, ForwardMethodBindingInfo methodBinding)
            {
                return new ForwardBinding(ForwardBindingKind.Method, proxyMethod, targetMethod, targetField: null, methodBinding, fieldBinding: null);
            }

            internal static ForwardBinding ForFieldGet(MethodDef proxyMethod, FieldDef targetField, ForwardFieldBindingInfo fieldBinding)
            {
                return new ForwardBinding(ForwardBindingKind.FieldGet, proxyMethod, targetMethod: null, targetField, methodBinding: null, fieldBinding);
            }

            internal static ForwardBinding ForFieldSet(MethodDef proxyMethod, FieldDef targetField, ForwardFieldBindingInfo fieldBinding)
            {
                return new ForwardBinding(ForwardBindingKind.FieldSet, proxyMethod, targetMethod: null, targetField, methodBinding: null, fieldBinding);
            }
        }

        private readonly struct ForwardMethodBindingInfo
        {
            internal ForwardMethodBindingInfo(IReadOnlyList<MethodParameterBinding> parameterBindings, MethodReturnConversion returnConversion)
            {
                ParameterBindings = parameterBindings;
                ReturnConversion = returnConversion;
            }

            internal IReadOnlyList<MethodParameterBinding> ParameterBindings { get; }

            internal MethodReturnConversion ReturnConversion { get; }
        }

        private readonly struct MethodParameterBinding
        {
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

            internal bool IsByRef { get; }

            internal bool UseLocalForByRef { get; }

            internal bool IsOut { get; }

            internal TypeSig ProxyTypeSig { get; }

            internal TypeSig TargetTypeSig { get; }

            internal TypeSig? ProxyByRefElementTypeSig { get; }

            internal TypeSig? TargetByRefElementTypeSig { get; }

            internal MethodArgumentConversion PreCallConversion { get; }

            internal MethodReturnConversion PostCallConversion { get; }

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

        private readonly struct ForwardFieldBindingInfo
        {
            private ForwardFieldBindingInfo(MethodArgumentConversion argumentConversion, MethodReturnConversion returnConversion)
            {
                ArgumentConversion = argumentConversion;
                ReturnConversion = returnConversion;
            }

            internal MethodArgumentConversion ArgumentConversion { get; }

            internal MethodReturnConversion ReturnConversion { get; }

            internal static ForwardFieldBindingInfo None()
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.None());
            }

            internal static ForwardFieldBindingInfo UnwrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.UnwrapValueWithType(wrapperTypeSig, innerTypeSig), MethodReturnConversion.None());
            }

            internal static ForwardFieldBindingInfo WrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.WrapValueWithType(wrapperTypeSig, innerTypeSig));
            }

            internal static ForwardFieldBindingInfo ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.ExtractDuckTypeInstance(wrapperTypeSig, innerTypeSig), MethodReturnConversion.None());
            }

            internal static ForwardFieldBindingInfo DuckChainToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.DuckChainToProxy(wrapperTypeSig, innerTypeSig));
            }

            internal static ForwardFieldBindingInfo ReturnTypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.None(), MethodReturnConversion.TypeConversion(actualTypeSig, expectedTypeSig));
            }

            internal static ForwardFieldBindingInfo TypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new ForwardFieldBindingInfo(MethodArgumentConversion.TypeConversion(actualTypeSig, expectedTypeSig), MethodReturnConversion.None());
            }
        }

        private readonly struct MethodArgumentConversion
        {
            private MethodArgumentConversion(MethodArgumentConversionKind kind, TypeSig? wrapperTypeSig, TypeSig? innerTypeSig)
            {
                Kind = kind;
                WrapperTypeSig = wrapperTypeSig;
                InnerTypeSig = innerTypeSig;
            }

            internal MethodArgumentConversionKind Kind { get; }

            internal TypeSig? WrapperTypeSig { get; }

            internal TypeSig? InnerTypeSig { get; }

            internal static MethodArgumentConversion None()
            {
                return new MethodArgumentConversion(MethodArgumentConversionKind.None, wrapperTypeSig: null, innerTypeSig: null);
            }

            internal static MethodArgumentConversion UnwrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(MethodArgumentConversionKind.UnwrapValueWithType, wrapperTypeSig, innerTypeSig);
            }

            internal static MethodArgumentConversion ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(MethodArgumentConversionKind.ExtractDuckTypeInstance, wrapperTypeSig, innerTypeSig);
            }

            internal static MethodArgumentConversion TypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new MethodArgumentConversion(MethodArgumentConversionKind.TypeConversion, actualTypeSig, expectedTypeSig);
            }
        }

        private readonly struct MethodReturnConversion
        {
            private MethodReturnConversion(MethodReturnConversionKind kind, TypeSig? wrapperTypeSig, TypeSig? innerTypeSig)
            {
                Kind = kind;
                WrapperTypeSig = wrapperTypeSig;
                InnerTypeSig = innerTypeSig;
            }

            internal MethodReturnConversionKind Kind { get; }

            internal TypeSig? WrapperTypeSig { get; }

            internal TypeSig? InnerTypeSig { get; }

            internal static MethodReturnConversion None()
            {
                return new MethodReturnConversion(MethodReturnConversionKind.None, wrapperTypeSig: null, innerTypeSig: null);
            }

            internal static MethodReturnConversion WrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.WrapValueWithType, wrapperTypeSig, innerTypeSig);
            }

            internal static MethodReturnConversion DuckChainToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.DuckChainToProxy, wrapperTypeSig, innerTypeSig);
            }

            internal static MethodReturnConversion TypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.TypeConversion, actualTypeSig, expectedTypeSig);
            }
        }

        private readonly struct ParameterDirection
        {
            internal ParameterDirection(bool isOut, bool isIn)
            {
                IsOut = isOut;
                IsIn = isIn;
            }

            internal bool IsOut { get; }

            internal bool IsIn { get; }
        }

        private readonly struct ByRefWriteBackPlan
        {
            internal ByRefWriteBackPlan(Parameter proxyParameter, Local targetLocal, MethodParameterBinding parameterBinding)
            {
                ProxyParameter = proxyParameter;
                TargetLocal = targetLocal;
                ParameterBinding = parameterBinding;
            }

            internal Parameter ProxyParameter { get; }

            internal Local TargetLocal { get; }

            internal MethodParameterBinding ParameterBinding { get; }
        }

        private readonly struct MethodCompatibilityFailure
        {
            internal MethodCompatibilityFailure(string detail)
            {
                Detail = detail;
            }

            internal string Detail { get; }
        }

        private sealed class ImportedMembers
        {
            internal ImportedMembers(ModuleDef moduleDef)
            {
                var getTypeFromHandleMethod = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new[] { typeof(RuntimeTypeHandle) });
                if (getTypeFromHandleMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve Type.GetTypeFromHandle(RuntimeTypeHandle).");
                }

                var funcObjectObjectCtor = typeof(Func<object, object>).GetConstructor(new[] { typeof(object), typeof(IntPtr) });
                if (funcObjectObjectCtor is null)
                {
                    throw new InvalidOperationException("Unable to resolve Func<object, object> constructor.");
                }

                var registerAotProxyMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotProxy),
                    new[] { typeof(Type), typeof(Type), typeof(Type), typeof(Func<object, object>) });
                if (registerAotProxyMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotProxy(Type, Type, Type, Func<object, object>).");
                }

                var registerAotReverseProxyMethod = typeof(DuckType).GetMethod(
                    nameof(DuckType.RegisterAotReverseProxy),
                    new[] { typeof(Type), typeof(Type), typeof(Type), typeof(Func<object, object>) });
                if (registerAotReverseProxyMethod is null)
                {
                    throw new InvalidOperationException("Unable to resolve DuckType.RegisterAotReverseProxy(Type, Type, Type, Func<object, object>).");
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
                if (importedIgnoresAccessChecksToCtor is null)
                {
                    throw new InvalidOperationException("Unable to import IgnoresAccessChecksToAttribute(string).");
                }

                IgnoresAccessChecksToAttributeCtor = importedIgnoresAccessChecksToCtor;
            }

            internal IMethod GetTypeFromHandleMethod { get; }

            internal IMethod FuncObjectObjectCtor { get; }

            internal IMethod RegisterAotProxyMethod { get; }

            internal IMethod RegisterAotReverseProxyMethod { get; }

            internal IMethod EnableAotModeMethod { get; }

            internal IMethod ValidateAotRegistryContractMethod { get; }

            internal IMethod ObjectCtor { get; }

            internal IMethod ObjectToStringMethod { get; }

            internal ITypeDefOrRef IDuckTypeType { get; }

            internal IMethod IDuckTypeInstanceGetter { get; }

            internal ICustomAttributeType IgnoresAccessChecksToAttributeCtor { get; }
        }
    }
}
