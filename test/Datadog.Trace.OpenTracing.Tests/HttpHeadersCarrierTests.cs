using System.Linq;
using System.Net.Http;
using Xunit;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class HttpHeadersCarrierTests
    {
        private HttpRequestMessage _request;
        private OpenTracingHttpHeadersCarrier _carrier;

        public HttpHeadersCarrierTests()
        {
            _request = new HttpRequestMessage();
            _carrier = new OpenTracingHttpHeadersCarrier(_request.Headers);
        }

        [Fact]
        public void Set_Value_HeaderIsSet()
        {
            _carrier.Set("key", "value");

            Assert.Equal("value", _request.Headers.GetValues("key").Single());
        }

        [Fact]
        public void Set_ExistingValue_Override()
        {
            _request.Headers.Add("key", "value1");

            _carrier.Set("key", "value2");

            Assert.Equal("value2", _request.Headers.GetValues("key").Single());
        }

        [Fact]
        public void Get_SingleValue_ValueIsCorrectlyRead()
        {
            _request.Headers.Add("key", "value");

            Assert.Equal("value", _carrier.Get("key"));
        }

        [Fact]
        public void Get_MultipleValues_CommaConcatenatedValues()
        {
            _request.Headers.Add("key", "value1");
            _request.Headers.Add("key", "value2");

            Assert.Equal("value1,value2", _carrier.Get("key"));
        }

        [Fact]
        public void GetEntries_MultipleKeys_MultipleKeys()
        {
            _request.Headers.Add("key1", "value");
            _request.Headers.Add("key2", "value");

            Assert.Equal(2, _carrier.Count());
        }
    }
}
