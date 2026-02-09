// <copyright file="UriHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using FluentAssertions;
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
        [InlineData("/some-file/123/432/234.png", "/some-file/?/?/?.png")]
        [InlineData("/some-file/1234.png", "/some-file/?.png")]
        [InlineData("/some-file/123.456.png", "/some-file/123.456.png")]
        [InlineData("/some-file/1234.", "/some-file/?.")]
        [InlineData("/some-file/1234.c", "/some-file/?.c")]
        [InlineData("/some-file/.12345", "/some-file/.12345")]
        [InlineData("/some-file/123.12345/nada", "/some-file/123.12345/nada")]
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
        [InlineData("ftp://example.org/controller/action/2022.png", "/controller/action/?.png")]
        public void GetCleanUriPath_ByUri_ShouldExtractThePathAndRemoveIds(string url, string expected)
        {
            Assert.Equal(expected, Trace.Util.UriHelpers.GetCleanUriPath(new Uri(url), virtualPathToRemove: null));
        }

        [Theory]
        [InlineData("http://localhost:5040", "", "/")]
        [InlineData("http://localhost:5040", "/", "/")]
        [InlineData("http://localhost:5040/", "", "/")]
        [InlineData("http://localhost:5040/", "/", "/")]
        [InlineData("http://localhost:5040/controller/", "", "/controller/")]
        [InlineData("http://localhost:5040/controller/", "/", "/controller/")]
        [InlineData("http://localhost:5040/controller/", "/Some-value", "/controller/")]
        [InlineData("http://localhost:5040/Some-value/controller/", "", "/Some-value/controller/")]
        [InlineData("http://localhost:5040/Some-value/controller/", "/", "/Some-value/controller/")]
        [InlineData("http://localhost:5040/Some-value/controller/", "/Some-value", "/controller/")]
        [InlineData("http://localhost:5040/controller/action/2022", "/weeble", "/controller/action/?")]
        [InlineData("https://localhost:5040/WEEBLE/controller/action/2022", "/weeble", "/controller/action/?")]
        [InlineData("https://example.org/controller/action/2022", "/sup", "/controller/action/?")]
        [InlineData("https://example.org/supcontroller/action/2022", "/sup", "/supcontroller/action/?")]
        [InlineData("https://example.org/sup/controller/action/2022", "/sup", "/controller/action/?")]
        [InlineData("https://example.org/sup/sup/controller/action/2022", "/sup", "/sup/controller/action/?")]
        [InlineData("https://example.org/sup/sup/controller/action/2022", "/sup/sup", "/controller/action/?")]
        public void GetCleanUriPath_ByUri_ShouldRemoveThePrefixIfPresent(string url, string prefix, string expected)
        {
            Assert.Equal(expected, Trace.Util.UriHelpers.GetCleanUriPath(new Uri(url), prefix));
        }

        [Theory]
        [InlineData("http://localhost", "some/path")]
        [InlineData("http://localhost/", "some/path")]
        [InlineData("http://localhost", "/some/path")]
        [InlineData("http://localhost/", "/some/path")]
        [InlineData("http://localhost/some", "path")]
        [InlineData("http://localhost/some/", "path")]
        [InlineData("http://localhost/some", "/path")]
        [InlineData("http://localhost/some/", "/path")]
        public void CombineUri_ShouldMergeCorrectly(string baseUri, string relativePath)
        {
            Trace.Util.UriHelpers.Combine(new Uri(baseUri), relativePath).Should().Be("http://localhost/some/path");
        }

        [Theory]
        [InlineData("http://localhost", "some/path")]
        [InlineData("http://localhost/", "some/path")]
        [InlineData("http://localhost", "/some/path")]
        [InlineData("http://localhost/", "/some/path")]
        [InlineData("http://localhost/some", "path")]
        [InlineData("http://localhost/some/", "path")]
        [InlineData("http://localhost/some", "/path")]
        [InlineData("http://localhost/some/", "/path")]
        public void CombineString_ShouldMergeCorrectly(string baseUri, string relativePath)
        {
            Trace.Util.UriHelpers.Combine(baseUri, relativePath).Should().Be("http://localhost/some/path");
        }

        [Theory]
        [InlineData("http://localhost", "some/path")]
        [InlineData("http://localhost/", "some/path")]
        [InlineData("http://localhost", "/some/path")]
        [InlineData("http://localhost/", "/some/path")]
        [InlineData("http://localhost/some", "path")]
        [InlineData("http://localhost/some/", "path")]
        [InlineData("http://localhost/some", "/path")]
        [InlineData("http://localhost/some/", "/path")]
        public void CombineString_ShouldMergeAbsolutePathCorrectly(string baseUri, string relativePath)
        {
            Trace.Util.UriHelpers.Combine(new Uri(baseUri).AbsolutePath, relativePath).Should().Be("/some/path");
        }

        [Theory]
        // Empty and edge cases
        [InlineData("", 0, 0, false)] // Empty segment
        [InlineData("abc", 0, 0, false)] // Zero length
        [InlineData("abc", 5, 1, false)] // Start index beyond string (handled by loop bounds)
        // Pure numbers - should be identifiers
        [InlineData("123", 0, 3, true)]
        [InlineData("0", 0, 1, true)]
        [InlineData("999999999999999999999999999999", 0, 30, true)]
        [InlineData("2024", 0, 4, true)]
        // Numbers with hyphens - should be identifiers
        [InlineData("123-456", 0, 7, true)]
        [InlineData("123-456-789", 0, 11, true)]
        [InlineData("1-2-3-4-5", 0, 9, true)]
        // Pure hex letters (no numbers) - not identifiers
        [InlineData("abcdef", 0, 6, false)]
        [InlineData("ABCDEF", 0, 6, false)]
        [InlineData("abcdefabcdefabcdef", 0, 18, false)] // Long but no numbers
        // Short hex with numbers (< 16 chars) - not identifiers (too aggressive)
        [InlineData("abc123", 0, 6, false)]
        [InlineData("12ab34", 0, 6, false)]
        [InlineData("0123456789ABCDE", 0, 15, false)] // 15 chars - just under threshold
        // Long hex with numbers (>= 16 chars) - should be identifiers
        [InlineData("0123456789ABCDEF", 0, 16, true)] // Exactly 16 chars
        [InlineData("0123456789abcdef", 0, 16, true)] // Lowercase
        [InlineData("a1b2c3d4e5f6a1b2c3d4", 0, 20, true)]
        [InlineData("b37855d4bae34bd3b3357fc554ad334e", 0, 32, true)] // 32-char hex (MD5-like)
        // GUIDs - should be identifiers
        [InlineData("14bb2eed-34f0-4aa2-b2c3-09c0e2166d4d", 0, 36, true)]
        [InlineData("00000000-0000-0000-0000-000000000000", 0, 36, true)]
        [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", 0, 36, false)] // All hex, no digits, we'd _like_ to catch this, but we don't currently
        // Contains non-identifier characters - not identifiers
        [InlineData("123_456", 0, 7, false)] // Underscore
        [InlineData("123.456", 0, 7, false)] // Period
        [InlineData("123/456", 0, 7, false)] // Slash
        [InlineData("abc123xyz", 0, 9, false)] // Non-hex letters
        [InlineData("123G456", 0, 7, false)] // G is not hex
        [InlineData("hello", 0, 5, false)] // Regular word
        [InlineData("controller", 0, 10, false)]
        // Comma cases
        [InlineData("1,2,3", 0, 5, true)] // Comma with numbers only
        [InlineData("123,456,789", 0, 11, true)]
        [InlineData(",,,", 0, 3, false)] // Only commas, no numbers
        [InlineData("a,b,c,1", 0, 7, false)] // Comma with hex - not allowed
        [InlineData("abc,123,def", 0, 11, false)] // Comma with hex letters
        // Pure hyphens - not identifiers (no numbers)
        [InlineData("---", 0, 3, false)]
        [InlineData("-", 0, 1, false)]
        // Substring tests (startIndex > 0)
        [InlineData("/123/456", 1, 3, true)] // "123"
        [InlineData("/abc/123", 5, 3, true)] // "123"
        [InlineData("prefix12345suffix", 6, 5, true)] // "12345"
        public void IsIdentifierSegment_ShouldIdentifyCorrectly(string path, int startIndex, int length, bool expected)
        {
            Trace.Util.UriHelpers.IsIdentifierSegment(path, startIndex, length).Should().Be(expected);
        }
    }
}
