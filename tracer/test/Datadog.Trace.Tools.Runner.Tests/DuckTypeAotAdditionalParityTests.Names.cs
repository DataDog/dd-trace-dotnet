// <copyright file="DuckTypeAotAdditionalParityTests.Names.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Type names of maps and generic instantiation roots (unqualified or mixed generic arguments, array suffixes, escaped
/// commas), and the other runtime types of a mapped target the generator registers (generic roots, derived types whose
/// dependencies the generator doesn't have).
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    private static readonly Lazy<string> NamesGenericTargetsPath = new(WriteNamesGenericTargets);

    [Theory]
    [InlineData("unqualified-corelib-argument")]
    [InlineData("qualified-corelib-argument")]
    [InlineData("unqualified-root-assembly-argument")]
    public void GeneratedRegistryShouldRegisterGenericRootsWhateverTheSpellingOfTheirArguments(string spelling)
    {
        var rootTypeName = spelling switch
        {
            "unqualified-corelib-argument" => typeof(NamesDerived<>).FullName + "[System.Int32]",
            "qualified-corelib-argument" => typeof(NamesDerived<>).FullName + "[[System.Int32, System.Private.CoreLib]]",
            _ => typeof(NamesDerived<>).FullName + "[" + typeof(NamesItem).FullName + "]",
        };
        Func<object?> exercise = spelling == "unqualified-root-assembly-argument"
                                     ? () => DuckType.Create<INamesNameProxy>(new NamesDerived<NamesItem>())!.Name
                                     : () => DuckType.Create<INamesNameProxy>(new NamesDerived<int>())!.Name;
        AssertSameOutcomeWithInputs(
            exercise,
            [TestAssemblyPath],
            JsonConvert.SerializeObject(new[] { new { type = rootTypeName, assembly = typeof(NamesDerived<>).Assembly.GetName().Name } }),
            Mapping(typeof(INamesNameProxy), typeof(NamesBase)));
    }

    [Theory]
    // An array of a closed generic type with unqualified (or mixed) generic arguments, and as a generic argument.
    [InlineData("array-mixed-unqualified-arguments")]
    [InlineData("array-qualified-arguments")]
    [InlineData("array-unqualified-corelib-arguments")]
    [InlineData("array-as-generic-argument")]
    // A compiler-generated name with an escaped comma (an iterator of an explicitly implemented generic interface method), as
    // the target and as a generic argument.
    [InlineData("escaped-comma")]
    [InlineData("escaped-comma-generic-argument")]
    public void GeneratedRegistryShouldResolveMappedTypeNamesLikeTheRuntime(string scenario)
    {
        var item = typeof(NamesItem).FullName;
        var iteratorType = ((IEnumerable<KeyValuePair<string, string>>)new NamesPairSource()).GetEnumerator().GetType();
        var (exercise, mapping) = scenario switch
        {
            "array-mixed-unqualified-arguments" => (
                (Func<object?>)(() => DescribeArrayProxy(new NamesBox2<NamesItem, int>[3])),
                NamesMapping(typeof(INamesLengthProxy), typeof(NamesBox2<,>).FullName + "[" + item + ",System.Int32][]")),
            "array-qualified-arguments" => (
                () => DescribeArrayProxy(new NamesBox2<NamesItem, int>[3]),
                NamesMapping(typeof(INamesLengthProxy), typeof(NamesBox2<,>).FullName + "[[" + item + ", " + typeof(NamesItem).Assembly.GetName().Name + "],[System.Int32, System.Private.CoreLib]][]")),
            "array-unqualified-corelib-arguments" => (
                () => DescribeArrayProxy(new NamesBox2<string, int>[3]),
                NamesMapping(typeof(INamesLengthProxy), typeof(NamesBox2<,>).FullName + "[System.String,System.Int32][]")),
            "array-as-generic-argument" => (
                () => DuckType.Create<INamesNameProxy>(new NamesHolder<NamesBox2<NamesItem, int>[]>())!.Name,
                NamesMapping(typeof(INamesNameProxy), typeof(NamesHolder<>).FullName + "[" + typeof(NamesBox2<,>).FullName + "[" + item + ",System.Int32][]]")),
            "escaped-comma" => (
                () =>
                {
                    var enumerator = ((IEnumerable<KeyValuePair<string, string>>)new NamesPairSource()).GetEnumerator();
                    return DuckType.Create<INamesMoveNextProxy>(enumerator)!.MoveNext() + "|" + ((IDuckType)DuckType.Create<INamesMoveNextProxy>(enumerator)!).Type.Name;
                },
                Mapping(typeof(INamesMoveNextProxy), iteratorType)),
            _ => (
                () => DuckType.Create<INamesNameProxy>(Activator.CreateInstance(typeof(NamesHolder<>).MakeGenericType(iteratorType))!)!.Name,
                Mapping(typeof(INamesNameProxy), typeof(NamesHolder<>).MakeGenericType(iteratorType))),
        };

        AssertSameOutcomeWithInputs(exercise, [TestAssemblyPath], genericInstantiationsJson: null, mapping);

        static string DescribeArrayProxy(Array array)
            => DuckType.Create<INamesLengthProxy>(array)!.Length + "|" + ((IDuckType)DuckType.Create<INamesLengthProxy>(array)!).Type;

        static DuckTypeAotMapping NamesMapping(Type proxy, string targetTypeName)
            => new(proxy.FullName!, proxy.Assembly.GetName().Name!, targetTypeName, typeof(NamesItem).Assembly.GetName().Name!, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile);
    }

    [Theory]
    // A backtick that isn't a generic arity, or a '!', in the name of a target: it isn't an open generic type.
    [InlineData("A`B")]
    [InlineData("A`1x")]
    [InlineData("A!B")]
    public void GeneratedRegistryShouldMapTargetsWhoseNamesLookGeneric(string name)
    {
        var module = CreateNamesModule("NamesLookGeneric" + Guid.NewGuid().ToString("N"));
        AddNamesClass(module, "NamesLookGeneric", name, module.CorLibTypes.Object.TypeDefOrRef, "named:" + name);
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, module.Assembly.Name + ".dll");
        module.Write(path);
        var target = Assembly.LoadFrom(path).GetType("NamesLookGeneric." + name, throwOnError: true)!;
        AssertSameOutcomeWithInputs(
            () => DuckType.Create<INamesNameProxy>(Activator.CreateInstance(target)!)!.Name,
            [TestAssemblyPath, path],
            genericInstantiationsJson: null,
            new DuckTypeAotMapping(typeof(INamesNameProxy).FullName!, typeof(INamesNameProxy).Assembly.GetName().Name!, target.FullName!, module.Assembly.Name!, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile));
    }

    [Theory]
    [InlineData("unqualified")]
    [InlineData("qualified")]
    public void GeneratedRegistryShouldExpandOpenGenericMappingsFromRootsWithUnqualifiedArguments(string spelling)
    {
        // The argument of the root is a type of the target assembly: the expanded proxy type (of another assembly) has it too.
        var targetsPath = NamesGenericTargetsPath.Value;
        var targets = Assembly.LoadFrom(targetsPath);
        var targetsName = targets.GetName().Name!;
        var item = targets.GetType("NamesGenericTargets.Item", throwOnError: true)!;
        var closedTarget = targets.GetType("NamesGenericTargets.GenericTarget`1", throwOnError: true)!.MakeGenericType(item);
        var closedProxy = typeof(INamesGenericProxy<>).MakeGenericType(item);
        var root = spelling == "unqualified" ? "NamesGenericTargets.GenericTarget`1[NamesGenericTargets.Item]" : $"NamesGenericTargets.GenericTarget`1[[NamesGenericTargets.Item, {targetsName}]]";
        AssertSameOutcomeWithInputs(
            () => closedProxy.GetProperty("Name")!.GetValue(DuckType.Create(closedProxy, Activator.CreateInstance(closedTarget)!)),
            [TestAssemblyPath, targetsPath],
            JsonConvert.SerializeObject(new[] { new { type = root, assembly = targetsName } }),
            new DuckTypeAotMapping(typeof(INamesGenericProxy<>).FullName!, typeof(INamesGenericProxy<>).Assembly.GetName().Name!, "NamesGenericTargets.GenericTarget`1", targetsName, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile));
    }

    [Fact]
    public void GeneratedRegistryShouldRegisterDerivedTypesWhoseDependenciesTheGeneratorDoesntHave()
    {
        // Derived : Target implements an interface of an assembly the generator doesn't get (the application has it): the
        // registry still registers the proxy of Derived.
        var suffix = Guid.NewGuid().ToString("N");
        var (targetsPath, dependencyPath) = WriteNamesDependencyAssemblies(suffix);
        var targets = Assembly.LoadFrom(targetsPath);
        ResolveEventHandler resolveDependency = (_, args) => args.Name is { } name && new AssemblyName(name).Name == "NamesDependency" + suffix ? Assembly.LoadFrom(dependencyPath) : null;
        try
        {
            AssertSameOutcomeWithInputs(
                () => DuckType.Create<INamesNameProxy>(Activator.CreateInstance(targets.GetType("NamesDependencyTargets.Derived", throwOnError: true)!)!)!.Name,
                [TestAssemblyPath, targetsPath],
                genericInstantiationsJson: null,
                beforeAot: () => AppDomain.CurrentDomain.AssemblyResolve += resolveDependency,
                new DuckTypeAotMapping(typeof(INamesNameProxy).FullName!, typeof(INamesNameProxy).Assembly.GetName().Name!, "NamesDependencyTargets.Target", "NamesDependencyTargets" + suffix, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile));
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolveDependency;
        }
    }

    private static void AssertSameOutcomeWithInputs(Func<object?> exercise, IReadOnlyList<string> targetAssemblies, string? genericInstantiationsJson, params DuckTypeAotMapping[] mappings)
        => AssertSameOutcomeWithInputs(exercise, targetAssemblies, genericInstantiationsJson, beforeAot: null, mappings);

    /// <summary>
    /// Runs a scenario in dynamic mode, then with a registry generated from the inputs, and compares the outcomes. When the
    /// application has something the generator doesn't (<paramref name="beforeAot"/> provides it after the generation), dynamic
    /// duck typing runs last, as it would load it in the generator's process first.
    /// </summary>
    private static void AssertSameOutcomeWithInputs(Func<object?> exercise, IReadOnlyList<string> targetAssemblies, string? genericInstantiationsJson, Action? beforeAot, params DuckTypeAotMapping[] mappings)
    {
        DuckType.ResetRuntimeModeForTests();
        var expected = beforeAot is null ? CaptureFailure(exercise) : null;
        string actual;
        var directory = CreateTemporaryDirectory();
        var loadContext = new AssemblyLoadContext("AdditionalParityNames", isCollectible: true);
        try
        {
            string? genericInstantiationsPath = null;
            if (genericInstantiationsJson is not null)
            {
                genericInstantiationsPath = Path.Combine(directory, "generic-instantiations.json");
                File.WriteAllText(genericInstantiationsPath, genericInstantiationsJson);
            }

            var outputPath = Path.Combine(directory, "Registry.dll");
            DuckTypeAotGenerateProcessor.Process(new DuckTypeAotGenerateOptions(
                proxyAssemblies: [TestAssemblyPath],
                targetAssemblies: targetAssemblies,
                targetFolders: [],
                targetFilters: ["*.dll"],
                mapFile: WriteMapFile(directory, mappings),
                genericInstantiationsFile: genericInstantiationsPath,
                outputPath: outputPath,
                assemblyName: "Registry",
                trimmerDescriptorPath: outputPath + ".linker.xml",
                propsPath: outputPath + ".props")).Should().Be(0);

            DuckType.ResetRuntimeModeForTests();
#if NETCOREAPP2_1
            var registry = Assembly.Load(File.ReadAllBytes(outputPath));
#else
            using var registryStream = File.OpenRead(outputPath);
            var registry = loadContext.LoadFromStream(registryStream);
#endif
            beforeAot?.Invoke();
            registry.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap")!
                    .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
            actual = CaptureFailure(exercise);
        }
        finally
        {
            DuckType.ResetRuntimeModeForTests();
            loadContext.Unload();
            Directory.Delete(directory, recursive: true);
        }

        expected ??= CaptureFailure(exercise);
        actual.Should().Be(expected, "AOT duck typing should behave like dynamic duck typing");
    }

    /// <summary>
    /// Writes NamesGenericTargets.dll: public class GenericTarget&lt;T&gt; { public string Name => "generic"; } and public class Item.
    /// </summary>
    private static string WriteNamesGenericTargets()
    {
        var module = CreateNamesModule("NamesGenericTargets" + Guid.NewGuid().ToString("N"));
        AddNamesClass(module, "NamesGenericTargets", "GenericTarget`1", module.CorLibTypes.Object.TypeDefOrRef, "generic", genericParameters: 1);
        AddNamesClass(module, "NamesGenericTargets", "Item", module.CorLibTypes.Object.TypeDefOrRef, nameValue: null);
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, module.Assembly.Name + ".dll");
        module.Write(path);
        return path;
    }

    /// <summary>
    /// Writes NamesDependency{suffix}.dll (public interface IMarker) in its own folder, and NamesDependencyTargets{suffix}.dll
    /// (Target { Name => "target" }, Derived : Target, IMarker) in another one: the generator only gets the second.
    /// </summary>
    private static (string TargetsPath, string DependencyPath) WriteNamesDependencyAssemblies(string suffix)
    {
        var directory = CreateTemporaryDirectory();
        var targetsDirectory = Path.Combine(directory, "targets");
        var dependencyDirectory = Path.Combine(directory, "dependency");
        Directory.CreateDirectory(targetsDirectory);
        Directory.CreateDirectory(dependencyDirectory);

        var dependencyModule = CreateNamesModule("NamesDependency" + suffix);
        dependencyModule.Types.Add(new TypeDefUser("NamesDependency", "IMarker") { Attributes = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract });
        var dependencyPath = Path.Combine(dependencyDirectory, "NamesDependency" + suffix + ".dll");
        dependencyModule.Write(dependencyPath);

        var module = CreateNamesModule("NamesDependencyTargets" + suffix);
        var target = AddNamesClass(module, "NamesDependencyTargets", "Target", module.CorLibTypes.Object.TypeDefOrRef, "target");
        var derived = AddNamesClass(module, "NamesDependencyTargets", "Derived", target, nameValue: null);
        derived.Interfaces.Add(new InterfaceImplUser(new TypeRefUser(module, "NamesDependency", "IMarker", new AssemblyRefUser("NamesDependency" + suffix, new Version(1, 0, 0, 0)))));
        var targetsPath = Path.Combine(targetsDirectory, "NamesDependencyTargets" + suffix + ".dll");
        module.Write(targetsPath);
        return (targetsPath, dependencyPath);
    }

    private static ModuleDefUser CreateNamesModule(string assemblyName)
    {
        var module = new ModuleDefUser(assemblyName + ".dll", Guid.NewGuid(), new AssemblyRefUser(typeof(object).Assembly.GetName())) { Kind = ModuleKind.Dll };
        new AssemblyDefUser(assemblyName, new Version(1, 0, 0, 0)).Modules.Add(module);
        return module;
    }

    private static TypeDefUser AddNamesClass(ModuleDef module, string ns, string name, ITypeDefOrRef baseType, string? nameValue, int genericParameters = 0)
    {
        var type = new TypeDefUser(ns, name, baseType) { Attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit };
        for (var i = 0; i < genericParameters; i++)
        {
            type.GenericParameters.Add(new GenericParamUser((ushort)i, GenericParamAttributes.NonVariant, "T" + i));
        }

        module.Types.Add(type);
        IMethod baseConstructor = baseType is TypeDef baseTypeDefinition
                                      ? baseTypeDefinition.FindDefaultConstructor()
                                      : new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), baseType);
        var constructor = new MethodDefUser(".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
        {
            Body = new CilBody()
        };
        constructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
        constructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseConstructor));
        constructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(constructor);
        if (nameValue is not null)
        {
            var getter = new MethodDefUser("get_Name", MethodSig.CreateInstance(module.CorLibTypes.String), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName)
            {
                Body = new CilBody()
            };
            getter.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(nameValue));
            getter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            type.Methods.Add(getter);
            type.Properties.Add(new PropertyDefUser("Name", PropertySig.CreateInstance(module.CorLibTypes.String)) { GetMethod = getter });
        }

        return type;
    }

    public interface INamesNameProxy
    {
        string Name { get; }
    }

    public interface INamesLengthProxy
    {
        int Length { get; }
    }

    public interface INamesMoveNextProxy
    {
        bool MoveNext();
    }

    public interface INamesGenericProxy<T>
    {
        string Name { get; }
    }

    public class NamesBase
    {
        public virtual string Name => "base";
    }

    public class NamesDerived<T> : NamesBase
    {
        public override string Name => "derived:" + typeof(T).Name;
    }

    public class NamesItem
    {
    }

    public class NamesBox2<T, TOther>
    {
    }

    public class NamesHolder<T>
    {
        public string Name => "holder:" + typeof(T).Name;
    }

    public class NamesPairSource : IEnumerable<KeyValuePair<string, string>>
    {
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            yield return new KeyValuePair<string, string>("key", "value");
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<string, string>>)this).GetEnumerator();
    }
}
