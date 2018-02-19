using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class HttpHeadersWrapperTests
    {
        private HttpHeaders _headers;
        private HttpHeadersWrapper _wrapper;

        public HttpHeadersWrapperTests()
        {
            _headers = new HttpRequestMessage().Headers;
            _wrapper = new HttpHeadersWrapper(_headers);
        }

        [Fact]
        public void Set_Value_ValueIsSet()
        {
            const string key = "azerty";
            const string value = "plop";
            _wrapper.Set(key, value);

            Assert.Equal(value, _headers.GetValues(key).Single());
        }

        [Fact]
        public void Set_ValueSetTwice_LastValueIsSet()
        {
            const string key = "azerty";
            const string value1 = "plop";
            const string value2 = "toto";
            _wrapper.Set(key, value1);

            Assert.Equal(value1, _headers.GetValues(key).Single());

            _wrapper.Set(key, value2);

            Assert.Equal(value2, _headers.GetValues(key).Single());
        }

        [Fact]
        public void Get_NoValue_Null()
        {
            const string key = "azerty";

            Assert.Null(_wrapper.Get(key));
        }

        [Fact]
        public void Get_Value_Value()
        {
            const string key = "azerty";
            const string value = "plop";

            _headers.Add(key, value);

            Assert.Equal(value, _wrapper.Get(key));
        }
    }
}
