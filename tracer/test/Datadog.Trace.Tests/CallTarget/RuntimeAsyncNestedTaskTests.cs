// <copyright file="RuntimeAsyncNestedTaskTests.cs" company="Datadog">
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

public class RuntimeAsyncNestedTaskTests
{
    [Fact]
    public void TaskOfTask_InvokesASynchronousCallbackWithTheInnerTask()
    {
        TaskOfTaskIntegration.Reset();
        var target = new TestTarget();
        var state = CallTargetState.GetDefault();
        var inner = Task.Delay(0);

        var returned = CallTargetInvoker
                      .EndMethodRuntimeAsync<TaskOfTaskIntegration, TestTarget, Task, Task<Task>>(target, inner, null, in state)
                      .GetReturnValue();

        TaskOfTaskIntegration.Calls.Should().Be(1);
        IntegrationOptions<TaskOfTaskIntegration, TestTarget>.IsIntegrationEnabled.Should().BeTrue();

        // The callback sees the inner task as an ordinary value, which is what a state-machine
        // target passes too: ContinuationsHelper.GetResultType(typeof(Task<Task>)) is Task.
        TaskOfTaskIntegration.ReturnValue.Should().BeSameAs(inner);
        returned.Should().BeSameAs(inner);
    }

    [Fact]
    public void TaskOfTaskOfInt_InvokesASynchronousCallbackWithTheInnerTask()
    {
        TaskOfTaskOfIntIntegration.Reset();
        var state = CallTargetState.GetDefault();
        var inner = Task.FromResult(42);

        var returned = CallTargetInvoker
                      .EndMethodRuntimeAsync<TaskOfTaskOfIntIntegration, TestTarget, Task<int>, Task<Task<int>>>(new TestTarget(), inner, null, in state)
                      .GetReturnValue();

        TaskOfTaskOfIntIntegration.Calls.Should().Be(1);
        TaskOfTaskOfIntIntegration.ReturnValue.Should().BeSameAs(inner);
        returned.Should().BeSameAs(inner);
    }

    [Fact]
    public void ValueTaskOfValueTask_InvokesASynchronousCallbackWithTheInnerValueTask()
    {
        ValueTaskOfValueTaskIntegration.Reset();
        var state = CallTargetState.GetDefault();
        var inner = new ValueTask(Task.Delay(0));

        var returned = CallTargetInvoker
                      .EndMethodRuntimeAsync<ValueTaskOfValueTaskIntegration, TestTarget, ValueTask, ValueTask<ValueTask>>(new TestTarget(), inner, null, in state)
                      .GetReturnValue();

        ValueTaskOfValueTaskIntegration.Calls.Should().Be(1);
        ValueTaskOfValueTaskIntegration.ReturnValue.Should().Be(inner);
        returned.Should().Be(inner);
    }

    [Fact]
    public void TaskOfTask_RejectsAGenuinelyAsyncCallback()
    {
        AsyncCallbackOnTaskOfTaskIntegration.Reset();
        var state = CallTargetState.GetDefault();
        var inner = Task.Delay(0);

        IntegrationOptions<AsyncCallbackOnTaskOfTaskIntegration, TestTarget>.IsIntegrationEnabled.Should().BeTrue();

        var returned = CallTargetInvoker
                      .EndMethodRuntimeAsync<AsyncCallbackOnTaskOfTaskIntegration, TestTarget, Task, Task<Task>>(new TestTarget(), inner, null, in state)
                      .GetReturnValue();

        // Here the callback really does return Task<TReturn> - Task<Task> - and the epilog runs
        // inside a finally, where the spec forbids suspending. So the integration is disabled for this target
        AsyncCallbackOnTaskOfTaskIntegration.Calls.Should().Be(0);
        returned.Should().BeSameAs(inner);
        IntegrationOptions<AsyncCallbackOnTaskOfTaskIntegration, TestTarget>.IsIntegrationEnabled.Should().BeFalse();
    }

    internal class TestTarget
    {
    }

    internal class TaskOfTaskIntegration
    {
        public static int Calls { get; private set; }

        public static object ReturnValue { get; private set; }

        public static void Reset()
        {
            Calls = 0;
            ReturnValue = null;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Calls++;
            ReturnValue = returnValue;
            return returnValue;
        }
    }

    internal class TaskOfTaskOfIntIntegration
    {
        public static int Calls { get; private set; }

        public static object ReturnValue { get; private set; }

        public static void Reset()
        {
            Calls = 0;
            ReturnValue = null;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Calls++;
            ReturnValue = returnValue;
            return returnValue;
        }
    }

    internal class ValueTaskOfValueTaskIntegration
    {
        public static int Calls { get; private set; }

        public static object ReturnValue { get; private set; }

        public static void Reset()
        {
            Calls = 0;
            ReturnValue = null;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Calls++;
            ReturnValue = returnValue;
            return returnValue;
        }
    }

    internal class AsyncCallbackOnTaskOfTaskIntegration
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
}

#endif
