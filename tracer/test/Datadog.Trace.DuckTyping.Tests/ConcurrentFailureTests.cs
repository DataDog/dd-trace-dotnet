// <copyright file="ConcurrentFailureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.WrongFieldName;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests;

public class ConcurrentFailureTests
{
    private const int ConcurrentCallCount = 4;

    [Fact]
    public void ConcurrentFailedGenericCreateInstanceCallsAreSafe()
    {
        var target = ObscureObject.GetObject(nameof(ObscureObject.GetFieldInternalObject));

        AssertConcurrentThrowsAreSafe(
            () => CaptureException(() => target.DuckCast<IWrongFieldName.IProtectedValueTypeField>()),
            exception => exception);
    }

    [Fact]
    public void ConcurrentFailedNonGenericCreateInstanceCallsAreSafe()
    {
        var target = ObscureObject.GetObject(nameof(ObscureObject.GetFieldInternalObject));
        var proxyType = typeof(IWrongFieldName.IProtectedValueTypeField);

        AssertConcurrentThrowsAreSafe(
            () => CaptureException(() => target.DuckCast(proxyType)),
            UnwrapTargetInvocationException);
    }

    [Fact]
    public void ConcurrentFailedProxyTypeCallsAreSafe()
    {
        var target = ObscureObject.GetObject(nameof(ObscureObject.GetFieldInternalObject));
        var result = DuckType.GetOrCreateProxyType(typeof(IWrongFieldName.IProtectedValueTypeField), target.GetType());

        AssertConcurrentThrowsAreSafe(
            () => CaptureException(() => _ = result.ProxyType),
            exception => exception);
    }

    [Fact]
    public void CachedFailureThrowPreservesOriginalStackTrace()
    {
        var originalException = CaptureOriginalFailure();
        var originalStackTrace = originalException.StackTrace;
        originalStackTrace.Should().NotBeNull();
        originalStackTrace.Should().Contain(nameof(ThrowOriginalFailure));

        var result = new DuckType.CreateTypeResult(
            typeof(IWrongFieldName.IProtectedValueTypeField),
            proxyType: null,
            targetType: typeof(object),
            activator: null,
            ExceptionDispatchInfo.Capture(originalException));

        var thrownException = CaptureCachedProxyTypeFailure(result);
        thrownException.Should().BeOfType(originalException.GetType());
#if NET6_0_OR_GREATER
        thrownException.Should().BeSameAs(
            originalException,
            "the fixed runtime should preserve the existing cached-exception behavior without the workaround");
#else
        thrownException.Should().NotBeSameAs(
            originalException,
            "targets without a confirmed runtime fix must throw an independent copy of the cached exception");
#endif

        // The workaround copies the Exception before capturing and throwing it. Verify that the complete
        // original trace survives that sequence and that ExceptionDispatchInfo appends the current dispatch
        // path, rather than replacing the diagnostic information from the proxy-creation failure.
        thrownException.StackTrace.Should().Contain(originalStackTrace!);
        thrownException.StackTrace.Should().Contain(nameof(CaptureCachedProxyTypeFailure));

#if !NET6_0_OR_GREATER
        // Throwing the copy must not mutate the cached source exception. Besides preserving diagnostics for
        // later callers, this separation is what prevents concurrent throws from racing over its CLR fields.
        originalException.StackTrace.Should().Be(originalStackTrace);
#endif
    }

