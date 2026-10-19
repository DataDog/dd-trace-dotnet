// <copyright file="AotCodeOriginLocationsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.Tools.Runner.Aot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The source locations of the endpoint methods a NativeAOT build records for Code Origin for spans (the application has
/// neither PDBs nor metadata tokens at runtime) are found by the key the runtime computes by reflection.
/// </summary>
public class AotCodeOriginLocationsTests
{
    private static readonly string TestAssemblyPath = typeof(AotCodeOriginLocationsTests).Assembly.Location;
    private static readonly ModuleDefMD TestModule = ModuleDefMD.Load(TestAssemblyPath);

    // A minimal API handler (a lambda of a compiler-generated class), and its line.
    private static readonly (Func<int, string> Handler, int Line) Endpoint = (id => id.ToString(CultureInfo.InvariantCulture), CurrentLine());

    [Theory]
    [InlineData(typeof(Handlers), nameof(Handlers.NoParameters))]
    [InlineData(typeof(Handlers), nameof(Handlers.Primitives))]
    [InlineData(typeof(Handlers), nameof(Handlers.ByRef))]
    [InlineData(typeof(Handlers), nameof(Handlers.Arrays))]
    [InlineData(typeof(Handlers), nameof(Handlers.GenericInstances))]
    [InlineData(typeof(Handlers), nameof(Handlers.Nested))]
    [InlineData(typeof(Handlers), nameof(Handlers.GenericMethod))]
    [InlineData(typeof(Handlers), nameof(Handlers.Pointer))]
    [InlineData(typeof(Handlers.Generic<>), nameof(Handlers.Generic<int>.Handle))]
    public void KeysAreTheRuntimeKeys(Type type, string name)
    {
        var method = type.GetMethod(name)!;
        CodeOriginLocations.GetKey((MethodDef)TestModule.ResolveToken(method.MetadataToken)).Should().Be(SpanCodeOrigin.GetBuildTimeKey(method));
    }

    [Fact]
    public void OverloadsHaveTheirOwnKeys()
    {
        var keys = typeof(Handlers).GetMethods().Where(m => m.Name == nameof(Handlers.Overload)).Select(m => CodeOriginLocations.GetKey((MethodDef)TestModule.ResolveToken(m.MetadataToken))).ToList();
        keys.Should().HaveCount(2).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void LocationsOfEndpointMethodsAreCollected()
    {
        var entries = new List<string>();
        CodeOriginLocations.CollectEndpoints(TestModule, TestAssemblyPath, "Tests", entries).Should().BeGreaterThan(0);

        var key = SpanCodeOrigin.GetBuildTimeKey(Endpoint.Handler.Method);
        var index = Enumerable.Range(0, entries.Count / 5).Select(i => i * 5).Single(i => entries[i + 1] == key);
        entries[index].Should().Be("Tests");
        entries[index + 2].Should().EndWith($"/{nameof(AotCodeOriginLocationsTests)}.cs");
        entries[index + 3].Should().Be(Endpoint.Line.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void DatadogAndThirdPartyAssembliesAreSkipped()
        => CodeOriginLocations.Collect(TestModule, TestAssemblyPath, []).Should().Be(0);

    private static int CurrentLine([CallerLineNumber] int line = 0) => line;

    public static class Handlers
    {
        public static void NoParameters()
        {
        }

        public static void Primitives(int number, string text, bool flag, object value)
        {
        }

        public static void ByRef(ref int number, out string text, in long value) => text = string.Empty;

        public static void Arrays(int[] numbers, string[,] matrix, int[][] jagged)
        {
        }

        public static void GenericInstances(List<int> numbers, Dictionary<string, List<int>> map, int? optional)
        {
        }

        public static void Nested(Inner inner, Generic<string> generic)
        {
        }

        public static void GenericMethod<TValue>(TValue value, List<TValue> values)
        {
        }

        public static unsafe void Pointer(int* number)
        {
        }

        public static void Overload(int number)
        {
        }

        public static void Overload(string text)
        {
        }

        public class Inner
        {
        }

        public class Generic<T>
        {
            public void Handle(T value, List<T> values)
            {
            }
        }
    }
}
#endif
