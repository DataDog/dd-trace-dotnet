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
        public void GetStaticHashCode_IsDeterministic(string input, int result)
        {
            var i = input.GetStaticHashCode();
            i.Should().Be(result);
        }

        [Fact]
        public void GetStaticHashCode_Vulnerability_GetHashCode_IsDeterministic()
        {
            var source = new Source(SourceType.RequestParameterName, "name", "sqlvalue1");
            var source2 = new Source(SourceType.RequestParameterValue, "name2", "sql_value2");
            var ranges = new Range[] { new Range(0, 2, source), new Range(2, 2, source2) };
            var evidence = new Evidence("sql_query", ranges);
            var vulnerability = new Vulnerability("sqli", new Location(), evidence);

            var i = vulnerability.GetHashCode();
            i.Should().Be(453421860);
        }
    }
}
