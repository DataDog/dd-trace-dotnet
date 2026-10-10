// <copyright file="ApiWebRequestFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent.Transports
{
    [Collection(nameof(WebRequestCollection))]
    public class ApiWebRequestFactoryTests
    {
        /// <summary>
        /// This test ensures that the ApiWebRequestFactory behaves correctly when
        /// a different type of WebRequest is assigned to the http:// prefix
        /// </summary>
        [Fact]
        public void OverrideHttpPrefix()
        {
            // Couldn't find a way to "officially" unregister a prefix but that shouldn't stop us
            var prefixListProperty = typeof(WebRequest).GetProperty("PrefixList", BindingFlags.Static | BindingFlags.NonPublic);
            var oldPrefixList = prefixListProperty.GetValue(null);

            WebRequest.RegisterPrefix("http://", new CustomWebRequestCreator());

            // Make sure we properly hooked the WebRequest factory
            Assert.IsType<FakeWebRequest>(WebRequest.Create("http://localhost/"));

            try
            {
                var factory = new ApiWebRequestFactory(new Uri("http://localhost"), AgentHttpHeaderNames.DefaultHeaders);

                var request = factory.Create(factory.GetEndpoint(string.Empty));

                Assert.NotNull(request);
            }
            finally
            {
                // Unregister the prefix
                prefixListProperty.SetValue(null, oldPrefixList);
            }

            // Make sure we properly restored the old WebRequest factory
            Assert.IsType<HttpWebRequest>(WebRequest.Create("http://localhost/"));
        }

        [Fact]
        public async Task RejectsUnsafeDefaultApiKeyHeader()
        {
            var factory = new ApiWebRequestFactory(
                new Uri("http://example.com"),
                [new KeyValuePair<string, string>(ApiKeyHttpTransportGuard.ApiKeyHeaderName, "test-key")]);
            var request = factory.Create(factory.GetEndpoint("/intake"));

            GetHttpWebRequest(request).AllowAutoRedirect.Should().BeFalse();
            await Assert.ThrowsAsync<ApiKeyHttpTransportException>(() => request.GetAsync());
        }

        [Fact]
        public void RejectsApiKeyAddedToKeylessFactoryRequest()
        {
            var factory = new ApiWebRequestFactory(
                new Uri("https://example.com"),
                []);
            var request = factory.Create(factory.GetEndpoint("/intake"));

            var action = () => request.AddHeader(ApiKeyHttpTransportGuard.ApiKeyHeaderName.ToLowerInvariant(), "test-key");

            action.Should().Throw<ApiKeyHttpTransportException>();
        }

        [Fact]
        public void DisablesProxyForPlaintextLoopbackWithApiKey()
        {
            var factory = new ApiWebRequestFactory(
                new Uri("http://localhost"),
                [new KeyValuePair<string, string>(ApiKeyHttpTransportGuard.ApiKeyHeaderName, "test-key")]);
            factory.SetProxy(new WebProxy("http://example.com"), credential: null);

            var request = factory.Create(factory.GetEndpoint("/intake"));

            GetHttpWebRequest(request).Proxy.Should().BeNull();
        }

        [Fact]
        public async Task RejectsPlaintextLoopbackIfProxyIsReenabled()
        {
            var factory = new ApiWebRequestFactory(
                new Uri("http://localhost"),
                [new KeyValuePair<string, string>(ApiKeyHttpTransportGuard.ApiKeyHeaderName, "test-key")]);
            var request = factory.Create(factory.GetEndpoint("/intake"));
            GetHttpWebRequest(request).Proxy = new NeverBypassProxy();

            await Assert.ThrowsAsync<ApiKeyHttpTransportException>(() => request.GetAsync());
        }

        private static HttpWebRequest GetHttpWebRequest(IApiRequest request)
        {
            var field = typeof(ApiWebRequest).GetField("_request", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return field!.GetValue(request).Should().BeOfType<HttpWebRequest>().Subject;
        }

        private sealed class NeverBypassProxy : IWebProxy
        {
            public ICredentials Credentials { get; set; }

            public Uri GetProxy(Uri destination) => new("http://example.com");

            public bool IsBypassed(Uri host) => false;
        }

        private class CustomWebRequestCreator : IWebRequestCreate
        {
            public WebRequest Create(Uri uri)
            {
                return new FakeWebRequest();
            }
        }

        private class FakeWebRequest : WebRequest
        {
        }
    }
}
