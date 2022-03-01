// <copyright file="SecuritySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class SecuritySettingsTests
    {
        [Fact]
        public void NoPostFixShouldDefaultToMicroSeconds()
        {
            var target = CreateTestTarget("500");

            Assert.Equal(500ul, target.WafTimeoutMicroSeconds);
        }

        [Fact]
        public void UnknowPostfixShouldMeanDefaultValue()
        {
            var target = CreateTestTarget("500d");

            Assert.Equal(100_000ul, target.WafTimeoutMicroSeconds);
        }

        [Fact]
        public void JunkShouldMeanDefaultValue()
        {
            var target = CreateTestTarget("gibberish");

            Assert.Equal(100_000ul, target.WafTimeoutMicroSeconds);
        }

        [Fact]
        public void NullShouldMeanDefaultValue()
        {
            var target = CreateTestTarget(null);

            Assert.Equal(100_000ul, target.WafTimeoutMicroSeconds);
        }

        [Fact]
        public void TestSecondsPostFix()
        {
            var target = CreateTestTarget("5s");

            Assert.Equal(5_000_000ul, target.WafTimeoutMicroSeconds);
        }

        [Fact]
        public void TestMillSecondsPostFix()
        {
            var target = CreateTestTarget("50ms");

            Assert.Equal(50_000ul, target.WafTimeoutMicroSeconds);
        }

        [Fact]
        public void TestMicroSecondsPostFix()
        {
            var target = CreateTestTarget("500us");

            Assert.Equal(500ul, target.WafTimeoutMicroSeconds);
        }

        private static SecuritySettings CreateTestTarget(string stringToBeParsed)
        {
            var configurationSourceMock = new Mock<IConfigurationSource>();

            configurationSourceMock.Setup(x => x.GetString(It.IsAny<string>())).Returns(stringToBeParsed);

            var target = new SecuritySettings(configurationSourceMock.Object);
            return target;
        }
    }
}
