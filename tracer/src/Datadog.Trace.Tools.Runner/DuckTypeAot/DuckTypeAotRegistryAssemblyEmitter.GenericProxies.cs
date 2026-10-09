// <copyright file="DuckTypeAotRegistryAssemblyEmitter.GenericProxies.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.DuckTyping;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Generic proxies: the proxies of the instantiations of an open generic target type ("Library.Type`2"), which a library
    /// creates over types only known at runtime (e.g. over the application's types, through dependency injection), so neither
    /// the declarations nor the generic instantiations found in the assemblies name them. The mapping is emitted like any other
    /// for the target instantiated over placeholder classes (a template, evaluated with dynamic duck typing like the others);
    /// then the proxy type (and the activators of the template) become generic over the type parameters of the target, in a
    /// factory the registry instantiates at runtime with the type arguments of the runtime type
    /// (<see cref="DuckType.RegisterAotGenericProxy"/>). NativeAOT builds those instantiations from the canonical code of the
    /// generic types for reference type arguments; for a value type argument it didn't compile, there is no proxy.
    /// </summary>
    internal static partial class DuckTypeAotRegistryAssemblyEmitter
    {
        /// <summary>
        /// The assembly of the placeholder classes the templates are instantiated over. It's only an input of the generator: no
        /// reference to it is left in the registry.
        /// </summary>
        internal const string GenericArgumentsAssemblyName = "Datadog.Trace.DuckType.GenericArguments";

        private const string GenericArgumentsNamespace = "Datadog.Trace.DuckType.GenericArguments";

        private const int MaxGenericProxyArity = 16;

        private static readonly object GenericArgumentsAssemblyLock = new();

        private static string? _genericArgumentsAssemblyPath;

        /// <summary>
        /// The templates of the current emission (the mappings over placeholders), with the mapping of the open generic target
        /// each one stands for, by template key.
        /// </summary>
        private static IReadOnlyDictionary<string, DuckTypeAotMapping>? genericProxyTemplates;

        /// <summary>
        /// Gets whether a mapping is emitted as a generic proxy: a forward mapping of a proxy that isn't generic to an open
        /// generic target type ("Library.Type`2").
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <returns>true for a generic proxy; otherwise, false.</returns>
        internal static bool IsGenericProxyMapping(DuckTypeAotMapping mapping)
            => mapping.Mode == DuckTypeAotMappingMode.Forward &&
               !DuckTypeAotNameHelpers.IsGenericTypeName(mapping.ProxyTypeName) &&
               DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.TargetTypeName) &&
               mapping.TargetTypeName.IndexOf('[') < 0 &&
               DuckTypeAotNameHelpers.GetDeclaredGenericArity(mapping.TargetTypeName) is > 0 and <= MaxGenericProxyArity;

        /// <summary>
        /// Replaces the forward mappings of open generic target types by their templates, over the placeholder classes.
        /// </summary>
        /// <param name="resolution">The mapping resolution.</param>
        /// <param name="templates">The mapping each template stands for, by template key.</param>
        /// <returns>The resolution with the templates (the same when there's none).</returns>
        private static DuckTypeAotMappingResolutionResult PrepareGenericProxyTemplates(DuckTypeAotMappingResolutionResult resolution, out Dictionary<string, DuckTypeAotMapping> templates)
        {
            templates = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);
            var mappings = new List<DuckTypeAotMapping>(resolution.Mappings.Count);
            foreach (var mapping in resolution.Mappings)
            {
                if (!IsGenericProxyMapping(mapping) || !resolution.TargetAssemblyPathsByName.ContainsKey(mapping.TargetAssemblyName))
                {
                    mappings.Add(mapping);
                    continue;
                }

                var arity = DuckTypeAotNameHelpers.GetDeclaredGenericArity(mapping.TargetTypeName);
                var arguments = string.Join(",", Enumerable.Range(0, arity).Select(i => $"[{GenericArgumentsNamespace}.Arg{i}, {GenericArgumentsAssemblyName}]"));
                var template = new DuckTypeAotMapping(
                    mapping.ProxyTypeName,
                    mapping.ProxyAssemblyName,
                    $"{mapping.TargetTypeName}[{arguments}]",
                    mapping.TargetAssemblyName,
                    mapping.Mode,
                    mapping.Source,
                    mapping.ScenarioId,
                    mapping.IncludesDerivedTypes);
                templates[template.Key] = mapping;
                mappings.Add(template);
            }

            if (templates.Count == 0)
            {
                return resolution;
            }

            var targetAssemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in resolution.TargetAssemblyPathsByName)
            {
                targetAssemblies[entry.Key] = entry.Value;
            }

            targetAssemblies[GenericArgumentsAssemblyName] = GetGenericArgumentsAssemblyPath();
            return new DuckTypeAotMappingResolutionResult(mappings, resolution.ProxyAssemblyPathsByName, targetAssemblies, resolution.GenericTypeRoots, resolution.Warnings, resolution.Errors);
        }

        /// <summary>
        /// Gets the results of the mappings the templates stand for, instead of the templates.
        /// </summary>
        /// <param name="emissionResult">The emission result.</param>
        /// <param name="templates">The mapping each template stands for, by template key.</param>
        /// <returns>The emission result for the mappings of the caller.</returns>
        private static DuckTypeAotRegistryEmissionResult RestoreGenericProxyTemplateMappings(DuckTypeAotRegistryEmissionResult emissionResult, IReadOnlyDictionary<string, DuckTypeAotMapping> templates)
        {
            if (templates.Count == 0)
            {
                return emissionResult;
            }

            var results = new Dictionary<string, DuckTypeAotMappingEmissionResult>(StringComparer.Ordinal);
            foreach (var entry in emissionResult.MappingResultsByKey)
            {
                if (templates.TryGetValue(entry.Key, out var mapping))
                {
                    results[mapping.Key] = entry.Value.WithMapping(mapping);
                }
                else
                {
                    results[entry.Key] = entry.Value;
                }
            }

            var registrations = emissionResult.RuntimeRegistrations
                                              .Select(registration => templates.TryGetValue(registration.Mapping.Key, out var mapping)
                                                                          ? new DuckTypeAotRuntimeRegistration(mapping, mapping.Key, registration.Kind)
                                                                          : registration)
                                              .ToList();
            return new DuckTypeAotRegistryEmissionResult(emissionResult.RegistryAssemblyInfo, results, registrations, emissionResult.Warnings);
        }

        /// <summary>
        /// Gets the path of the assembly of the placeholder classes, written once per process (its content never changes).
        /// </summary>
        /// <returns>The path.</returns>
        private static string GetGenericArgumentsAssemblyPath()
        {
            lock (GenericArgumentsAssemblyLock)
            {
                if (_genericArgumentsAssemblyPath is { } existing && File.Exists(existing))
                {
                    return existing;
                }

                var directory = Path.Combine(Path.GetTempPath(), "datadog-ducktype-aot", Process.GetCurrentProcess().Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, GenericArgumentsAssemblyName + ".dll");
                if (!File.Exists(path))
                {
                    var module = new ModuleDefUser(GenericArgumentsAssemblyName + ".dll", new Guid("6d1f3c2a-8a4e-4c7b-9f61-2b1e6b0a4d10"), AssemblyRefUser.CreateMscorlibReferenceCLR40()) { Kind = ModuleKind.Dll };
                    new AssemblyDefUser(GenericArgumentsAssemblyName, new Version(1, 0, 0, 0)).Modules.Add(module);
                    var objectCtor = new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), module.CorLibTypes.Object.TypeDefOrRef);
                    for (var i = 0; i < MaxGenericProxyArity; i++)
                    {
                        // A public class with a public default constructor: it satisfies the class and new() constraints.
                        var type = new TypeDefUser(GenericArgumentsNamespace, "Arg" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), module.CorLibTypes.Object.TypeDefOrRef)
                        {
                            Attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit
                        };
                        var ctor = new MethodDefUser(
                            ".ctor",
                            MethodSig.CreateInstance(module.CorLibTypes.Void),
                            MethodImplAttributes.IL | MethodImplAttributes.Managed,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                        {
                            Body = new CilBody()
                        };
                        ctor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                        ctor.Body.Instructions.Add(OpCodes.Call.ToInstruction(objectCtor));
                        ctor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                        type.Methods.Add(ctor);
                        module.Types.Add(type);
                    }

                    var temporaryPath = path + "." + Guid.NewGuid().ToString("N");
                    module.Write(temporaryPath);
                    File.Move(temporaryPath, path);
                }

                _genericArgumentsAssemblyPath = path;
                return path;
            }
        }

        /// <summary>
        /// Gets whether a type reference is one of the placeholder classes, and its index (the type parameter it stands for).
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="index">The index.</param>
        /// <returns>true for a placeholder class; otherwise, false.</returns>
        private static bool IsGenericArgumentPlaceholder(ITypeDefOrRef? type, out int index)
        {
            index = -1;
            return type is not null &&
                   string.Equals(type.Namespace, GenericArgumentsNamespace, StringComparison.Ordinal) &&
                   type.Name.String.StartsWith("Arg", StringComparison.Ordinal) &&
                   string.Equals(type.DefinitionAssembly?.Name, GenericArgumentsAssemblyName, StringComparison.Ordinal) &&
                   int.TryParse(type.Name.String.Substring(3), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index);
        }

        /// <summary>
        /// Turns what the emission of a template added into a generic proxy: the proxy type and the activators of the template
        /// become generic over the type parameters of the target (the activators in a factory implementing
        /// <see cref="IDuckTypeAotGenericProxyActivator"/>), and the registration of the template is replaced by the one of the open
        /// generic target type.
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <param name="bootstrapType">The bootstrap type.</param>
        /// <param name="registrationMethod">The registration method of the template.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="mapping">The mapping of the open generic target the template stands for.</param>
        /// <param name="mappingIndex">The index of the mapping.</param>
        /// <param name="templateResult">The emission result of the template.</param>
        /// <param name="typeCountBefore">The number of types of the module before the template was emitted.</param>
        /// <param name="bootstrapMethodCountBefore">The number of methods of the bootstrap type before the template was emitted.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name.</param>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name.</param>
        /// <returns>The emission result of the mapping.</returns>
        private static DuckTypeAotMappingEmissionResult CompleteGenericProxyTemplate(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef registrationMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            DuckTypeAotMappingEmissionResult templateResult,
            int typeCountBefore,
            int bootstrapMethodCountBefore,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName)
        {
            var newTypes = moduleDef.Types.Skip(typeCountBefore).ToList();
            var newMethods = bootstrapType.Methods.Skip(bootstrapMethodCountBefore).ToList();

            // The registration of the template (or of its failure) names the placeholders.
            registrationMethod.Body.Instructions.Clear();
            registrationMethod.Body.ExceptionHandlers.Clear();
            registrationMethod.Body.Variables.Clear();

            if (!string.Equals(templateResult.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal))
            {
                RemoveTemplate();
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    templateResult.Status,
                    templateResult.DiagnosticCode ?? StatusCodeUnsupportedProxyKind,
                    $"Generic proxy of the instantiations of '{mapping.TargetTypeName}': {templateResult.Detail}");
            }

            try
            {
                var arity = DuckTypeAotNameHelpers.GetDeclaredGenericArity(mapping.TargetTypeName);
                var proxyType = newTypes.FirstOrDefault(type => string.Equals(type.FullName, templateResult.GeneratedProxyTypeName, StringComparison.Ordinal))
                             ?? throw new InvalidOperationException($"the generated proxy type '{templateResult.GeneratedProxyTypeName}' wasn't found");
                if (newTypes.Any(type => type.HasNestedTypes))
                {
                    throw new InvalidOperationException("the generated proxy has nested types");
                }

                // The activator receiving the runtime type (the proxy reports it as IDuckType.Type), else the one of the target.
                var entry = newMethods.FirstOrDefault(method => method.Name.StartsWith("ActivateFallbackProxy_", StringComparison.Ordinal))
                         ?? newMethods.FirstOrDefault(method => method.Name.StartsWith("ActivateProxy_", StringComparison.Ordinal))
                         ?? throw new InvalidOperationException("the template has no activator");
                var activators = CollectCalledMethods(entry, newMethods);
                foreach (var method in newMethods)
                {
                    _ = bootstrapType.Methods.Remove(method);
                }

                var factory = new TypeDefUser(GeneratedProxyNamespace, $"DuckTypeGenericProxyFactory_{mappingIndex:D4}", moduleDef.CorLibTypes.Object.TypeDefOrRef)
                {
                    Attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit
                };
                moduleDef.Types.Add(factory);
                foreach (var activator in activators)
                {
                    factory.Methods.Add(activator);
                }

                var genericTypes = new List<TypeDef>(newTypes) { factory };
                foreach (var type in genericTypes)
                {
                    for (var i = 0; i < arity; i++)
                    {
                        // Reference types only: the instantiations NativeAOT can build at runtime, and the instantiations the
                        // template (over classes) was emitted for.
                        type.GenericParameters.Add(new GenericParamUser((ushort)i, GenericParamAttributes.ReferenceTypeConstraint, "T" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }

                new GenericProxyTypeGenerifier(moduleDef, arity, genericTypes).Apply();
                var factoryInstance = GenericProxyTypeGenerifier.Instantiation(factory, arity);
                var proxyInstance = GenericProxyTypeGenerifier.Instantiation(proxyType, arity);

                var importer = RuntimeImporter(moduleDef);
                var objectSig = moduleDef.CorLibTypes.Object;
                factory.Interfaces.Add(new InterfaceImplUser(importer.Import(typeof(IDuckTypeAotGenericProxyActivator))));
                var constructor = new MethodDefUser(
                    ".ctor",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.Void),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                {
                    Body = new CilBody()
                };
                constructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                constructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.ObjectCtor));
                constructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                factory.Methods.Add(constructor);

                const MethodAttributes interfaceMethodAttributes = MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;
                var createInstance = new MethodDefUser(
                    nameof(IDuckTypeAotGenericProxyActivator.CreateInstance),
                    MethodSig.CreateInstance(objectSig, objectSig, importedMembers.SystemTypeSig),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    interfaceMethodAttributes)
                {
                    Body = new CilBody()
                };
                createInstance.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
                if (entry.MethodSig.Params.Count == 2)
                {
                    createInstance.Body.Instructions.Add(OpCodes.Ldarg_2.ToInstruction());
                }

                createInstance.Body.Instructions.Add(OpCodes.Call.ToInstruction(moduleDef.UpdateRowId(new MemberRefUser(moduleDef, entry.Name, entry.MethodSig, factoryInstance))));
                createInstance.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                factory.Methods.Add(createInstance);

                var getProxyType = new MethodDefUser(
                    nameof(IDuckTypeAotGenericProxyActivator.GetProxyType),
                    MethodSig.CreateInstance(importedMembers.SystemTypeSig),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    interfaceMethodAttributes)
                {
                    Body = new CilBody()
                };
                getProxyType.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(proxyInstance));
                getProxyType.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
                getProxyType.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                factory.Methods.Add(getProxyType);

                // typeof(Factory<,>).MakeGenericType(arguments): NativeAOT keeps the canonical code of the factory and of the types
                // it uses, which it instantiates at runtime for reference type arguments.
                var createActivator = new MethodDefUser(
                    $"CreateGenericProxyActivator_{mappingIndex:D4}",
                    MethodSig.CreateStatic(objectSig, new SZArraySig(importedMembers.SystemTypeSig)),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig)
                {
                    Body = new CilBody()
                };
                createActivator.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(factory));
                createActivator.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
                createActivator.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                createActivator.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importer.Import(typeof(Type).GetMethod(nameof(Type.MakeGenericType), [typeof(Type[])])!)));
                createActivator.Body.Instructions.Add(OpCodes.Call.ToInstruction(importer.Import(typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetUninitializedObject), [typeof(Type)])!)));
                createActivator.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
