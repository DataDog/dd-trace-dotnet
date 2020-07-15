using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using Xunit;
using Xunit.Abstractions;

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
    }
}
