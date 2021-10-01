// <copyright file="HttpHeadersCarrierTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Net.Http;
using NUnit.Framework;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class HttpHeadersCarrierTests
    {
        private HttpRequestMessage _request;
        private OpenTracingHttpHeadersCarrier _carrier;

        [SetUp]
        public void Before()
        {
            _request = new HttpRequestMessage();
            _carrier = new OpenTracingHttpHeadersCarrier(_request.Headers);
        }

        [Test]
        public void Set_Value_HeaderIsSet()
        {
            _carrier.Set("key", "value");

            Assert.AreEqual("value", _request.Headers.GetValues("key").Single());
        }

        [Test]
        public void Set_ExistingValue_Override()
        {
            _request.Headers.Add("key", "value1");

            _carrier.Set("key", "value2");

            Assert.AreEqual("value2", _request.Headers.GetValues("key").Single());
        }

        [Test]
        public void Get_SingleValue_ValueIsCorrectlyRead()
        {
            _request.Headers.Add("key", "value");

            Assert.AreEqual("value", _carrier.Get("key"));
        }

        [Test]
        public void Get_MultipleValues_CommaConcatenatedValues()
        {
            _request.Headers.Add("key", "value1");
            _request.Headers.Add("key", "value2");

            Assert.AreEqual("value1,value2", _carrier.Get("key"));
        }

        [Test]
        public void GetEntries_MultipleKeys_MultipleKeys()
        {
            _request.Headers.Add("key1", "value");
            _request.Headers.Add("key2", "value");

            Assert.AreEqual(2, _carrier.Count());
        }
    }
}
