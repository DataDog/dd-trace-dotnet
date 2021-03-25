using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class TracerSettingsTests
    {
        private readonly Mock<ITraceWriter> _writerMock;
        private readonly Mock<ISampler> _samplerMock;

        public TracerSettingsTests()
        {
            _writerMock = new Mock<ITraceWriter>();
            _samplerMock = new Mock<ISampler>();
        }

        [Theory]
        [InlineData(ConfigurationKeys.Environment, Tags.Env, null)]
        [InlineData(ConfigurationKeys.Environment, Tags.Env, "custom-env")]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version, null)]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version, "custom-version")]
        public void ConfiguredTracerSettings_DefaultTagsSetFromEnvironmentVariable(string environmentVariableKey, string tagKey, string value)
        {
            var collection = new NameValueCollection { { environmentVariableKey, value } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.Equal(span.GetTag(tagKey), value);
        }

        [Theory]
        [InlineData(ConfigurationKeys.Environment, Tags.Env)]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version)]
        public void DDVarTakesPrecedenceOverDDTags(string envKey, string tagKey)
        {
            string envValue = $"ddenv-custom-{tagKey}";
            string tagsLine = $"{tagKey}:ddtags-custom-{tagKey}";
            var collection = new NameValueCollection { { envKey, envValue }, { ConfigurationKeys.GlobalTags, tagsLine } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);
            Assert.True(settings.GlobalTags.Any());

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.Equal(span.GetTag(tagKey), envValue);
        }

        [Theory]
        [InlineData("", true)]
        [InlineData("1", true)]
        [InlineData("0", false)]
        public void TraceEnabled(string value, bool areTracesEnabled)
        {
            var settings = new NameValueCollection
            {
                { ConfigurationKeys.TraceEnabled, value }
            };

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(settings));

            Assert.Equal(areTracesEnabled, tracerSettings.TraceEnabled);

            _writerMock.Invocations.Clear();

            var tracer = new Tracer(tracerSettings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("TestTracerDisabled");
            span.Dispose();

            var assertion = areTracesEnabled ? Times.Once() : Times.Never();

            _writerMock.Verify(w => w.WriteTrace(It.IsAny<Span[]>()), assertion);
        }

        [Theory]
        [InlineData("http://localhost:7777/agent?querystring", "http://127.0.0.1:7777/agent?querystring")]
        [InlineData("http://datadog:7777/agent?querystring", "http://datadog:7777/agent?querystring")]
        public void ReplaceLocalhost(string original, string expected)
        {
            var settings = new NameValueCollection
            {
                { ConfigurationKeys.AgentUri, original }
            };

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(settings));

            Assert.Equal(expected, tracerSettings.AgentUri.ToString());
        }

        [Theory]
        [InlineData("404 -401, 419,344_ 23-302, 201,_5633-55, 409-411", "401,402,403,404,419,201,409,410,411")]
        [InlineData("-33, 500-503,113#53,500-502-200,456_2, 590-590", "500,501,502,503,590")]
        [InlineData("800", "")]
        [InlineData("599-605,700-800", "599")]
        public void ParseHttpCodes(string original, string expected)
        {
            var tracerSettings = new TracerSettings();

            bool[] errorStatusCodesArray = tracerSettings.ParseHttpCodesToArray(original);
            string[] expectedKeysArray = expected.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var value in expectedKeysArray)
            {
                Assert.True(errorStatusCodesArray[int.Parse(value)]);
            }
        }

        [Fact]
        public void SetClientHttpCodes()
        {
            SetAndValidateStatusCodes((s, c) => s.SetHttpClientErrorStatusCodes(c), s => s.HttpClientErrorStatusCodes);
        }

        [Fact]
        public void SetServerHttpCodes()
        {
            SetAndValidateStatusCodes((s, c) => s.SetHttpServerErrorStatusCodes(c), s => s.HttpServerErrorStatusCodes);
        }

        private void SetAndValidateStatusCodes(Action<TracerSettings, IEnumerable<int>> setStatusCodes, Func<TracerSettings, bool[]> getStatusCodes)
        {
            var settings = new TracerSettings();
            var statusCodes = new Queue<int>(new[] { 100, 201, 503 });

            setStatusCodes(settings, statusCodes);

            var result = getStatusCodes(settings);

            for (int i = 0; i < 600; i++)
            {
                if (result[i])
                {
                    var code = statusCodes.Dequeue();

                    Assert.Equal(code, i);
                }
            }

            Assert.Empty(statusCodes);
        }
    }
}
