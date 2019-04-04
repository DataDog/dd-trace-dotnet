using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ConfigurationSourceTests
    {
        public static IEnumerable<object[]> GetDefaultTestData()
        {
            yield return new object[] { CreateFunc(s => s.TraceEnabled), true };
            yield return new object[] { CreateFunc(s => s.DebugEnabled), false };
            yield return new object[] { CreateFunc(s => s.AgentUri), new Uri("http://localhost:8126/") };
            yield return new object[] { CreateFunc(s => s.Environment), null };
            yield return new object[] { CreateFunc(s => s.ServiceName), null };
            yield return new object[] { CreateFunc(s => s.DisabledIntegrationNames.Count), 0 };
        }

        public static IEnumerable<object[]> GetTestData()
        {
            yield return new object[] { ConfigurationKeys.TraceEnabled, "true", CreateFunc(s => s.TraceEnabled), true };
            yield return new object[] { ConfigurationKeys.TraceEnabled, "false", CreateFunc(s => s.TraceEnabled), false };

            yield return new object[] { ConfigurationKeys.DebugEnabled, "true", CreateFunc(s => s.DebugEnabled), true };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "false", CreateFunc(s => s.DebugEnabled), false };

            yield return new object[] { ConfigurationKeys.AgentHost, "test-host", CreateFunc(s => s.AgentUri), new Uri("http://test-host:8126/") };
            yield return new object[] { ConfigurationKeys.AgentPort, "9000", CreateFunc(s => s.AgentUri), new Uri("http://localhost:9000/") };

            yield return new object[] { ConfigurationKeys.Environment, "staging", CreateFunc(s => s.Environment), "staging" };

            yield return new object[] { ConfigurationKeys.ServiceName, "web-service", CreateFunc(s => s.ServiceName), "web-service" };

            yield return new object[] { ConfigurationKeys.DisabledIntegrations, "integration1;integration2", CreateFunc(s => s.DisabledIntegrationNames.Count), 2 };
        }

        public static Func<TracerSettings, object> CreateFunc(Func<TracerSettings, object> settingGetter)
        {
            return settingGetter;
        }

        [Theory]
        [MemberData(nameof(GetDefaultTestData))]
        public void DefaultSetting(Func<TracerSettings, object> settingGetter, object expectedValue)
        {
            var settings = new TracerSettings();
            object actualValue = settingGetter(settings);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void NameValueConfigurationSource(
            string key,
            string value,
            Func<TracerSettings, object> settingGetter,
            object expectedValue)
        {
            var collection = new NameValueCollection { { key, value } };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);

            object actualValue = settingGetter(settings);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void EnvironmentConfigurationSource(
            string key,
            string value,
            Func<TracerSettings, object> settingGetter,
            object expectedValue)
        {
            var originalValue = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);

            IConfigurationSource source = new EnvironmentConfigurationSource();
            var settings = new TracerSettings(source);
            object actualValue = settingGetter(settings);

            Assert.Equal(expectedValue, actualValue);

            Environment.SetEnvironmentVariable(key, originalValue, EnvironmentVariableTarget.Process);
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void NameValueConfigurationSource1(
            string key,
            string value,
            Func<TracerSettings, object> settingGetter,
            object expectedValue)
        {
            var config = new Dictionary<string, string> { [key] = value };
            string json = JsonConvert.SerializeObject(config);
            IConfigurationSource source = new JsonConfigurationSource(json);
            var settings = new TracerSettings(source);

            object actualValue = settingGetter(settings);
            Assert.Equal(expectedValue, actualValue);
        }
    }
}
