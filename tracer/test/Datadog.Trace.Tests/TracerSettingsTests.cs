// <copyright file="TracerSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using NUnit.Framework;

namespace Datadog.Trace.Tests
{
    public class TracerSettingsTests
    {
        private readonly Mock<IAgentWriter> _writerMock;
        private readonly Mock<ISampler> _samplerMock;

        public TracerSettingsTests()
        {
            _writerMock = new Mock<IAgentWriter>();
            _samplerMock = new Mock<ISampler>();
        }

        [Theory]
        [TestCase(ConfigurationKeys.Environment, Tags.Env, null)]
        [TestCase(ConfigurationKeys.Environment, Tags.Env, "custom-env")]
        [TestCase(ConfigurationKeys.ServiceVersion, Tags.Version, null)]
        [TestCase(ConfigurationKeys.ServiceVersion, Tags.Version, "custom-version")]
        public void ConfiguredTracerSettings_DefaultTagsSetFromEnvironmentVariable(string environmentVariableKey, string tagKey, string value)
        {
            var collection = new NameValueCollection { { environmentVariableKey, value } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.AreEqual(span.GetTag(tagKey), value);
        }

        [Theory]
        [TestCase(ConfigurationKeys.Environment, Tags.Env)]
        [TestCase(ConfigurationKeys.ServiceVersion, Tags.Version)]
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

            Assert.AreEqual(span.GetTag(tagKey), envValue);
        }

        [Theory]
        [TestCase("", true)]
        [TestCase("1", true)]
        [TestCase("0", false)]
        public void TraceEnabled(string value, bool areTracesEnabled)
        {
            var settings = new NameValueCollection
            {
                { ConfigurationKeys.TraceEnabled, value }
            };

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(settings));

            Assert.AreEqual(areTracesEnabled, tracerSettings.TraceEnabled);

            _writerMock.Invocations.Clear();

            var tracer = new Tracer(tracerSettings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("TestTracerDisabled");
            span.Dispose();

            var assertion = areTracesEnabled ? Times.Once() : Times.Never();

            _writerMock.Verify(w => w.WriteTrace(It.IsAny<ArraySegment<Span>>()), assertion);
        }

        [Theory]
        [TestCase("http://localhost:7777/agent?querystring", "http://127.0.0.1:7777/agent?querystring")]
        [TestCase("http://datadog:7777/agent?querystring", "http://datadog:7777/agent?querystring")]
        public void ReplaceLocalhost(string original, string expected)
        {
            var settings = new NameValueCollection
            {
                { ConfigurationKeys.AgentUri, original }
            };

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(settings));

            Assert.AreEqual(expected, tracerSettings.AgentUri.ToString());
        }

        [Theory]
        [TestCase("a,b,c,d,,f", new[] { "a", "b", "c", "d", "f" })]
        [TestCase(" a, b ,c, ,,f ", new[] { "a", "b", "c", "f" })]
        [TestCase("a,b, c ,d,      e      ,f  ", new[] { "a", "b", "c", "d", "e", "f" })]
        [TestCase("a,b,c,d,e,f", new[] { "a", "b", "c", "d", "e", "f" })]
        [TestCase("", new string[0])]
        public void ParseStringArraySplit(string input, string[] expected)
        {
            var tracerSettings = new TracerSettings();
            var result = tracerSettings.TrimSplitString(input, ',').ToArray();
            Assert.AreEqual(expected: expected, actual: result);
        }

        [Theory]
        [TestCase("404 -401, 419,344_ 23-302, 201,_5633-55, 409-411", "401,402,403,404,419,201,409,410,411")]
        [TestCase("-33, 500-503,113#53,500-502-200,456_2, 590-590", "500,501,502,503,590")]
        [TestCase("800", "")]
        [TestCase("599-605,700-800", "599")]
        [TestCase("400-403, 500-501-234, s342, 500-503", "400,401,402,403,500,501,502,503")]
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

        [Test]
        public void SetClientHttpCodes()
        {
            SetAndValidateStatusCodes((s, c) => s.SetHttpClientErrorStatusCodes(c), s => s.HttpClientErrorStatusCodes);
        }

        [Test]
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

                    Assert.AreEqual(code, i);
                }
            }

            Assert.IsEmpty(statusCodes);
        }
    }
}
