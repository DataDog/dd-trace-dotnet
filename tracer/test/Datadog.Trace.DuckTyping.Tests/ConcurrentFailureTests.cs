// <copyright file="ConcurrentFailureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
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
    public void ConcurrentFailedGenericCreateInstanceCallsAreSerialized()
    {
        var target = ObscureObject.GetObject(nameof(ObscureObject.GetFieldInternalObject));

        AssertConcurrentThrowsAreSerialized(
            () => CaptureException(() => target.DuckCast<IWrongFieldName.IProtectedValueTypeField>()),
            exception => exception);
    }

    [Fact]
    public void ConcurrentFailedNonGenericCreateInstanceCallsAreSerialized()
    {
        var target = ObscureObject.GetObject(nameof(ObscureObject.GetFieldInternalObject));
        var proxyType = typeof(IWrongFieldName.IProtectedValueTypeField);

        AssertConcurrentThrowsAreSerialized(
            () => CaptureException(() => target.DuckCast(proxyType)),
            UnwrapTargetInvocationException);
    }

    [Fact]
    public void ConcurrentFailedProxyTypeCallsAreSerialized()
    {
        var target = ObscureObject.GetObject(nameof(ObscureObject.GetFieldInternalObject));
        var result = DuckType.GetOrCreateProxyType(typeof(IWrongFieldName.IProtectedValueTypeField), target.GetType());

        AssertConcurrentThrowsAreSerialized(
            () => CaptureException(() => _ = result.ProxyType),
            exception => exception);
    }

    // The CoreCLR access violation is Windows-only and timing-dependent, so trying to provoke the native
    // crash directly would make this regression test both platform-specific and flaky. FirstChanceException
    // runs synchronously on the throwing thread before the exception is caught. By holding each participating
    // thread briefly in that callback, the test deterministically observes whether the same cached Exception
    // can be thrown concurrently. That is the exact precondition for the runtime race, while remaining safe
    // and meaningful on every target framework and operating system.
    private static void AssertConcurrentThrowsAreSerialized(Func<Exception> invoke, Func<Exception, Exception> unwrap)
    {
        var firstException = invoke();
        firstException.Should().NotBeNull();
        var cachedException = unwrap(firstException!);
        cachedException.Should().BeAssignableTo<DuckTypeException>();

        var activeThrows = 0;
        var maximumConcurrentThrows = 0;
        EventHandler<FirstChanceExceptionEventArgs> handler = (_, args) =>
        {
            if (!ReferenceEquals(args.Exception, cachedException))
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

        foreach (var exception in exceptions)
        {
            exception.Should().NotBeNull();
            unwrap(exception!).Should().BeSameAs(cachedException);
        }

        maximumConcurrentThrows.Should().Be(1);
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
