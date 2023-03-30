// <copyright file="QueryStringManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Util.Http
{
    public class QueryStringManagerTests
    {
        [Theory]
        [InlineData(false, 5000, null, "")]
        [InlineData(true, 2, null, "ab")]
        [InlineData(true, 0, null, "abcde")]
        [InlineData(true, 0, ".*", "<redacted><redacted>")]
        public void Test(bool reportQueryString, int maxSizeBeforeObfuscation, string pattern, string expectedResult)
        {
            const int timeout = 1000;
            const string inputString = "abcde";
            var logger = new Mock<IDatadogLogger>();

            var queryStringManager = new QueryStringManager(reportQueryString, timeout, maxSizeBeforeObfuscation, pattern, logger.Object);

            var result = queryStringManager.TruncateAndObfuscate(inputString);
            result.Should().Be(expectedResult);
        }
    }
}
