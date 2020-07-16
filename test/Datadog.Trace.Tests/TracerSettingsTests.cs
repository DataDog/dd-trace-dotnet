using System;
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
            // save original value so we can restore later
            var originalValue = Environment.GetEnvironmentVariable(environmentVariableKey);

            Environment.SetEnvironmentVariable(environmentVariableKey, value, EnvironmentVariableTarget.Process);
            IConfigurationSource source = new EnvironmentConfigurationSource();
            var settings = new TracerSettings(source);

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            // restore original value
            Environment.SetEnvironmentVariable(environmentVariableKey, originalValue, EnvironmentVariableTarget.Process);

            Assert.Equal(span.GetTag(tagKey), value);
        }

        [Theory]
        [InlineData(ConfigurationKeys.Environment, Tags.Env)]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version)]
        public void DDVarTakesPrecedenceOverDDTags(string envKey, string tagKey)
        {
            string envValue = $"ddenv-custom-{tagKey}";
            string tagsLine = $"{tagKey}:ddtags-custom-{tagKey}";

            // save original values so we can restore later
            var originalEnvValue = Environment.GetEnvironmentVariable(envKey);
            var originalTagsValue = Environment.GetEnvironmentVariable(ConfigurationKeys.GlobalTags);

            Environment.SetEnvironmentVariable(envKey, envValue, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ConfigurationKeys.GlobalTags, tagsLine, EnvironmentVariableTarget.Process);

            IConfigurationSource source = new EnvironmentConfigurationSource();
            var settings = new TracerSettings(source);
            Assert.True(settings.GlobalTags.Any());

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            // restore original value
            Environment.SetEnvironmentVariable(envKey, originalEnvValue, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ConfigurationKeys.GlobalTags, originalTagsValue, EnvironmentVariableTarget.Process);

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

            _writerMock.ResetCalls();

            var tracer = new Tracer(tracerSettings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("TestTracerDisabled");
            span.Dispose();

            var assertion = areTracesEnabled ? Times.Once() : Times.Never();

            _writerMock.Verify(w => w.WriteTrace(It.IsAny<Span[]>()), assertion);
        }
    }
}
