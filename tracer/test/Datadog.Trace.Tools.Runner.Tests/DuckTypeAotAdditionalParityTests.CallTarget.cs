// <copyright file="DuckTypeAotAdditionalParityTests.CallTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The CallTarget handlers (IntegrationMapper) with the proxies of a registry: they create the proxies of the instance, the
/// arguments and the return value of the instrumented method with `newobj ProxyType.GetConstructors()[0]`, the target on the
/// stack, and unwrap returned proxies.
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    private static readonly List<string> CallTargetObservations = [];

    public delegate CallTargetReturn<TReturn> CallTargetEndDelegate<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state);

    [Theory]
    [InlineData("instance-class-target", "name=target type=CallTargetNameTarget")]
    [InlineData("instance-value-type-target", "twice=10 type=CallTargetValueTarget")]
    // Targets of the core library whose proxy also serves other runtime types (classes that aren't sealed, arrays).
    [InlineData("argument-list", "count=2 type=List`1")]
    [InlineData("argument-array", "length=3 type=String[]")]
    [InlineData("argument-stream", "length=4 type=MemoryStream")]
    // Another array type, served by the proxy of the registered one: the proxy reports the target type.
    [InlineData("argument-other-array-type", "length=2 type=Int32[]")]
    [InlineData("return-list", "count=1 type=List`1|same=True")]
    [InlineData("async-return-list", "count=1 type=List`1|same=True")]
    public void GeneratedRegistryShouldServeCallTargetHandlersLikeDynamicMode(string scenario, string expected)
    {
        var (exercise, mappings) = scenario switch
        {
            "instance-class-target" => (
                Run(() => CallTargetBegin(typeof(CallTargetInstanceIntegration), typeof(CallTargetNameTarget), new CallTargetNameTarget(), [])),
                new[] { Mapping(typeof(ICallTargetNameProxy), typeof(CallTargetNameTarget)) }),
            "instance-value-type-target" => (
                Run(() => CallTargetBegin(typeof(CallTargetValueInstanceIntegration), typeof(CallTargetValueTarget), new CallTargetValueTarget { Value = 5 }, [])),
                new[] { Mapping(typeof(ICallTargetTwiceProxy), typeof(CallTargetValueTarget)) }),
            "argument-list" => (
                Run(() => CallTargetBegin(typeof(CallTargetCountArgumentIntegration), typeof(CallTargetNameTarget), new CallTargetNameTarget(), [typeof(List<string>)], new List<string> { "a", "b" })),
                new[] { Mapping(typeof(ICallTargetCountProxy), typeof(List<string>)) }),
            "argument-array" => (
                Run(() => CallTargetBegin(typeof(CallTargetLengthArgumentIntegration), typeof(CallTargetNameTarget), new CallTargetNameTarget(), [typeof(string[])], new object?[] { new[] { "a", "b", "c" } })),
                new[] { Mapping(typeof(ICallTargetLengthProxy), typeof(string[])) }),
            "argument-stream" => (
                Run(() => CallTargetBegin(typeof(CallTargetStreamArgumentIntegration), typeof(CallTargetNameTarget), new CallTargetNameTarget(), [typeof(MemoryStream)], new MemoryStream(new byte[4]))),
                new[] { Mapping(typeof(ICallTargetStreamLengthProxy), typeof(MemoryStream)) }),
            "argument-other-array-type" => (
                Run(() => CallTargetBegin(typeof(CallTargetLengthArgumentIntegration), typeof(CallTargetNameTarget), new CallTargetNameTarget(), [typeof(int[])], new[] { 1, 2 })),
                new[] { Mapping(typeof(ICallTargetLengthProxy), typeof(string[])) }),
            "return-list" => (
                Run(() => CallTargetEnd(typeof(CallTargetCountReturnIntegration), new CallTargetNameTarget(), new List<string> { "x" })),
                new[] { Mapping(typeof(ICallTargetCountProxy), typeof(List<string>)) }),
            "async-return-list" => (
                Run(() => CallTargetAsyncEnd(typeof(CallTargetCountAsyncReturnIntegration), typeof(CallTargetNameTarget), typeof(List<string>), new CallTargetNameTarget(), new List<string> { "x" })),
                new[] { Mapping(typeof(ICallTargetCountProxy), typeof(List<string>)) }),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        DuckType.ResetRuntimeModeForTests();
        var dynamicOutcome = CaptureFailure(exercise);
        dynamicOutcome.Should().Be(expected);
        string? aotOutcome = null;
        WithGeneratedRegistry(() => aotOutcome = CaptureFailure(exercise), compatibilityAssertions: null, extraTargetAssemblies: [typeof(object).Assembly.Location], mappings);
        aotOutcome.Should().Be(dynamicOutcome);
    }

    [Fact]
    public void GeneratedProxiesServingOtherRuntimeTypesShouldHaveAPublicConstructorTakingTheInstance()
    {
        // Like the proxy dynamic duck typing creates (code such as IntegrationMapper or the Activity listeners calls the first
        // public constructor with the target): the constructor receiving the served runtime type is internal.
        WithGeneratedRegistry(
            () =>
            {
                // Another array type is served by the proxy of the registered one, whose public constructor reports the registered
                // type (IntegrationMapper passes the target type to the internal constructor instead, see the theory above).
                var cases = new[]
                {
                    (Proxy: typeof(ICallTargetCountProxy), Target: typeof(List<string>), Instance: (object)new List<string>(), ReportedType: typeof(List<string>)),
                    (Proxy: typeof(ICallTargetLengthProxy), Target: typeof(string[]), Instance: new string[1], ReportedType: typeof(string[])),
                    (Proxy: typeof(ICallTargetLengthProxy), Target: typeof(int[]), Instance: new int[1], ReportedType: typeof(string[])),
                };
                foreach (var (proxy, target, instance, reportedType) in cases)
                {
                    var constructor = DuckType.GetOrCreateProxyType(proxy, target).ProxyType!.GetConstructors().Should().ContainSingle().Subject;
                    constructor.GetParameters().Should().ContainSingle().Which.ParameterType.IsAssignableFrom(target).Should().BeTrue();
                    ((IDuckType)constructor.Invoke([instance])).Type.Should().Be(reportedType);
                }
            },
            compatibilityAssertions: null,
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            Mapping(typeof(ICallTargetCountProxy), typeof(List<string>)),
            Mapping(typeof(ICallTargetLengthProxy), typeof(string[])));
    }

    // Like the IL the native profiler injects: the handler's dynamic method, invoked with the target and the arguments.
    private static object? CallTargetBegin(Type integration, Type target, object? instance, Type[] argumentTypes, params object?[] arguments)
    {
        var handler = IntegrationMapper.CreateBeginMethodDelegate(integration, target, argumentTypes.Select(type => type.MakeByRefType()).ToArray());
        if (handler is null)
        {
            return "no handler";
        }

        CallTargetObservations.Clear();
        handler.Invoke(null, new[] { instance }.Concat(arguments).ToArray());
        return string.Join("|", CallTargetObservations);
    }

    private static object? CallTargetEnd<TTarget, TReturn>(Type integration, TTarget instance, TReturn returnValue)
    {
        var handler = IntegrationMapper.CreateEndMethodDelegate(integration, typeof(TTarget), typeof(TReturn));
        if (handler is null)
        {
            return "no handler";
        }

        CallTargetObservations.Clear();
        var state = CallTargetState.GetDefault();
        var returned = ((CallTargetEndDelegate<TTarget, TReturn>)handler.CreateDelegate(typeof(CallTargetEndDelegate<TTarget, TReturn>)))(instance, returnValue, null, in state).GetReturnValue();
        return string.Join("|", CallTargetObservations) + $"|same={ReferenceEquals(returned, returnValue)}";
    }

    private static object? CallTargetAsyncEnd(Type integration, Type target, Type result, object? instance, object? returnValue)
    {
        var handler = IntegrationMapper.CreateAsyncEndMethodDelegate(integration, target, result);
        if (handler.Method is null)
        {
            return "no handler";
        }

        CallTargetObservations.Clear();
        var returned = handler.Method.Invoke(null, [instance, returnValue, null, CallTargetState.GetDefault()]);
        if (returned is Task task)
        {
            task.GetAwaiter().GetResult();
            returned = task.GetType().GetProperty("Result")!.GetValue(task);
        }

        return string.Join("|", CallTargetObservations) + $"|same={ReferenceEquals(returned, returnValue)}";
    }

    public class CallTargetNameTarget
    {
        public string Name => "target";
    }

    public struct CallTargetValueTarget
    {
        public int Value;

        public int Twice => Value * 2;
    }

    public interface ICallTargetNameProxy
    {
        string Name { get; }
    }

    public interface ICallTargetTwiceProxy
    {
        int Twice { get; }
    }

    public interface ICallTargetCountProxy
    {
        int Count { get; }
    }

    public interface ICallTargetLengthProxy
    {
        int Length { get; }
    }

    public interface ICallTargetStreamLengthProxy
    {
        long Length { get; }
    }

    public static class CallTargetInstanceIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : ICallTargetNameProxy
        {
            CallTargetObservations.Add($"name={instance.Name} type={((IDuckType)instance).Type.Name}");
            return CallTargetState.GetDefault();
        }
    }

    public static class CallTargetValueInstanceIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : ICallTargetTwiceProxy
        {
            CallTargetObservations.Add($"twice={instance.Twice} type={((IDuckType)instance).Type.Name}");
            return CallTargetState.GetDefault();
        }
    }

    public static class CallTargetCountArgumentIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 argument)
            where TArg1 : ICallTargetCountProxy
        {
            CallTargetObservations.Add($"count={argument.Count} type={((IDuckType)argument).Type.Name}");
            return CallTargetState.GetDefault();
        }
    }

    public static class CallTargetLengthArgumentIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 argument)
            where TArg1 : ICallTargetLengthProxy
        {
            CallTargetObservations.Add($"length={argument.Length} type={((IDuckType)argument).Type.Name}");
            return CallTargetState.GetDefault();
        }
    }

    public static class CallTargetStreamArgumentIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 argument)
            where TArg1 : ICallTargetStreamLengthProxy
        {
            CallTargetObservations.Add($"length={argument.Length} type={((IDuckType)argument).Type.Name}");
            return CallTargetState.GetDefault();
        }
    }

    public static class CallTargetCountReturnIntegration
    {
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
            where TReturn : ICallTargetCountProxy, IDuckType
        {
            CallTargetObservations.Add($"count={returnValue.Count} type={returnValue.Type.Name}");
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }

    public static class CallTargetCountAsyncReturnIntegration
    {
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
            where TReturn : ICallTargetCountProxy, IDuckType
        {
            CallTargetObservations.Add($"count={returnValue.Count} type={returnValue.Type.Name}");
            return returnValue;
        }
    }
}
