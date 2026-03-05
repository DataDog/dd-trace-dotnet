// <copyright file="ServerlessCompatPipeNameHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Serverless;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Serverless
{
    public class ServerlessCompatPipeNameHelperTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("x")]
        [InlineData("dd_trace")]
        [InlineData("dd_dogstatsd")]
        public void GenerateUniquePipeName_ReturnsCorrectFormat(string baseName)
        {
            var result = ServerlessCompatPipeNameHelper.GenerateUniquePipeName(baseName, "test");

            // Format: {baseName}_{32-char-hex-guid}
            result.Should().StartWith(baseName + "_");
            var guidPart = result.Substring(baseName.Length + 1);
            guidPart.Should().HaveLength(32);
            Guid.TryParse(guidPart, out _).Should().BeTrue();
        }

        [Fact]
        public void GenerateUniquePipeName_TruncatesBaseNameExceeding214Chars()
        {
            var longBase = new string('a', 215);

            var result = ServerlessCompatPipeNameHelper.GenerateUniquePipeName(longBase, "test");

            // Truncated to 214 + "_" + 32-char guid = 247 total
            result.Should().HaveLength(214 + 1 + 32);
        }

        [Fact]
        public void GenerateUniquePipeName_BaseNameAtLimit_IsNotTruncated()
        {
            var exactBase = new string('a', 214);

            var result = ServerlessCompatPipeNameHelper.GenerateUniquePipeName(exactBase, "test");

            result.Should().StartWith(exactBase + "_");
            result.Should().HaveLength(214 + 1 + 32);
        }

        [Fact]
        public void GenerateUniquePipeName_ProducesDifferentNamesPerCall()
        {
            var first = ServerlessCompatPipeNameHelper.GenerateUniquePipeName("dd_trace", "test");
            var second = ServerlessCompatPipeNameHelper.GenerateUniquePipeName("dd_trace", "test");

            first.Should().NotBe(second);
        }
    }
}
