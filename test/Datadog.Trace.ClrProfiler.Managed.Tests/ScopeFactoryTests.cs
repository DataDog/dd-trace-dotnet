using System;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ScopeFactoryTests
    {
        private const string Id = ScopeFactory.UrlIdPlaceholder;

        [Theory]
        [InlineData("users/", "users/")]
        [InlineData("users", "users")]
        [InlineData("123/", Id + "/")]
        [InlineData("123", Id)]
        [InlineData("E653C852-227B-4F0C-9E48-D30D83C68BF3/", Id + "/")]
        [InlineData("E653C852-227B-4F0C-9E48-D30D83C68BF3", Id)]
        [InlineData("E653C852227B4F0C9E48D30D83C68BF3/", Id + "/")]
        [InlineData("E653C852227B4F0C9E48D30D83C68BF3", Id)]
        public void CleanUriSegment(string segment, string expected)
        {
            string actual = ScopeFactory.CleanUriSegment(segment);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("https://username:password@example.com/path/to/file.aspx?query=1#fragment", "example.com/path/to/file.aspx")]
        [InlineData("https://username@example.com/path/to/file.aspx", "example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx?query=1", "example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx#fragment", "example.com/path/to/file.aspx")]
        [InlineData("http://example.com/path/to/file.aspx", "example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/123/file.aspx", "example.com/path/" + Id + "/file.aspx")]
        [InlineData("https://example.com/path/123/", "example.com/path/" + Id + "/")]
        [InlineData("https://example.com/path/123", "example.com/path/" + Id)]
        [InlineData("https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3", "example.com/path/" + Id)]
        [InlineData("https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3", "example.com/path/" + Id)]
        public void CleanUri_RemoveIdSegmentsEnabled(string uri, string expected)
        {
            string actual = ScopeFactory.CleanUri(new Uri(uri), tryRemoveIds: true);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("https://username:password@example.com/path/to/file.aspx?query=1#fragment", "example.com/path/to/file.aspx")]
        [InlineData("https://username@example.com/path/to/file.aspx", "example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx?query=1", "example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx#fragment", "example.com/path/to/file.aspx")]
        [InlineData("http://example.com/path/to/file.aspx", "example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/123/file.aspx", "example.com/path/123/file.aspx")]
        [InlineData("https://example.com/path/123/", "example.com/path/123/")]
        [InlineData("https://example.com/path/123", "example.com/path/123")]
        [InlineData("https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3", "example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3")]
        [InlineData("https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3", "example.com/path/E653C852227B4F0C9E48D30D83C68BF3")]
        public void CleanUri_RemoveIdSegmentsDisabled(string uri, string expected)
        {
            string actual = ScopeFactory.CleanUri(new Uri(uri), tryRemoveIds: false);

            Assert.Equal(expected, actual);
        }
    }
}