    // The CoreCLR access violation is Windows-only and timing-dependent, so trying to provoke the native
    // crash directly on an affected runtime would make the result flaky. FirstChanceException runs
    // synchronously on the throwing thread before the exception is caught. By holding each participating
    // thread briefly in that callback, the test deterministically observes whether the exact same cached
    // Exception can be thrown concurrently and validates the appropriate contract for each target:
    //
    // - Every pre-.NET 6 target, including .NET Framework, must throw a distinct copy of the cached exception.
    //   The native CoreCLR race is confirmed on old runtimes, while the public .NET Framework Reference Source
    //   exposes the same managed race window but not the native clr.dll implementation needed to prove it safe.
    // - .NET 6+ must continue throwing the cached exception itself. Requiring the callbacks to overlap proves
    //   that those tests exercise the runtime fix rather than accidentally retaining a serialization workaround.
    // - Every target must allow callbacks to overlap. This guards against holding a monitor while synchronous
    //   FirstChanceException callbacks or exception filters execute, which could deadlock customer processes.
    //   If a runtime regressed, the non-generic test could additionally terminate the test process while
    //   reflection copies Watson buckets into TargetInvocationException.
    private static void AssertConcurrentThrowsAreSafe(Func<Exception> invoke, Func<Exception, Exception> unwrap)
    {
        var firstException = invoke();
        firstException.Should().NotBeNull();
        var cachedException = unwrap(firstException!);
        cachedException.Should().BeAssignableTo<DuckTypeException>();

        var activeThrows = 0;
        var maximumConcurrentThrows = 0;
        EventHandler<FirstChanceExceptionEventArgs> handler = (_, args) =>
        {
            if (args.Exception.GetType() != cachedException.GetType() || args.Exception.Message != cachedException.Message)
            {
                return;
            }

            var currentActiveThrows = Interlocked.Increment(ref activeThrows);
            try
            {
                UpdateMaximum(ref maximumConcurrentThrows, currentActiveThrows);

                // Keep each throwing thread inside the first-chance callback long enough for the other
                // dedicated threads to enter it too. Without serialization, this makes the overlap
                // deterministic instead of relying on the much narrower CoreCLR Watson-bucket race.
                Thread.Sleep(50);
            }
            finally
            {
                Interlocked.Decrement(ref activeThrows);
            }
        };

        var exceptions = new Exception[ConcurrentCallCount];
        using var ready = new CountdownEvent(ConcurrentCallCount);
        using var start = new ManualResetEventSlim();
        var tasks = new Task[ConcurrentCallCount];

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try
        {
            for (var i = 0; i < tasks.Length; i++)
            {
                var index = i;
                tasks[index] = Task.Factory.StartNew(
                    () =>
                    {
                        ready.Signal();
                        start.Wait();
                        exceptions[index] = invoke();
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            ready.Wait();
            start.Set();
            Task.WaitAll(tasks);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        var unwrappedExceptions = new Exception[exceptions.Length];
        for (var i = 0; i < exceptions.Length; i++)
        {
            var exception = exceptions[i];
            exception.Should().NotBeNull();
            var unwrapped = unwrap(exception!);
            unwrappedExceptions[i] = unwrapped;
            unwrapped.Should().BeOfType(cachedException.GetType());
            unwrapped.Message.Should().Be(cachedException.Message);
#if NET6_0_OR_GREATER
            unwrapped.Should().BeSameAs(
                cachedException,
                "the fixed runtime should preserve the existing cached-exception behavior without the workaround");
#else
            unwrapped.Should().NotBeSameAs(
                cachedException,
                "targets without a confirmed runtime fix must throw independent copies of the cached exception");
#endif
        }

#if !NET6_0_OR_GREATER
        for (var i = 0; i < unwrappedExceptions.Length; i++)
        {
            for (var j = i + 1; j < unwrappedExceptions.Length; j++)
            {
                unwrappedExceptions[i].Should().NotBeSameAs(
                    unwrappedExceptions[j],
                    "concurrent callers must never share an exception instance on targets without the runtime fix");
            }
        }
#endif

        maximumConcurrentThrows.Should().BeGreaterThan(
            1,
            "exception dispatch must not hold a monitor while synchronous callbacks or exception filters execute");
    }

    private static Exception CaptureException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DuckTypeException CaptureOriginalFailure()
    {
        try
        {
            ThrowOriginalFailure();
            return null;
        }
        catch (DuckTypeException exception)
        {
            return exception;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOriginalFailure() => throw DuckTypeException.Create("Original proxy-creation failure");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CaptureCachedProxyTypeFailure(DuckType.CreateTypeResult result)
        => CaptureException(() => _ = result.ProxyType);

    private static Exception UnwrapTargetInvocationException(Exception exception)
    {
        exception.Should().BeOfType<TargetInvocationException>();
        exception.InnerException.Should().NotBeNull();
        return exception.InnerException!;
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        var observedMaximum = Volatile.Read(ref maximum);
        while (candidate > observedMaximum)
        {
            var previousMaximum = Interlocked.CompareExchange(ref maximum, candidate, observedMaximum);
            if (previousMaximum == observedMaximum)
            {
                return;
            }

            observedMaximum = previousMaximum;
        }
    }
}
