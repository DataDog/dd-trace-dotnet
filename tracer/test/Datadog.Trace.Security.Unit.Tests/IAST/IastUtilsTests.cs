// <copyright file="IastUtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST
{
    public class IastUtilsTests
    {
        [Theory]
        [InlineData(null, -1)]
        [InlineData("", 17)]
        [InlineData("a", -1964493196)]
        [InlineData("ab", -24234380)]
        [InlineData("abc", 1247340381)]
        [InlineData("𐐷", -1450785452)]
        public void Test_GetStaticHashCode(string input, int result)
        {
            var i = input.GetStaticHashCode();
            i.Should().Be(result);
        }
    }
}
