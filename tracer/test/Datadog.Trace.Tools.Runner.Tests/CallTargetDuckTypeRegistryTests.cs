// <copyright file="CallTargetDuckTypeRegistryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The DuckType AOT registry of the proxies the NativeAOT CallTarget adapters create.
/// </summary>
public class CallTargetDuckTypeRegistryTests
{
    [Fact]
    public void ProxiesLookedUpByRuntimeTypeGetAProxyPerRuntimeTypeUnlessTheyBindNoMember()
    {
        // The slow begin path (more than 8 arguments) looks the proxies of the arguments up by runtime type
        // (IntegrationMapper.ConvertType): the proxy of each type assignable to the static type, like dynamic duck typing creates
        // them. A marker interface binds no member: the proxy of System.Object serves every type, instead of an alias per type of
        // the inputs (for System.Object, thousands, which the NativeAOT compiler of Windows can't compile).
        var proxies = BuildRegistry((module, sig, directory) => CallTargetDuckTypeRegistry.Build(
            [],
            [(sig(typeof(IMarkerProxy)), module.CorLibTypes.Object), (sig(typeof(IMarkerProxy)), sig(typeof(RuntimeTarget))), (sig(typeof(INameProxy)), sig(typeof(RuntimeTarget)))],
            [],
            [],
            AssemblyPaths,
            directory,
            "Datadog.Trace.DuckType.AotRegistry.CallTargetDuckTypeRegistryTests",
            typeof(Tracer).Assembly.Location,
            Resolver!.Resolve));

        proxies.Should().BeEquivalentTo(
            "System.Object",
            typeof(RuntimeTarget).FullName!.Replace('+', '/'),
            typeof(DerivedRuntimeTarget).FullName!.Replace('+', '/'));
    }

    [Fact]
    public void InstantiationsOverValueTypesOfOpenGenericTargetsGetAProxyOfTheirOwn()
    {
        // The generic proxy of an open generic target only serves reference type arguments in NativeAOT: an instantiation over a
        // value type the application creates (see GenericDuckTypeTargets) gets a closed proxy, next to the generic one.
        var openMapping = new DuckTypeAotMapping(typeof(INameProxy).FullName!, typeof(INameProxy).Assembly.GetName().Name!, typeof(GenericTarget<>).FullName!, typeof(GenericTarget<>).Assembly.GetName().Name!, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.Attribute);
        var proxies = BuildRegistry((module, sig, directory) =>
        {
            var definition = module.Find(typeof(GenericTarget<>).FullName!, isReflectionName: true)!;
            return CallTargetDuckTypeRegistry.Build(
                [],
                [],
                [openMapping],
                [(openMapping, new GenericInstSig(new ClassSig(definition), module.CorLibTypes.Int32))],
                AssemblyPaths,
                directory,
                "Datadog.Trace.DuckType.AotRegistry.CallTargetDuckTypeRegistryTests",
                typeof(Tracer).Assembly.Location,
                Resolver!.Resolve);
        });

        proxies.Should().Contain(typeof(GenericTarget<>).FullName!.Replace('+', '/') + "<System.Int32>");
    }

    private static Dictionary<string, string> AssemblyPaths { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [typeof(CallTargetDuckTypeRegistryTests).Assembly.GetName().Name!] = typeof(CallTargetDuckTypeRegistryTests).Assembly.Location,
        ["System.Runtime"] = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll"),
        ["System.Private.CoreLib"] = typeof(object).Assembly.Location,
        ["Datadog.Trace"] = typeof(Tracer).Assembly.Location,
    };

    private static LoadedModulesTypeResolver? Resolver { get; set; }

    /// <summary>
    /// Builds a registry into a temporary directory, and returns the types the instances of its proxies have.
    /// </summary>
    private static List<string> BuildRegistry(Func<ModuleDef, Func<Type, TypeSig>, string, CallTargetDuckTypeRegistry?> build)
    {
        var modules = AssemblyPaths.Values.Select(path => ModuleDefMD.Load(path)).ToList();
        var module = modules.Single(m => m.Location == typeof(CallTargetDuckTypeRegistryTests).Assembly.Location);
        Resolver = new LoadedModulesTypeResolver(modules);
        TypeSig Sig(Type type) => module.Find(type.FullName!, isReflectionName: true)!.ToTypeSig();

        var directory = Path.Combine(Path.GetTempPath(), "dd-trace-aot-calltarget-registry", Guid.NewGuid().ToString("N"));
        try
        {
            var registry = build(module, Sig, directory);
            registry.Should().NotBeNull();
            return registry!.Module.GetTypes()
                            .Where(type => type.Interfaces.Any(i => i.Interface.FullName == "Datadog.Trace.DuckTyping.IDuckType"))
                            .Select(type => type.Fields.Single(field => field.Name == "_currentInstance").FieldSig.Type.FullName)
                            .ToList();
        }
        finally
        {
            modules.ForEach(m => m.Dispose());
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    public interface IMarkerProxy
    {
    }

    public interface INameProxy
    {
        string Name { get; }
    }

    public class RuntimeTarget
    {
        public string Name => "base";
    }

    public class DerivedRuntimeTarget : RuntimeTarget
    {
        public new string Name => "derived";
    }

    public class GenericTarget<T>
    {
        public string Name => typeof(T).Name;
    }
}
#endif
