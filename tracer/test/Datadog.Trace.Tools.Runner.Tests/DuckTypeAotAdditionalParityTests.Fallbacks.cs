// <copyright file="DuckTypeAotAdditionalParityTests.Fallbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The runtime types a registry can't name: the other array types of an array mapping, the arrays a mapping that isn't an
/// array type is assignable from, and the non-public types of the core library. Every scenario runs each exercise in dynamic
/// mode, then with the generated registry, and compares the outcomes (values, or exception types and messages).
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // A failure registered for an array type is thrown for the other array types with their own name in the message.
    [InlineData("failure-other-arrays")]
    [InlineData("failure-covariant-arrays")]
    [InlineData("failure-derived-element-arrays")]
    // A System.Array mapping binds what reflection finds on System.Array (an explicit interface implementation here), which
    // dynamic duck typing doesn't find on an array type: it serves System.Array only.
    [InlineData("system-array-only")]
    [InlineData("system-array-and-int-array")]
    // [DuckCopy] struct mappings serve the other array types too.
    [InlineData("duck-copy-int-array")]
    [InlineData("duck-copy-object-array")]
    // Mappings that aren't array types, but that arrays are assignable to.
    [InlineData("object-mapping-arrays")]
    [InlineData("enumerable-mapping-arrays")]
    // The activator of another array type casts the instance like dynamic duck typing's.
    [InlineData("activator-of-another-array-type")]
    // A reverse proxy with an array delegation type.
    [InlineData("reverse-array-delegation")]
    // Non-public types of the core library: RuntimeType for System.Type, the box of an async method for Task<T>.
    [InlineData("runtime-type")]
    [InlineData("runtime-type-failure")]
    [InlineData("async-method-task")]
    public void GeneratedRegistryShouldServeRuntimeTypesItCantNameLikeDynamicMode(string scenario)
    {
        var (exercises, mappings) = scenario switch
        {
            "failure-other-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackMissingProxy>(new int[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new long[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new string[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new int[1, 1])!.Length,
                },
                new[] { Mapping(typeof(IFallbackMissingProxy), typeof(int[])) }),
            "failure-covariant-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackMissingProxy>(new object[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new string[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new FallbackElement[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new int[1])!.Length,
                },
                new[] { Mapping(typeof(IFallbackMissingProxy), typeof(object[])) }),
            "failure-derived-element-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackMissingProxy>(new FallbackElement[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new FallbackDerivedElement[1])!.Length,
                },
                new[] { Mapping(typeof(IFallbackMissingProxy), typeof(FallbackElement[])) }),
            "system-array-only" => (
                new Func<object?>[]
                {
                    () => DuckType.CreateCache<IFallbackExplicitCountProxy>.CreateFrom<Array>(new int[3])!.Count,
                    () => DuckType.Create<IFallbackExplicitCountProxy>(new int[3])!.Count,
                    () => DuckType.CanCreate<IFallbackExplicitCountProxy>(new int[3]),
                },
                new[] { Mapping(typeof(IFallbackExplicitCountProxy), typeof(Array)) }),
            "system-array-and-int-array" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackExplicitCountProxy>(new int[3])!.Count,
                    () => DuckType.Create<IFallbackExplicitCountProxy>(new long[4])!.Count,
                    () => DuckType.Create<IFallbackExplicitCountProxy>(new uint[5])!.Count,
                    () => DuckType.CreateCache<IFallbackExplicitCountProxy>.CreateFrom<Array>(new long[6])!.Count,
                },
                new[] { Mapping(typeof(IFallbackExplicitCountProxy), typeof(Array)), Mapping(typeof(IFallbackExplicitCountProxy), typeof(int[])) }),
            "duck-copy-int-array" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<FallbackLengthCopy>(new int[3]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new uint[4]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new long[5]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new int[2, 3]).Rank,
                    () => DuckType.Create<FallbackLengthCopy>(new string[6]).Length,
                },
                new[] { Mapping(typeof(FallbackLengthCopy), typeof(int[])) }),
            "duck-copy-object-array" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<FallbackLengthCopy>(new object[3]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new string[4]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new int[5]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new FallbackElement[6]).Length,
                },
                new[] { Mapping(typeof(FallbackLengthCopy), typeof(object[])) }),
            "object-mapping-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackEqualsProxy>(new FallbackElement())!.Equals(null),
                    () => DescribeDuckType(DuckType.Create<IFallbackEqualsProxy>(new int[1])),
                    () => DescribeDuckType(DuckType.Create<IFallbackEqualsProxy>(new string[1])),
                },
                new[] { Mapping(typeof(IFallbackEqualsProxy), typeof(object)) }),
            "enumerable-mapping-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackEnumerableProxy>(new ArrayList { 1 })!.GetEnumerator().MoveNext(),
                    () => DuckType.Create<IFallbackEnumerableProxy>(new[] { 1 })!.GetEnumerator().MoveNext(),
                    () => DescribeDuckType(DuckType.Create<IFallbackEnumerableProxy>(new[] { "a", "b" })),
                },
                new[] { Mapping(typeof(IFallbackEnumerableProxy), typeof(IEnumerable)) }),
            "activator-of-another-array-type" => (
                new Func<object?>[]
                {
                    () => DescribeDuckType(DuckType.GetOrCreateProxyType(typeof(IFallbackLengthProxy), typeof(long[])).CreateInstance<IFallbackLengthProxy>(new int[3])),
                    () => DescribeDuckType(DuckType.GetOrCreateProxyType(typeof(IFallbackLengthProxy), typeof(int[])).CreateInstance<IFallbackLengthProxy>(new long[3])),
                    () => DescribeDuckType(DuckType.GetOrCreateProxyType(typeof(IFallbackLengthProxy), typeof(long[])).CreateInstance<IFallbackLengthProxy>("not an array")),
                },
                new[] { Mapping(typeof(IFallbackLengthProxy), typeof(int[])) }),
            "reverse-array-delegation" => (
                new Func<object?>[]
                {
                    () =>
                    {
                        var reverse = DuckType.CreateReverse(typeof(FallbackVirtualNameBase), new int[2]);
                        return ((FallbackVirtualNameBase)reverse).Name + "|" + ((IDuckType)reverse).Type.Name;
                    },
                },
                new[] { Mapping(typeof(FallbackVirtualNameBase), typeof(int[]), reverse: true) }),
            "runtime-type" => (
                new Func<object?>[] { () => DescribeDuckType(DuckType.Create<IFallbackNameProxy>(typeof(string))) },
                new[] { Mapping(typeof(IFallbackNameProxy), typeof(Type)) }),
            "runtime-type-failure" => (
                new Func<object?>[] { () => DuckType.Create<IFallbackMissingProxy>(typeof(string))!.Missing },
                new[] { Mapping(typeof(IFallbackMissingProxy), typeof(Type)) }),
            _ => (
                new Func<object?>[]
                {
                    () =>
                    {
                        var task = FallbackAsyncValueAsync();
                        task.Wait();
                        return DuckType.Create<IFallbackTaskResultProxy>(task)!.Result + "|" + (task.GetType() != typeof(Task<int>));
                    },
                },
                new[] { Mapping(typeof(IFallbackTaskResultProxy), typeof(Task<int>)) }),
        };

        DuckType.ResetRuntimeModeForTests();
        var expected = exercises.Select(CaptureFailure).ToArray();
        WithGeneratedRegistry(
            () => exercises.Select(CaptureFailure).Should().Equal(expected, "AOT duck typing should behave like dynamic duck typing"),
            compatibilityAssertions: null,
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            mappings);
    }

    [Fact]
    public void GeneratedRegistryShouldReplayTheFailureOfTargetsWhoseNameDynamicDuckTypingCantUse()
    {
        // The dynamic assembly dynamic duck typing creates for a type that isn't visible is named after the type: an '=' in the
        // name (compiler-generated static array data) fails in every process, which the registry replays.
        var staticArrayDataType = typeof(DuckTypeAotAdditionalParityTests).Assembly.GetTypes().First(type => type.Name.StartsWith("__StaticArrayInitTypeSize=", StringComparison.Ordinal));
        Func<object?> exercise = () => DescribeDuckType(DuckType.Create<IFallbackEqualsProxy>(Activator.CreateInstance(staticArrayDataType)!));
        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureWithInnerException(exercise);
        expected.Should().StartWith("throws:", "dynamic duck typing can't create this proxy");
        WithGeneratedRegistry(
            () => CaptureWithInnerException(exercise).Should().Be(expected, "AOT duck typing should behave like dynamic duck typing"),
            matrix => matrix.Mappings.Should().ContainSingle().Which.DynamicFailureReplayed.Should().BeTrue(),
            new DuckTypeAotMapping(typeof(IFallbackEqualsProxy).FullName!, typeof(IFallbackEqualsProxy).Assembly.GetName().Name!, staticArrayDataType.FullName!, staticArrayDataType.Assembly.GetName().Name!, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile));
    }

    [Fact]
    public void GeneratedRegistryShouldEvaluateObjectMappingsWithDynamicDuckTypingWhenCoreLibIsATarget()
    {
        // Every type of the target assemblies derives from System.Object: the mapping is checked with dynamic duck typing, even
        // with the core library's types among its other runtime types.
        WithGeneratedRegistry(
            () => DuckType.Create<IFallbackEqualsProxy>(new FallbackElement())!.Equals(null).Should().BeFalse(),
            matrix => matrix.Mappings.Should().ContainSingle().Which.Should().Match<DuckTypeAotCompatibilityMapping>(mapping => mapping.Status == DuckTypeAotCompatibilityStatuses.Compatible && !mapping.CheckedAgainstMetadataOnly),
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            Mapping(typeof(IFallbackEqualsProxy), typeof(object)));
    }

    private static string DescribeDuckType(object? proxy)
        => proxy is IDuckType duckType
               ? $"{duckType.Type.Name}|{duckType.Instance?.GetType().Name}|{(proxy is IFallbackLengthProxy lengthProxy ? lengthProxy.Length : -1)}"
               : "null";

    private static async Task<int> FallbackAsyncValueAsync()
    {
        await Task.Yield();
        return 42;
    }

    public interface IFallbackLengthProxy
    {
        int Length { get; }
    }

    public interface IFallbackMissingProxy
    {
        int Length { get; }

        int Missing { get; }
    }

    public interface IFallbackExplicitCountProxy
    {
        [Duck(ExplicitInterfaceTypeName = "System.Collections.ICollection")]
        int Count { get; }
    }

    public interface IFallbackEqualsProxy
    {
        bool Equals(object? obj);
    }

    public interface IFallbackEnumerableProxy
    {
        IEnumerator GetEnumerator();
    }

    public interface IFallbackNameProxy
    {
        string Name { get; }
    }

    public interface IFallbackTaskResultProxy
    {
        int Result { get; }
    }

    [DuckCopy]
    public struct FallbackLengthCopy
    {
        public int Length;
        public int Rank;
    }

    public class FallbackElement
    {
    }

    public class FallbackDerivedElement : FallbackElement
    {
    }

    public class FallbackVirtualNameBase
    {
        public virtual string Name => "base";
    }
}
