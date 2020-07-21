using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ConfigurationKeyFallbacksTests
    {
        [Theory]
        [InlineData("DD_TRACE_CONFIG_FILE", "foo", "foo")]
        [InlineData("DD_DOTNET_TRACER_CONFIG_FILE", "bar", "bar")]
        public void IntegrationEnabled(string settingName, string settingValue, string expected)
        {
            var source = new NameValueConfigurationSource(
                new NameValueCollection
                {
                    { settingName, settingValue }
                });

            string actual = source.GetStringWithFallbacks(ConfigurationKeys.ConfigurationFileName);
            Assert.Equal(expected, actual);
        }
    }
}
