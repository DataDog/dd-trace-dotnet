// <copyright file="CallTargetAotHolderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget;

/// <summary>
/// Runs the same CallTarget scenarios through the dynamic handlers (IntegrationMapper) and through the NativeAOT
/// holders (callbacks registered the way a generated registry does), and checks both paths behave the same.
/// Every scenario uses its own target types because the handlers cache their callbacks per closed type.
/// </summary>
public class CallTargetAotHolderTests
{
    [Fact]
    public void BeginWithRefArgumentAndEndWithReturnValue()
    {
        var dynamic = Record(() => RunBeginEnd(new DynamicTarget1()));

        CallTargetAot<RefArgIntegration, BeginMethodHandler<RefArgIntegration, AotTarget1, int>.InvokeDelegate>.Register(
            (AotTarget1? instance, ref int arg1) => Recorder.Hit(RefArgIntegration.OnMethodBegin(instance, ref arg1)));
        CallTargetAot<RefArgIntegration, EndMethodHandler<RefArgIntegration, AotTarget1, int>.InvokeDelegate>.Register(
            (AotTarget1? instance, int returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RefArgIntegration.OnMethodEnd(instance, returnValue, exception, in state)));
        var aot = RecordAot(2, () => RunBeginEnd(new AotTarget1()));

        aot.Should().Equal(dynamic);
        dynamic.Should().Contain("arg after begin: 6").And.Contain("return: 43");

        static void RunBeginEnd<TTarget>(TTarget target)
        {
            var arg = 5;
            var state = CallTargetInvoker.BeginMethod<RefArgIntegration, TTarget, int>(target, ref arg);
            Recorder.Add($"arg after begin: {arg}");
            var result = CallTargetInvoker.EndMethod<RefArgIntegration, TTarget, int>(target, 42, null, in state);
            Recorder.Add($"return: {result.GetReturnValue()}");
        }
    }

    [Fact]
    public void VoidEndAndSlowBegin()
    {
        var dynamic = Record(() => RunSlow(new DynamicTarget2()));

        CallTargetAot<SlowIntegration, BeginMethodSlowHandler<SlowIntegration, AotTarget2>.InvokeDelegate>.Register(
            (instance, arguments) => Recorder.Hit(SlowIntegration.OnMethodBegin(instance, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7], arguments[8])));
        CallTargetAot<SlowIntegration, EndMethodHandler<SlowIntegration, AotTarget2>.InvokeDelegate>.Register(
            (AotTarget2? instance, Exception? exception, in CallTargetState state) => Recorder.Hit(SlowIntegration.OnMethodEnd(instance, exception, in state)));
        var aot = RecordAot(2, () => RunSlow(new AotTarget2()));

        aot.Should().Equal(dynamic);
        dynamic.Should().Contain(call => call.StartsWith("begin9", StringComparison.Ordinal));

