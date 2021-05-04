// <copyright file="PropertyFetcherTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Util;
using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class PropertyFetcherTests
    {
        [Fact]
        public void ReferenceTypeObject_FetchesReferenceTypeProperty()
        {
            const string expected = "ReferenceType";

            var element = new ExampleReferenceType(123, expected);
            var fetcher = new PropertyFetcher("Name");
            var actual = fetcher.Fetch<string>(element);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ReferenceTypeObject_FetchesValueTypeProperty()
        {
            const int expected = 123;

            var element = new ExampleReferenceType(expected, "ReferenceType");
            var fetcher = new PropertyFetcher("Id");
            var actual = fetcher.Fetch<int>(element);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValueTypeObject_FetchesReferenceTypeProperty()
        {
            const string expected = "ValueType";

            var element = new ExampleValueType(123, expected);
            var fetcher = new PropertyFetcher("Name");
            var actual = fetcher.Fetch<string>(element);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValueTypeObject_FetchesValueTypeProperty()
        {
            const int expected = 123;

            var element = new ExampleValueType(expected, "ValueType");
            var fetcher = new PropertyFetcher("Id");
            var actual = fetcher.Fetch<int>(element);

            Assert.Equal(expected, actual);
        }
    }

    internal class ExampleReferenceType
    {
        public int Id { get; }

        public string Name { get; }

        public ExampleReferenceType(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    internal struct ExampleValueType
    {
        public int Id { get; }

        public string Name { get; }

        public ExampleValueType(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
