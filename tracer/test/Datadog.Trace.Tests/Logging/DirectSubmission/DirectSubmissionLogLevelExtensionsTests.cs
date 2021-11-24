// <copyright file="DirectSubmissionLogLevelExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Logging.DirectSubmission;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission
{
    public class DirectSubmissionLogLevelExtensionsTests
    {
        [Fact]
        public void GetName_IsValidForAllLevels()
        {
            var allValues = Enum.GetValues(typeof(DirectSubmissionLogLevel))
                                .Cast<DirectSubmissionLogLevel>();

            foreach (var value in allValues)
            {
                value.GetName().Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public void GetName_ThrowsForUnknownLogLevel()
        {
            Assert.Throws<InvalidOperationException>(() => ((DirectSubmissionLogLevel)123).GetName());
        }

        [Fact]
        public void Parse_IsValidForAllValidValues()
        {
            var allValues = Enum.GetValues(typeof(DirectSubmissionLogLevel))
                                .Cast<DirectSubmissionLogLevel>();

            foreach (var value in allValues)
            {
                var parsed = DirectSubmissionLogLevelExtensions.Parse(value.ToString(), defaultLevel: (DirectSubmissionLogLevel)123);
                parsed.Should().Be(value);
            }
        }

        [Theory]
        [InlineData("TRACE")]
        [InlineData("Trace")]
        [InlineData("trace")]
        [InlineData("Verbose")]
        [InlineData("verbose")]
        public void Parse_ReturnsExpectedForKnownValues(string value)
        {
            var defaultValue = (DirectSubmissionLogLevel)123;
            var parsed = DirectSubmissionLogLevelExtensions.Parse(value, defaultValue);
            parsed.Should().Be(DirectSubmissionLogLevel.Verbose);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("INVALID")]
        [InlineData("NOT_A_LEVEL")]
        public void Parse_ReturnsDefaultForUnknownValues(string value)
        {
            var defaultValue = (DirectSubmissionLogLevel)123;
            var parsed = DirectSubmissionLogLevelExtensions.Parse(value, defaultValue);
            parsed.Should().Be(defaultValue);
        }
    }
}
