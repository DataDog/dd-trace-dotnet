// <copyright file="ObfuscatorTraceProcessorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Datadog.Trace.Tests.TraceProcessors
{
    public class ObfuscatorTraceProcessorTests
    {
        public static IEnumerable<object[]> GetSqlObfuscatedQuery()
        {
            yield return new object[] { string.Empty, string.Empty };
            yield return new object[] { "   ", "   " };
            yield return new object[] { "         ", "         " };
            yield return new object[] { "罿", "罿" };
            yield return new object[] { "罿潯", "罿潯" };
            yield return new object[] { "罿潯罿潯罿潯罿潯罿潯", "罿潯罿潯罿潯罿潯罿潯" };
            yield return new object[] { "SELECT * FROM TABLE WHERE userId = 'abc1287681964'", "SELECT * FROM TABLE WHERE userId = ?" };
            yield return new object[] { "SELECT * FROM TABLE WHERE userId = 'abc\\'1287681964'", "SELECT * FROM TABLE WHERE userId = ?" };
            yield return new object[] { "SELECT * FROM TABLE WHERE userId = '\\'abc1287681964'", "SELECT * FROM TABLE WHERE userId = ?" };
            yield return new object[] { "SELECT * FROM TABLE WHERE userId = 'abc1287681964\\''", "SELECT * FROM TABLE WHERE userId = ?" };
            yield return new object[] { "SELECT * FROM TABLE WHERE userId = '\\'abc1287681964\\''", "SELECT * FROM TABLE WHERE userId = ?" };
            yield return new object[] { "SELECT * FROM TABLE WHERE userId = 'abc\\'1287681\\'964'", "SELECT * FROM TABLE WHERE userId = ?" };
            yield return new object[] { "SELECT * FROM TABLE WHERE userId = 'abc\\'1287\\'681\\'964'", "SELECT * FROM TABLE WHERE userId = ?" };
            yield return new object[] { "SELECT * FROM TABLE WHERE userId = 'abc\\'1287\\'681\\'\\'\\'\\'964'", "SELECT * FROM TABLE WHERE userId = ?" };
        }

        // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/traceutil/normalize_test.go#L12
        [Theory]
        [MemberData(nameof(GetSqlObfuscatedQuery))]
        public void SqlObfuscatorTests(string inValue, string expectedValue)
        {
            var actualValue = Trace.TraceProcessors.Obfuscator.Normalize(inValue);
            Assert.Equal(expectedValue, actualValue);
        }
    }
}