        static void RunSlow<TTarget>(TTarget target)
        {
            var state = CallTargetInvoker.BeginMethod<SlowIntegration, TTarget>(target, new object[] { 1, "two", 3L, 4.0, '5', (byte)6, (short)7, 8u, null! });
            CallTargetInvoker.EndMethod<SlowIntegration, TTarget>(target, new InvalidOperationException("boom"), in state);
        }
    }

    [Fact]
    public async Task TaskOfTWithSyncContinuation()
    {
        var dynamic = await RecordAsync(() => RunTask(new DynamicTarget3(), CompleteLater(7)));

        RegisterTaskOfInt<AotTarget3, RecordingIntegration>(
            (AotTarget3? instance, int returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RecordingIntegration.OnAsyncMethodEnd(instance, returnValue, exception, in state)));
        var aot = await RecordAotAsync(3, () => RunTask(new AotTarget3(), CompleteLater(7)));

        aot.Should().Equal(dynamic);
        dynamic.Should().Contain("result: 70");
    }

    [Fact]
    public async Task TaskOfTFaultedAndNull()
    {
        var dynamic = await RecordAsync(async () =>
        {
            await RunTask(new DynamicTarget4(), FailLater());
            await RunTask(new DynamicTarget4(), null);
        });

        RegisterTaskOfInt<AotTarget4, RecordingIntegration>(
            (AotTarget4? instance, int returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RecordingIntegration.OnAsyncMethodEnd(instance, returnValue, exception, in state)));
        var aot = await RecordAotAsync(6, async () =>
        {
            await RunTask(new AotTarget4(), FailLater());
            await RunTask(new AotTarget4(), null);
        });

        aot.Should().Equal(dynamic);
        dynamic.Should().Contain("exception: async boom").And.Contain("returned task is null: True");
    }

    [Fact]
    public async Task TaskOfTWithAsyncContinuation()
    {
        var dynamic = await RecordAsync(() => RunTask<DynamicTarget5, AsyncRecordingIntegration>(new DynamicTarget5(), CompleteLater(3)));

        CallTargetAot<AsyncRecordingIntegration, BeginMethodHandler<AsyncRecordingIntegration, AotTarget5>.InvokeDelegate>.Register(null);
        CallTargetAot<AsyncRecordingIntegration, EndMethodHandler<AsyncRecordingIntegration, AotTarget5, Task<int>>.InvokeDelegate>.Register(null);
        CallTargetAot<AsyncRecordingIntegration, ContinuationGenerator<AotTarget5, Task<int>, int>.AsyncContinuationMethodDelegate>.Register(
            (AotTarget5? instance, int returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(AsyncRecordingIntegration.OnAsyncMethodEnd(instance, returnValue, exception, state)));
        CallTargetAotContinuation<AsyncRecordingIntegration, AotTarget5, Task<int>>.Register(new TaskContinuationGenerator<AsyncRecordingIntegration, AotTarget5, Task<int>, int>());
        var aot = await RecordAotAsync(1, () => RunTask<AotTarget5, AsyncRecordingIntegration>(new AotTarget5(), CompleteLater(3)));

        aot.Should().Equal(dynamic);
        dynamic.Should().Contain("result: 33");
    }

    [Fact]
    public async Task NonGenericTaskWithObjectContinuation()
    {
        var dynamic = await RecordAsync(() => RunVoidTask(new DynamicTarget6()));

        CallTargetAot<RecordingIntegration, BeginMethodHandler<RecordingIntegration, AotTarget6>.InvokeDelegate>.Register(
            instance => Recorder.Hit(RecordingIntegration.OnMethodBegin(instance)));
        CallTargetAot<RecordingIntegration, EndMethodHandler<RecordingIntegration, AotTarget6, Task>.InvokeDelegate>.Register(
            (AotTarget6? instance, Task? returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RecordingIntegration.OnMethodEnd<AotTarget6?, Task>(instance, returnValue!, exception, in state)));
        CallTargetAot<RecordingIntegration, ContinuationGenerator<AotTarget6, Task>.ObjectContinuationMethodDelegate>.Register(
            (AotTarget6? instance, object? returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RecordingIntegration.OnAsyncMethodEnd(instance, returnValue, exception, in state)));
        var aot = await RecordAotAsync(3, () => RunVoidTask(new AotTarget6()));

        aot.Should().Equal(dynamic);
        dynamic.Should().Contain(call => call.StartsWith("async end", StringComparison.Ordinal));

        static async Task RunVoidTask<TTarget>(TTarget target)
        {
            var state = CallTargetInvoker.BeginMethod<RecordingIntegration, TTarget>(target);
            var result = CallTargetInvoker.EndMethod<RecordingIntegration, TTarget, Task>(target, Task.Delay(10), null, in state);
            await result.GetReturnValue()!;
            Recorder.Add("completed");
        }
    }

    [Fact]
    public async Task IntegrationWithoutAsyncEndPassesTheTaskThrough()
    {
        var dynamic = await RecordAsync(() => RunTask<DynamicTarget7, EndOnlyIntegration>(new DynamicTarget7(), CompleteLater(5)));

        CallTargetAot<EndOnlyIntegration, BeginMethodHandler<EndOnlyIntegration, AotTarget7>.InvokeDelegate>.Register(null);
        CallTargetAot<EndOnlyIntegration, EndMethodHandler<EndOnlyIntegration, AotTarget7, Task<int>>.InvokeDelegate>.Register(
            (AotTarget7? instance, Task<int>? returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(EndOnlyIntegration.OnMethodEnd<AotTarget7?, Task<int>>(instance, returnValue!, exception, in state)));
        CallTargetAot<EndOnlyIntegration, ContinuationGenerator<AotTarget7, Task<int>, int>.ContinuationMethodDelegate>.Register(null);
        CallTargetAotContinuation<EndOnlyIntegration, AotTarget7, Task<int>>.Register(new TaskContinuationGenerator<EndOnlyIntegration, AotTarget7, Task<int>, int>());
        var aot = await RecordAotAsync(1, () => RunTask<AotTarget7, EndOnlyIntegration>(new AotTarget7(), CompleteLater(5)));

        aot.Should().Equal(dynamic);
        dynamic.Should().Contain("result: 5");
    }

#if NETCOREAPP3_1_OR_GREATER
    [Fact]
    public async Task ValueTaskOfTWithSyncContinuation()
    {
        var dynamic = await RecordAsync(() => RunValueTask(new DynamicTarget8()));

        CallTargetAot<RecordingIntegration, BeginMethodHandler<RecordingIntegration, AotTarget8>.InvokeDelegate>.Register(
            instance => Recorder.Hit(RecordingIntegration.OnMethodBegin(instance)));
        CallTargetAot<RecordingIntegration, EndMethodHandler<RecordingIntegration, AotTarget8, ValueTask<int>>.InvokeDelegate>.Register(
            (AotTarget8? instance, ValueTask<int> returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RecordingIntegration.OnMethodEnd(instance, returnValue, exception, in state)));
        CallTargetAot<RecordingIntegration, ContinuationGenerator<AotTarget8, ValueTask<int>, int>.ContinuationMethodDelegate>.Register(
            (AotTarget8? instance, int returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RecordingIntegration.OnAsyncMethodEnd(instance, returnValue, exception, in state)));
        CallTargetAotContinuation<RecordingIntegration, AotTarget8, ValueTask<int>>.Register(new ValueTaskContinuationGenerator<RecordingIntegration, AotTarget8, ValueTask<int>, int>());
        var aot = await RecordAotAsync(3, () => RunValueTask(new AotTarget8()));

        aot.Should().Equal(dynamic);
        dynamic.Should().Contain("result: 90");

        static async Task RunValueTask<TTarget>(TTarget target)
        {
            var state = CallTargetInvoker.BeginMethod<RecordingIntegration, TTarget>(target);
            var result = CallTargetInvoker.EndMethod<RecordingIntegration, TTarget, ValueTask<int>>(target, new ValueTask<int>(CompleteLater(9)), null, in state);
            Recorder.Add($"result: {await result.GetReturnValue()}");
        }
    }
#endif

#if NET6_0_OR_GREATER
    [Fact]
    public void RuntimeAsyncEndWithCallbacks()
    {
        var dynamic = Record(() => RunRuntimeAsync(new DynamicTarget9()));

        CallTargetAot<RecordingIntegration, BeginMethodHandler<RecordingIntegration, AotTarget9>.InvokeDelegate>.Register(
            instance => Recorder.Hit(RecordingIntegration.OnMethodBegin(instance)));
        CallTargetAot<RecordingIntegration, ContinuationGenerator<AotTarget9, int, int>.ContinuationMethodDelegate>.Register(
            (AotTarget9? instance, int returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RecordingIntegration.OnAsyncMethodEnd(instance, returnValue, exception, in state)));
        CallTargetAot<RecordingIntegration, EndMethodHandler<RecordingIntegration, AotTarget9, Task<int>>.InvokeDelegate>.Register(
            (AotTarget9? instance, Task<int>? returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RecordingIntegration.OnMethodEnd<AotTarget9?, Task<int>>(instance, returnValue!, exception, in state)));
        var aot = RecordAot(3, () => RunRuntimeAsync(new AotTarget9()));

        aot.Should().Equal(dynamic);
        dynamic.Should().Contain("runtime async return: 40");

        static void RunRuntimeAsync<TTarget>(TTarget target)
        {
            var state = CallTargetInvoker.BeginMethod<RecordingIntegration, TTarget>(target);
            var result = CallTargetInvoker.EndMethodRuntimeAsync<RecordingIntegration, TTarget, int, Task<int>>(target, 4, null, in state);
            Recorder.Add($"runtime async return: {result.GetReturnValue()}");
        }
    }
#endif

    private static void RegisterTaskOfInt<TTarget, TIntegration>(ContinuationGenerator<TTarget, Task<int>, int>.ContinuationMethodDelegate continuation)
    {
        CallTargetAot<TIntegration, BeginMethodHandler<TIntegration, TTarget>.InvokeDelegate>.Register(instance => Recorder.Hit(RecordingIntegration.OnMethodBegin(instance)));
        CallTargetAot<TIntegration, EndMethodHandler<TIntegration, TTarget, Task<int>>.InvokeDelegate>.Register(
            (TTarget? instance, Task<int>? returnValue, Exception? exception, in CallTargetState state) => Recorder.Hit(RecordingIntegration.OnMethodEnd<TTarget?, Task<int>>(instance, returnValue!, exception, in state)));
        CallTargetAot<TIntegration, ContinuationGenerator<TTarget, Task<int>, int>.ContinuationMethodDelegate>.Register(continuation);
        CallTargetAotContinuation<TIntegration, TTarget, Task<int>>.Register(new TaskContinuationGenerator<TIntegration, TTarget, Task<int>, int>());
    }

    private static async Task RunTask<TTarget>(TTarget target, Task<int>? task) => await RunTask<TTarget, RecordingIntegration>(target, task);

    private static async Task RunTask<TTarget, TIntegration>(TTarget target, Task<int>? task)
    {
        var state = CallTargetInvoker.BeginMethod<TIntegration, TTarget>(target);
        var result = CallTargetInvoker.EndMethod<TIntegration, TTarget, Task<int>>(target, task, null, in state);
        var returned = result.GetReturnValue();
        Recorder.Add($"returned task is null: {returned is null}");
        if (returned is null)
        {
            return;
        }

        try
        {
            Recorder.Add($"result: {await returned}");
        }
        catch (Exception ex)
        {
            Recorder.Add($"exception: {ex.Message}");
        }
    }

    private static async Task<int> CompleteLater(int value)
    {
        await Task.Delay(10).ConfigureAwait(false);
        return value;
    }

    private static async Task<int> FailLater()
    {
        await Task.Delay(10).ConfigureAwait(false);
        throw new InvalidOperationException("async boom");
    }

    private static List<string> Record(Action action)
    {
        lock (Recorder.Calls)
        {
            Recorder.Calls.Clear();
        }

        Recorder.ResetHits();
        action();
        return Recorder.Snapshot();
    }

    private static List<string> RecordAot(int expectedCallbacks, Action action)
    {
        var records = Record(action);
        Recorder.Hits.Should().Be(expectedCallbacks, "every registered NativeAOT callback must be used instead of IntegrationMapper");
        return records;
    }

    private static async Task<List<string>> RecordAsync(Func<Task> action)
    {
        lock (Recorder.Calls)
        {
            Recorder.Calls.Clear();
        }

        Recorder.ResetHits();
        await action();
        return Recorder.Snapshot();
    }

    private static async Task<List<string>> RecordAotAsync(int expectedCallbacks, Func<Task> action)
    {
        var records = await RecordAsync(action);
        Recorder.Hits.Should().Be(expectedCallbacks, "every registered NativeAOT callback must be used instead of IntegrationMapper");
        return records;
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal static class Recorder
    {
        internal static readonly List<string> Calls = new();

        private static int _hits;

        internal static int Hits => System.Threading.Volatile.Read(ref _hits);

        internal static void ResetHits() => System.Threading.Interlocked.Exchange(ref _hits, 0);

        internal static void Add(string call)
        {
            lock (Calls)
            {
                Calls.Add(call);
            }
        }

        internal static List<string> Snapshot()
        {
            lock (Calls)
            {
                return new List<string>(Calls);
            }
        }

        internal static string Describe<TTarget>(TTarget instance) => instance is null ? "null" : "target";

        // Marks that a callback registered in a NativeAOT holder ran, so the tests prove the AOT path was used
        // instead of the IntegrationMapper fallback (which would produce the same records under JIT).
        internal static T Hit<T>(T value)
        {
            System.Threading.Interlocked.Increment(ref _hits);
            return value;
        }

        internal static CallTargetReturn Hit(CallTargetReturn value)
        {
            System.Threading.Interlocked.Increment(ref _hits);
            return value;
        }

        internal static CallTargetReturn<T> Hit<T>(CallTargetReturn<T> value)
        {
            System.Threading.Interlocked.Increment(ref _hits);
            return value;
        }
    }

    // IntegrationMapper resolves OnMethodBegin/OnMethodEnd/OnAsyncMethodEnd by name, so each integration has one of each.
    internal sealed class RefArgIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 arg1)
        {
            Recorder.Add($"begin1 {Recorder.Describe(instance)} {typeof(TArg1).Name}={arg1}");
            if (arg1 is int value)
            {
                arg1 = (TArg1)(object)(value + 1);
            }

            return new CallTargetState(null, "state1");
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            Recorder.Add($"end {Recorder.Describe(instance)} {typeof(TReturn).Name}={returnValue} {exception?.Message} {state.State}");
            if (returnValue is int value)
            {
                return new CallTargetReturn<TReturn>((TReturn)(object)(value + 1));
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }

    internal sealed class SlowIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9)
        {
            Recorder.Add($"begin9 {Recorder.Describe(instance)} {typeof(TArg1).Name}={arg1} {typeof(TArg2).Name}={arg2} {typeof(TArg9).Name}={arg9 is null}");
            return new CallTargetState(null, "state9");
        }

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        {
            Recorder.Add($"end void {Recorder.Describe(instance)} {exception?.Message} {state.State}");
            return CallTargetReturn.GetDefault();
        }
    }

    internal sealed class RecordingIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            Recorder.Add($"begin0 {Recorder.Describe(instance)}");
            return new CallTargetState(null, "state0");
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            Recorder.Add($"end {Recorder.Describe(instance)} {typeof(TReturn).Name} {exception?.Message} {state.State}");
            return new CallTargetReturn<TReturn>(returnValue);
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            Recorder.Add($"async end {Recorder.Describe(instance)} {typeof(TReturn).Name}={returnValue} {exception?.Message} {state.State}");
            if (returnValue is int value)
            {
                return (TReturn)(object)(value * 10);
            }

            return returnValue;
        }
    }

    internal sealed class AsyncRecordingIntegration
    {
        internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, CallTargetState state)
        {
            await Task.Delay(5).ConfigureAwait(false);
            Recorder.Add($"async task end {typeof(TReturn).Name}={returnValue} {exception?.Message}");
            if (returnValue is int value)
            {
                return (TReturn)(object)(value * 11);
            }

            return returnValue;
        }
    }

    internal sealed class EndOnlyIntegration
    {
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            Recorder.Add($"end only {typeof(TReturn).Name} {exception?.Message}");
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }

    internal sealed class DynamicTarget1;

    internal sealed class AotTarget1;

    internal sealed class DynamicTarget2;

    internal sealed class AotTarget2;

    internal sealed class DynamicTarget3;

    internal sealed class AotTarget3;

    internal sealed class DynamicTarget4;

    internal sealed class AotTarget4;

    internal sealed class DynamicTarget5;

    internal sealed class AotTarget5;

    internal sealed class DynamicTarget6;

    internal sealed class AotTarget6;

    internal sealed class DynamicTarget7;

    internal sealed class AotTarget7;

    internal sealed class DynamicTarget8;

    internal sealed class AotTarget8;

    internal sealed class DynamicTarget9;

    internal sealed class AotTarget9;
#pragma warning restore SA1402
}
