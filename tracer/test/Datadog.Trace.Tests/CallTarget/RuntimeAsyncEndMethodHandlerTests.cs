// <copyright file="RuntimeAsyncEndMethodHandlerTests.cs" company="Datadog">
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
/// Covers <c>RuntimeAsyncEndMethodHandler&lt;TIntegration, TTarget, TDeclaredReturn&gt;</c>, the
/// epilog handler for a runtime-async method declaring a non-generic <see cref="Task"/> or
/// <see cref="ValueTask"/>. Such a method's body leaves nothing on the evaluation stack, so the
/// rewriter treats its effective return type as void.
/// </summary>
/// <remarks>
/// Tests drive <see cref="CallTargetInvoker"/>.<c>EndMethodRuntimeAsync</c> rather than the handler
/// directly, because that is the entry point the rewritten IL calls.
/// <para>
/// The handler is a static generic class that binds its delegate once per closed generic
/// instantiation, so the integration types below are private to this file - reusing one across
/// files would share a binding. This mirrors the existing convention in
/// <see cref="TaskContinuationGeneratorTests"/>, where the same fake integrations are duplicated
/// per test file for exactly that reason.
/// </para>
/// </remarks>
public class RuntimeAsyncEndMethodHandlerTests
{
    [Fact]
    public void OnAsyncMethodEnd_OnSuccess_IsInvokedWithNoReturnValue()
    {
        AsyncEndIntegration.Reset();
        var target = new TestTarget();
        var state = CallTargetState.GetDefault();

        CallTargetInvoker.EndMethodRuntimeAsync<AsyncEndIntegration, TestTarget, Task>(target, null, in state);

        AsyncEndIntegration.Calls.Should().Be(1);
        AsyncEndIntegration.Instance.Should().BeSameAs(target);
        AsyncEndIntegration.Exception.Should().BeNull();

        // A non-generic Task target has no result, so the callback sees null - the same value
        // TaskContinuationGenerator.SyncCallbackHandler passes for the state-machine equivalent.
        AsyncEndIntegration.ReturnValue.Should().BeNull();
    }

    [Fact]
    public void OnAsyncMethodEnd_OnException_ReceivesTheException()
    {
        AsyncEndIntegration.Reset();
        var exception = new InvalidOperationException("boom");
        var state = CallTargetState.GetDefault();

        CallTargetInvoker.EndMethodRuntimeAsync<AsyncEndIntegration, TestTarget, Task>(new TestTarget(), exception, in state);

        AsyncEndIntegration.Calls.Should().Be(1);

        // The runtime drives suspension inside the body, so an awaited failure arrives here as a
        // thrown exception rather than as a faulted task - no GetBaseException unwrapping needed.
        AsyncEndIntegration.Exception.Should().BeSameAs(exception);
    }

    [Fact]
    public void OnMethodEnd_OnSuccess_ReceivesACompletedTask()
    {
        MethodEndIntegration.Reset();
        var target = new TestTarget();
        var state = CallTargetState.GetDefault();

        CallTargetInvoker.EndMethodRuntimeAsync<MethodEndIntegration, TestTarget, Task>(target, null, in state);

        MethodEndIntegration.Calls.Should().Be(1);
        MethodEndIntegration.Instance.Should().BeSameAs(target);
        MethodEndIntegration.Exception.Should().BeNull();

        // The body never materialises the Task it declares, but OnMethodEnd is written against the
        // declared signature. By the time the epilog runs the operation really has completed, so a
        // completed task is an accurate stand-in (and Task.CompletedTask is allocation-free).
        MethodEndIntegration.ReturnValue.Should().BeSameAs(Task.CompletedTask);
    }

    [Fact]
    public void OnMethodEnd_OnException_ReceivesNoTask()
    {
        MethodEndIntegration.Reset();
        var exception = new InvalidOperationException("boom");
        var state = CallTargetState.GetDefault();

        CallTargetInvoker.EndMethodRuntimeAsync<MethodEndIntegration, TestTarget, Task>(new TestTarget(), exception, in state);

        MethodEndIntegration.Calls.Should().Be(1);
        MethodEndIntegration.Exception.Should().BeSameAs(exception);

        // Not a faulted task: the exception is carried by the exception argument, matching what a
        // state-machine target hands over when it throws before its first suspension.
        MethodEndIntegration.ReturnValue.Should().BeNull();
    }

