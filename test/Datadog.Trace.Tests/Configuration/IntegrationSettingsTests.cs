using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class IntegrationSettingsTests
    {
        [Theory]
        [InlineData("DD_TRACE_FOO_ENABLED", "true", true)]
        [InlineData("DD_TRACE_FOO_ENABLED", "false", false)]
        [InlineData("DD_FOO_ENABLED", "true", true)]
        [InlineData("DD_FOO_ENABLED", "false", false)]
        public void IntegrationEnabled(string settingName, string settingValue, bool expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
                                                          {
                                                              { settingName, settingValue }
                                                          });

            var settings = new IntegrationSettings("FOO", source);
            Assert.Equal(expected, settings.Enabled);
        }

        [Theory]
        [InlineData("DD_TRACE_FOO_ANALYTICS_ENABLED", "true", true)]
        [InlineData("DD_TRACE_FOO_ANALYTICS_ENABLED", "false", false)]
        [InlineData("DD_FOO_ANALYTICS_ENABLED", "true", true)]
        [InlineData("DD_FOO_ANALYTICS_ENABLED", "false", false)]
        public void IntegrationAnalyticsEnabled(string settingName, string settingValue, bool expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
                                                          {
                                                              { settingName, settingValue }
                                                          });

            var settings = new IntegrationSettings("FOO", source);
            Assert.Equal(expected, settings.AnalyticsEnabled);
        }

        [Theory]
        [InlineData("DD_TRACE_FOO_ANALYTICS_SAMPLE_RATE", "0.2", 0.2)]
        [InlineData("DD_FOO_ANALYTICS_SAMPLE_RATE", "0.6", 0.6)]
        public void IntegrationAnalyticsSampleRate(string settingName, string settingValue, double expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
                                                          {
                                                              { settingName, settingValue }
                                                          });

            var settings = new IntegrationSettings("FOO", source);
            Assert.Equal(expected, settings.AnalyticsSampleRate);
        }
    }
}
