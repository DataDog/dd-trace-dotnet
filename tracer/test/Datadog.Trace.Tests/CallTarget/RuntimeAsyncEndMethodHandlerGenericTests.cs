// <copyright file="RuntimeAsyncEndMethodHandlerGenericTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Runtime-async only exists on .NET 10+, so there is no point exercising this handler on any other
// target - the code path it serves can never be reached there.
#if NET10_0_OR_GREATER

using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget;

/// <summary>
/// Covers <c>RuntimeAsyncEndMethodHandler&lt;TIntegration, TTarget, TReturn, TDeclaredReturn&gt;</c>,
/// the epilog handler for a runtime-async method declaring <see cref="Task{TResult}"/> or
/// <c>ValueTask&lt;TResult&gt;</c>. <c>TReturn</c> is the unwrapped result the body actually leaves
/// on the evaluation stack; <c>TDeclaredReturn</c> is what the signature says.
/// </summary>
/// <remarks>
/// The integration types here are deliberately separate from those in
/// <see cref="RuntimeAsyncEndMethodHandlerTests"/>: the handler binds its delegate once per closed
/// generic instantiation, so sharing a type across files would share a binding.
/// <para>
/// Several tests route the invoker call through a small non-async helper. That is not incidental -
/// <c>CallTargetReturn&lt;T&gt;</c> is a <c>ref struct</c>, which cannot appear as a local in an
/// async method, so a test that needs to await the captured task must keep the two apart.
/// </para>
/// </remarks>
public class RuntimeAsyncEndMethodHandlerGenericTests
{
    [Fact]
    public void OnAsyncMethodEnd_OnSuccess_ReceivesTheUnwrappedResult()
    {
        AsyncEndIntegration.Reset();
        var target = new TestTarget();
        var state = CallTargetState.GetDefault();

        var returned = CallTargetInvoker
                      .EndMethodRuntimeAsync<AsyncEndIntegration, TestTarget, string, Task<string>>(target, "value", null, in state)
                      .GetReturnValue();

        AsyncEndIntegration.Calls.Should().Be(1);
        AsyncEndIntegration.Instance.Should().BeSameAs(target);
        AsyncEndIntegration.Exception.Should().BeNull();

        // The callback sees the unwrapped T, exactly as TaskContinuationGenerator passes it for a
        // state-machine target - so duck typing and generic constraints behave identically.
        AsyncEndIntegration.ReturnValue.Should().Be("value");
        returned.Should().Be("value");
    }

    [Fact]
    public void OnAsyncMethodEnd_CanSubstituteTheResult()
    {
        var state = CallTargetState.GetDefault();

        var returned = CallTargetInvoker
                      .EndMethodRuntimeAsync<SubstitutingAsyncEndIntegration, TestTarget, string, Task<string>>(new TestTarget(), "value", null, in state)
                      .GetReturnValue();

        // The rewriter stores this back into the return local, so the substituted value really is
        // what the instrumented method returns. This is the capability OnMethodEnd cannot offer.
        returned.Should().Be("value[Modified]");
    }

    [Fact]
    public void OnAsyncMethodEnd_OnException_ReceivesTheException()
    {
        AsyncEndIntegration.Reset();
        var exception = new InvalidOperationException("boom");
        var state = CallTargetState.GetDefault();

        CallTargetInvoker.EndMethodRuntimeAsync<AsyncEndIntegration, TestTarget, string, Task<string>>(new TestTarget(), null, exception, in state);

        AsyncEndIntegration.Calls.Should().Be(1);
        AsyncEndIntegration.Exception.Should().BeSameAs(exception);
    }

    [Fact]
    public async Task OnMethodEnd_OnSuccess_ReceivesACompletedTaskCarryingTheResult()
    {
        MethodEndIntegration.Reset();

        InvokeMethodEndWithTask("value", exception: null);

        MethodEndIntegration.Calls.Should().Be(1);
        MethodEndIntegration.Exception.Should().BeNull();

        // OnMethodEnd is bound against the declared Task<string>, not the unwrapped string, so an
        // integration sees the same shape whether or not its target happens to be runtime-async.
        var declared = MethodEndIntegration.ReturnValue.Should().BeAssignableTo<Task<string>>().Subject;
        declared.IsCompletedSuccessfully.Should().BeTrue();
        (await declared).Should().Be("value");
    }

