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

    [Fact]
    public void FailingTryDuckCastForMissingPropertyRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new MissingPropertyTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<IMissingPropertyProxy>(out var proxy);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
        proxy.Should().BeNull();
    }

    [Fact]
    public void FailingNonGenericTryDuckCastForMissingPropertyRaisesNoFirstChanceException()
    {
        Warmup();

        // The customer hit the non-generic overload.
        object target = new NonGenericMissingPropertyTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast(typeof(INonGenericMissingPropertyProxy), out var proxy);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
        proxy.Should().BeNull();
    }

    [Fact]
    public void FailingDuckIsForMissingPropertyRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new DuckIsMissingPropertyTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.DuckIs<IDuckIsMissingPropertyProxy>();

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
    }

    [Fact]
    public void FailingDuckAsForMissingPropertyRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new DuckAsMissingPropertyTarget();
        using var counter = new FirstChanceExceptionCounter();

        var proxy = target.DuckAs<IDuckAsMissingPropertyProxy>();

        counter.Exceptions.Should().BeEmpty();
        proxy.Should().BeNull();
    }

    [Fact]
    public void FailingCanCreateForMissingPropertyRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new CanCreateMissingPropertyTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = DuckType.CanCreate(typeof(ICanCreateMissingPropertyProxy), target);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
    }

    [Fact]
    public void FailingTryDuckCastForUnreadablePropertyRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new UnreadablePropertyTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<IUnreadablePropertyProxy>(out _);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
    }

    [Fact]
    public void FailingTryDuckCastForMissingFieldRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new MissingFieldTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<IMissingFieldProxy>(out _);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
    }

    [Fact]
    public void FailingTryDuckCastForReadonlyFieldRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new ReadonlyFieldTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<IReadonlyFieldProxy>(out _);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
    }

    /// <summary>
    /// The throwing entry points keep throwing, and must still raise exactly one first-chance exception -
    /// from the rethrow at the call site, outside the Lazy value factory, where it is safe.
    /// </summary>
    [Fact]
    public void FailingDuckCastRaisesExactlyOneFirstChanceException()
    {
        Warmup();

        object target = new ThrowingCastTarget();
        using var counter = new FirstChanceExceptionCounter();

        var cast = () => target.DuckCast<IThrowingCastProxy>();

        cast.Should().Throw<DuckTypePropertyOrFieldNotFoundException>();
        counter.Exceptions.Should().ContainSingle()
               .Which.Should().BeOfType<DuckTypePropertyOrFieldNotFoundException>();
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

    // Each failing test needs its own pair too: a cached failure result short-circuits creation just as a
    // cached success does, so a shared pair would measure the cache rather than the code under test.
    public interface IMissingPropertyProxy
    {
        string NotOnTheTarget { get; }
    }

    public interface INonGenericMissingPropertyProxy
    {
        string NotOnTheTarget { get; }
    }

    public interface IDuckIsMissingPropertyProxy
    {
        string NotOnTheTarget { get; }
    }

    public interface IDuckAsMissingPropertyProxy
    {
        string NotOnTheTarget { get; }
    }

    public interface ICanCreateMissingPropertyProxy
    {
        string NotOnTheTarget { get; }
    }

    public interface IThrowingCastProxy
    {
        string NotOnTheTarget { get; }
    }

    public interface IUnreadablePropertyProxy
    {
        string OnlySetter { get; set; }
    }

    public interface IMissingFieldProxy
    {
        [DuckField(Name = "_notOnTheTarget")]
        string Field { get; }
    }

    public interface IReadonlyFieldProxy
    {
        [DuckField(Name = "_readonly")]
        string Field { get; set; }
    }

    internal class MissingPropertyTarget
    {
        public string Present => "ok";
    }

    internal class NonGenericMissingPropertyTarget
    {
        public string Present => "ok";
    }

    internal class DuckIsMissingPropertyTarget
    {
        public string Present => "ok";
    }

    internal class DuckAsMissingPropertyTarget
    {
        public string Present => "ok";
    }

    internal class CanCreateMissingPropertyTarget
    {
        public string Present => "ok";
    }

    internal class ThrowingCastTarget
    {
        public string Present => "ok";
    }

    internal class UnreadablePropertyTarget
    {
        public string OnlySetter
        {
            set { }
        }
    }

    internal class MissingFieldTarget
    {
        public string Present => "ok";
    }

    internal class ReadonlyFieldTarget
    {
#pragma warning disable CS0414 // read by the duck type proxy via reflection, never by this class
        private readonly string _readonly = "ok";
#pragma warning restore CS0414
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
