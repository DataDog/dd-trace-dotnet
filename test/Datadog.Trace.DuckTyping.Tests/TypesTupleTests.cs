// <copyright file="TypesTupleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.DuckTyping.Tests
{
    public class TypesTupleTests
    {
        [Fact]
        public void EqualsTupleTest()
        {
            TypesTuple tuple1 = new TypesTuple(typeof(string), typeof(int));
            TypesTuple tuple2 = new TypesTuple(typeof(string), typeof(int));

            Assert.True(tuple1.Equals(tuple2));
            Assert.True(tuple1.Equals((object)tuple2));
            Assert.False(tuple1.Equals("Hello World"));
        }
    }
}