#if NET5_0_OR_GREATER
                AddSuppressMessage(moduleDef, createActivator, "AOT", "IL3050");
                AddSuppressMessage(moduleDef, createActivator, "Trimming", "IL2055");
                AddSuppressMessage(moduleDef, createActivator, "Trimming", "IL2072");
#endif
                bootstrapType.Methods.Add(createActivator);

                if (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule) ||
                    !TryResolveType(proxyModule, mapping.ProxyTypeName, out var proxyDefinition) ||
                    !targetModulesByAssemblyName.TryGetValue(mapping.TargetAssemblyName, out var targetModule) ||
                    !TryResolveType(targetModule, mapping.TargetTypeName, out var targetDefinition))
                {
                    throw new InvalidOperationException("the proxy or the target type wasn't found");
                }

                var instructions = registrationMethod.Body.Instructions;
                instructions.Add(OpCodes.Ldtoken.ToInstruction(ImportTypeDefOrRefCached(moduleDef, proxyDefinition, $"proxy type '{proxyDefinition.FullName}'")));
                instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
                instructions.Add(OpCodes.Ldtoken.ToInstruction(ImportTypeDefOrRefCached(moduleDef, targetDefinition, $"target type '{targetDefinition.FullName}'")));
                instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
                instructions.Add((mapping.IncludesDerivedTypes ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0).ToInstruction());
                instructions.Add(OpCodes.Ldnull.ToInstruction());
                instructions.Add(OpCodes.Ldftn.ToInstruction(createActivator));
                instructions.Add(OpCodes.Newobj.ToInstruction(importer.Import(typeof(Func<Type[], object>).GetConstructor([typeof(object), typeof(IntPtr)])!)));
                instructions.Add(OpCodes.Call.ToInstruction(importer.Import(typeof(DuckType).GetMethod(nameof(DuckType.RegisterAotGenericProxy))!)));

                if (genericTypes.Concat([bootstrapType]).SelectMany(type => type.Methods).FirstOrDefault(ReferencesGenericArgumentPlaceholder) is { } leftover)
                {
                    throw new InvalidOperationException($"'{leftover.FullName}' still references the placeholder types");
                }

                return DuckTypeAotMappingEmissionResult.Compatible(mapping, templateResult.GeneratedProxyAssemblyName ?? string.Empty, proxyType.FullName);
            }
            catch (Exception ex)
            {
                registrationMethod.Body.Instructions.Clear();
                RemoveTemplate();
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnsupportedProxyKind,
                    $"Generic proxy of the instantiations of '{mapping.TargetTypeName}' can't be generated: {ex.Message}.");
            }

            void RemoveTemplate()
            {
                foreach (var type in moduleDef.Types.Skip(typeCountBefore).ToList())
                {
                    _ = moduleDef.Types.Remove(type);
                }

                foreach (var method in newMethods)
                {
                    _ = bootstrapType.Methods.Remove(method);
                }

                foreach (var method in bootstrapType.Methods.Where(method => method.Name.StartsWith("CreateGenericProxyActivator_" + mappingIndex.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)).ToList())
                {
                    _ = bootstrapType.Methods.Remove(method);
                }
            }
        }

        /// <summary>
        /// Gets a method and the methods among the given ones it calls (or loads), transitively.
        /// </summary>
        /// <param name="entry">The method.</param>
        /// <param name="candidates">The methods to look for.</param>
        /// <returns>The methods.</returns>
        private static List<MethodDef> CollectCalledMethods(MethodDef entry, IReadOnlyCollection<MethodDef> candidates)
        {
            var collected = new List<MethodDef> { entry };
            var pending = new Stack<MethodDef>();
            pending.Push(entry);
            while (pending.Count > 0)
            {
                foreach (var instruction in pending.Pop().Body?.Instructions ?? [])
                {
                    if (instruction.Operand is MethodDef called && candidates.Contains(called) && !collected.Contains(called))
                    {
                        collected.Add(called);
                        pending.Push(called);
                    }
                }
            }

            return collected;
        }

        private static bool ReferencesGenericArgumentPlaceholder(MethodDef method)
        {
            if (ContainsPlaceholder(method.MethodSig?.RetType) || method.MethodSig?.Params.Any(ContainsPlaceholder) == true)
            {
                return true;
            }

            if (method.Body is not { } body)
            {
                return false;
            }

            return body.Variables.Any(variable => ContainsPlaceholder(variable.Type)) ||
                   body.Instructions.Any(instruction => instruction.Operand switch
                   {
                       ITypeDefOrRef type => ContainsPlaceholder(type.ToTypeSig()),
                       MemberRef member => ContainsPlaceholder((member.Class as ITypeDefOrRef)?.ToTypeSig()) || ContainsPlaceholder(member.FieldSig?.Type) || ContainsPlaceholder(member.MethodSig?.RetType) || member.MethodSig?.Params.Any(ContainsPlaceholder) == true,
                       MethodSpec spec => spec.GenericInstMethodSig.GenericArguments.Any(ContainsPlaceholder) || ContainsPlaceholder((spec.Method as MemberRef)?.Class is ITypeDefOrRef parent ? parent.ToTypeSig() : null),
                       _ => false,
                   });

            static bool ContainsPlaceholder(TypeSig? sig)
            {
                while (sig is not null)
                {
                    switch (sig)
                    {
                        case TypeDefOrRefSig typeSig:
                            return IsGenericArgumentPlaceholder(typeSig.TypeDefOrRef, out _) || (typeSig.TypeDefOrRef is TypeSpec spec && ContainsPlaceholder(spec.TypeSig));
                        case GenericInstSig instance:
                            return ContainsPlaceholder(instance.GenericType) || instance.GenericArguments.Any(ContainsPlaceholder);
                        default:
                            sig = sig.Next;
                            break;
                    }
                }

                return false;
            }
        }

