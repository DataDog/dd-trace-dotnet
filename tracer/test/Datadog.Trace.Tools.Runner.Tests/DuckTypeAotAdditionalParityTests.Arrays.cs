// <copyright file="DuckTypeAotAdditionalParityTests.Arrays.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Linq;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Array targets: dynamic duck typing binds the members of System.Array for any array type, so one generated proxy per array
/// mapping serves every array type the instance can have at runtime.
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // Arrays of arrays (or of multi-dimensional arrays) are arrays of object.
    [InlineData("object-array-of-arrays", "2|String[][];3|Int32[][];4|Object[][];5|String[,][]")]
    // int[] is an IEnumerable: an int[][] is an IEnumerable[].
    [InlineData("interface-array-of-value-type-arrays", "2|Int32[][];3|String[]")]
    // A value type implementing the element interface: its array isn't an IArrayBaseElement[], dynamic duck typing creates its
    // proxy anyway.
    [InlineData("value-type-elements", "2|ArrayElementStruct[];1|ArrayDerivedElementImpl[]")]
    // CLR array covariance between an enum, uint and int.
    [InlineData("enum-and-unsigned-arrays", "3|ArrayElementEnum[];2|UInt32[];1|Int32[]")]
    // Rank-1 multi-dimensional arrays ("[*]") of a derived element type.
    [InlineData("non-sz-rank-one", "2|ArrayDerivedElement[*];3|ArrayBaseElement[*]")]
    public void GeneratedRegistryShouldServeEveryRuntimeArrayTypeLikeDynamicMode(string scenario, string expected)
    {
        var (arrays, mapping, extraTargets) = scenario switch
        {
            "object-array-of-arrays" => (new Array[] { new string[2][], new int[3][], new object[4][], new string[5][,] }, Mapping(typeof(ILengthProxy), typeof(object[])), new[] { typeof(object).Assembly.Location }),
            "interface-array-of-value-type-arrays" => (new Array[] { new int[2][], new string[3] }, Mapping(typeof(ILengthProxy), typeof(IEnumerable[])), new[] { typeof(object).Assembly.Location }),
            "value-type-elements" => (new Array[] { new ArrayElementStruct[2], new ArrayDerivedElementImpl[1] }, Mapping(typeof(ILengthProxy), typeof(IArrayBaseElement[])), Array.Empty<string>()),
            "enum-and-unsigned-arrays" => (new Array[] { new ArrayElementEnum[3], new uint[2], new int[1] }, Mapping(typeof(ILengthProxy), typeof(int[])), new[] { typeof(object).Assembly.Location }),
            _ => (
                new[] { Array.CreateInstance(typeof(ArrayDerivedElement), [2], [1]), Array.CreateInstance(typeof(ArrayBaseElement), [3], [1]) },
                new DuckTypeAotMapping(typeof(ILengthProxy).FullName!, typeof(ILengthProxy).Assembly.GetName().Name!, typeof(ArrayBaseElement).FullName + "[*]", typeof(ArrayBaseElement).Assembly.GetName().Name!, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile),
                Array.Empty<string>()),
        };

        Func<object?> exercise = () => string.Join(
            ";",
            arrays.Select(array =>
            {
                var proxy = DuckType.Create<ILengthProxy>(array)!;
                return FormattableString.Invariant($"{proxy.Length}|{((IDuckType)proxy).Type.Name}");
            }));

        DuckType.ResetRuntimeModeForTests();
        CaptureOutcome(exercise).Should().Be(expected, "dynamic duck typing should behave as expected");
        WithGeneratedRegistry(
            () => CaptureOutcome(exercise).Should().Be(expected, "AOT duck typing should behave like dynamic duck typing"),
            compatibilityAssertions: null,
            extraTargetAssemblies: extraTargets,
            mapping);
    }

    [Fact]
    public void GeneratedRegistryShouldServeArraysOfGeneratedReverseProxies()
    {
        AssertSameOutcome(
            "1|True;1",
            () =>
            {
                var reverse = DuckType.CreateReverse(typeof(IArrayReverseContract), new ArrayReverseDelegation());
                var array = Array.CreateInstance(reverse.GetType(), 1);
                var proxy = DuckType.Create<ILengthProxy>(array)!;
                var contracts = new[] { (IArrayReverseContract)reverse };
                return FormattableString.Invariant($"{proxy.Length}|{((IDuckType)proxy).Type == array.GetType()};{DuckType.Create<ILengthProxy>(contracts)!.Length}");
            },
            Mapping(typeof(IArrayReverseContract), typeof(ArrayReverseDelegation), reverse: true),
            Mapping(typeof(ILengthProxy), typeof(IArrayReverseContract[])));
    }

    [Fact]
    public void GeneratedRegistryShouldInitializeWithObjectMappingsOverCoreLib()
    {
        // With CoreLib among the target assemblies, every CoreLib type derives from System.Object, and object[] holds any array
        // of references: the registry must not register types it can't reference (System.__Canon, by-ref-like types...), or
        // its initialization fails for every mapping.
        AssertSameOutcomeWithTargets(
            "42|2",
            () => FormattableString.Invariant($"{DuckType.Create<IHashCodeProxy>(new HashCodeTarget())!.GetHashCode()}|{DuckType.Create<ILengthProxy>(new object[2][])!.Length}"),
            [typeof(object).Assembly.Location],
            Mapping(typeof(IHashCodeProxy), typeof(object)),
            Mapping(typeof(ILengthProxy), typeof(object[][])));
    }

    private static void AssertSameOutcomeWithTargets(string expected, Func<object?> exercise, string[] extraTargetAssemblies, params DuckTypeAotMapping[] mappings)
    {
        DuckType.ResetRuntimeModeForTests();
        CaptureOutcome(exercise).Should().Be(expected, "dynamic duck typing should behave as expected");
        WithGeneratedRegistry(
            () => CaptureOutcome(exercise).Should().Be(expected, "AOT duck typing should behave like dynamic duck typing"),
            compatibilityAssertions: null,
            extraTargetAssemblies,
            mappings);
    }

    public interface IHashCodeProxy
    {
        int GetHashCode();
    }

    public class HashCodeTarget
    {
        public override int GetHashCode() => 42;
    }

    public enum ArrayElementEnum
    {
        None,
    }

    public struct ArrayElementStruct : IArrayBaseElement
    {
    }

    public class ArrayDerivedElementImpl : IArrayDerivedElement
    {
    }

    public interface IArrayReverseContract
    {
        string Name { get; }
    }

    public class ArrayReverseDelegation
    {
        [DuckReverseMethod]
        public string Name => "n";
    }
}
