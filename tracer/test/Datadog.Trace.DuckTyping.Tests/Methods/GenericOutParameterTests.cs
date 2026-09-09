// <copyright file="GenericOutParameterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Methods
{
    public class GenericOutParameterTests
    {
        private const byte BeforeSentinel = 0xA5;
        private const byte AfterSentinel = 0x5A;

        public enum GenericOutParameterType
        {
            Boolean,
            Byte,
            SByte,
            Int16,
            UInt16,
            Char,
            Int32,
            UInt32,
            Single,
            Int64,
            UInt64,
            Double,
            Decimal,
            Guid,
            ThreeByteStruct,
            TwelveByteStruct,
            String,
            Object,
        }

        private interface IGenericOutParameterProxy
        {
            void GetDefault<T>(out T output);

            void Preserve<T>(ref T value);
        }

        [Theory]
        [InlineData(GenericOutParameterType.Boolean)]
        [InlineData(GenericOutParameterType.Byte)]
        [InlineData(GenericOutParameterType.SByte)]
        [InlineData(GenericOutParameterType.Int16)]
        [InlineData(GenericOutParameterType.UInt16)]
        [InlineData(GenericOutParameterType.Char)]
        [InlineData(GenericOutParameterType.Int32)]
        [InlineData(GenericOutParameterType.UInt32)]
        [InlineData(GenericOutParameterType.Single)]
        [InlineData(GenericOutParameterType.Int64)]
        [InlineData(GenericOutParameterType.UInt64)]
        [InlineData(GenericOutParameterType.Double)]
        [InlineData(GenericOutParameterType.Decimal)]
        [InlineData(GenericOutParameterType.Guid)]
        [InlineData(GenericOutParameterType.ThreeByteStruct)]
        [InlineData(GenericOutParameterType.TwelveByteStruct)]
        [InlineData(GenericOutParameterType.String)]
        [InlineData(GenericOutParameterType.Object)]
        public void GenericOutAndRefParametersDoNotCorruptAdjacentFields(GenericOutParameterType parameterType)
        {
            var proxy = new GenericOutParameterTarget().DuckCast<IGenericOutParameterProxy>();

            switch (parameterType)
            {
                case GenericOutParameterType.Boolean:
                    AssertGenericByRefParameters(proxy, true);
                    break;
                case GenericOutParameterType.Byte:
                    AssertGenericByRefParameters(proxy, byte.MaxValue);
                    break;
                case GenericOutParameterType.SByte:
                    AssertGenericByRefParameters(proxy, sbyte.MinValue);
                    break;
                case GenericOutParameterType.Int16:
                    AssertGenericByRefParameters(proxy, short.MinValue);
                    break;
                case GenericOutParameterType.UInt16:
                    AssertGenericByRefParameters(proxy, ushort.MaxValue);
                    break;
                case GenericOutParameterType.Char:
                    AssertGenericByRefParameters(proxy, '\u1234');
                    break;
                case GenericOutParameterType.Int32:
                    AssertGenericByRefParameters(proxy, int.MinValue);
                    break;
                case GenericOutParameterType.UInt32:
                    AssertGenericByRefParameters(proxy, uint.MaxValue);
                    break;
                case GenericOutParameterType.Single:
                    AssertGenericByRefParameters(proxy, 123.5f);
                    break;
                case GenericOutParameterType.Int64:
                    AssertGenericByRefParameters(proxy, long.MinValue);
                    break;
                case GenericOutParameterType.UInt64:
                    AssertGenericByRefParameters(proxy, ulong.MaxValue);
                    break;
                case GenericOutParameterType.Double:
                    AssertGenericByRefParameters(proxy, 123.5d);
                    break;
                case GenericOutParameterType.Decimal:
                    AssertGenericByRefParameters(proxy, 123.5m);
                    break;
                case GenericOutParameterType.Guid:
                    AssertGenericByRefParameters(proxy, new Guid("00112233-4455-6677-8899-aabbccddeeff"));
                    break;
                case GenericOutParameterType.ThreeByteStruct:
                    AssertGenericByRefParameters(proxy, new ThreeByteStruct(0x12, 0x34, 0x56));
                    break;
                case GenericOutParameterType.TwelveByteStruct:
                    AssertGenericByRefParameters(proxy, new TwelveByteStruct(0x0123456789abcdef, 0x12345678));
                    break;
                case GenericOutParameterType.String:
                    AssertGenericByRefParameters(proxy, "expected");
                    break;
                case GenericOutParameterType.Object:
                    AssertGenericByRefParameters(proxy, new object());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(parameterType), parameterType, null);
            }
        }

        private static void AssertGenericByRefParameters<T>(IGenericOutParameterProxy proxy, T initialValue)
        {
            var outGuarded = new GuardedValue<T>
            {
                Before = BeforeSentinel,
                Value = initialValue,
                After = AfterSentinel,
            };

            proxy.GetDefault<T>(out outGuarded.Value);

            Assert.Equal(BeforeSentinel, outGuarded.Before);
            Assert.Equal(default(T), outGuarded.Value);
            Assert.Equal(AfterSentinel, outGuarded.After);

            var refGuarded = new GuardedValue<T>
            {
                Before = BeforeSentinel,
                Value = initialValue,
                After = AfterSentinel,
            };

            proxy.Preserve<T>(ref refGuarded.Value);

            Assert.Equal(BeforeSentinel, refGuarded.Before);
            Assert.Equal(initialValue, refGuarded.Value);
            Assert.Equal(AfterSentinel, refGuarded.After);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GuardedValue<T>
        {
            public byte Before;
            public T Value;
            public byte After;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private readonly struct ThreeByteStruct
        {
            private readonly byte _first;
            private readonly byte _second;
            private readonly byte _third;

            public ThreeByteStruct(byte first, byte second, byte third)
            {
                _first = first;
                _second = second;
                _third = third;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private readonly struct TwelveByteStruct
        {
            private readonly long _first;
            private readonly int _second;

            public TwelveByteStruct(long first, int second)
            {
                _first = first;
                _second = second;
            }
        }

        private class GenericOutParameterTarget
        {
            public void GetDefault<T>(out T output) => output = default;

            public void Preserve<T>(ref T value)
            {
            }
        }
    }
}
