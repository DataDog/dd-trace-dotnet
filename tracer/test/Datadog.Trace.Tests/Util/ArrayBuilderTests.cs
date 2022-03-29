// <copyright file="ArrayBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    public class ArrayBuilderTests
    {
        // Store the builder in a field, to make sure copy-semantics of structs don't get in the way
        // of how the type is expected to be used
        private ArrayBuilder<int> _builder;

        [Fact]
        public void BuildArray()
        {
            const int numberOfElements = 20;

            _builder = default;

            for (int i = 0; i < numberOfElements; i++)
            {
                _builder.Add(i);
            }

            _builder.Count.Should().Be(numberOfElements);

            var result = _builder.GetArray();

            result.Should().BeEquivalentTo(Enumerable.Range(0, numberOfElements));
        }

        [Fact]
        public void InitialCapacity()
        {
            const int numberOfElements = 10;

            var builder = new ArrayBuilder<int>(numberOfElements);

            var result = builder.GetArray();

            result.Count.Should().Be(0);
            result.Array.Length.Should().Be(numberOfElements);
        }

        [Fact]
        public void Empty()
        {
            ArrayBuilder<int> builder = default;

            var result = builder.GetArray();

            result.Count.Should().Be(0);
        }

        [Fact]
        public void DoubleSizeWhenGrowing()
        {
            ArrayBuilder<int> builder = default;

            builder.GetArray().Array.Should().BeEmpty();

            for (int i = 0; i < 4; i++)
            {
                builder.Add(i);
                builder.GetArray().Array.Should().HaveCount(4);
            }

            for (int i = 0; i < 4; i++)
            {
                builder.Add(i);
                builder.GetArray().Array.Should().HaveCount(8);
            }

            for (int i = 0; i < 8; i++)
            {
                builder.Add(i);
                builder.GetArray().Array.Should().HaveCount(16);
            }
        }
    }
}
