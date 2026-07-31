// <copyright file="FirstChanceExceptionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.DuckTyping.Tests;

public class FirstChanceExceptionTests
{
    [Fact]
    public void CounterObservesThrownExceptions()
    {
        using var counter = new FirstChanceExceptionCounter();

        try
        {
            throw new HarnessSentinelException();
        }
        catch (HarnessSentinelException)
        {
            // expected
        }

        counter.Exceptions.Should().ContainSingle();
    }

    [Fact]
    public void SuccessfulDuckCastRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new SuccessTarget();
        using var counter = new FirstChanceExceptionCounter();

        var proxy = target.DuckCast<ISuccessProxy>();

        counter.Exceptions.Should().BeEmpty();
        proxy.Value.Should().Be("ok");
        proxy.Add(2, 3).Should().Be(5);
    }

    [Fact]
    public void SuccessfulTryDuckCastRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new TryCastSuccessTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<ITryCastSuccessProxy>(out var proxy);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeTrue();
        proxy.Value.Should().Be("ok");
    }

    [Fact]
    public void SuccessfulNonGenericTryDuckCastRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new NonGenericTryCastSuccessTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast(typeof(INonGenericTryCastSuccessProxy), out var proxy);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeTrue();
        proxy.Should().BeAssignableTo<INonGenericTryCastSuccessProxy>();
    }

    [Fact]
    public void SuccessfulDuckIsRaisesNoFirstChanceException()
    {
        Warmup();

        // DuckIs goes through CanCreate, i.e. the dry-run leg followed by the real leg.
        object target = new DuckIsSuccessTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.DuckIs<IDuckIsSuccessProxy>();

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeTrue();
    }

    [Fact]
    public void SuccessfulCanCreateRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new CanCreateSuccessTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = DuckType.CanCreate(typeof(ICanCreateSuccessProxy), target);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeTrue();
    }

    [Fact]
    public void SuccessfulDuckCopyRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new DuckCopySuccessTarget();
        using var counter = new FirstChanceExceptionCounter();

        var copy = target.DuckCast<DuckCopySuccessProxy>();

        counter.Exceptions.Should().BeEmpty();
        copy.Value.Should().Be("ok");
    }

    private static void Warmup()
    {
        new WarmupTarget().DuckCast<IWarmupProxy>().Value.Should().Be("warm");
    }

    public interface IWarmupProxy
    {
        string Value { get; }
    }

    public interface ISuccessProxy
    {
        string Value { get; }

        int Add(int a, int b);
    }

    public interface ITryCastSuccessProxy
    {
        string Value { get; }
    }

    public interface INonGenericTryCastSuccessProxy
    {
        string Value { get; }
    }

    public interface IDuckIsSuccessProxy
    {
        string Value { get; }
    }

    public interface ICanCreateSuccessProxy
    {
        string Value { get; }
    }

    [DuckCopy]
    public struct DuckCopySuccessProxy
    {
        public string Value;
    }

    internal class WarmupTarget
    {
        public string Value => "warm";
    }

    internal class SuccessTarget
    {
        public string Value => "ok";

        public int Add(int a, int b) => a + b;
    }

    internal class TryCastSuccessTarget
    {
        public string Value => "ok";
    }

    internal class NonGenericTryCastSuccessTarget
    {
        public string Value => "ok";
    }

    internal class DuckIsSuccessTarget
    {
        public string Value => "ok";
    }

    internal class CanCreateSuccessTarget
    {
        public string Value => "ok";
    }

    internal class DuckCopySuccessTarget
    {
        public string Value => "ok";
    }

    internal sealed class FirstChanceExceptionCounter : IDisposable
    {
        private readonly int _threadId;
        private readonly List<Exception> _exceptions = [];

        public FirstChanceExceptionCounter()
        {
            _threadId = Environment.CurrentManagedThreadId;
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        }

        public List<Exception> Exceptions
        {
            get
            {
                lock (_exceptions)
                {
                    return [.._exceptions];
                }
            }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
        }

        private void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            // Keep this handler as simple as possible: it runs during the first pass of SEH for every
            // exception in the AppDomain, and anything that throws in here would be a nightmare to debug.
            if (Environment.CurrentManagedThreadId != _threadId || e?.Exception is null)
            {
                return;
            }

            lock (_exceptions)
            {
                _exceptions.Add(e.Exception);
            }
        }
    }

    internal class HarnessSentinelException : Exception
    {
    }
}
