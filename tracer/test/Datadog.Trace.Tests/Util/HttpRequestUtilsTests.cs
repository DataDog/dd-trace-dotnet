// <copyright file="HttpRequestUtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util.Http;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    public class HttpRequestUtilsTests
    {
        [Fact]
        public void GetUrl_WithAbsoluteUri_ReturnsCorrectUrl()
        {
            // Arrange
            var uri = new Uri("https://example.com:8080/path/to/resource?query=123");

            // Act
            var result = HttpRequestUtils.GetUrl(uri);

            // Assert
            Assert.Equal("https://example.com:8080/path/to/resource", result);
        }

        [Fact]
        public void GetUrl_WithAbsoluteUriAndQueryStringManager_ReturnsProcessedUrl()
        {
            // Arrange
            var uri = new Uri("https://example.com/path?secret=ee123");
            var queryStringManager = new QueryStringManager(true, 1000, 1000, TracerSettingsConstants.DefaultObfuscationQueryStringRegex);

            // Act
            var result = HttpRequestUtils.GetUrl(uri, queryStringManager);

            // Assert
            Assert.Equal("https://example.com/path?<redacted>", result);
        }

        [Fact]
        public void GetUrl_WithRelativeUri_ReturnsUriAsString()
        {
            // Arrange
            var uri = new Uri("/relative/path?query=123", UriKind.Relative);

            // Act
            var result = HttpRequestUtils.GetUrl(uri);

            // Assert
            Assert.Equal("/relative/path?query=123", result);
        }
    }
}
