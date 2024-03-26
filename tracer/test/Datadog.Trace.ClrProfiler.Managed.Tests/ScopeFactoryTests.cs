// <copyright file="ScopeFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ScopeFactoryTests
    {
        // declare here instead of using ScopeFactory.UrlIdPlaceholder so tests fails if value changes
        private const string Id = "?";

        [Theory]
        [InlineData("users/", "users/")]
        [InlineData("users", "users")]
        [InlineData("123/", Id + "/")]
        [InlineData("123", Id)]
        [InlineData("4294967294/", Id + "/")]
        [InlineData("4294967294", Id)]
        [InlineData("E653C852-227B-4F0C-9E48-D30D83C68BF3/", Id + "/")]
        [InlineData("E653C852-227B-4F0C-9E48-D30D83C68BF3", Id)]
        [InlineData("E653C852227B4F0C9E48D30D83C68BF3/", Id + "/")]
        [InlineData("E653C852227B4F0C9E48D30D83C68BF3", Id)]
        [InlineData("123,123/", Id + "/")]
        [InlineData("123,123", Id)]
        [InlineData("users,123/", "users,123/")]
        [InlineData("123af,12a/", "123af,12a/")]
        [InlineData("123afafafafaf,12a/", "123afafafafaf,12a/")]
        [InlineData("afafafafaf,afafafafaf/", "afafafafaf,afafafafaf/")]
        public void CleanUriSegment(string segment, string expected)
        {
            string actual = UriHelpers.GetCleanUriPath(segment);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("https://username:password@example.com/path/to/file.aspx?query=1#fragment", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://username@example.com/path/to/file.aspx", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx?query=1", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx#fragment", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("http://example.com/path/to/file.aspx", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/123/file.aspx", "GET", "GET example.com/path/" + Id + "/file.aspx")]
        [InlineData("https://example.com/path/123,123/file.aspx", "GET", "GET example.com/path/" + Id + "/file.aspx")]
        [InlineData("https://example.com/path/123afafafafaf,12a/file.aspx", "GET", "GET example.com/path/123afafafafaf,12a/file.aspx")]
        [InlineData("https://example.com/path/afafafafaf,afafafafaf/file.aspx", "GET", "GET example.com/path/afafafafaf,afafafafaf/file.aspx")]
        [InlineData("https://example.com/path/123/", "GET", "GET example.com/path/" + Id + "/")]
        [InlineData("https://example.com/path/123", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/123,123/", "GET", "GET example.com/path/" + Id + "/")]
        [InlineData("https://example.com/path/123,123", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/4294967294/file.aspx", "GET", "GET example.com/path/" + Id + "/file.aspx")]
        [InlineData("https://example.com/path/4294967294/", "GET", "GET example.com/path/" + Id + "/")]
        [InlineData("https://example.com/path/4294967294", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3", "GET", "GET example.com/path/" + Id)]
        public void CleanUri_ResourceName(string uri, string method, string expected)
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            using (var automaticScope = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(uri), IntegrationId.HttpMessageHandler, out _))
            {
                Assert.Equal(expected, automaticScope.Span.ResourceName);
            }
        }

        [Fact]
        public void CreateOutboundHttpScope_Null_ResourceUri()
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            using (var automaticScope = ScopeFactory.CreateOutboundHttpScope(tracer, "GET", null, IntegrationId.HttpMessageHandler, out _))
            {
                Assert.Equal(expected: "GET ",  actual: automaticScope.Span.ResourceName);
            }
        }

        [Theory]
        [InlineData("https://username:password@example.com/path/to/file.aspx?query=1#fragment", "https://example.com/path/to/file.aspx", false)]
        [InlineData("https://username:password@example.com/path/to/file.aspx?query=1#fragment", "https://example.com/path/to/file.aspx?query=1", true)]
        [InlineData("https://username@example.com/path/to/file.aspx", "https://example.com/path/to/file.aspx", false)]
        [InlineData("https://username@example.com/path/to/file.aspx", "https://example.com/path/to/file.aspx", true)]
        [InlineData("https://example.com/path/to/file.aspx?query=1", "https://example.com/path/to/file.aspx", false)]
        [InlineData("https://example.com/path/to/file.aspx?query=1", "https://example.com/path/to/file.aspx?query=1", true)]
        [InlineData("https://example.com:5000/path/to/file.aspx?query=1", "https://example.com:5000/path/to/file.aspx", false)]
        [InlineData("https://example.com:5000/path/to/file.aspx?query=1", "https://example.com:5000/path/to/file.aspx?query=1", true)]
        [InlineData("https://example.com:443/path/to/file.aspx?query=1", "https://example.com/path/to/file.aspx", false)]
        [InlineData("https://example.com:443/path/to/file.aspx?query=1", "https://example.com/path/to/file.aspx?query=1", true)]
        [InlineData("http://example.com:80/path/to/file.aspx?query=1", "http://example.com/path/to/file.aspx", false)]
        [InlineData("http://example.com:80/path/to/file.aspx?query=1", "http://example.com/path/to/file.aspx?query=1", true)]
        [InlineData("https://example.com/path/to/file.aspx#fragment", "https://example.com/path/to/file.aspx", false)]
        [InlineData("http://example.com/path/to/file.aspx", "http://example.com/path/to/file.aspx", false)]
        [InlineData("https://example.com/path/123/file.aspx", "https://example.com/path/123/file.aspx", false)]
        [InlineData("https://example.com/path/123,123/file.aspx", "https://example.com/path/123,123/file.aspx", false)]
        [InlineData("https://example.com/path/123/", "https://example.com/path/123/", false)]
        [InlineData("https://example.com/path/123", "https://example.com/path/123", false)]
        [InlineData("https://example.com/path/123,123/", "https://example.com/path/123,123/", false)]
        [InlineData("https://example.com/path/123,123", "https://example.com/path/123,123", false)]
        [InlineData("https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3", "https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3", false)]
        [InlineData("https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3", "https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3", false)]
        public void CleanUri_HttpUrlTag(string uri, string expected, bool includeQuerystring)
        {
            // Set up Tracer
            var dictionary = new Dictionary<string, object>
            {
                { ConfigurationKeys.QueryStringReportingEnabled, includeQuerystring },
                { ConfigurationKeys.ObfuscationQueryStringRegexTimeout, "5000" }
            };

#if NETCOREAPP2_1
            // Add old one otherwise NullReferenceException on arm64/netcoreapp2.1
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            {
                dictionary.Add(ConfigurationKeys.ObfuscationQueryStringRegex, """((?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?)(?:(?:\s|%20)*(?:=|%3D)[^&]+|(?:"|%22)(?:\s|%20)*(?::|%3A)(?:\s|%20)*(?:"|%22)(?:%2[^2]|%[^2]|[^"%])+(?:"|%22))|bearer(?:\s|%20)+[a-z0-9\._\-]|token(?::|%3A)[a-z0-9]{13}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L](?:[\w=-]|%3D)+\.ey[I-L](?:[\w=-]|%3D)+(?:\.(?:[\w.+\/=-]|%3D|%2F|%2B)+)?|[\-]{5}BEGIN(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY[\-]{5}[^\-]+[\-]{5}END(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY|ssh-rsa(?:\s|%20)*(?:[a-z0-9\/\.+]|%2F|%5C|%2B){100,})""");
            }
#endif
            var settings = TracerSettings.Create(dictionary);
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            const string method = "GET";

            using (var automaticScope = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(uri), IntegrationId.HttpMessageHandler, out var tags))
            {
                expected.Should().Be(automaticScope.Span.GetTag(Tags.HttpUrl));
                expected.Should().Be(tags.HttpUrl);
            }
        }

        [Theory]
        [InlineData((int)IntegrationId.HttpMessageHandler, (int)IntegrationId.HttpMessageHandler)] // This scenario may occur on any .NET runtime with nested HttpMessageHandler's and HttpSocketHandler's
        [InlineData((int)IntegrationId.WebRequest, (int)IntegrationId.HttpMessageHandler)] // This scenario may occur on .NET Core where the underlying transport for WebRequest is HttpMessageHandler
        public void CreateOutboundHttpScope_AlwaysCreatesOneAutomaticInstrumentationScope(int integration1, int integration2)
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            const string method = "GET";
            const string url = "http://www.contoso.com";

            // Manually create a span decorated with HTTP information
            using (var manualScope = tracer.StartActive("http.request"))
            {
                manualScope.Span.Type = SpanTypes.Http;
                manualScope.Span.ResourceName = $"{method} {url}";
                manualScope.Span.ServiceName = $"{tracer.DefaultServiceName}-http-client";

                using (var automaticScope1 = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(url), (IntegrationId)integration1, out _))
                {
                    using (var automaticScope2 = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(url), (IntegrationId)integration2, out _))
                    {
                        Assert.NotNull(manualScope);
                        Assert.NotNull(automaticScope1);
                        Assert.Null(automaticScope2);
                    }
                }
            }
        }
    }
}
