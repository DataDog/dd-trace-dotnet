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
        [InlineData("/controller/action/14bb2eed-34f0X4aa2-b2c3-09c0e2166d4d", "/controller/action/14bb2eed-34f0X4aa2-b2c3-09c0e2166d4d")]
        [InlineData("/controller/action/14bb2eed-34f0-4aa2Xb2c3-09c0e2166d4d", "/controller/action/14bb2eed-34f0-4aa2Xb2c3-09c0e2166d4d")]
        [InlineData("/controller/action/14bb2eed-34f0A4aa2Bb2c3C09c0e2166d4d", "/controller/action/14bb2eed-34f0A4aa2Bb2c3C09c0e2166d4d")]
        [InlineData("/DataDog/dd-trace-dotnet/blob/e2d83dec7d6862d4181937776ddaf72819e291ce/src/Datadog.Trace/Util/UriHelpers.cs", "/DataDog/dd-trace-dotnet/blob/e2d83dec7d6862d4181937776ddaf72819e291ce/src/Datadog.Trace/Util/UriHelpers.cs")]
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
