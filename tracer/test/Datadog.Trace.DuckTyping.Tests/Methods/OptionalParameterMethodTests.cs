// <copyright file="OptionalParameterMethodTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Methods;

#pragma warning disable SA1201 // Elements should appear in the correct order

public class OptionalParameterMethodTests
{
    [Fact]
    public void OmittedOptionalTargetParametersUseTheirDeclaredDefaults()
    {
        var proxy = new OptionalParameterTarget().DuckCast<IOptionalParameterProxy>();

        proxy.Boolean().Should().BeTrue();
        proxy.Character().Should().Be('\u1234');
        proxy.SignedByte().Should().Be(-100);
        proxy.Byte().Should().Be(200);
        proxy.Int16().Should().Be(-12_345);
        proxy.UInt16().Should().Be(54_321);
        proxy.Int32().Should().Be(-123_456_789);
        proxy.UInt32().Should().Be(4_000_000_000);
        proxy.Int64().Should().Be(-1_234_567_890_123_456_789);
        proxy.UInt64().Should().Be(18_000_000_000_000_000_000);
        proxy.Single().Should().Be(1.25f);
        proxy.Double().Should().Be(-2.5);
        proxy.Decimal().Should().Be(1.5m);
        proxy.String().Should().Be("expected");
        proxy.NullReference().Should().BeTrue();
        proxy.Enum().Should().Be(200);
        proxy.Nullable().Should().BeTrue();
        proxy.DateTime().Should().Be(0);
        proxy.DateTimeConstant().Should().Be(638_000_000_000_000_000);
        proxy.Struct().Should().Be(0);
        proxy.Multiple().Should().Be(42);
    }

    [Fact]
    public void OptionalAttributeWithoutConstantUsesCompilerDefaults()
    {
        var proxy = new OptionalAttributeTarget().DuckCast<IOptionalAttributeProxy>();

        proxy.ValueType().Should().Be(0);
        proxy.ReferenceType().Should().BeTrue();
        proxy.ExplicitDefault().Should().Be(7);
    }

    [Fact]
    public void ExactArityOverloadIsPreferredOverOmittingOptionalArgument()
    {
        var proxy = new OverloadTarget().DuckCast<IOverloadProxy>();

        proxy.GetValue("expected").Should().Be(1);
    }

    [Fact]
    public void OmittedCallerInfoParameterIsRejected()
    {
        var target = new CallerInfoTarget();

        DuckType.CanCreate<ICallerInfoProxy>(target).Should().BeFalse();
        target.DuckIs<ICallerInfoProxy>().Should().BeFalse();
        target.TryDuckCast<ICallerInfoProxy>(out var proxy).Should().BeFalse();
        proxy.Should().BeNull();

        DuckType.CanCreate<ICallerFilePathProxy>(new CallerFilePathTarget()).Should().BeFalse();
        DuckType.CanCreate<ICallerLineNumberProxy>(new CallerLineNumberTarget()).Should().BeFalse();
#if NETCOREAPP3_1_OR_GREATER
        DuckType.CanCreate<ICallerArgumentExpressionProxy>(new CallerArgumentExpressionTarget()).Should().BeFalse();
#endif
    }

    [Fact]
    public void OptionalTargetParameterCanBeSuppliedByProxy()
    {
        var target = new OptionalParameterTarget();
        var proxy = target.DuckCast<IExplicitParameterProxy>();

        proxy.Int32(21).Should().Be(21);
    }

    [Fact]
    public void OmittedRequiredTargetParameterIsRejected()
    {
        var target = new RequiredParameterTarget();

        DuckType.CanCreate<IRequiredParameterProxy>(target).Should().BeFalse();
        target.DuckIs<IRequiredParameterProxy>().Should().BeFalse();
        target.TryDuckCast<IRequiredParameterProxy>(out var proxy).Should().BeFalse();
        proxy.Should().BeNull();
    }

    private interface IOptionalParameterProxy
    {
        bool Boolean();

