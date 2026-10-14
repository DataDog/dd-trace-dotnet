// <copyright file="GenericInstantiationDiscoveryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The search of the closed instantiations of instrumented generic methods (H-76): it only follows the generic code that
/// leads to them, so unrelated code doesn't use up its budget, and it cuts generic cycles.
/// </summary>
public class GenericInstantiationDiscoveryTests
{
    private readonly ModuleDefUser _module;
    private readonly TypeDef _target;
    private readonly MethodDef _targetRun;

    public GenericInstantiationDiscoveryTests()
    {
        _module = new ModuleDefUser("Generics.dll") { Kind = ModuleKind.Dll };
        new AssemblyDefUser("Generics", new Version(1, 0, 0, 0)).Modules.Add(_module);

        // The instrumented method: a method of a generic type, whose integration needs the closed instantiations of the type.
        _target = AddType("Target", genericParameters: 1);
        _targetRun = AddMethod(_target, "Run");
    }

    [Fact]
    public void UnrelatedCodeDoesntUseUpTheBudget()
    {
        // Code that instantiates other generic types, before the one that instantiates the target.
        var unrelated = AddType("Unrelated", genericParameters: 1);
        var unrelatedRun = AddMethod(unrelated, "Run");
        Calls(unrelatedRun);
        for (var i = 0; i < 20; i++)
        {
            Calls(AddMethod(AddType("Caller" + i), "Main"), Call(unrelated, "Run", _module.CorLibTypes.Int32));
        }

        Calls(AddMethod(AddType("Seed"), "Main"), Call(_target, "Run", _module.CorLibTypes.String));

        var instantiations = Discover(out var statistics, maxContexts: 1);

        Instantiations(instantiations).Should().BeEquivalentTo("System.String");
        statistics.LimitReached.Should().BeFalse();
        statistics.Truncated.Should().BeFalse();
    }

    [Fact]
    public void TypeArgumentsPropagateThroughGenericCode()
    {
        // Seed: Outer<string>.Run(); Outer<T>.Run: Inner<T>.Execute(); Inner<T>.Execute: Target<T>.Run().
        var inner = AddType("Inner", genericParameters: 1);
        Calls(AddMethod(inner, "Execute"), Call(_target, "Run", new GenericVar(0, inner)));
        var outer = AddType("Outer", genericParameters: 1);
        Calls(AddMethod(outer, "Run"), Call(inner, "Execute", new GenericVar(0, outer)));
        Calls(AddMethod(AddType("Seed"), "Main"), Call(outer, "Run", _module.CorLibTypes.String));

        Instantiations(Discover(out var statistics)).Should().BeEquivalentTo("System.String");
        statistics.Truncated.Should().BeFalse();
    }

    [Fact]
    public void GenericMethodsAndTheirVirtualImplementationsAreFollowed()
    {
        // Seed: IRunner.Run<int>() (an interface call); Runner.Run<T>: Target<T>.Run().
        var runnerInterface = AddType("IRunner");
        runnerInterface.Attributes = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
        runnerInterface.BaseType = null;
        var interfaceRun = AddMethod(runnerInterface, "Run", genericParameters: 1, instance: true);
        interfaceRun.Attributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.NewSlot | MethodAttributes.HideBySig;
        interfaceRun.Body = null;
        var runner = AddType("Runner");
        runner.Interfaces.Add(new InterfaceImplUser(runnerInterface));
        var runnerRun = AddMethod(runner, "Run", genericParameters: 1, instance: true);
        runnerRun.Attributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.HideBySig;
        Calls(runnerRun, Call(_target, "Run", new GenericMVar(0, runnerRun)));
        Calls(AddMethod(AddType("Seed"), "Main"), new MethodSpecUser(interfaceRun, new GenericInstMethodSig(_module.CorLibTypes.Int32)));

        Instantiations(Discover(out _)).Should().BeEquivalentTo("System.Int32");
    }

    [Fact]
    public void ClosedReferencesOfGenericCodeAreFound()
    {
        // Nothing instantiates Unused<T>, whose code references Target<long>: found anyway (the search doesn't need its context).
        var unused = AddType("Unused", genericParameters: 1);
        Calls(AddMethod(unused, "Run"), Call(_target, "Run", _module.CorLibTypes.Int64));

        Instantiations(Discover(out _)).Should().BeEquivalentTo("System.Int64");
    }