    [Fact]
    public void OnMethodEnd_OnException_ReceivesNoTask()
    {
        MethodEndIntegration.Reset();
        var exception = new InvalidOperationException("boom");

        InvokeMethodEndWithTask(null, exception);

        MethodEndIntegration.Calls.Should().Be(1);
        MethodEndIntegration.Exception.Should().BeSameAs(exception);

        // Not a faulted task - the exception travels in the exception argument.
        MethodEndIntegration.ReturnValue.Should().BeNull();
    }

    [Fact]
    public async Task OnMethodEnd_WithDeclaredValueTask_ReceivesACompletedValueTaskCarryingTheResult()
    {
        MethodEndIntegration.Reset();

        InvokeMethodEndWithValueTask(42);

        MethodEndIntegration.Calls.Should().Be(1);
        MethodEndIntegration.ReturnValue.Should().BeOfType<ValueTask<int>>();

        var declared = (ValueTask<int>)MethodEndIntegration.ReturnValue;
        declared.IsCompletedSuccessfully.Should().BeTrue();
        (await declared).Should().Be(42);
    }

    [Fact]
    public void OnMethodEnd_ReturnValueIsDiscarded()
    {
        var state = CallTargetState.GetDefault();

        var returned = CallTargetInvoker
                      .EndMethodRuntimeAsync<SubstitutingMethodEndIntegration, TestTarget, string, Task<string>>(new TestTarget(), "value", null, in state)
                      .GetReturnValue();

        // OnMethodEnd returns a Task, and a runtime-async body has no task slot to put a
        // replacement into - the body returns the unwrapped value. So whatever OnMethodEnd hands
        // back is dropped and the original value flows on. That is inherent, not a choice; an
        // integration that needs to substitute a result must use OnAsyncMethodEnd.
        returned.Should().Be("value");
    }

    [Fact]
    public async Task WhenBothCallbacksAreDeclared_BothRunAndTheSubstitutedValueFlowsForward()
    {
        BothCallbacksIntegration.Reset();

        var returned = InvokeBothCallbacks("value");

        // EndMethodHandler runs its continuation generator and then OnMethodEnd, so we do too -
        // TraceAnnotationsIntegration declares both, and [Trace] can target a runtime-async method.
        BothCallbacksIntegration.AsyncCalls.Should().Be(1);
        BothCallbacksIntegration.SyncCalls.Should().Be(1);
        returned.Should().Be("value[Async]");

        // OnAsyncMethodEnd substituted, so the task handed to OnMethodEnd carries the new value -
        // matching EndMethodHandler, where OnMethodEnd sees the continuation generator's output.
        var declared = BothCallbacksIntegration.SyncReturnValue.Should().BeAssignableTo<Task<string>>().Subject;
        (await declared).Should().Be("value[Async]");
    }

    [Fact]
    public void WhenOnAsyncMethodEndIsItselfAsync_DisablesTheIntegrationAndLeavesTheResultUnchanged()
    {
        AsyncCallbackIntegration.Reset();
        var state = CallTargetState.GetDefault();

        IntegrationOptions<AsyncCallbackIntegration, TestTarget>.IsIntegrationEnabled.Should().BeTrue();

        var returned = CallTargetInvoker
                      .EndMethodRuntimeAsync<AsyncCallbackIntegration, TestTarget, string, Task<string>>(new TestTarget(), "value", null, in state)
                      .GetReturnValue();

        // The epilog runs inside a finally and the runtime-async spec forbids suspending there, so
        // an async callback cannot be honoured. Skipping it is not enough on its own: OnMethodBegin
        // has already run and created state that only that callback would clean up, so the
        // integration is disabled for this target rather than left leaking once per call.
        AsyncCallbackIntegration.Calls.Should().Be(0);
        returned.Should().Be("value");
        IntegrationOptions<AsyncCallbackIntegration, TestTarget>.IsIntegrationEnabled.Should().BeFalse();

        // Disabled means the gate in CallTargetInvoker short-circuits, and the caller's value still
        // passes through untouched.
        CallTargetInvoker
           .EndMethodRuntimeAsync<AsyncCallbackIntegration, TestTarget, string, Task<string>>(new TestTarget(), "again", null, in state)
           .GetReturnValue()
           .Should()
           .Be("again");
        AsyncCallbackIntegration.Calls.Should().Be(0);
    }

