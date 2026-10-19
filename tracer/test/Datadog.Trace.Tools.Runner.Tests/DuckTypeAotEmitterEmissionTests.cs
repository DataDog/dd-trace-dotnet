// <copyright file="DuckTypeAotEmitterEmissionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FluentAssertions;
using Xunit;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The emission layer of the registry emitter: the IL of the proxy of a mapping, emitted into a registry module set up like
/// the registry layer sets it up, loaded and called.
/// </summary>
public class DuckTypeAotEmitterEmissionTests : IDisposable
{
    private static readonly Type Emitter = typeof(DuckTypeAotMapping).Assembly.GetType("Datadog.Trace.Tools.Runner.DuckTypeAot.DuckTypeAotRegistryAssemblyEmitter")!;
    private static readonly string AssemblyName = typeof(DuckTypeAotEmitterEmissionTests).Assembly.GetName().Name!;
    private static readonly IReadOnlyDictionary<string, string> AssemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [AssemblyName] = typeof(DuckTypeAotEmitterEmissionTests).Assembly.Location,
    };

    private readonly ModuleDefUser _registry;
    private readonly TypeDefUser _bootstrap;
    private readonly MethodDefUser _initialize;
    private readonly object _importedMembers;
    private readonly IReadOnlyDictionary<string, ModuleDefMD> _modules;

    public DuckTypeAotEmitterEmissionTests()
    {
        // The registry module and the per-emission state, set like the registry layer sets them at the start of a pass.
        var registryName = "Registry" + Guid.NewGuid().ToString("N");
        var assembly = new AssemblyDefUser(registryName, new Version(1, 0, 0, 0));
        _registry = new ModuleDefUser(registryName + ".dll", Guid.NewGuid(), new AssemblyRefUser(typeof(object).Assembly.GetName())) { Kind = ModuleKind.Dll };
        assembly.Modules.Add(_registry);

        var references = new Dictionary<string, AssemblyRef>(StringComparer.OrdinalIgnoreCase);
        var datadogTrace = (AssemblyRef)Call("AddAssemblyReference", _registry, references, typeof(DuckType).Assembly.Location)!;
        Call("AddAssemblyReference", _registry, references, typeof(DuckTypeAotEmitterEmissionTests).Assembly.Location);
        SetState("runtimeImporter", new Importer(_registry, default, default, (ImportMapper)Create("DatadogTraceImportMapper", _registry, datadogTrace)));
        _importedMembers = Create("ImportedMembers", _registry);

        _bootstrap = new TypeDefUser("Registry", "Bootstrap", _registry.CorLibTypes.Object.TypeDefOrRef) { Attributes = TypeAttributes.Public | TypeAttributes.Sealed };
        _initialize = new MethodDefUser("Initialize", MethodSig.CreateStatic(_registry.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.Static) { Body = new CilBody() };
        _bootstrap.Methods.Add(_initialize);
        _registry.Types.Add(_bootstrap);

        var context = (ModuleContext)Call("CreateModuleLoadContext", (object)new[] { AssemblyPaths })!;
        _registry.Context = context;
        _modules = (IReadOnlyDictionary<string, ModuleDefMD>)Call("LoadModules", AssemblyPaths, context)!;

        var index = Create(
            "TargetTypeIndex",
            new Dictionary<string, TypeDef>(),
            Activator.CreateInstance(typeof(List<>).MakeGenericType(Emitter.GetNestedType("TargetTypeIndexEntry", BindingFlags.NonPublic)!)),
            EmptyAliases(),
            EmptyAliases());
        SetState("_currentExecutionContext", Create("EmitterExecutionContext", index, Create("DynamicOracleCache")));
        SetState("runtimeTypeResolutionAssemblyPathsByName", AssemblyPaths);

        static object EmptyAliases()
            => Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(IReadOnlyList<>).MakeGenericType(Emitter.GetNestedType("TargetAliasTargetInfo", BindingFlags.NonPublic)!)))!;
    }

    public interface IGreetingProxy
    {
        string Name { get; }

        string Greet(string greeting);
    }

    public void Dispose()
    {
        SetState("_currentExecutionContext", null);
        SetState("runtimeTypeResolutionAssemblyPathsByName", null);
        SetState("runtimeImporter", null);
        foreach (var module in _modules.Values)
        {
            module.Dispose();
        }
    }

    [Fact]
    public void TheEmittedProxyCallsTheTargetMembers()
    {
        var mapping = new DuckTypeAotMapping(typeof(IGreetingProxy).FullName!, AssemblyName, typeof(GreetingTarget).FullName!, AssemblyName, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile);

        var result = (DuckTypeAotMappingEmissionResult)Call("EmitMapping", _registry, _bootstrap, _initialize, _importedMembers, mapping, 1, _modules, _modules, AssemblyPaths, AssemblyPaths, new List<string>(), null)!;

        result.Status.Should().Be(DuckTypeAotCompatibilityStatuses.Compatible, result.Detail);
        var generated = _registry.Find(result.GeneratedProxyTypeName, isReflectionName: true);
        generated.Should().NotBeNull();
        generated!.Interfaces.Select(i => i.Interface.FullName).Should().Contain(typeof(IGreetingProxy).FullName!.Replace('+', '/'));

        // Loaded and called: the generated proxy wraps the target instance and forwards to its members.
        _initialize.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        using var stream = new MemoryStream();
        _registry.Write(stream);
        stream.Position = 0;
        var proxyType = AssemblyLoadContext.Default.LoadFromStream(stream).GetType(result.GeneratedProxyTypeName!, throwOnError: true)!;
        var target = new GreetingTarget();
        var proxy = (IGreetingProxy)Activator.CreateInstance(proxyType, target)!;
        proxy.Name.Should().Be("target");
        proxy.Greet("hello").Should().Be("hello target");
        ((IDuckType)proxy).Instance.Should().BeSameAs(target);
    }

    private static object? Call(string name, params object?[] arguments)
        => Emitter.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(m => m.Name == name && m.GetParameters().Length == arguments.Length).Invoke(null, arguments);

    private static object Create(string nestedType, params object?[] arguments)
        => Activator.CreateInstance(Emitter.GetNestedType(nestedType, BindingFlags.NonPublic)!, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, binder: null, arguments, culture: null)!;

    private static void SetState(string field, object? value)
        => Emitter.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, value);

    public class GreetingTarget
    {
        public string Name => "target";

        public string Greet(string greeting) => $"{greeting} {Name}";
    }
}
#endif
