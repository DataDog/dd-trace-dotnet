// <copyright file="ParseUtilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class ParseUtilityTests
    {
        public static TheoryData<IEnumerable<string>, ulong?> UInt64
            => new()
            {
                { new[] { "42" }, 42 },
                { new[] { "4x" }, null },
                { new[] { "4x", "42" }, 42 }, // return first valid value
                { new[] { string.Empty }, null },
                { new[] { (string)null }, null },
                { new List<string> { "42" }, 42 },
                { new List<string> { "4x" }, null },
                { new List<string> { string.Empty }, null },
                { new List<string> { null }, null },
                { null, null }, // null collection returns null
            };

        public static TheoryData<IEnumerable<string>, int?> Int32
            => new()
            {
                { new[] { "42" }, 42 },
                { new[] { "4x" }, null },
                { new[] { "4x", "42" }, 42 }, // return first valid value
                { new[] { string.Empty }, null },
                { new[] { (string)null }, null },
                { new List<string> { "42" }, 42 },
                { new List<string> { "4x" }, null },
                { new List<string> { string.Empty }, null },
                { new List<string> { null }, null },
                { null, null }, // null collection returns null
            };

        public static TheoryData<IEnumerable<string>, string> String
            => new()
            {
                { new[] { "42" }, "42" },
                { new[] { "4x" }, "4x" },
                { new[] { "4x", "42" }, "4x" },   // return first valid value
                { new[] { string.Empty }, null }, // null or empty returns null
                { new[] { (string)null }, null }, // null or empty returns null
                { new List<string> { "42" }, "42" },
                { new List<string> { "4x" }, "4x" },
                { new List<string> { string.Empty }, null }, // null or empty returns null
                { new List<string> { null }, null },         // null or empty returns null
                { null, null },                              // null collection returns null
            };

        [Theory]
        [MemberData(nameof(UInt64))]
        public void ParseUInt64Test(IEnumerable<string> values, ulong? expected)
        {
            var result = ParseUtility.ParseUInt64(
                (object)null,
                new FuncGetter<object>((_, _) => values),
                string.Empty);

            result.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(Int32))]
        public void ParseInt32Test(IEnumerable<string> values, int? expected)
        {
            var result = ParseUtility.ParseInt32(
                (object)null,
                new FuncGetter<object>((_, _) => values),
                string.Empty);

            result.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(String))]
        public void ParseStringTest(IEnumerable<string> values, string expected)
        {
            var result = ParseUtility.ParseString(
                (object)null,
                new FuncGetter<object>((_, _) => values),
                string.Empty);

            result.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(String))]
        public void ParseStringWithHeaders(IEnumerable<string> values, string expected)
        {
            var result = ParseUtility.ParseString(
                new HeaderStruct(() => values),
                string.Empty);

            result.Should().Be(expected);
        }

        private readonly struct FuncGetter<TCarrier> : ICarrierGetter<TCarrier>
        {
            private readonly Func<TCarrier, string, IEnumerable<string>> _getter;

            public FuncGetter(Func<TCarrier, string, IEnumerable<string>> getter)
            {
                _getter = getter;
            }

            public IEnumerable<string> Get(TCarrier carrier, string key)
            {
                return _getter(carrier, key);
            }
        }

        private readonly struct HeaderStruct : IHeadersCollection
        {
            private readonly Func<IEnumerable<string>> _getter;

            public HeaderStruct(Func<IEnumerable<string>> getter)
            {
                _getter = getter;
            }

            public IEnumerable<string> GetValues(string name)
            {
                return _getter();
            }

            public void Set(string name, string value)
            {
                throw new NotImplementedException();
            }

            public void Add(string name, string value)
            {
                throw new NotImplementedException();
            }

            public void Remove(string name)
            {
                throw new NotImplementedException();
            }
        }
    }
}
