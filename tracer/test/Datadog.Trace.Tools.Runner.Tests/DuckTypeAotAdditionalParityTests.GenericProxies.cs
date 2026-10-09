// <copyright file="DuckTypeAotAdditionalParityTests.GenericProxies.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Generic proxies: a mapping of an open generic target type serves its instantiations over types only known at runtime (a
/// library creating them over the application's types), which no input names. Every scenario runs each exercise in dynamic mode,
/// then with the generated registry, and compares the outcomes.
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // Members of the target typed with its type parameters, typeof(T) in the target, virtual members.
    [InlineData("instantiation")]
    // Instantiations created with MakeGenericType, over a type no input instantiates the target with.
    [InlineData("runtime-instantiation")]
    // The classes deriving from an instantiation, with IncludeDerivedTypes.
    [InlineData("derived-types")]
    // A [DuckCopy] struct of the instantiations.
    [InlineData("duck-copy")]
    // The classes implementing an instantiation of an open generic interface.
    [InlineData("interface")]
    public void GeneratedRegistryShouldServeTheInstantiationsOfOpenGenericTargetsLikeDynamicMode(string scenario)
    {
        var definition = typeof(GenericProxyTarget<,>);
        var (exercises, mappings) = scenario switch
        {
            "instantiation" => (
                new Func<object?>[]
                {
                    () => Describe(DuckType.Create<IGenericProxyTargetProxy>(new GenericProxyTarget<GenericProxyApplicationType, string>(new GenericProxyApplicationType("a"), "b"))),
                    () => Describe(DuckType.Create<IGenericProxyTargetProxy>(new GenericProxyTarget<string, GenericProxyApplicationType>("c", new GenericProxyApplicationType("d")))),
                    () => DescribeDuckType(DuckType.Create<IGenericProxyTargetProxy>(new GenericProxyTarget<string, string>("e", "f"))),
                },
                new[] { OpenMapping(typeof(IGenericProxyTargetProxy), definition) }),
            "runtime-instantiation" => (
                new Func<object?>[]
                {
                    () => Describe(DuckType.Create<IGenericProxyTargetProxy>(CreateAtRuntime(typeof(GenericProxyRuntimeOnlyType), typeof(Uri)))),
                    () => DuckType.CanCreate<IGenericProxyTargetProxy>(CreateAtRuntime(typeof(GenericProxyRuntimeOnlyType), typeof(Uri))),
                },
                new[] { OpenMapping(typeof(IGenericProxyTargetProxy), definition) }),
            "derived-types" => (
                new Func<object?>[]
                {
                    () => Describe(DuckType.Create<IGenericProxyTargetProxy>(new GenericProxyDerivedTarget<GenericProxyApplicationType>(new GenericProxyApplicationType("g")))),
                    () => DescribeDuckType(DuckType.Create<IGenericProxyTargetProxy>(new GenericProxyDerivedTarget<GenericProxyApplicationType>(new GenericProxyApplicationType("h")))),
                    () => Describe(DuckType.Create<IGenericProxyTargetProxy>(new GenericProxyTarget<string, string>("i", "j"))),
                },
                new[] { OpenMapping(typeof(IGenericProxyTargetProxy), definition).WithDerivedTypes() }),
            "duck-copy" => (
                new Func<object?>[]
                {
                    () =>
                    {
                        var copy = DuckType.Create<GenericProxyTargetCopy>(new GenericProxyTarget<GenericProxyApplicationType, string>(new GenericProxyApplicationType("m"), "n"));
                        return $"{copy.Value}|{copy.Second}|{copy.Count}";
                    },
                },
                new[] { OpenMapping(typeof(GenericProxyTargetCopy), definition) }),
            _ => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IGenericProxyContractProxy>(new GenericProxyContractImplementation())!.Get(),
                    () => DescribeDuckType(DuckType.Create<IGenericProxyContractProxy>(new GenericProxyContractImplementation())),
                },
                new[] { OpenMapping(typeof(IGenericProxyContractProxy), typeof(IGenericProxyContract<>)).WithDerivedTypes() }),
        };

        DuckType.ResetRuntimeModeForTests();
        var expected = exercises.Select(CaptureFailure).ToArray();
        WithGeneratedRegistry(
            () => exercises.Select(CaptureFailure).Should().Equal(expected, "AOT duck typing should behave like dynamic duck typing"),
            matrix => matrix.Mappings.Should().ContainSingle().Which.Status.Should().Be(DuckTypeAotCompatibilityStatuses.Compatible, matrix.Mappings[0].Details),
            mappings);
    }

    [Fact]
    public void GeneratedRegistryShouldNotServeInstantiationsOverValueTypes()
    {
        // NativeAOT can't build them at runtime: the generic proxy serves reference type arguments only (a recorded map gives the
        // others their own proxy).
        Func<object?> exercise = () => DuckType.Create<IGenericProxyTargetProxy>(new GenericProxyTarget<string, int>("o", 1))!.Second;
        DuckType.ResetRuntimeModeForTests();
        CaptureFailure(exercise).Should().Be("1");
        WithGeneratedRegistry(
            () => CaptureFailure(exercise).Should().StartWith("throws:DuckTypeAotMissingProxyRegistrationException:"),
            OpenMapping(typeof(IGenericProxyTargetProxy), typeof(GenericProxyTarget<,>)));
    }

    [Fact]
    public void GeneratedRegistryShouldPreferTheProxyOfAnInstantiationItNames()
    {
        // A closed mapping (e.g. recorded) of an instantiation the generic proxy also serves: its own registration.
        Func<object?> exercise = () => DescribeDuckType(DuckType.Create<IGenericProxyTargetProxy>(new GenericProxyTarget<string, int>("p", 2)));
        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureFailure(exercise);
        WithGeneratedRegistry(
            () => CaptureFailure(exercise).Should().Be(expected),
            OpenMapping(typeof(IGenericProxyTargetProxy), typeof(GenericProxyTarget<,>)),
            Mapping(typeof(IGenericProxyTargetProxy), typeof(GenericProxyTarget<string, int>)));
    }

    private static DuckTypeAotMapping OpenMapping(Type proxy, Type openTarget)
        => new(proxy.FullName!, proxy.Assembly.GetName().Name!, openTarget.FullName!, openTarget.Assembly.GetName().Name!, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile);

    private static object CreateAtRuntime(Type first, Type second)
        => Activator.CreateInstance(typeof(GenericProxyTarget<,>).MakeGenericType(first, second), Activator.CreateInstance(first), Activator.CreateInstance(second, "http://second/"))!;

    private static string Describe(IGenericProxyTargetProxy? proxy)
        => proxy is null ? "null" : $"{proxy.Value}|{proxy.Second}|{proxy.Count}|{proxy.ValueTypeName}|{proxy.Describe()}|{proxy.Echo(proxy.Value)}";

    public interface IGenericProxyTargetProxy
    {
        object? Value { get; }

        object? Second { get; }

        int Count { get; }

        string ValueTypeName { get; }

        string Describe();

        object? Echo(object? value);
    }

    public interface IGenericProxyContractProxy
    {
        object? Get();
    }

    public interface IGenericProxyContract<T>
        where T : class
    {
        T Get();
    }

    [DuckCopy]
    public struct GenericProxyTargetCopy
    {
        public object? Value;

        public object? Second;

        public int Count;
    }

    public class GenericProxyTarget<TValue, TSecond>
    {
        private readonly TValue _value;

        public GenericProxyTarget(TValue value, TSecond second)
        {
            _value = value;
            Second = second;
        }

        public TValue Value => _value;

        public TSecond Second { get; }

        public int Count => 42;

        public string ValueTypeName => typeof(TValue).Name;

        public virtual string Describe() => $"target<{typeof(TValue).Name},{typeof(TSecond).Name}>";

        public TValue Echo(TValue value) => value;
    }

    public class GenericProxyDerivedTarget<T> : GenericProxyTarget<T, string>
    {
        public GenericProxyDerivedTarget(T value)
            : base(value, "derived")
        {
        }

        public override string Describe() => $"derived<{typeof(T).Name}>";
    }

    public class GenericProxyApplicationType
    {
        private readonly string _name;

        public GenericProxyApplicationType()
            : this("default")
        {
        }

        public GenericProxyApplicationType(string name) => _name = name;

        public override string ToString() => "app:" + _name;
    }

    public class GenericProxyRuntimeOnlyType
    {
        public override string ToString() => "runtime-only";
    }

    public class GenericProxyContractImplementation : IGenericProxyContract<GenericProxyApplicationType>
    {
        public GenericProxyApplicationType Get() => new("contract");
    }
}
