using System;
using System.Collections.Generic;
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
        [InlineData(ConfigurationKeys.Version, Tags.Version, null)]
        [InlineData(ConfigurationKeys.Version, Tags.Version, "custom-version")]
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
        [InlineData(ConfigurationKeys.Version, Tags.Version)]
        public void DDVarTakesPrecedenceOverDDTags(string envKey, string tagKey)
        {
            string envValue = $"ddenv-custom-{tagKey}";
            string tagsLine = $"{tagKey}:ddtags-custom-{tagKey}";

            // save original values so we can restore later
            var originalEnvValue = Environment.GetEnvironmentVariable(envKey);
            var originalTagsValue = Environment.GetEnvironmentVariable(ConfigurationKeys.Tags);

            Environment.SetEnvironmentVariable(envKey, envValue, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ConfigurationKeys.Tags, tagsLine, EnvironmentVariableTarget.Process);

            IConfigurationSource source = new EnvironmentConfigurationSource();
            var settings = new TracerSettings(source);
            Assert.True(settings.GlobalTags.Any());

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            // restore original value
            Environment.SetEnvironmentVariable(envKey, originalEnvValue, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ConfigurationKeys.Tags, originalTagsValue, EnvironmentVariableTarget.Process);

            Assert.Equal(span.GetTag(tagKey), envValue);
        }
    }
}