    [Fact]
    public void OnMethodEnd_WithDeclaredValueTask_ReceivesACompletedValueTask()
    {
        MethodEndIntegration.Reset();
        var state = CallTargetState.GetDefault();

        CallTargetInvoker.EndMethodRuntimeAsync<MethodEndIntegration, TestTarget, ValueTask>(new TestTarget(), null, in state);

        MethodEndIntegration.Calls.Should().Be(1);
        MethodEndIntegration.ReturnValue.Should().BeOfType<ValueTask>();
        ((ValueTask)MethodEndIntegration.ReturnValue).IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void WhenBothCallbacksAreDeclared_BothRun()
    {
        BothCallbacksIntegration.Reset();
        var state = CallTargetState.GetDefault();

        CallTargetInvoker.EndMethodRuntimeAsync<BothCallbacksIntegration, TestTarget, Task>(new TestTarget(), null, in state);

        // Matches EndMethodHandler<TIntegration, TTarget, TReturn>, which runs the continuation
        // generator AND the OnMethodEnd delegate when an integration declares both.
        // TraceAnnotationsIntegration does declare both, and [Trace]/DD_TRACE_METHODS can target
        // an arbitrary customer method - including a runtime-async one.
        BothCallbacksIntegration.AsyncCalls.Should().Be(1);
        BothCallbacksIntegration.SyncCalls.Should().Be(1);

        // OnMethodEnd is handed a completed Task, not null, which is what lets a TraceAnnotations
        // style `returnValue is Task` guard correctly leave disposal to OnAsyncMethodEnd.
        BothCallbacksIntegration.SyncReturnValue.Should().BeSameAs(Task.CompletedTask);
    }

    [Fact]
    public void WhenOnAsyncMethodEndIsItselfAsync_DisablesTheIntegration()
    {
        AsyncCallbackIntegration.Reset();
        var state = CallTargetState.GetDefault();

        IntegrationOptions<AsyncCallbackIntegration, TestTarget>.IsIntegrationEnabled.Should().BeTrue();

        CallTargetInvoker.EndMethodRuntimeAsync<AsyncCallbackIntegration, TestTarget, Task>(new TestTarget(), null, in state);

        // We cannot await in the epilog: it runs inside a finally, and the runtime-async spec
        // forbids suspension points in handler blocks. Only CI Visibility integrations have async
        // callbacks today, and none of them target a runtime-async method.
        AsyncCallbackIntegration.Calls.Should().Be(0);

        // Skipping the callback is not enough on its own: OnMethodBegin has already run and created
        // state that only that callback would clean up, so leaving the integration live would leak
        // once per call. Disabling it means every subsequent BeginMethod and EndMethod for this
        // <integration, target> pair is gated off, and the disable is reported to telemetry.
        IntegrationOptions<AsyncCallbackIntegration, TestTarget>.IsIntegrationEnabled.Should().BeFalse();

        CallTargetInvoker.EndMethodRuntimeAsync<AsyncCallbackIntegration, TestTarget, Task>(new TestTarget(), null, in state);
        AsyncCallbackIntegration.Calls.Should().Be(0);
    }

    [Fact]
    public void OnMethodEnd_SubstitutionIsReportedAndNotHonoured()
    {
        SubstitutingMethodEndIntegration.Reset();
        var state = CallTargetState.GetDefault();

        CallTargetInvoker.EndMethodRuntimeAsync<SubstitutingMethodEndIntegration, TestTarget, Task>(new TestTarget(), null, in state);

        // A method declaring a non-generic Task leaves nothing on the evaluation stack and the
        // epilog's CallTargetReturn carries no value, so unlike the generic case there is nothing
        // to write a replacement into and no result to take back out of it. The attempt is
        // reported rather than dropped in silence.
        SubstitutingMethodEndIntegration.Calls.Should().Be(1);

        // Reported, not disabled - OnMethodEnd ran, so only the substitution is lost.
        IntegrationOptions<SubstitutingMethodEndIntegration, TestTarget>.IsIntegrationEnabled.Should().BeTrue();
    }

    [Fact]
    public void OnMethodEnd_ReturningTheTaskItWasGiven_IsNotTreatedAsASubstitution()
    {
        MethodEndIntegration.Reset();
        var state = CallTargetState.GetDefault();

        CallTargetInvoker.EndMethodRuntimeAsync<MethodEndIntegration, TestTarget, Task>(new TestTarget(), null, in state);

        // MethodEndIntegration hands back the completed task it was given, so nothing is reported
        // and the pass-through case is unaffected by substitution detection.
        MethodEndIntegration.Calls.Should().Be(1);
        IntegrationOptions<MethodEndIntegration, TestTarget>.IsIntegrationEnabled.Should().BeTrue();
    }

    [Fact]
    public void WhenNoCallbacksAreDeclared_DoesNothing()
    {
        // With neither callback bound the handler must be an inert pass-through. There is no return
        // value to inspect on the non-generic overload, so "does not throw" is the whole assertion.
        var act = static () =>
        {
            var state = CallTargetState.GetDefault();
            CallTargetInvoker.EndMethodRuntimeAsync<NoCallbackIntegration, TestTarget, Task>(new TestTarget(), null, in state);
        };

        act.Should().NotThrow();
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

    internal class SubstitutingMethodEndIntegration
    {
        public static int Calls { get; private set; }

        public static void Reset() => Calls = 0;

        // Replaces the task entirely, the way ForceFlushAsyncIntegration does. On a runtime-async
        // target there is no task to replace, so this can only be reported.
        public static CallTargetReturn<Task> OnMethodEnd<TTarget>(TTarget instance, Task returnValue, Exception exception, in CallTargetState state)
        {
            Calls++;
            return new CallTargetReturn<Task>(new TaskCompletionSource<object>().Task);
        }
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

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            AsyncCalls++;
            return returnValue;
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
