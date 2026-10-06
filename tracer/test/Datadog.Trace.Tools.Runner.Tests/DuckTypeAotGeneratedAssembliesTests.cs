// <copyright file="DuckTypeAotGeneratedAssembliesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
#if NETCOREAPP2_1
using AssemblyLoadContext = Datadog.Trace.Tools.Runner.Tests.NetCore21AssemblyLoadContext;
#else
using System.Runtime.Loader;
#endif
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FluentAssertions;
using Xunit;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Generation scenarios with assemblies built for the test: contracts that declare their own duck typing attributes (which the
/// generator's dynamic duck typing can't read), and targets that reference an assembly the generator can't load.
/// </summary>
[Collection(nameof(DuckTypeAotProcessorConsoleCollection))]
public class DuckTypeAotGeneratedAssembliesTests
{
    private const string ContractsAssemblyName = "StandaloneDuckContracts";
    private const string ContractsNamespace = "StandaloneDuckContracts";

    public interface IHandlerProxy
    {
        string Handle(object value);
    }

    [Fact]
    public void GeneratedRegistryShouldRegisterTheFailureOfStandaloneContractsThatCantBeLoaded()
    {
        // The generator can't ask dynamic duck typing about a contract with its own [DuckIgnore]: the abstract member it ignores
        // is detected from metadata, and registered as a failure instead of a type that would make the registry fail to load.
        WithStandaloneContracts((contractsPath, directory) =>
        {
            var registryPath = Generate(
                directory,
                contractsPath,
                Mapping("IIgnoredMemberProxy", "NamedTarget"),
                Mapping("INamedProxy", "NamedTarget"));
            var matrix = ReadMatrix(registryPath);
            var ignoredMemberMapping = matrix.Mappings.Single(entry => entry.ProxyType!.EndsWith("IIgnoredMemberProxy", StringComparison.Ordinal));
            ignoredMemberMapping.DiagnosticCode.Should().Be("DTAOT0215");
            ignoredMemberMapping.DynamicFailureReplayed.Should().BeFalse("the generator's dynamic duck typing doesn't read this contract's attributes");
            ignoredMemberMapping.CheckedAgainstMetadataOnly.Should().BeTrue();

            WithRegistry(registryPath, contractsPath, contracts =>
            {
                var target = Activator.CreateInstance(contracts.GetType($"{ContractsNamespace}.NamedTarget")!)!;
                var namedProxyType = contracts.GetType($"{ContractsNamespace}.INamedProxy")!;
                namedProxyType.GetProperty("Name")!.GetValue(DuckType.Create(namedProxyType, target)).Should().Be("standalone");

                var create = () => DuckType.Create(contracts.GetType($"{ContractsNamespace}.IIgnoredMemberProxy")!, target);
                create.Should().Throw<TargetInvocationException>().WithInnerException<DuckTypeException>();
            });
        });
    }

    [Fact]
    public void GeneratedRegistryShouldRegisterTheFailureOfGeneratedProxyTypesTheRuntimeCantLoad()
    {
        // Neither dynamic duck typing (standalone contract) nor the metadata rules tell this proxy type can't be created (the
        // ToString it copies from the target overrides the sealed ToString of the proxy class): the generator loads the
        // registry, finds the type it can't load, and registers that failure instead.
        WithStandaloneContracts((contractsPath, directory) =>
        {
            var registryPath = Generate(
                directory,
                contractsPath,
                Mapping("SealedToStringProxy", "ToStringTarget"),
                Mapping("INamedProxy", "NamedTarget"));
            var sealedToStringMapping = ReadMatrix(registryPath).Mappings.Single(entry => entry.ProxyType!.EndsWith("SealedToStringProxy", StringComparison.Ordinal));
            sealedToStringMapping.DiagnosticCode.Should().Be("DTAOT0215");
            sealedToStringMapping.CheckedAgainstMetadataOnly.Should().BeTrue();

            WithRegistry(registryPath, contractsPath, contracts =>
            {
                var target = Activator.CreateInstance(contracts.GetType($"{ContractsNamespace}.NamedTarget")!)!;
                DuckType.GetOrCreateProxyType(contracts.GetType($"{ContractsNamespace}.INamedProxy")!, target.GetType()).CanCreate().Should().BeTrue();
                var toStringTarget = contracts.GetType($"{ContractsNamespace}.ToStringTarget")!;
                DuckType.GetOrCreateProxyType(contracts.GetType($"{ContractsNamespace}.SealedToStringProxy")!, toStringTarget).CanCreate().Should().BeFalse();
            });
        });
    }

#if NET7_0_OR_GREATER
    [Fact]
    public void GeneratedRegistryShouldRegisterTheFailureOfStandaloneContractsWithStaticAbstractMembers()
    {
        // The generator can't ask dynamic duck typing about a standalone contract: the static abstract member the proxy can't
        // implement is detected from metadata, and registered as a failure instead of a type that would make the registry fail
        // to load.
        WithStandaloneContracts((contractsPath, directory) =>
        {
            var registryPath = Generate(
                directory,
                contractsPath,
                Mapping("IStaticMemberProxy", "NamedTarget"),
                Mapping("INamedProxy", "NamedTarget"));
            var staticMemberMapping = ReadMatrix(registryPath).Mappings.Single(entry => entry.ProxyType!.EndsWith("IStaticMemberProxy", StringComparison.Ordinal));
            staticMemberMapping.DiagnosticCode.Should().Be("DTAOT0215");
            staticMemberMapping.CheckedAgainstMetadataOnly.Should().BeTrue();

            WithRegistry(registryPath, contractsPath, contracts =>
            {
                var target = Activator.CreateInstance(contracts.GetType($"{ContractsNamespace}.NamedTarget")!)!;
                DuckType.GetOrCreateProxyType(contracts.GetType($"{ContractsNamespace}.INamedProxy")!, target.GetType()).CanCreate().Should().BeTrue();
                DuckType.GetOrCreateProxyType(contracts.GetType($"{ContractsNamespace}.IStaticMemberProxy")!, target.GetType()).CanCreate().Should().BeFalse();
            });
        });
    }
#endif

    [Fact]
    public void GeneratedRegistryShouldBindStandaloneContractsFromMetadataWhateverTheirMemberOrder()
    {
        // The duck attributes of a standalone contract are only read from metadata: the whole mapping is, even when the first
        // member bound comes from an interface without them.
        WithStandaloneContracts((contractsPath, directory) =>
        {
            var registryPath = Generate(directory, contractsPath, Mapping("ICompositeNamedProxy", "NamedTarget"));
            var mapping = ReadMatrix(registryPath).Mappings.Should().ContainSingle().Subject;
            mapping.Status.Should().Be(DuckTypeAotCompatibilityStatuses.Compatible);
            mapping.CheckedAgainstMetadataOnly.Should().BeTrue();

            WithRegistry(registryPath, contractsPath, contracts =>
            {
                var target = Activator.CreateInstance(contracts.GetType($"{ContractsNamespace}.NamedTarget")!)!;
                var proxy = DuckType.Create(contracts.GetType($"{ContractsNamespace}.ICompositeNamedProxy")!, target);
                contracts.GetType($"{ContractsNamespace}.IRenamedProxy")!.GetProperty("Alias")!.GetValue(proxy).Should().Be("standalone");
            });
        });
    }

    [Theory]
    // A class proxy overriding the abstract property of its base type, with an abstract override or an override: the metadata
    // rules see the property is implemented, and the proxy type is generated like dynamic duck typing creates it.
    [InlineData("ReabstractedNameProxy")]
    [InlineData("OverriddenNameProxy")]
    public void GeneratedRegistryShouldCreateStandaloneProxiesOverridingAbstractMembers(string proxyTypeName)
    {
        WithStandaloneContracts((contractsPath, directory) =>
        {
            var registryPath = Generate(directory, contractsPath, Mapping(proxyTypeName, "NamedTarget"));
            var mapping = ReadMatrix(registryPath).Mappings.Should().ContainSingle().Subject;
            mapping.Status.Should().Be(DuckTypeAotCompatibilityStatuses.Compatible);
            mapping.CheckedAgainstMetadataOnly.Should().BeTrue();

            WithRegistry(registryPath, contractsPath, contracts =>
            {
                var proxyType = contracts.GetType($"{ContractsNamespace}.{proxyTypeName}")!;
                var proxy = DuckType.Create(proxyType, Activator.CreateInstance(contracts.GetType($"{ContractsNamespace}.NamedTarget")!)!);
                proxyType.GetProperty("Name")!.GetValue(proxy).Should().Be("standalone");
            });
        });
    }

    [Fact]
    public void GeneratedRegistryShouldReportStandaloneMappingsThatFailToBindAsCheckedAgainstMetadataOnly()
    {
        // The target has no member for the renamed property: binding fails from metadata, before anything else is evaluated.
        WithStandaloneContracts((contractsPath, directory) =>
        {
            var mapping = ReadMatrix(Generate(directory, contractsPath, Mapping("IRenamedProxy", "Inner"))).Mappings.Should().ContainSingle().Subject;
            mapping.Status.Should().NotBe(DuckTypeAotCompatibilityStatuses.Compatible);
            mapping.CheckedAgainstMetadataOnly.Should().BeTrue();
        });
    }

    [Fact]
    public void GeneratedRegistryShouldNotReplayDynamicFailuresForStandaloneDuckCopyChaining()
    {
        // The generator's dynamic duck typing doesn't see a contract's own [DuckCopy]: it can't decide how the member is duck
        // chained, so whatever the outcome, the registry doesn't claim to replay a dynamic duck typing failure.
        WithStandaloneContracts((contractsPath, directory) =>
        {
            var registryPath = Generate(directory, contractsPath, Mapping("IHolderProxy", "Holder"));
            ReadMatrix(registryPath).Mappings.Should().ContainSingle().Which.Should().Match<DuckTypeAotCompatibilityMapping>(
                entry => entry.Status == DuckTypeAotCompatibilityStatuses.Compatible || !entry.DynamicFailureReplayed);
        });
    }

    [Fact]
    public void GeneratedRegistryShouldNotReplayFailuresOfTheGeneratorEnvironment()
    {
        // The target references an assembly the generator doesn't have: dynamic duck typing fails in the generator because of
        // the generator process, not like it does in the application, so its failure isn't replayed (and the gates report it).
        var directory = CreateTemporaryDirectory();
        try
        {
            var targetPath = WriteTargetReferencingMissingAssembly(directory);
            var registryPath = Generate(directory, targetPath, new DuckTypeAotMapping(
                typeof(IHandlerProxy).FullName!,
                typeof(IHandlerProxy).Assembly.GetName().Name!,
                "MissingDependencyTargets.Handler",
                "MissingDependencyTargets",
                DuckTypeAotMappingMode.Forward,
                DuckTypeAotMappingSource.MapFile));
            var mapping = ReadMatrix(registryPath).Mappings.Should().ContainSingle().Subject;
            mapping.DynamicFailureReplayed.Should().BeFalse();
            mapping.CheckedAgainstMetadataOnly.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DuckTypeAotMapping Mapping(string proxyTypeName, string targetTypeName)
        => new(
            $"{ContractsNamespace}.{proxyTypeName}",
            ContractsAssemblyName,
            $"{ContractsNamespace}.{targetTypeName}",
            ContractsAssemblyName,
            DuckTypeAotMappingMode.Forward,
            DuckTypeAotMappingSource.MapFile);

    private static void WithStandaloneContracts(Action<string, string> test)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            test(WriteStandaloneContracts(directory), directory);
        }
        finally
        {
            DuckType.ResetRuntimeModeForTests();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Generate(string directory, string extraAssemblyPath, params DuckTypeAotMapping[] mappings)
    {
        var mapPath = Path.Combine(directory, "map.json");
        File.WriteAllText(mapPath, JsonConvert.SerializeObject(new
        {
            mappings = mappings.Select(mapping => new
            {
                mode = mapping.Mode.ToString().ToLowerInvariant(),
                proxyType = mapping.ProxyTypeName,
                proxyAssembly = mapping.ProxyAssemblyName,
                targetType = mapping.TargetTypeName,
                targetAssembly = mapping.TargetAssemblyName
            })
        }));
        var outputPath = Path.Combine(directory, "Registry.dll");
        var testAssemblyPath = typeof(DuckTypeAotGeneratedAssembliesTests).Assembly.Location;
        DuckTypeAotGenerateProcessor.Process(new DuckTypeAotGenerateOptions(
            proxyAssemblies: [extraAssemblyPath, testAssemblyPath],
            targetAssemblies: [extraAssemblyPath, typeof(object).Assembly.Location],
            targetFolders: [],
            targetFilters: ["*.dll"],
            mapFile: mapPath,
            genericInstantiationsFile: null,
            outputPath: outputPath,
            assemblyName: "Registry",
            trimmerDescriptorPath: outputPath + ".linker.xml",
            propsPath: outputPath + ".props")).Should().Be(0);
        return outputPath;
    }

    private static DuckTypeAotCompatibilityMatrix ReadMatrix(string registryPath)
        => JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(registryPath + ".compat.json"))!;

    private static void WithRegistry(string registryPath, string contractsPath, Action<Assembly> assertions)
    {
        var loadContext = new AssemblyLoadContext("DuckTypeAotGeneratedAssembliesTests", isCollectible: true);
#if NETCOREAPP2_1
        var contracts = Assembly.Load(File.ReadAllBytes(contractsPath));
        ResolveEventHandler resolveContracts = (_, args) => new AssemblyName(args.Name).Name == ContractsAssemblyName ? contracts : null;
        AppDomain.CurrentDomain.AssemblyResolve += resolveContracts;
#endif
        try
        {
#if NETCOREAPP2_1
            var registry = Assembly.Load(File.ReadAllBytes(registryPath));
#else
            var contracts = loadContext.LoadFromAssemblyPath(contractsPath);
            using var registryStream = File.OpenRead(registryPath);
            var registry = loadContext.LoadFromStream(registryStream);
#endif
            DuckType.ResetRuntimeModeForTests();
            registry.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap")!
                    .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
            assertions(contracts);
        }
        finally
        {
#if NETCOREAPP2_1
            AppDomain.CurrentDomain.AssemblyResolve -= resolveContracts;
#endif
            DuckType.ResetRuntimeModeForTests();
            loadContext.Unload();
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dd-trace-ducktype-aot-generated-assemblies", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Writes contracts that declare their own Datadog.Trace.DuckTyping attributes, like standalone AOT contracts can.
    /// </summary>
    private static string WriteStandaloneContracts(string directory)
    {
        var module = CreateModule(ContractsAssemblyName);
        var stringSig = module.CorLibTypes.String;
        var duckIgnoreConstructor = AddAttribute(module, "DuckIgnoreAttribute");
        var duckCopyConstructor = AddAttribute(module, "DuckCopyAttribute");

        // public class NamedTarget { public string Name => "standalone"; }
        var namedTarget = AddClass(module, "NamedTarget");
        AddConstantProperty(module, namedTarget, "Name", "standalone");

        // public interface INamedProxy { string Name { get; } }
        var namedProxy = AddInterface(module, "INamedProxy");
        AddAbstractProperty(namedProxy, "Name", stringSig);

        // public interface IPlainNamedProxy { string Name { get; } }, public interface IRenamedProxy { [Duck(Name = "Name")] string Alias { get; } }
        // and public interface ICompositeNamedProxy : IPlainNamedProxy, IRenamedProxy { }
        var duckConstructor = AddAttribute(module, "DuckAttribute");
        duckConstructor.DeclaringType.Fields.Add(new FieldDefUser("Name", new FieldSig(stringSig), FieldAttributes.Public));
        var plainNamedProxy = AddInterface(module, "IPlainNamedProxy");
        AddAbstractProperty(plainNamedProxy, "Name", stringSig);
        var renamedProxy = AddInterface(module, "IRenamedProxy");
        AddAbstractProperty(renamedProxy, "Alias", stringSig).CustomAttributes.Add(
            new CustomAttribute(duckConstructor, Array.Empty<CAArgument>(), new[] { new CANamedArgument(isField: true, stringSig, "Name", new CAArgument(stringSig, "Name")) }));
        var compositeNamedProxy = AddInterface(module, "ICompositeNamedProxy");
        compositeNamedProxy.Interfaces.Add(new InterfaceImplUser(plainNamedProxy));
        compositeNamedProxy.Interfaces.Add(new InterfaceImplUser(renamedProxy));

        // public interface IIgnoredMemberProxy { string Name { get; } [DuckIgnore] string Ignored { get; } }
        var ignoredMemberProxy = AddInterface(module, "IIgnoredMemberProxy");
        AddAbstractProperty(ignoredMemberProxy, "Name", stringSig);
        AddAbstractProperty(ignoredMemberProxy, "Ignored", stringSig).CustomAttributes.Add(new CustomAttribute(duckIgnoreConstructor));

        // [DuckIgnore] public interface IStaticMemberProxy { string Name { get; } static abstract string Make(); }: the
        // attribute on the type only makes the contract one the generator's dynamic duck typing can't read.
        var staticMemberProxy = AddInterface(module, "IStaticMemberProxy");
        staticMemberProxy.CustomAttributes.Add(new CustomAttribute(duckIgnoreConstructor));
        AddAbstractProperty(staticMemberProxy, "Name", stringSig);
        staticMemberProxy.Methods.Add(new MethodDefUser(
            "Make",
            MethodSig.CreateStatic(stringSig),
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig));

        // [DuckIgnore] public abstract class SealedToStringProxy { public abstract string Name { get; } public sealed override string ToString() => "proxy"; }
        var sealedToStringProxy = new TypeDefUser(ContractsNamespace, "SealedToStringProxy", module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit
        };
        module.Types.Add(sealedToStringProxy);
        sealedToStringProxy.CustomAttributes.Add(new CustomAttribute(duckIgnoreConstructor));
        AddDefaultConstructor(module, sealedToStringProxy, module.CorLibTypes.Object.TypeDefOrRef);
        AddAbstractProperty(sealedToStringProxy, "Name", stringSig);
        AddToString(module, sealedToStringProxy, "proxy", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig);

        // [DuckIgnore] public abstract class AbstractNameBase { public abstract string Name { get; } }, and the proxies
        // public abstract class ReabstractedNameProxy : AbstractNameBase { public abstract override string Name { get; } } and
        // public abstract class OverriddenNameProxy : AbstractNameBase { public override string Name => "overridden"; }: the
        // attribute on the base type only makes the contracts ones the generator's dynamic duck typing can't read.
        var abstractNameBase = AddAbstractClass(module, "AbstractNameBase", module.CorLibTypes.Object.TypeDefOrRef);
        abstractNameBase.CustomAttributes.Add(new CustomAttribute(duckIgnoreConstructor));
        AddAbstractProperty(abstractNameBase, "Name", stringSig);
        var reabstractedNameProxy = AddAbstractClass(module, "ReabstractedNameProxy", abstractNameBase);
        var reabstractedGetter = new MethodDefUser(
            "get_Name",
            MethodSig.CreateInstance(stringSig),
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName);
        reabstractedNameProxy.Methods.Add(reabstractedGetter);
        reabstractedNameProxy.Properties.Add(new PropertyDefUser("Name", PropertySig.CreateInstance(stringSig)) { GetMethod = reabstractedGetter });
        var overriddenNameProxy = AddAbstractClass(module, "OverriddenNameProxy", abstractNameBase);
        AddConstantProperty(module, overriddenNameProxy, "Name", "overridden", MethodAttributes.Virtual);

        // public class ToStringTarget { public string Name => "standalone"; public override string ToString() => "target"; }
        var toStringTarget = AddClass(module, "ToStringTarget");
        AddConstantProperty(module, toStringTarget, "Name", "standalone");
        AddToString(module, toStringTarget, "target", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig);

        // [DuckCopy] public struct ValueCopy { public string Value; }
        var valueCopy = new TypeDefUser(ContractsNamespace, "ValueCopy", module.CorLibTypes.GetTypeRef("System", "ValueType"))
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit
        };
        valueCopy.CustomAttributes.Add(new CustomAttribute(duckCopyConstructor));
        valueCopy.Fields.Add(new FieldDefUser("Value", new FieldSig(stringSig), FieldAttributes.Public));
        module.Types.Add(valueCopy);

        // public interface IHolderProxy { ValueCopy Inner { get; } }
        var holderProxy = AddInterface(module, "IHolderProxy");
        AddAbstractProperty(holderProxy, "Inner", valueCopy.ToTypeSig());

        // public class Inner { public string Value => "inner"; }
        var inner = AddClass(module, "Inner");
        AddConstantProperty(module, inner, "Value", "inner");

        // public class Holder { public Inner Inner => new Inner(); }
        var holder = AddClass(module, "Holder");
        var getInner = new MethodDefUser("get_Inner", MethodSig.CreateInstance(inner.ToTypeSig()), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName)
        {
            Body = new CilBody()
        };
        getInner.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(inner.FindDefaultConstructor()));
        getInner.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        holder.Methods.Add(getInner);
        holder.Properties.Add(new PropertyDefUser("Inner", PropertySig.CreateInstance(inner.ToTypeSig())) { GetMethod = getInner });

        var path = Path.Combine(directory, ContractsAssemblyName + ".dll");
        module.Write(path);
        return path;
    }

    /// <summary>
    /// Writes a target whose method takes a type of an assembly that isn't written.
    /// </summary>
    private static string WriteTargetReferencingMissingAssembly(string directory)
    {
        var module = CreateModule("MissingDependencyTargets");
        var missingAssembly = new AssemblyRefUser("MissingDependency", new Version(1, 0, 0, 0));
        var missingType = new TypeRefUser(module, "MissingDependency", "MissingType", missingAssembly);

        // public class Handler { public string Handle(MissingDependency.MissingType value) => "handled"; }
        var handler = new TypeDefUser("MissingDependencyTargets", "Handler", module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit
        };
        module.Types.Add(handler);
        AddDefaultConstructor(module, handler, module.CorLibTypes.Object.TypeDefOrRef);
        var handle = new MethodDefUser("Handle", MethodSig.CreateInstance(module.CorLibTypes.String, new ClassSig(missingType)), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig)
        {
            Body = new CilBody()
        };
        handle.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("handled"));
        handle.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        handler.Methods.Add(handle);

        var path = Path.Combine(directory, "MissingDependencyTargets.dll");
        module.Write(path);
        return path;
    }

    private static ModuleDefUser CreateModule(string assemblyName)
    {
        var module = new ModuleDefUser(assemblyName + ".dll", Guid.NewGuid(), new AssemblyRefUser(typeof(object).Assembly.GetName())) { Kind = ModuleKind.Dll };
        new AssemblyDefUser(assemblyName, new Version(1, 0, 0, 0)).Modules.Add(module);
        return module;
    }

    private static MethodDef AddAttribute(ModuleDef module, string name)
    {
        var attributeBase = module.CorLibTypes.GetTypeRef("System", "Attribute");
        var attribute = new TypeDefUser("Datadog.Trace.DuckTyping", name, attributeBase)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class | TypeAttributes.BeforeFieldInit
        };
        module.Types.Add(attribute);
        return AddDefaultConstructor(module, attribute, attributeBase);
    }

    private static TypeDef AddClass(ModuleDef module, string name)
    {
        var type = new TypeDefUser(ContractsNamespace, name, module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit
        };
        module.Types.Add(type);
        AddDefaultConstructor(module, type, module.CorLibTypes.Object.TypeDefOrRef);
        return type;
    }

    private static TypeDef AddAbstractClass(ModuleDef module, string name, ITypeDefOrRef baseType)
    {
        var type = new TypeDefUser(ContractsNamespace, name, baseType)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit
        };
        module.Types.Add(type);
        AddDefaultConstructor(module, type, baseType);
        return type;
    }

    private static TypeDef AddInterface(ModuleDef module, string name)
    {
        var type = new TypeDefUser(ContractsNamespace, name)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract
        };
        module.Types.Add(type);
        return type;
    }

    private static MethodDef AddDefaultConstructor(ModuleDef module, TypeDef type, ITypeDefOrRef baseType)
    {
        var constructor = new MethodDefUser(
            ".ctor",
            MethodSig.CreateInstance(module.CorLibTypes.Void),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
        {
            Body = new CilBody()
        };
        constructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
        constructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseType is TypeDef baseTypeDefinition
                                                                         ? baseTypeDefinition.FindDefaultConstructor()
                                                                         : new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), baseType)));
        constructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(constructor);
        return constructor;
    }

    private static PropertyDef AddAbstractProperty(TypeDef type, string name, TypeSig propertyType)
    {
        var getter = new MethodDefUser(
            "get_" + name,
            MethodSig.CreateInstance(propertyType),
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
        type.Methods.Add(getter);
        var property = new PropertyDefUser(name, PropertySig.CreateInstance(propertyType)) { GetMethod = getter };
        type.Properties.Add(property);
        return property;
    }

    private static void AddToString(ModuleDef module, TypeDef type, string value, MethodAttributes attributes)
    {
        var toString = new MethodDefUser("ToString", MethodSig.CreateInstance(module.CorLibTypes.String), MethodImplAttributes.IL | MethodImplAttributes.Managed, attributes)
        {
            Body = new CilBody()
        };
        toString.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(value));
        toString.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(toString);
    }

    private static void AddConstantProperty(ModuleDef module, TypeDef type, string name, string value, MethodAttributes extraAttributes = 0)
    {
        var getter = new MethodDefUser(
            "get_" + name,
            MethodSig.CreateInstance(module.CorLibTypes.String),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | extraAttributes)
        {
            Body = new CilBody()
        };
        getter.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(value));
        getter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(getter);
        type.Properties.Add(new PropertyDefUser(name, PropertySig.CreateInstance(module.CorLibTypes.String)) { GetMethod = getter });
    }
}