        char Character();

        sbyte SignedByte();

        byte Byte();

        short Int16();

        ushort UInt16();

        int Int32();

        uint UInt32();

        long Int64();

        ulong UInt64();

        float Single();

        double Double();

        decimal Decimal();

        string String();

        bool NullReference();

        int Enum();

        bool Nullable();

        long DateTime();

        long DateTimeConstant();

        int Struct();

        int Multiple();
    }

    private interface IOptionalAttributeProxy
    {
        int ValueType();

        bool ReferenceType();

        int ExplicitDefault();
    }

    private interface IOverloadProxy
    {
        int GetValue(object value);
    }

    private interface ICallerInfoProxy
    {
        string GetCaller();
    }

    private interface ICallerFilePathProxy
    {
        string GetCallerFilePath();
    }

    private interface ICallerLineNumberProxy
    {
        int GetCallerLineNumber();
    }

#if NETCOREAPP3_1_OR_GREATER
    private interface ICallerArgumentExpressionProxy
    {
        string GetCallerArgumentExpression(object value);
    }
#endif

    private interface IExplicitParameterProxy
    {
        int Int32(int value);
    }

    private interface IRequiredParameterProxy
    {
        int Required();
    }

    private class OptionalParameterTarget
    {
        public bool Boolean(bool value = true) => value;

        public char Character(char value = '\u1234') => value;

        public sbyte SignedByte(sbyte value = -100) => value;

        public byte Byte(byte value = 200) => value;

        public short Int16(short value = -12_345) => value;

        public ushort UInt16(ushort value = 54_321) => value;

        public int Int32(int value = -123_456_789) => value;

        public uint UInt32(uint value = 4_000_000_000) => value;

        public long Int64(long value = -1_234_567_890_123_456_789) => value;

        public ulong UInt64(ulong value = 18_000_000_000_000_000_000) => value;

        public float Single(float value = 1.25f) => value;

        public double Double(double value = -2.5) => value;

        public decimal Decimal(decimal value = 1.5m) => value;

        public string String(string value = "expected") => value;

        public bool NullReference(string value = null) => value is null;

        public int Enum(ByteEnum value = ByteEnum.Expected) => (int)value;

        public bool Nullable(int? value = null) => value is null;

        public long DateTime(DateTime value = default) => value.Ticks;

        public long DateTimeConstant([Optional, DateTimeConstant(638_000_000_000_000_000)] DateTime value) => value.Ticks;

        public int Struct(OptionalStruct value = default) => value.Value;

        public int Multiple(int first = 21, int second = 21) => first + second;
    }

    private class OverloadTarget
    {
        public int GetValue(string value, int optional = 2) => optional;

        public int GetValue(Uri value, int optional = 3) => optional;

        public int GetValue(string value) => 1;
    }

    private class CallerInfoTarget
    {
        public string GetCaller([CallerMemberName] string caller = null) => caller;
    }

    private class CallerFilePathTarget
    {
        public string GetCallerFilePath([CallerFilePath] string path = null) => path;
    }

    private class CallerLineNumberTarget
    {
        public int GetCallerLineNumber([CallerLineNumber] int line = 0) => line;
    }

#if NETCOREAPP3_1_OR_GREATER
    private class CallerArgumentExpressionTarget
    {
        public string GetCallerArgumentExpression(
            object value,
            [CallerArgumentExpression("value")] string expression = null)
            => expression;
    }
#endif

    private class OptionalAttributeTarget
    {
        public int ValueType([Optional] int value) => value;

        public bool ReferenceType([Optional] object value) => ReferenceEquals(value, Missing.Value);

        public int ExplicitDefault([Optional, DefaultParameterValue(7)] int value) => value;
    }

    private class RequiredParameterTarget
    {
        public int Required(int value) => value;
    }

    private enum ByteEnum : byte
    {
        Expected = 200,
    }

    private struct OptionalStruct
    {
        public int Value { get; set; }
    }
}
