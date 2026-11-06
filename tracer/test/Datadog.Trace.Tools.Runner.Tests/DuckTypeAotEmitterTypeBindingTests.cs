// <copyright file="DuckTypeAotEmitterTypeBindingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The type binding layer of the registry emitter: the members of a proxy type bind to the target type's, and dynamic duck
/// typing, run in the generator's process, decides whether a mapping can be created and how it fails.
/// </summary>
public class DuckTypeAotEmitterTypeBindingTests : IDisposable
{
    private static readonly string AssemblyName = typeof(DuckTypeAotEmitterTypeBindingTests).Assembly.GetName().Name!;
    private static readonly IReadOnlyDictionary<string, string> AssemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [AssemblyName] = typeof(DuckTypeAotEmitterTypeBindingTests).Assembly.Location,
    };

    private static readonly Type Emitter = typeof(DuckTypeAotMapping).Assembly.GetType("Datadog.Trace.Tools.Runner.DuckTypeAot.DuckTypeAotRegistryAssemblyEmitter")!;

    private readonly ModuleDefMD _module = ModuleDefMD.Load(typeof(DuckTypeAotEmitterTypeBindingTests).Assembly.Location);

    public DuckTypeAotEmitterTypeBindingTests()
    {
        // The per-emission state the registry layer sets at the start of each pass: the execution context (with an empty
        // index of target types) and the assembly paths runtime types are resolved from.
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

    public interface INameProxy
    {
        string Name { get; }
    }

    public interface IMissingMemberProxy
    {
        string Missing { get; }
    }

    public void Dispose()
    {
        SetState("_currentExecutionContext", null);
        SetState("runtimeTypeResolutionAssemblyPathsByName", null);
        _module.Dispose();
    }

    [Fact]
    public void DynamicDuckTypingDecidesWhetherAMappingCanBeCreated()
    {
        CreationOutcome(typeof(INameProxy), typeof(NameTarget), out var exceptionType).Should().Be("Created");
        exceptionType.Should().BeNull();

        CreationOutcome(typeof(IMissingMemberProxy), typeof(NameTarget), out exceptionType).Should().Be("Failed");
        exceptionType.Should().Be(typeof(DuckTypePropertyOrFieldNotFoundException));
    }

    [Fact]
    public void APropertyBindsToTheGetterOfTheTargetProperty()
    {
        var getter = FindType(typeof(INameProxy)).Methods.Single(method => method.Name == "get_Name");

        var arguments = BindingArguments(typeof(INameProxy), getter);
        Invoke("TryResolveForwardBinding", arguments).Should().Be(true, ((DuckTypeAotMappingEmissionResult?)arguments[6])?.Detail);

        var binding = arguments[5]!;
        ((MethodDef?)Property(binding, "TargetMethod"))!.FullName.Should().Contain(nameof(NameTarget) + "::get_Name");
    }

    [Fact]
    public void AMemberTheTargetDoesNotHaveFailsTheMapping()
    {
        var getter = FindType(typeof(IMissingMemberProxy)).Methods.Single(method => method.Name == "get_Missing");

        var arguments = BindingArguments(typeof(IMissingMemberProxy), getter);
        Invoke("TryResolveForwardBinding", arguments).Should().Be(false);

        var failure = (DuckTypeAotMappingEmissionResult)arguments[6]!;
        failure.Status.Should().NotBe(DuckTypeAotCompatibilityStatuses.Compatible);
    }

    [Fact]
    public void TheFieldsOfADuckCopyStructBindToTheTargetMembers()
    {
        var mapping = Mapping(typeof(NameCopy), typeof(NameTarget));
        var arguments = new object?[] { mapping, FindType(typeof(NameCopy)), FindType(typeof(NameTarget)), null, null, null, null };
        Invoke("TryCollectStructCopyBindings", arguments).Should().Be(true, ((DuckTypeAotMappingEmissionResult?)arguments[6])?.Detail);

        var bindings = ((IEnumerable)arguments[5]!).Cast<object>().ToList();
        bindings.Should().ContainSingle();
        ((FieldDef)Property(bindings[0], "ProxyField")!).Name.String.Should().Be(nameof(NameCopy.Name));
        ((PropertyDef?)Property(bindings[0], "SourceProperty"))!.Name.String.Should().Be(nameof(NameTarget.Name));
    }

    private static DuckTypeAotMapping Mapping(Type proxy, Type target)
        => new(proxy.FullName!, AssemblyName, target.FullName!, AssemblyName, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile);

    private static string CreationOutcome(Type proxy, Type target, out Type? exceptionType)
    {
        var arguments = new object?[] { Mapping(proxy, target), AssemblyPaths, AssemblyPaths, null, null };
        var outcome = Invoke("GetDynamicCreationOutcome", arguments, parameterTypes: 5, firstParameter: typeof(DuckTypeAotMapping))!;
        exceptionType = (Type?)arguments[3];
        return outcome.ToString()!;
    }

    private static object? Invoke(string name, object?[] arguments, int? parameterTypes = null, Type? firstParameter = null)
    {
        var method = Emitter.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                            .Single(m => m.Name == name &&
                                         m.GetParameters().Length == (parameterTypes ?? arguments.Length) &&
                                         (firstParameter is null || m.GetParameters()[0].ParameterType == firstParameter));
        return method.Invoke(null, arguments);
    }

    private static object Create(string nestedType, params object?[] arguments)
        => Activator.CreateInstance(Emitter.GetNestedType(nestedType, BindingFlags.NonPublic)!, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, binder: null, arguments, culture: null)!;

    private static void SetState(string field, object? value)
        => Emitter.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, value);

    private static object? Property(object instance, string name)
        => instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(instance);

    private object?[] BindingArguments(Type proxy, MethodDef proxyMethod)
        => new object?[] { Mapping(proxy, typeof(NameTarget)), FindType(typeof(NameTarget)), null, null, proxyMethod, null, null };

    private TypeDef FindType(Type type) => _module.Find(type.FullName, isReflectionName: true)!;

    [DuckCopy]
    public struct NameCopy
    {
        public string Name;
    }

    public class NameTarget
    {
        public string Name => "name";
    }
}
