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
        private const string StatusCodeUnsupportedProxyKind = "DTAOT0202";
        private const string StatusCodeUnsupportedTargetValueType = "DTAOT0203";
        private const string StatusCodeMissingProxyType = "DTAOT0204";
        private const string StatusCodeMissingTargetType = "DTAOT0205";
        private const string StatusCodeUnsupportedGenericMethod = "DTAOT0206";
        private const string StatusCodeMissingMethod = "DTAOT0207";
        private const string StatusCodeIncompatibleSignature = "DTAOT0209";
        private const string StatusCodeUnsupportedProxyConstructor = "DTAOT0210";
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
            ExtractDuckTypeInstance
        }

        private enum MethodReturnConversionKind
        {
            None,
            WrapValueWithType,
            DuckChainToProxy
        }

        internal static DuckTypeAotRegistryEmissionResult Emit(
            DuckTypeAotGenerateOptions options,
            DuckTypeAotArtifactPaths artifactPaths,
            DuckTypeAotMappingResolutionResult mappingResolutionResult)
        {
            var generatedAssemblyName = options.AssemblyName ?? Path.GetFileNameWithoutExtension(artifactPaths.OutputAssemblyPath);
            var deterministicMvid = ComputeDeterministicMvid(generatedAssemblyName, mappingResolutionResult.Mappings);

            var assemblyDef = new AssemblyDefUser(generatedAssemblyName, new Version(1, 0, 0, 0));
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

            if (!proxyType.IsInterface && !proxyType.IsClass)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnsupportedProxyKind,
                    $"Proxy type '{mapping.ProxyTypeName}' is not supported. Only interface and class proxies are emitted in this phase.");
            }

            if (targetType.IsValueType)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedTargetValueType,
                    StatusCodeUnsupportedTargetValueType,
                    $"Target type '{mapping.TargetTypeName}' is a value type. Value-type targets are not emitted in this phase.");
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
            generatedConstructor.Body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedTargetType));
            generatedConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(targetField));
            generatedConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedType.Methods.Add(generatedConstructor);

            EmitIDuckTypeImplementation(moduleDef, generatedType, importedTargetType, targetField, importedMembers);

            foreach (var binding in bindings!)
            {
                var proxyMethod = binding.ProxyMethod;
                var importedProxyMethod = moduleDef.Import(proxyMethod);

                var generatedMethod = new MethodDefUser(
                    proxyMethod.Name,
                    importedProxyMethod.MethodSig,
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    isInterfaceProxy ? GetInterfaceMethodAttributes(proxyMethod) : GetClassOverrideMethodAttributes(proxyMethod));

                generatedMethod.Body = new CilBody();
                switch (binding.Kind)
                {
                    case ForwardBindingKind.Method:
                    {
                        var targetMethod = binding.TargetMethod!;
                        var methodBinding = binding.MethodBinding!.Value;
                        if (!targetMethod.IsStatic)
                        {
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(targetField));
                        }

                        for (var parameterIndex = 0; parameterIndex < proxyMethod.MethodSig.Params.Count; parameterIndex++)
                        {
                            var argumentConversion = methodBinding.ArgumentConversions[parameterIndex];
                            generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[parameterIndex + 1]));
                            if (argumentConversion.Kind == MethodArgumentConversionKind.UnwrapValueWithType)
                            {
                                var valueFieldRef = CreateValueWithTypeValueFieldRef(moduleDef, argumentConversion.WrapperTypeSig!, argumentConversion.InnerTypeSig!);
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(valueFieldRef));
                            }
                            else if (argumentConversion.Kind == MethodArgumentConversionKind.ExtractDuckTypeInstance)
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                                generatedMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                                EmitObjectToExpectedTypeConversion(moduleDef, generatedMethod.Body, argumentConversion.InnerTypeSig!, $"target parameter of method '{targetMethod.FullName}'");
                            }
                        }

                        var importedTargetMethod = moduleDef.Import(targetMethod);
                        var targetCallOpcode = targetMethod.IsStatic ? OpCodes.Call : (targetMethod.IsVirtual || targetMethod.DeclaringType.IsInterface ? OpCodes.Callvirt : OpCodes.Call);
                        generatedMethod.Body.Instructions.Add(targetCallOpcode.ToInstruction(importedTargetMethod));

                        if (methodBinding.ReturnConversion.Kind == MethodReturnConversionKind.WrapValueWithType)
                        {
                            var importedTargetReturnType = ResolveImportedTypeForTypeToken(moduleDef, methodBinding.ReturnConversion.InnerTypeSig!, $"target method '{targetMethod.FullName}'");
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetReturnType));
                            generatedMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));

                            var createMethodRef = CreateValueWithTypeCreateMethodRef(
                                moduleDef,
                                methodBinding.ReturnConversion.WrapperTypeSig!,
                                methodBinding.ReturnConversion.InnerTypeSig!);
                            generatedMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(createMethodRef));
                        }
                        else if (methodBinding.ReturnConversion.Kind == MethodReturnConversionKind.DuckChainToProxy)
                        {
                            EmitDuckChainToProxyConversion(
                                moduleDef,
                                generatedMethod.Body,
                                methodBinding.ReturnConversion.WrapperTypeSig!,
                                methodBinding.ReturnConversion.InnerTypeSig!,
                                $"target method '{targetMethod.FullName}'");
                        }

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
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(targetField));
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(importedTargetMemberField));
                        }

                        if (fieldBinding.ReturnConversion.Kind == MethodReturnConversionKind.WrapValueWithType)
                        {
                            var importedFieldTypeForToken = ResolveImportedTypeForTypeToken(moduleDef, fieldBinding.ReturnConversion.InnerTypeSig!, $"target field '{binding.TargetField!.FullName}'");
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedFieldTypeForToken));
                            generatedMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));

                            var createMethodRef = CreateValueWithTypeCreateMethodRef(
                                moduleDef,
                                fieldBinding.ReturnConversion.WrapperTypeSig!,
                                fieldBinding.ReturnConversion.InnerTypeSig!);
                            generatedMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(createMethodRef));
                        }
                        else if (fieldBinding.ReturnConversion.Kind == MethodReturnConversionKind.DuckChainToProxy)
                        {
                            EmitDuckChainToProxyConversion(
                                moduleDef,
                                generatedMethod.Body,
                                fieldBinding.ReturnConversion.WrapperTypeSig!,
                                fieldBinding.ReturnConversion.InnerTypeSig!,
                                $"target field '{binding.TargetField!.FullName}'");
                        }

                        break;
                    }

                    case ForwardBindingKind.FieldSet:
                    {
                        var fieldBinding = binding.FieldBinding!.Value;
                        var importedTargetMemberField = moduleDef.Import(binding.TargetField!);
                        if (binding.TargetField!.IsStatic)
                        {
                            generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                            if (fieldBinding.ArgumentConversion.Kind == MethodArgumentConversionKind.UnwrapValueWithType)
                            {
                                var valueFieldRef = CreateValueWithTypeValueFieldRef(moduleDef, fieldBinding.ArgumentConversion.WrapperTypeSig!, fieldBinding.ArgumentConversion.InnerTypeSig!);
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(valueFieldRef));
                            }
                            else if (fieldBinding.ArgumentConversion.Kind == MethodArgumentConversionKind.ExtractDuckTypeInstance)
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                                generatedMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                                EmitObjectToExpectedTypeConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion.InnerTypeSig!, $"target field '{binding.TargetField!.FullName}'");
                            }

                            generatedMethod.Body.Instructions.Add(OpCodes.Stsfld.ToInstruction(importedTargetMemberField));
                        }
                        else
                        {
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                            generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(targetField));
                            generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                            if (fieldBinding.ArgumentConversion.Kind == MethodArgumentConversionKind.UnwrapValueWithType)
                            {
                                var valueFieldRef = CreateValueWithTypeValueFieldRef(moduleDef, fieldBinding.ArgumentConversion.WrapperTypeSig!, fieldBinding.ArgumentConversion.InnerTypeSig!);
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(valueFieldRef));
                            }
                            else if (fieldBinding.ArgumentConversion.Kind == MethodArgumentConversionKind.ExtractDuckTypeInstance)
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                                generatedMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                                EmitObjectToExpectedTypeConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion.InnerTypeSig!, $"target field '{binding.TargetField!.FullName}'");
                            }

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

            return DuckTypeAotMappingEmissionResult.Compatible(mapping);
        }

        private static void EmitIDuckTypeImplementation(
            ModuleDef moduleDef,
            TypeDef generatedType,
            ITypeDefOrRef importedTargetType,
            FieldDef targetField,
            ImportedMembers importedMembers)
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

                if (proxyMethod.MethodSig.GenParamCount > 0)
                {
                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedGenericMethod,
                        StatusCodeUnsupportedGenericMethod,
                        $"Proxy method '{proxyMethod.FullName}' is generic. Generic methods are not emitted in this phase.");
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
                if (proxyMethod.MethodSig.GenParamCount > 0)
                {
                    failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedGenericMethod,
                        StatusCodeUnsupportedGenericMethod,
                        $"Proxy method '{proxyMethod.FullName}' is generic. Generic methods are not emitted in this phase.");
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

                        if (candidate.MethodSig.GenParamCount > 0 || candidate.MethodSig.Params.Count != proxyMethod.MethodSig.Params.Count)
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
            var argumentConversions = new MethodArgumentConversion[proxyMethod.MethodSig.Params.Count];
            for (var parameterIndex = 0; parameterIndex < proxyMethod.MethodSig.Params.Count; parameterIndex++)
            {
                var proxyParameterType = proxyMethod.MethodSig.Params[parameterIndex];
                var targetParameterType = targetMethod.MethodSig.Params[parameterIndex];

                if (proxyParameterType.ElementType == ElementType.ByRef || targetParameterType.ElementType == ElementType.ByRef)
                {
                    if (!AreTypesEquivalent(proxyParameterType, targetParameterType))
                    {
                        failure = new MethodCompatibilityFailure(
                            $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                        binding = default;
                        return false;
                    }

                    argumentConversions[parameterIndex] = MethodArgumentConversion.None();
                    continue;
                }

                if (AreTypesEquivalent(proxyParameterType, targetParameterType))
                {
                    argumentConversions[parameterIndex] = MethodArgumentConversion.None();
                    continue;
                }

                if (TryGetValueWithTypeArgument(proxyParameterType, out var proxyValueWithTypeArgument) && AreTypesEquivalent(proxyValueWithTypeArgument!, targetParameterType))
                {
                    argumentConversions[parameterIndex] = MethodArgumentConversion.UnwrapValueWithType(proxyParameterType, proxyValueWithTypeArgument!);
                    continue;
                }

                if (IsDuckChainingRequired(targetParameterType, proxyParameterType))
                {
                    argumentConversions[parameterIndex] = MethodArgumentConversion.ExtractDuckTypeInstance(proxyParameterType, targetParameterType);
                    continue;
                }

                failure = new MethodCompatibilityFailure(
                    $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                binding = default;
                return false;
            }

            MethodReturnConversion returnConversion;
            if (AreTypesEquivalent(proxyMethod.MethodSig.RetType, targetMethod.MethodSig.RetType))
            {
                returnConversion = MethodReturnConversion.None();
            }
            else if (TryGetValueWithTypeArgument(proxyMethod.MethodSig.RetType, out var proxyReturnValueWithTypeArgument) && AreTypesEquivalent(proxyReturnValueWithTypeArgument!, targetMethod.MethodSig.RetType))
            {
                returnConversion = MethodReturnConversion.WrapValueWithType(proxyMethod.MethodSig.RetType, proxyReturnValueWithTypeArgument!);
            }
            else if (IsDuckChainingRequired(targetMethod.MethodSig.RetType, proxyMethod.MethodSig.RetType))
            {
                returnConversion = MethodReturnConversion.DuckChainToProxy(proxyMethod.MethodSig.RetType, targetMethod.MethodSig.RetType);
            }
            else
            {
                failure = new MethodCompatibilityFailure(
                    $"Return type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                binding = default;
                return false;
            }

            binding = new ForwardMethodBindingInfo(argumentConversions, returnConversion);
            failure = null;
            return true;
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

            var createCacheCreateMethodRef = CreateDuckTypeCreateCacheCreateMethodRef(moduleDef, proxyTypeSig);
            body.Instructions.Add(OpCodes.Call.ToInstruction(createCacheCreateMethodRef));
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
                    if (AreTypesEquivalent(proxyMethod.MethodSig.RetType, targetField.FieldSig.Type))
                    {
                        return true;
                    }

                    if (TryGetValueWithTypeArgument(proxyMethod.MethodSig.RetType, out var proxyReturnValueWithTypeArgument)
                     && AreTypesEquivalent(proxyReturnValueWithTypeArgument!, targetField.FieldSig.Type))
                    {
                        fieldBinding = ForwardFieldBindingInfo.WrapValueWithType(proxyMethod.MethodSig.RetType, proxyReturnValueWithTypeArgument!);
                        return true;
                    }

                    if (IsDuckChainingRequired(targetField.FieldSig.Type, proxyMethod.MethodSig.RetType))
                    {
                        fieldBinding = ForwardFieldBindingInfo.DuckChainToProxy(proxyMethod.MethodSig.RetType, targetField.FieldSig.Type);
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
                    if (AreTypesEquivalent(proxyParameterType, targetField.FieldSig.Type))
                    {
                        return true;
                    }

                    if (TryGetValueWithTypeArgument(proxyParameterType, out var proxyParameterValueWithTypeArgument)
                     && AreTypesEquivalent(proxyParameterValueWithTypeArgument!, targetField.FieldSig.Type))
                    {
                        fieldBinding = ForwardFieldBindingInfo.UnwrapValueWithType(proxyParameterType, proxyParameterValueWithTypeArgument!);
                        return true;
                    }

                    if (IsDuckChainingRequired(targetField.FieldSig.Type, proxyParameterType))
                    {
                        fieldBinding = ForwardFieldBindingInfo.ExtractDuckTypeInstance(proxyParameterType, targetField.FieldSig.Type);
                        return true;
                    }

                    if (!AreTypesEquivalent(proxyParameterType, targetField.FieldSig.Type))
                    {
                        failureReason = $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target field '{targetField.FullName}'.";
                        return false;
                    }

                    return true;
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
            if (proxyType.ContainsGenericParameter || targetType.ContainsGenericParameter)
            {
                return false;
            }

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

            var proxyTypeDefOrRef = proxyType.ToTypeDefOrRef();
            var targetTypeDefOrRef = targetType.ToTypeDefOrRef();
            if (proxyTypeDefOrRef is null || targetTypeDefOrRef is null)
            {
                return false;
            }

            var proxyTypeDef = proxyTypeDefOrRef.ResolveTypeDef();
            if (proxyTypeDef?.IsValueType == true)
            {
                return false;
            }

            if (proxyType.DefinitionAssembly?.IsCorLib() == true)
            {
                return false;
            }

            if (IsAssignableFrom(proxyTypeDefOrRef, targetTypeDefOrRef))
            {
                return false;
            }

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
            internal ForwardMethodBindingInfo(IReadOnlyList<MethodArgumentConversion> argumentConversions, MethodReturnConversion returnConversion)
            {
                ArgumentConversions = argumentConversions;
                ReturnConversion = returnConversion;
            }

            internal IReadOnlyList<MethodArgumentConversion> ArgumentConversions { get; }

            internal MethodReturnConversion ReturnConversion { get; }
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

            internal IMethod ObjectCtor { get; }

            internal IMethod ObjectToStringMethod { get; }

            internal ITypeDefOrRef IDuckTypeType { get; }

            internal IMethod IDuckTypeInstanceGetter { get; }

            internal ICustomAttributeType IgnoresAccessChecksToAttributeCtor { get; }
        }
    }
}
