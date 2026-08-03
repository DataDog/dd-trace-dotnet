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

    [Fact]
    public void FailingTryDuckCastForMissingMethodRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new MissingMethodTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<IMissingMethodProxy>(out _);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
    }

    [Fact]
    public void FailingTryDuckCastForWrongArgumentTypeRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new WrongArgumentTypeTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<IWrongArgumentTypeProxy>(out _);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
    }

    [Fact]
    public void FailingTryDuckCastForWrongNumberOfArgumentsRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new WrongArgumentCountTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<IWrongArgumentCountProxy>(out _);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
    }

    [Fact]
    public void FailingTryDuckCastForAmbiguousMethodRaisesNoFirstChanceException()
    {
        Warmup();

        // Reaches SelectTargetMethod's ambiguous-match path rather than a CreateMethods check.
        object target = new AmbiguousMethodTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<IAmbiguousMethodProxy>(out _);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
    }

    [Fact]
    public void SuccessfulTryDuckImplementRaisesNoFirstChanceException()
    {
        Warmup();

        object instance = new ReverseSuccessImplementation();
        using var counter = new FirstChanceExceptionCounter();

        var result = instance.TryDuckImplement(typeof(IReverseSuccessTarget), out var proxy);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeTrue();
        proxy.Should().BeAssignableTo<IReverseSuccessTarget>();
    }

    [Fact]
    public void FailingTryDuckImplementForMissingMethodRaisesNoFirstChanceException()
    {
        Warmup();

        object instance = new ReverseMissingMethodImplementation();
        using var counter = new FirstChanceExceptionCounter();

        var result = instance.TryDuckImplement(typeof(IReverseMissingMethodTarget), out var proxy);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
        proxy.Should().BeNull();
    }

    [Fact]
    public void FailingTryDuckImplementForMissingPropertyRaisesNoFirstChanceException()
    {
        Warmup();

        object instance = new ReverseMissingPropertyImplementation();
        using var counter = new FirstChanceExceptionCounter();

        var result = instance.TryDuckImplement(typeof(IReverseMissingPropertyTarget), out var proxy);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
        proxy.Should().BeNull();
    }

    [Fact]
    public void FailingTryDuckImplementForStructBaseRaisesNoFirstChanceException()
    {
        Warmup();

        // Hits the guard at the very top of CreateReverseProxyType, before any IL is written.
        object instance = new ReverseSuccessImplementation();
        using var counter = new FirstChanceExceptionCounter();

        var result = instance.TryDuckImplement(typeof(ReverseStructTarget), out var proxy);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
        proxy.Should().BeNull();
    }

    [Fact]
    public void FailingTryDuckImplementForNamedAttributeArgumentsRaisesNoFirstChanceException()
    {
        Warmup();

        // Reaches AddCustomAttributes, which runs after both property and method creation.
        object instance = new ReverseNamedArgumentsImplementation();
        using var counter = new FirstChanceExceptionCounter();

        var result = instance.TryDuckImplement(typeof(IReverseNamedArgumentsTarget), out var proxy);

        counter.Exceptions.Should().BeEmpty();
        result.Should().BeFalse();
        proxy.Should().BeNull();
    }

    [Fact]
    public void FailingTryDuckCastForUnresolvableGenericParameterTypeNameRaisesNoFirstChanceException()
    {
        Warmup();

        object target = new GenericTypeNameTarget();
        using var counter = new FirstChanceExceptionCounter();

        var result = target.TryDuckCast<IUnresolvableGenericTypeNameProxy>(out _);

        counter.Exceptions.Should().NotContain(x => x is DuckTypeException);
        result.Should().BeFalse();

        // .NET Core 2.1 raises a FileNotFoundException from AssemblyLoadContext.ResolveUsingEvent while
        // probing for the assembly, even though we now ask for throwOnError: false - so this path cannot be
        // made completely first-chance-free there without changing type resolution semantics. Every other
        // supported runtime is clean, so hold them to the stronger guarantee.
#if !NETCOREAPP2_1
        counter.Exceptions.Should().BeEmpty();
#endif
    }

    [Fact]
    public void UnresolvableGenericParameterTypeNameReportsWhichTypeWasMissing()
    {
        object target = new GenericTypeNameMessageTarget();

        var cast = () => target.DuckCast<IUnresolvableGenericTypeNameMessageProxy>();

        // Pins that the failure really is the type-resolution path, not some earlier mismatch.
        cast.Should().Throw<DuckTypeException>()
            .WithMessage("Type not found: Not.A.Real.Type, Not.A.Real.Assembly");
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

    public interface IMissingMethodProxy
    {
        string NotOnTheTarget();
    }

    public interface IWrongArgumentTypeProxy
    {
        string Echo(Guid value);
    }

    public interface IWrongArgumentCountProxy
    {
        string Echo(string a, string b, string c);
    }

    public interface IAmbiguousMethodProxy
    {
        object Echo(object value);
    }

    public interface IUnresolvableGenericTypeNameProxy
    {
        [Duck(GenericParameterTypeNames = new[] { "Not.A.Real.Type, Not.A.Real.Assembly" })]
        string Echo(string value);
    }

    public interface IUnresolvableGenericTypeNameMessageProxy
    {
        [Duck(GenericParameterTypeNames = new[] { "Not.A.Real.Type, Not.A.Real.Assembly" })]
        string Echo(string value);
    }

    public interface IReverseSuccessTarget
    {
        string Value { get; }

        string Echo(string value);
    }

    public interface IReverseMissingMethodTarget
    {
        string Echo(string value);
    }

    public interface IReverseMissingPropertyTarget
    {
        string Value { get; set; }
    }

    public interface IReverseNamedArgumentsTarget
    {
        string Value { get; set; }
    }

    public struct ReverseStructTarget
    {
        public string Value;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ReverseNamedArgumentAttribute : Attribute
    {
        public string Alias { get; set; } = string.Empty;
    }

    public class ReverseSuccessImplementation
    {
        [DuckReverseMethod]
        public string Value => "ok";

        [DuckReverseMethod]
        public string Echo(string value) => value;
    }

    public class ReverseMissingMethodImplementation
    {
        [DuckReverseMethod]
        public string NotOnTheTarget(string value) => value;
    }

    public class ReverseMissingPropertyImplementation
    {
        [DuckReverseMethod]
        public string NotOnTheTarget { get; set; } = "ok";
    }

    [ReverseNamedArgument(Alias = "datadog")]
    public class ReverseNamedArgumentsImplementation
    {
        [DuckReverseMethod]
        public string Value { get; set; } = "ok";
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

    internal class MissingMethodTarget
    {
        public string Present() => "ok";
    }

    internal class WrongArgumentTypeTarget
    {
        public string Echo(string value) => value;
    }

    internal class WrongArgumentCountTarget
    {
        public string Echo(string a) => a;
    }

    internal class AmbiguousMethodTarget
    {
        public string Echo(string value) => value;

        public int Echo(int value) => value;
    }

    internal class GenericTypeNameTarget
    {
        public string Echo<T>(string value) => value;
    }

    internal class GenericTypeNameMessageTarget
    {
        public string Echo<T>(string value) => value;
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

    internal class HarnessSentinelException : Exception
    {
    }
}
