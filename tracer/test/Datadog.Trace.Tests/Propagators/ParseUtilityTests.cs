// <copyright file="ParseUtilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class ParseUtilityTests
    {
        public static TheoryData<SerializableList<string>, ulong?> UInt64
            => new()
               {
                   { ["42"], 42 },
                   { ["4x"], null },
                   { ["4x", "42"], 42 }, // return first valid value
                   { [string.Empty], null },
                   { [null], null },
                   { ["42"], 42 },
                   { ["4x"], null },
                   { [string.Empty], null },
                   { [null], null },
                   { null, null }, // null collection returns null
               };

        public static TheoryData<SerializableList<string>, int?> Int32
            => new()
               {
                   { ["42"], 42 },
                   { ["4x"], null },
                   { ["4x", "42"], 42 }, // return first valid value
                   { [string.Empty], null },
                   { [null], null },
                   { ["42"], 42 },
                   { ["4x"], null },
                   { [string.Empty], null },
                   { [null], null },
                   { null, null }, // null collection returns null
               };

        public static TheoryData<SerializableList<string>, string> String
            => new()
               {
                   { ["42"], "42" },
                   { ["4x"], "4x" },
                   { ["4x", "42"], "4x" },   // return first valid value
                   { [string.Empty], null }, // null or empty returns null
                   { [null], null },         // null or empty returns null
                   { ["42"], "42" },
                   { ["4x"], "4x" },
                   { [string.Empty], null }, // null or empty returns null
                   { [null], null },         // null or empty returns null
                   { null, null },           // null collection returns null
               };

        [Theory]
        [MemberData(nameof(UInt64))]
        public void ParseUInt64Test(SerializableList<string> values, ulong? expected)
        {
            var result = ParseUtility.ParseUInt64(
                (object)null,
                new FuncGetter<object>((_, _) => values),
                string.Empty);

            result.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(Int32))]
        public void ParseInt32Test(SerializableList<string> values, int? expected)
        {
            var result = ParseUtility.ParseInt32(
                (object)null,
                new FuncGetter<object>((_, _) => values),
                string.Empty);

            result.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(String))]
        public void ParseStringTest(SerializableList<string> values, string expected)
        {
            var result = ParseUtility.ParseString(
                (object)null,
                new FuncGetter<object>((_, _) => values),
                string.Empty);

            result.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(String))]
        public void ParseStringWithHeaders(SerializableList<string> values, string expected)
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
