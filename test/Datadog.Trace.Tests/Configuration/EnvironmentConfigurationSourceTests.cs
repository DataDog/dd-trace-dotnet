using System;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class EnvironmentConfigurationSourceTests
    {
        [Theory]
        [InlineData("DD_EnvironmentConfigurationSourceTests", "1", "1")]
        [InlineData("DD_ENVIRONMENTCONFIGURATIONSOURCETESTS", "2", "2")]
        [InlineData("dd_environmentconfigurationsourcetests", "3", "3")]
        public void CaseInsensitiveKey(string settingName, string settingValue, string expected)
        {
            Environment.SetEnvironmentVariable(settingName, settingValue);

            var source = new EnvironmentConfigurationSource();
            var actual = source.GetString(settingName);
            Assert.Equal(expected, actual);

            Environment.SetEnvironmentVariable(settingName, null);
        }
    }
}
