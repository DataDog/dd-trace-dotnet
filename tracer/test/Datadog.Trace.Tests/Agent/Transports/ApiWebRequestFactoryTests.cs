// <copyright file="ApiWebRequestFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Tests.Agent.Transports
{
    [Collection(nameof(WebRequestCollection))]
    public class ApiWebRequestFactoryTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AsyncDeadlineAbortsStalledJsonSendOnlyWhenEnabled(bool enforceAsyncTimeout)
        {
            using var intake = new HttpListener();
            var endpoint = new Uri($"http://127.0.0.1:{TcpPortProvider.GetOpenPort()}/");
            intake.Prefixes.Add(endpoint.ToString());
            intake.Start();
            var factory = new ApiWebRequestFactory(endpoint, AgentHttpHeaderNames.DefaultHeaders, TimeSpan.FromSeconds(1), enforceAsyncTimeout: enforceAsyncTimeout);
            var request = factory.Create(endpoint);
            var webRequest = (HttpWebRequest)typeof(ApiWebRequest).GetField("_request", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(request);
            // Isolate our async deadline on every runtime, including runtimes whose WebRequest
            // implementation happens to enforce Timeout for asynchronous calls too.
            webRequest.Timeout = Timeout.Infinite;
            var received = intake.GetContextAsync();
            var send = request.PostAsJsonAsync(new { Batch = "timeout" }, MultipartCompression.GZip);

            try
            {
                Assert.Same(received, await Task.WhenAny(received, Task.Delay(TimeSpan.FromSeconds(3))));
                var context = await received;
                await context.Request.InputStream.CopyToAsync(Stream.Null);
                if (enforceAsyncTimeout)
                {
                    Assert.Same(send, await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(3))));
                    var error = await Assert.ThrowsAsync<WebException>(() => send);
                    Assert.Equal(WebExceptionStatus.RequestCanceled, error.Status);
                }
                else
                {
                    Assert.NotSame(send, await Task.WhenAny(send, Task.Delay(TimeSpan.FromMilliseconds(1200))));
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    Assert.Same(send, await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(3))));
                    using var response = await send;
                    Assert.Equal(200, response.StatusCode);
                }
            }
            finally
            {
                webRequest.Abort();
                intake.Close();
                // Always release the stalled operation, including when the deadline assertion fails.
                await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(3)));
            }
        }

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