    [Fact]
    public void GenericCyclesAreCutAtTheirDepth()
    {
        // Seed: Recurse<int>(); Recurse<T>: Target<T>.Run(), Recurse<List<T>>(): infinitely many instantiations.
        var seed = AddType("Seed");
        var recurse = AddMethod(seed, "Recurse", genericParameters: 1);
        var list = new GenericInstSig(new ClassSig(_module.CorLibTypes.GetTypeRef("System.Collections.Generic", "List`1")), new GenericMVar(0, recurse));
        Calls(recurse, Call(_target, "Run", new GenericMVar(0, recurse)), new MethodSpecUser(recurse, new GenericInstMethodSig(list)));
        Calls(AddMethod(seed, "Main"), new MethodSpecUser(recurse, new GenericInstMethodSig(_module.CorLibTypes.Int32)));

        var instantiations = Instantiations(Discover(out var statistics));

        instantiations.Should().Contain("System.Int32").And.Contain("System.Collections.Generic.List`1<System.Int32>");
        instantiations.Should().HaveCount(GenericInstantiationDiscovery.MaxGenericDepth);
        statistics.DepthCuts.Should().BeGreaterThan(0);
        statistics.Truncated.Should().BeTrue();
    }

    [Fact]
    public void TheSearchStopsAtItsLimit()
    {
        // Seed: Outer<string>.Run(); Outer<T>.Run: Inner<T>.Execute(); Inner<T>.Execute: Target<T>.Run(): two contexts.
        var inner = AddType("Inner", genericParameters: 1);
        Calls(AddMethod(inner, "Execute"), Call(_target, "Run", new GenericVar(0, inner)));
        var outer = AddType("Outer", genericParameters: 1);
        Calls(AddMethod(outer, "Run"), Call(inner, "Execute", new GenericVar(0, outer)));
        Calls(AddMethod(AddType("Seed"), "Main"), Call(outer, "Run", _module.CorLibTypes.String));

        Instantiations(Discover(out var statistics, maxContexts: 1)).Should().BeEmpty();
        statistics.LimitReached.Should().BeTrue();
        statistics.Truncated.Should().BeTrue();
    }

    private Dictionary<MethodDef, List<GenericInstantiationDiscovery.Instantiation>> Discover(out GenericInstantiationDiscovery.Statistics statistics, int maxContexts = GenericInstantiationDiscovery.DefaultMaxContexts)
    {
        var corlib = ModuleDefMD.Load(typeof(object).Assembly.Location);
        var resolver = new LoadedModulesTypeResolver(new ModuleDef[] { _module, corlib });
        return GenericInstantiationDiscovery.Discover([_module], [_targetRun], resolver.Resolve, out statistics, maxContexts);
    }

    private List<string> Instantiations(Dictionary<MethodDef, List<GenericInstantiationDiscovery.Instantiation>> instantiations)
        => instantiations.TryGetValue(_targetRun, out var found) ? found.Select(i => i.TypeArguments.Single().FullName).ToList() : [];

    private TypeDef AddType(string name, int genericParameters = 0)
    {
        var type = new TypeDefUser("Generics", name, _module.CorLibTypes.Object.TypeDefOrRef) { Attributes = TypeAttributes.Public | TypeAttributes.Class };
        for (var i = 0; i < genericParameters; i++)
        {
            type.GenericParameters.Add(new GenericParamUser((ushort)i, GenericParamAttributes.NonVariant, "T" + i));
        }

        _module.Types.Add(type);
        return type;
    }

    private MethodDef AddMethod(TypeDef type, string name, int genericParameters = 0, bool instance = false)
    {
        var signature = (genericParameters, instance) switch
        {
            (0, false) => MethodSig.CreateStatic(_module.CorLibTypes.Void),
            (0, true) => MethodSig.CreateInstance(_module.CorLibTypes.Void),
            (_, false) => MethodSig.CreateStaticGeneric((uint)genericParameters, _module.CorLibTypes.Void),
            _ => MethodSig.CreateInstanceGeneric((uint)genericParameters, _module.CorLibTypes.Void),
        };
        var method = new MethodDefUser(name, signature, MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | (instance ? 0 : MethodAttributes.Static)) { Body = new CilBody() };
        for (var i = 0; i < genericParameters; i++)
        {
            method.GenericParameters.Add(new GenericParamUser((ushort)i, GenericParamAttributes.NonVariant, "M" + i));
        }

        type.Methods.Add(method);
        method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        return method;
    }

    /// <summary>
    /// A static method of a closed or open instantiation of a generic type.
    /// </summary>
    private IMethod Call(TypeDef type, string name, params TypeSig[] typeArguments)
        => new MemberRefUser(_module, name, MethodSig.CreateStatic(_module.CorLibTypes.Void), new TypeSpecUser(new GenericInstSig(new ClassSig(type), typeArguments)));

    private void Calls(MethodDef method, params IMethod[] callees)
    {
        method.Body.Instructions.Clear();
        foreach (var callee in callees)
        {
            method.Body.Instructions.Add(OpCodes.Call.ToInstruction(callee));
        }

        method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
    }
}
#endif
