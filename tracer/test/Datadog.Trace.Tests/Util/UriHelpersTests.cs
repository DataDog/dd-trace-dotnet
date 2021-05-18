// <copyright file="UriHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    public class UriHelpersTests
    {
        [Theory]
        [InlineData("/", "/")]
        [InlineData("/controller/action/b37855d4bae34bd3b3357fc554ad334e", "/controller/action/?")]
        [InlineData("/controller/action/14bb2eed-34f0-4aa2-b2c3-09c0e2166d4d", "/controller/action/?")]
        [InlineData("/controller/action/14bb2eed-34f0X4aa2-b2c3-09c0e2166d4d", "/controller/action/14bb2eed-34f0X4aa2-b2c3-09c0e2166d4d")] // contains non-hex letters
        [InlineData("/controller/action/14bb2eed-34f0-4aa2Xb2c3-09c0e2166d4d", "/controller/action/14bb2eed-34f0-4aa2Xb2c3-09c0e2166d4d")] // contains non-hex letters
        [InlineData("/controller/action/14bb2eed-34f0A4aa2Bb2c3C09c0e2166d4d", "/controller/action/?")]
        [InlineData("/controller/action/12345678901234567890123456789012345678901234567890", "/controller/action/?")]
        [InlineData("/controller/action/eeeee123", "/controller/action/eeeee123")] // Too short
        [InlineData("/controller/action/0123456789ABCDE", "/controller/action/0123456789ABCDE")] // Too short
        [InlineData("/controller/action/01234567890ABCDEFGH", "/controller/action/01234567890ABCDEFGH")] // Contains non-hex letters
        [InlineData("/controller/action/0123456789ABCDEF", "/controller/action/?")]
        [InlineData("/controller/action/0123456789ABCDEF0", "/controller/action/?")]
        [InlineData("/controller/action/01234567_89ABCDEF", "/controller/action/01234567_89ABCDEF")] // only hyphen '-' allowed other than hex
        [InlineData("/controller/action/123-456-789", "/controller/action/?")]
        [InlineData("/controller/action/eeeeeeeeeeeeeeeee", "/controller/action/eeeeeeeeeeeeeeeee")] // No numbers
        [InlineData("/DataDog/dd-trace-dotnet/blob/e2d83dec7d6862d4181937776ddaf72819e291ce/src/Datadog.Trace/Util/UriHelpers.cs", "/DataDog/dd-trace-dotnet/blob/?/src/Datadog.Trace/Util/UriHelpers.cs")]
        [InlineData("/controller/action/2022", "/controller/action/?")]
        [InlineData("/controller/action/", "/controller/action/")]
        public void GetCleanUriPath_ShouldRemoveIdsFromPaths(string url, string expected)
        {
            Assert.Equal(expected, Trace.Util.UriHelpers.GetCleanUriPath(url));
        }

        [Theory]
        [InlineData("http://localhost:5040", "/")]
        [InlineData("http://localhost:5040/", "/")]
        [InlineData("http://localhost:5040/controller/", "/controller/")]
        [InlineData("http://localhost:5040/controller/action/2022", "/controller/action/?")]
        [InlineData("https://localhost:5040/controller/action/2022", "/controller/action/?")]
        [InlineData("https://example.org/controller/action/2022", "/controller/action/?")]
        [InlineData("ftp://example.org/controller/action/2022", "/controller/action/?")]
        public void GetCleanUriPath_ByUri_ShouldExtractThePathAndRemoveIds(string url, string expected)
        {
            Assert.Equal(expected, Trace.Util.UriHelpers.GetCleanUriPath(new Uri(url)));
        }
    }
}
