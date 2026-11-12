// <copyright file="LocationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST;

public class LocationTests
{
    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored()
    {
        var method = "GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable";
        var typeName = "Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests";
        var location = new Location(typeName, method, 23, 4);
        location.Class.Should().Be(typeName);
        location.Method.Should().Be(method);
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored2()
    {
        var method = "GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable";
        var location = new Location(null, method, null, 4);
        location.Class.Should().BeNull();
        location.Method.Should().Be("GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable");
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored3()
    {
        var method = "Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests.GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable";
        var location = new Location(null, method, 23, 4);
        location.Class.Should().BeNull();
        location.Method.Should().Be("Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests.GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable");
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromNull_NothingIsStored()
    {
        var location = new Location(null, null, 23, 4);
        location.Class.Should().BeNull();
        location.Method.Should().BeNull();
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromStackFrame_ValueIsExpected()
    {
        var stack = new StackTrace(fNeedFileInfo: true);
        var frame = stack.GetFrame(0);
        var location = new Location(frame, stack, null, null);
        location.Class.Should().Be("Datadog.Trace.Security.Unit.Tests.IAST.LocationTests");
        location.Method.Should().Be("GivenALocation_WhenCreatedFromStackFrame_ValueIsExpected");
        location.Path.Should().Be("LocationTests.cs");
    }

    [Fact]
    public void GivenAnIdentityFrame_WhenCreatedFromStackFrame_TheHashComesFromItButTheValuesDoNot()
    {
        var stack = new StackTrace(fNeedFileInfo: true);
        var frame = stack.GetFrame(0);
        var identityFrame = GetIdentityFrame();

        var location = new Location(frame, stack, null, null, identityFrame);

        location.Class.Should().Be("Datadog.Trace.Security.Unit.Tests.IAST.LocationTests");
        location.Method.Should().Be(nameof(GivenAnIdentityFrame_WhenCreatedFromStackFrame_TheHashComesFromItButTheValuesDoNot));
        location.GetHashCode().Should().Be(new Location("Datadog.Trace.Security.Unit.Tests.IAST.LocationTests", nameof(GetIdentityFrame), null, null).GetHashCode());
    }

    [Fact]
    public async Task GivenAnAsyncMethod_WhenCreatedFromItsFrame_TheSourceMethodIsReported()
    {
        var location = await GetLocationFromAsyncMethod();

        location.Class.Should().Be(typeof(LocationTests).FullName);
        location.Method.Should().Be(nameof(GetLocationFromAsyncMethod));
        location.Path.Should().Be("LocationTests.cs");
    }

    [Fact]
    public void GivenAnIterator_WhenCreatedFromItsFrame_TheSourceMethodIsReported()
    {
        var location = GetLocationFromIterator().Single();

        location.Class.Should().Be(typeof(LocationTests).FullName);
        location.Method.Should().Be(nameof(GetLocationFromIterator));
    }

#if NETCOREAPP3_0_OR_GREATER
    [Fact]
    public async Task GivenAnAsyncIterator_WhenCreatedFromItsFrame_TheSourceMethodIsReported()
    {
        var locations = new List<Location>();
        await foreach (var item in GetLocationFromAsyncIterator())
        {
            locations.Add(item);
        }

        locations.Should().ContainSingle();
        locations[0].Class.Should().Be(typeof(LocationTests).FullName);
        locations[0].Method.Should().Be(nameof(GetLocationFromAsyncIterator));
    }
#endif

    [Fact]
    public async Task GivenAnAsyncLocalFunction_WhenCreatedFromItsFrame_TheLocalFunctionIsReported()
    {
        var location = await GetLocationFromLocalFunction();

        location.Class.Should().Be(typeof(LocationTests).FullName);
        location.Method.Should().StartWith($"<{nameof(GetLocationFromLocalFunction)}>g__Local");
    }

    [Fact]
    public async Task GivenAnAsyncLambda_WhenCreatedFromItsFrame_TheLambdaIsReported()
    {
        var suffix = "suffix";
        Func<Task<Location>> lambda = async () =>
        {
            await Task.Yield();
            suffix.Should().NotBeNull(); // captured, so the lambda lives in a display class
            return CreateLocationFromCaller();
        };

        var location = await lambda();

        location.Class.Should().StartWith(typeof(LocationTests).FullName + "+<>c__DisplayClass");
        location.Method.Should().StartWith($"<{nameof(GivenAnAsyncLambda_WhenCreatedFromItsFrame_TheLambdaIsReported)}>b__");
    }

    [Fact]
    public async Task GivenAnAsyncMethodOfAGenericType_WhenCreatedFromItsFrame_TheSourceMethodIsReported()
    {
        var location = await GenericHolder<string>.GetLocationFromAsyncMethod();

        location.Class.Should().Be(typeof(GenericHolder<>).FullName);
        location.Method.Should().Be(nameof(GenericHolder<string>.GetLocationFromAsyncMethod));
    }

    [Fact]
    public async Task GivenAnAsyncMethodOfAGenericTypeOfAValueType_WhenCreatedFromItsFrame_TheSourceMethodIsReported()
    {
        var location = await GenericHolder<int>.GetLocationFromAsyncMethod();

        location.Class.Should().Be(typeof(GenericHolder<>).FullName);
        location.Method.Should().Be(nameof(GenericHolder<int>.GetLocationFromAsyncMethod));
    }

    [Fact]
    public async Task GivenAGenericAsyncMethodOfAValueType_WhenCreatedFromItsFrame_TheSourceMethodIsReported()
    {
        var location = await GetLocationFromGenericAsyncMethod<int>();

        location.Class.Should().Be(typeof(LocationTests).FullName);
        location.Method.Should().Be(nameof(GetLocationFromGenericAsyncMethod));
    }

    [Fact]
    public async Task GivenTheSameStateMachine_WhenItIsResolvedAgain_TheSameSourceMethodIsReported()
    {
        var first = await GetLocationFromAsyncMethod();
        var second = await GetLocationFromAsyncMethod();

        second.Class.Should().Be(first.Class).And.Be(typeof(LocationTests).FullName);
        second.Method.Should().Be(first.Method).And.Be(nameof(GetLocationFromAsyncMethod));
    }

    [Fact]
    public async Task GivenAnAsyncMethod_WhenCreatedFromItsFrame_TheHashStillComesFromTheStateMachine()
    {
        var location = await GetLocationFromAsyncMethod();

        // the hash identifies the vulnerability in the backend, so it must not change with the reported names
        var stateMachineType = typeof(LocationTests).GetNestedTypes(BindingFlags.NonPublic).Single(t => t.Name.StartsWith($"<{nameof(GetLocationFromAsyncMethod)}>"));
        location.GetHashCode().Should().Be(new Location(stateMachineType.FullName, "MoveNext", null, null).GetHashCode());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static StackFrame GetIdentityFrame() => new StackFrame(0, false);

    // the frame of the caller, which is the MoveNext of a state machine for the methods below
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Location CreateLocationFromCaller()
    {
        var stack = new StackTrace(1, fNeedFileInfo: true);
        return new Location(stack.GetFrame(0), stack, null, null);
    }

    private static async Task<Location> GetLocationFromAsyncMethod()
    {
        await Task.Yield();
        return CreateLocationFromCaller();
    }

    private static async Task<Location> GetLocationFromGenericAsyncMethod<T>()
    {
        await Task.Yield();
        return CreateLocationFromCaller();
    }

    private static IEnumerable<Location> GetLocationFromIterator()
    {
        yield return CreateLocationFromCaller();
    }

#if NETCOREAPP3_0_OR_GREATER
    private static async IAsyncEnumerable<Location> GetLocationFromAsyncIterator()
    {
        await Task.Yield();
        yield return CreateLocationFromCaller();
    }
#endif

    private static Task<Location> GetLocationFromLocalFunction()
    {
        return Local();

        static async Task<Location> Local()
        {
            await Task.Yield();
            return CreateLocationFromCaller();
        }
    }

    private static class GenericHolder<T>
    {
        public static async Task<Location> GetLocationFromAsyncMethod()
        {
            await Task.Yield();
            return CreateLocationFromCaller();
        }
    }
}
