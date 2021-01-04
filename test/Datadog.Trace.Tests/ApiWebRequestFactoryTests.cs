using System;
using System.Net;
using System.Reflection;
using Datadog.Trace.Agent.Transports;
using Xunit;

namespace Datadog.Trace.Tests
{
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
                var factory = new ApiWebRequestFactory();

                var request = factory.Create(new Uri("http://localhost"));

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