    [Fact]
    public void WhenNoCallbacksAreDeclared_ReturnsTheOriginalValue()
    {
        var state = CallTargetState.GetDefault();

        var returned = CallTargetInvoker
                      .EndMethodRuntimeAsync<NoCallbackIntegration, TestTarget, string, Task<string>>(new TestTarget(), "value", null, in state)
                      .GetReturnValue();

        returned.Should().Be("value");
    }

    private static string InvokeBothCallbacks(string returnValue)
    {
        var state = CallTargetState.GetDefault();
        return CallTargetInvoker
              .EndMethodRuntimeAsync<BothCallbacksIntegration, TestTarget, string, Task<string>>(new TestTarget(), returnValue, null, in state)
              .GetReturnValue();
    }

    private static void InvokeMethodEndWithTask(string returnValue, Exception exception)
    {
        var state = CallTargetState.GetDefault();
        CallTargetInvoker.EndMethodRuntimeAsync<MethodEndIntegration, TestTarget, string, Task<string>>(new TestTarget(), returnValue, exception, in state);
    }

    private static void InvokeMethodEndWithValueTask(int returnValue)
    {
        var state = CallTargetState.GetDefault();
        CallTargetInvoker.EndMethodRuntimeAsync<MethodEndIntegration, TestTarget, int, ValueTask<int>>(new TestTarget(), returnValue, null, in state);
    }

    internal class TestTarget
    {
    }

    internal class AsyncEndIntegration
    {
        public static int Calls { get; private set; }

        public static object Instance { get; private set; }

        public static object ReturnValue { get; private set; }

        public static Exception Exception { get; private set; }

        public static void Reset()
        {
            Calls = 0;
            Instance = null;
            ReturnValue = null;
            Exception = null;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Calls++;
            Instance = instance;
            ReturnValue = returnValue;
            Exception = exception;
            return returnValue;
        }
    }

    internal class SubstitutingAsyncEndIntegration
    {
        public static string OnAsyncMethodEnd<TTarget>(TTarget instance, string returnValue, Exception exception, in CallTargetState state)
            => returnValue + "[Modified]";
    }

    internal class MethodEndIntegration
    {
        public static int Calls { get; private set; }

        public static object Instance { get; private set; }

        public static object ReturnValue { get; private set; }

        public static Exception Exception { get; private set; }

        public static void Reset()
        {
            Calls = 0;
            Instance = null;
            ReturnValue = null;
            Exception = null;
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Calls++;
            Instance = instance;
            ReturnValue = returnValue;
            Exception = exception;
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }

    internal class SubstitutingMethodEndIntegration
    {
        public static CallTargetReturn<Task<string>> OnMethodEnd<TTarget>(TTarget instance, Task<string> returnValue, Exception exception, in CallTargetState state)
            => new CallTargetReturn<Task<string>>(Task.FromResult("substituted"));
    }

    internal class BothCallbacksIntegration
    {
        public static int AsyncCalls { get; private set; }

        public static int SyncCalls { get; private set; }

        public static object SyncReturnValue { get; private set; }

        public static void Reset()
        {
            AsyncCalls = 0;
            SyncCalls = 0;
            SyncReturnValue = null;
        }

        // Substitutes, so the test can prove the value reaches OnMethodEnd rather than the original.
        public static string OnAsyncMethodEnd<TTarget>(TTarget instance, string returnValue, Exception exception, in CallTargetState state)
        {
            AsyncCalls++;
            return returnValue + "[Async]";
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            SyncCalls++;
            SyncReturnValue = returnValue;
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }

    internal class AsyncCallbackIntegration
    {
        public static int Calls { get; private set; }

        public static void Reset()
        {
            Calls = 0;
        }

        // Async callbacks take CallTargetState by value - an async method cannot have an `in` parameter.
        public static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            Calls++;
            await Task.Yield();
            return returnValue;
        }
    }

    internal class NoCallbackIntegration
    {
    }
}

#endif
