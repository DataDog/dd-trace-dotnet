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
        [Theory]
        [InlineData("00000000075bcd15", 123456789)]
        [InlineData("gfd", 0)]
        public void ParseFromHexOrDefaultTest(string hex, ulong res)
        {
            ParseUtility.ParseFromHexOrDefault(hex).Should().Be(res);
        }

        [Theory]
        [InlineData("42", 42UL)]
        [InlineData("4x", null)]
        public void ParseUInt64Test(string actual, object expected)
        {
            var actualResult = ParseUtility.ParseUInt64(
                (object)null,
                new FuncGetter<object>((carrier, name) => new[] { actual }),
                string.Empty);

            actualResult.Should().Be((ulong?)expected);

            actualResult = ParseUtility.ParseUInt64(
                (object)null,
                new FuncGetter<object>((carrier, name) => new List<string> { actual }),
                string.Empty);

            actualResult.Should().Be((ulong?)expected);
        }

        [Theory]
        [InlineData("42", 42)]
        [InlineData("4x", null)]
        public void ParseInt32Test(string actual, object expected)
        {
            var actualResult = ParseUtility.ParseInt32(
                (object)null,
                new FuncGetter<object>((carrier, name) => new[] { actual }),
                string.Empty);

            actualResult.Should().Be((int?)expected);

            actualResult = ParseUtility.ParseInt32(
                (object)null,
                new FuncGetter<object>((carrier, name) => new List<string> { actual }),
                string.Empty);

            actualResult.Should().Be((int?)expected);
        }

        [Theory]
        [InlineData("42", "42")]
        [InlineData("4x", "4x")]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ParseString(string actual, string expected)
        {
            var actualResult = ParseUtility.ParseString(
                (object)null,
                new FuncGetter<object>((carrier, name) => new[] { actual }),
                string.Empty);

            actualResult.Should().Be(expected);

            actualResult = ParseUtility.ParseString(
                (object)null,
                new FuncGetter<object>((carrier, name) => new List<string> { actual }),
                string.Empty);

            actualResult.Should().Be(expected);
        }

        [Theory]
        [InlineData("42", "42")]
        [InlineData("4x", "4x")]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ParseStringWithHeaders(string actual, string expected)
        {
            var actualResult = ParseUtility.ParseString(
                new HeaderStruct(() => new[] { actual }),
                string.Empty);

            actualResult.Should().Be(expected);

            actualResult = ParseUtility.ParseString(
                new HeaderStruct(() => new List<string> { actual }),
                string.Empty);

            actualResult.Should().Be(expected);
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
