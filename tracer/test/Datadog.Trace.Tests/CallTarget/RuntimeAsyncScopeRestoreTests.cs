// <copyright file="RuntimeAsyncScopeRestoreTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Runtime-async only exists on .NET 10+, so there is no point exercising this handler on any other
// target - the code path it serves can never be reached there.
#if NET10_0_OR_GREATER

using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget;

/// <summary>
/// Pins the one deliberate behavioural difference between the runtime-async epilog and the
/// state-machine one: the runtime-async handler does <em>not</em> call
/// <c>IntegrationOptions.RestoreScopeFromAsyncExecution</c>.
/// </summary>
/// <remarks>
/// This is intended behaviour, not an omission. CallTarget instruments a state-machine async method
/// via its <em>stub</em>, so <c>OnMethodBegin</c> runs before <c>builder.Start(ref stateMachine)</c>
/// - outside the ExecutionContext save/restore that <c>AsyncMethodBuilderCore.Start</c> performs
/// around the body. Its AsyncLocal write therefore escapes to the caller, and the explicit restore
/// is the manual fix-up for that (note the <c>_continuationGenerator != null</c> guard on it: it is
/// applied exactly when a stub exists).
/// <para>
/// A runtime-async method has no stub. The prologue and epilog both run inside the async method, so
/// they sit inside the region whose ExecutionContext changes the runtime already unwinds, and there
/// is nothing left to fix up. Calling the restore anyway would be wrong rather than merely
/// redundant: by this point the callback has disposed the scope and <c>AsyncLocalScopeManager.Close</c>
/// has already popped <c>Active</c> to the parent and reset the distributed context, so forcing
/// <c>PreviousDistributedSpanContext</c> on top would clobber any change the instrumented body
/// legitimately made.
/// </para>
/// <para>
/// All of the tracer's ambient state is <c>AsyncLocal</c> (<c>AsyncLocalScopeManager</c> is the only
/// <c>IScopeManager</c>, and <c>AutomaticTracer.DistributedTrace</c> is likewise), so the
/// thread-local leak New Relic had to fix for runtime-async in their agent (PR #3804,
/// <c>DetachFromPrimary</c>) has no analogue here - we have no thread-local ambient slot.
/// </para>
/// </remarks>
[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class RuntimeAsyncScopeRestoreTests
{
    [Fact]
    public async Task RuntimeAsyncEndMethod_LeavesTheActiveScopeAlone()
    {
        var scopeManager = new AsyncLocalScopeManager();
        await using var tracer = CreateTracer(scopeManager);

        // The caller's scope, active before the instrumented method was entered.
        using var callerScope = Activate(scopeManager, spanId: 1);

        // The scope the instrumented method's OnMethodBegin created.
        using var methodScope = Activate(scopeManager, spanId: 2);

        var state = new CallTargetState(methodScope, callerScope, previousDistributedSpanContext: null);

        InvokeRuntimeAsyncEndMethod(in state);

        NoOpIntegration.Calls.Should().Be(1);
        scopeManager.Active.Should().BeSameAs(methodScope);
        scopeManager.Active.Should().NotBeSameAs(callerScope);
    }

    [Fact]
    public async Task StateMachineEndMethod_RestoresThePreviousScope()
    {
        // The contrast case, so the difference above reads as a decision rather than an oversight.
        var scopeManager = new AsyncLocalScopeManager();
        await using var tracer = CreateTracer(scopeManager);

        using var callerScope = Activate(scopeManager, spanId: 1);
        using var methodScope = Activate(scopeManager, spanId: 2);

        var state = new CallTargetState(methodScope, callerScope, previousDistributedSpanContext: null);

        InvokeStateMachineEndMethod(in state);

        scopeManager.Active.Should().BeSameAs(callerScope);
    }

    private static ScopedTracer CreateTracer(AsyncLocalScopeManager scopeManager)
    {
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.ServiceName, "runtime-async-tests" },
                { ConfigurationKeys.PropagateProcessTags, "false" },
            });

        var tracer = TracerHelper.Create(settings, scopeManager: scopeManager);
        TracerRestorerAttribute.SetTracer(tracer);
        return tracer;
    }

    private static Scope Activate(AsyncLocalScopeManager scopeManager, ulong spanId)
    {
        NoOpIntegration.Reset();
        var span = new Span(new SpanContext(traceId: 1, spanId: spanId), DateTimeOffset.UtcNow);
        return scopeManager.Activate(span, finishOnClose: false);
    }

    // CallTargetReturn<T> is a ref struct and cannot be a local in an async method, so the invoker
    // calls live in non-async helpers.
    private static void InvokeRuntimeAsyncEndMethod(in CallTargetState state)
        => CallTargetInvoker.EndMethodRuntimeAsync<NoOpIntegration, TestTarget, Task>(new TestTarget(), null, in state);

    private static void InvokeStateMachineEndMethod(in CallTargetState state)
        => CallTargetInvoker.EndMethod<NoOpIntegration, TestTarget, Task>(new TestTarget(), Task.CompletedTask, null, in state);

    internal class TestTarget
    {
    }

    internal class NoOpIntegration
    {
        public static int Calls { get; private set; }

        public static void Reset() => Calls = 0;

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Calls++;
            return returnValue;
        }
    }
}

#endif