#if NET5_0_OR_GREATER
        private static void AddSuppressMessage(ModuleDef module, MethodDef method, string category, string checkId)
        {
            var importer = RuntimeImporter(module);
            var constructor = importer.Import(typeof(UnconditionalSuppressMessageAttribute).GetConstructor([typeof(string), typeof(string)])!) as ICustomAttributeType
                           ?? throw new InvalidOperationException("Unable to import UnconditionalSuppressMessageAttribute(string, string).");
            var attribute = new CustomAttribute(constructor);
            attribute.ConstructorArguments.Add(new CAArgument(module.CorLibTypes.String, category));
            attribute.ConstructorArguments.Add(new CAArgument(module.CorLibTypes.String, checkId));
            attribute.NamedArguments.Add(new CANamedArgument(isField: false, module.CorLibTypes.String, nameof(UnconditionalSuppressMessageAttribute.Justification), new CAArgument(module.CorLibTypes.String, "The generic proxies are instantiated for the reference type arguments of runtime types, whose canonical code exists.")));
            method.CustomAttributes.Add(attribute);
        }
#endif

        /// <summary>
        /// Makes types generic over the type parameters the placeholder classes stand for: the placeholders become type
        /// parameters, and the references to the types (and to their members) become references to their instantiation over their
        /// own type parameters, as members of generic types are referenced.
        /// </summary>
        private sealed class GenericProxyTypeGenerifier
        {
            private readonly ModuleDef _module;
            private readonly int _arity;
            private readonly HashSet<TypeDef> _types;
            private readonly Dictionary<TypeDef, TypeSpec> _instantiations = new();
            private readonly Dictionary<int, TypeSpec> _parameters = new();

            internal GenericProxyTypeGenerifier(ModuleDef module, int arity, IEnumerable<TypeDef> types)
            {
                _module = module;
                _arity = arity;
                _types = new HashSet<TypeDef>(types);
            }

            internal static TypeSpec Instantiation(TypeDef type, int arity)
            {
                ClassOrValueTypeSig definition = type.IsValueType ? new ValueTypeSig(type) : new ClassSig(type);
                return new TypeSpecUser(new GenericInstSig(definition, Enumerable.Range(0, arity).Select(i => (TypeSig)new GenericVar(i)).ToList()));
            }

            internal void Apply()
            {
                // The signatures of the definitions first: the references to the members of the types are made from them.
                foreach (var type in _types)
                {
                    if (type.BaseType is { } baseType)
                    {
                        type.BaseType = Type(baseType);
                    }

                    foreach (var implementation in type.Interfaces)
                    {
                        implementation.Interface = Type(implementation.Interface);
                    }

                    foreach (var field in type.Fields)
                    {
                        field.FieldSig = new FieldSig(Sig(field.FieldSig.Type));
                    }

                    foreach (var method in type.Methods)
                    {
                        method.MethodSig = MethodSignature(method.MethodSig);
                        method.Parameters.UpdateParameterTypes();
                    }

                    foreach (var property in type.Properties)
                    {
                        property.PropertySig = new PropertySig(property.PropertySig.HasThis, Sig(property.PropertySig.RetType), property.PropertySig.Params.Select(Sig).ToArray());
                    }
                }

                foreach (var method in _types.SelectMany(type => type.Methods))
                {
                    for (var i = 0; i < method.Overrides.Count; i++)
                    {
                        var methodOverride = method.Overrides[i];
                        method.Overrides[i] = new MethodOverride(methodOverride.MethodBody, (IMethodDefOrRef)Method(methodOverride.MethodDeclaration));
                    }

                    if (method.Body is not { } body)
                    {
                        continue;
                    }

                    foreach (var variable in body.Variables)
                    {
                        variable.Type = Sig(variable.Type);
                    }

                    foreach (var handler in body.ExceptionHandlers)
                    {
                        if (handler.CatchType is { } catchType)
                        {
                            handler.CatchType = Type(catchType);
                        }
                    }

                    foreach (var instruction in body.Instructions)
                    {
                        instruction.Operand = instruction.Operand switch
                        {
                            MemberRef { IsFieldRef: true } field => Field(field),
                            IField field and not MemberRef => Field(field),
                            IMethod called => Method(called),
                            ITypeDefOrRef type => Type(type),
                            MethodSig signature => MethodSignature(signature),
                            var operand => operand,
                        };
                    }
                }
            }

            private TypeSpec Parameter(int index)
            {
                if (index >= _arity)
                {
                    throw new InvalidOperationException($"placeholder {index} is out of the {_arity} type parameters of the target");
                }

                if (!_parameters.TryGetValue(index, out var parameter))
                {
                    parameter = new TypeSpecUser(new GenericVar(index));
                    _parameters[index] = parameter;
                }

                return parameter;
            }

            private TypeSpec Instance(TypeDef type)
            {
                if (!_instantiations.TryGetValue(type, out var instance))
                {
                    instance = Instantiation(type, _arity);
                    _instantiations[type] = instance;
                }

                return instance;
            }

            private ITypeDefOrRef Type(ITypeDefOrRef type)
            {
                if (IsGenericArgumentPlaceholder(type, out var index))
                {
                    return Parameter(index);
                }

                if (type is TypeDef definition && _types.Contains(definition))
                {
                    return Instance(definition);
                }

                if (type is TypeSpec spec)
                {
                    var sig = Sig(spec.TypeSig);
                    return ReferenceEquals(sig, spec.TypeSig) ? spec : new TypeSpecUser(sig);
                }

                return type;
            }

            private TypeSig Sig(TypeSig sig)
            {
                switch (sig)
                {
                    case TypeDefOrRefSig typeSig:
                        if (IsGenericArgumentPlaceholder(typeSig.TypeDefOrRef, out var index))
                        {
                            _ = Parameter(index);
                            return new GenericVar(index);
                        }

                        if (typeSig.TypeDefOrRef is TypeDef definition && _types.Contains(definition))
                        {
                            return Instance(definition).TypeSig;
                        }

                        if (typeSig.TypeDefOrRef is TypeSpec spec)
                        {
                            var specSig = Sig(spec.TypeSig);
                            return ReferenceEquals(specSig, spec.TypeSig) ? sig : specSig;
                        }

                        return sig;
                    case GenericInstSig instance:
                    {
                        var genericType = Sig(instance.GenericType);
                        var arguments = instance.GenericArguments.Select(Sig).ToList();
                        return ReferenceEquals(genericType, instance.GenericType) && arguments.SequenceEqual(instance.GenericArguments, ReferenceEqualityComparer.Instance)
                                   ? sig
                                   : new GenericInstSig((ClassOrValueTypeSig)genericType, arguments);
                    }

                    case SZArraySig array:
                        return Rewrap(array.Next, next => new SZArraySig(next));
                    case ArraySig array:
                        return Rewrap(array.Next, next => new ArraySig(next, array.Rank, array.Sizes, array.LowerBounds));
                    case ByRefSig byRef:
                        return Rewrap(byRef.Next, next => new ByRefSig(next));
                    case PtrSig pointer:
                        return Rewrap(pointer.Next, next => new PtrSig(next));
                    case PinnedSig pinned:
                        return Rewrap(pinned.Next, next => new PinnedSig(next));
                    case CModReqdSig required:
                        return Rewrap(required.Next, next => new CModReqdSig(Type(required.Modifier), next));
                    case CModOptSig optional:
                        return Rewrap(optional.Next, next => new CModOptSig(Type(optional.Modifier), next));
                    case FnPtrSig functionPointer:
                    {
                        var signature = MethodSignature(functionPointer.MethodSig);
                        return ReferenceEquals(signature, functionPointer.MethodSig) ? sig : new FnPtrSig(signature);
                    }

                    default:
                        return sig;
                }

                TypeSig Rewrap(TypeSig next, Func<TypeSig, TypeSig> create)
                {
                    var substituted = Sig(next);
                    return ReferenceEquals(substituted, next) ? sig : create(substituted);
                }
            }

            private MethodSig MethodSignature(MethodSig signature)
            {
                var returnType = Sig(signature.RetType);
                var parameters = signature.Params.Select(Sig).ToList();
                var afterSentinel = signature.ParamsAfterSentinel?.Select(Sig).ToList();
                if (ReferenceEquals(returnType, signature.RetType) &&
                    parameters.SequenceEqual(signature.Params, ReferenceEqualityComparer.Instance) &&
                    (afterSentinel is null || afterSentinel.SequenceEqual(signature.ParamsAfterSentinel!, ReferenceEqualityComparer.Instance)))
                {
                    return signature;
                }

                return new MethodSig(signature.CallingConvention, signature.GenParamCount, returnType, parameters, afterSentinel);
            }

            private IField Field(IField field)
            {
                if (field is FieldDef definition && definition.DeclaringType is { } declaringType && _types.Contains(declaringType))
                {
                    return _module.UpdateRowId(new MemberRefUser(_module, definition.Name, definition.FieldSig, Instance(declaringType)));
                }

                if (field is MemberRef reference)
                {
                    var parent = reference.Class is ITypeDefOrRef parentType ? Type(parentType) : null;
                    var type = Sig(reference.FieldSig.Type);
                    if ((parent is null || ReferenceEquals(parent, reference.Class)) && ReferenceEquals(type, reference.FieldSig.Type))
                    {
                        return reference;
                    }

                    return _module.UpdateRowId(new MemberRefUser(_module, reference.Name, new FieldSig(type), (IMemberRefParent?)parent ?? reference.Class));
                }

                return field;
            }

            private IMethod Method(IMethod method)
            {
                switch (method)
                {
                    case MethodDef definition when definition.DeclaringType is { } declaringType && _types.Contains(declaringType):
                        return _module.UpdateRowId(new MemberRefUser(_module, definition.Name, definition.MethodSig, Instance(declaringType)));
                    case MemberRef reference:
                    {
                        var parent = reference.Class is ITypeDefOrRef parentType ? Type(parentType) : null;
                        var signature = MethodSignature(reference.MethodSig);
                        if ((parent is null || ReferenceEquals(parent, reference.Class)) && ReferenceEquals(signature, reference.MethodSig))
                        {
                            return reference;
                        }

                        return _module.UpdateRowId(new MemberRefUser(_module, reference.Name, signature, (IMemberRefParent?)parent ?? reference.Class));
                    }

                    case MethodSpec specification:
                    {
                        var genericMethod = (IMethodDefOrRef)Method(specification.Method);
                        var arguments = specification.GenericInstMethodSig.GenericArguments.Select(Sig).ToList();
                        if (ReferenceEquals(genericMethod, specification.Method) && arguments.SequenceEqual(specification.GenericInstMethodSig.GenericArguments, ReferenceEqualityComparer.Instance))
                        {
                            return specification;
                        }

                        return _module.UpdateRowId(new MethodSpecUser(genericMethod, new GenericInstMethodSig(arguments)));
                    }

                    default:
                        return method;
                }
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<TypeSig>
        {
            internal static readonly ReferenceEqualityComparer Instance = new();

            public bool Equals(TypeSig? x, TypeSig? y) => ReferenceEquals(x, y);

            public int GetHashCode(TypeSig obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
