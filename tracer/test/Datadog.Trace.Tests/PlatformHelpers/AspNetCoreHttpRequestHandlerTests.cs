// <copyright file="AspNetCoreHttpRequestHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Util.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers
{
    public class AspNetCoreHttpRequestHandlerTests
    {
        public const string OriginalPath = "/somepath/Home/Index";

        [Theory]
        [MemberData(nameof(AspNetCoreHttpUrlTestData.EscapedPaths), MemberType = typeof(AspNetCoreHttpUrlTestData))]
        public void EscapesDecodedPathValuesInHttpUrl(string pathBase, string path, string expectedUrl)
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Scheme = "http",
                Host = new HostString("localhost"),
                PathBase = new PathString(pathBase),
                Path = new PathString(path),
            };
            var queryStringManager = new QueryStringManager(
                reportQueryString: false,
                timeout: 0,
                maxSizeBeforeObfuscation: 0,
                pattern: TracerSettingsConstants.DefaultObfuscationQueryStringRegex);

            request.GetUrlForSpan(queryStringManager).Should().Be(expectedUrl);
        }

        [Theory]
        [InlineData(null, "/somepath/Home/Index")]
        [InlineData("", "/somepath/Home/Index")]
        [InlineData("", "/somepath/home/index")]
        [InlineData("/somepath", "/home/index")]
        [InlineData("/somePath", "/home/index")]
        [InlineData("/somepath/home", "/index")]
        [InlineData("/somepath/Home", "/index")]
        [InlineData("/somepath/home", "/Index")]
        [InlineData("/somepath/home/index", "")]
        [InlineData("/somepath/home/Index", "")]
        [InlineData("/somepath/home/Index", null)]
        public void MatchesUrl(string pathBase, string path)
        {
            var feature = new AspNetCoreHttpRequestHandler.RequestTrackingFeature(new PathString(OriginalPath), null, null);

            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                PathBase = new PathString(pathBase),
                Path = new PathString(path),
            };

            feature.MatchesOriginalPath(request).Should().BeTrue();
        }

        [Theory]
        [InlineData(null, "/somepath/Home/Index/Tada")]
        [InlineData("", "/somepath/Home/Index/Tada")]
        [InlineData("", "/somepath/home/")]
        [InlineData("/NOPEpath", "/home/index")]
        [InlineData("/somePath", "/homeyindex")]
        [InlineData("/somepath/home", "/")]
        [InlineData("/somepath/Home", "/Nope")]
        [InlineData("/somepath/home/Nope", "")]
        [InlineData("/somepath/homeIndeed", "")]
        [InlineData("/somepath/homeIndeed", null)]
        public void DoesNotMatchDifferentUrl(string pathBase, string path)
        {
            var feature = new AspNetCoreHttpRequestHandler.RequestTrackingFeature(new PathString(OriginalPath), null, null);

            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                PathBase = new PathString(pathBase),
                Path = new PathString(path),
            };

            feature.MatchesOriginalPath(request).Should().BeFalse();
        }
    }
}
#endif
