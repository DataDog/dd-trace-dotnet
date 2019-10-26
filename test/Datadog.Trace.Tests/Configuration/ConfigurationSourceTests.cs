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
            yield return new object[] { CreateFunc(s => s.LogsInjectionEnabled), false };
            yield return new object[] { CreateFunc(s => s.GlobalTags.Count), 0 };
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

            yield return new object[] { ConfigurationKeys.GlobalTags, "k1:v1, k2:v2", CreateFunc(s => s.GlobalTags.Count), 2 };
        }

        // JsonConfigurationSource needs to be tested with JSON data, which cannot be used with the other IConfigurationSource implementations.
        public static IEnumerable<object[]> GetJsonTestData()
        {
            yield return new object[] { @"{ ""DD_TRACE_GLOBAL_TAGS"": { ""name1"":""value1"", ""name2"": ""value2""} }", CreateFunc(s => s.GlobalTags.Count), 2 };
        }

        public static IEnumerable<object[]> GetBadJsonTestData1()
        {
            // Extra opening brace
            yield return new object[] { @"{ ""DD_TRACE_GLOBAL_TAGS"": { { ""name1"":""value1"", ""name2"": ""value2""} }" };
        }

        public static IEnumerable<object[]> GetBadJsonTestData2()
        {
            // Missing closing brace
            yield return new object[] { @"{ ""DD_TRACE_GLOBAL_TAGS"": { ""name1"":""value1"", ""name2"": ""value2"" }" };
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
        [MemberData(nameof(GetJsonTestData))]
        public void JsonConfigurationSource(
            string value,
            Func<TracerSettings, object> settingGetter,
            object expectedValue)
        {
            IConfigurationSource source = new JsonConfigurationSource(value);
            var settings = new TracerSettings(source);

            var actualValue = settingGetter(settings);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [MemberData(nameof(GetBadJsonTestData1))]
        public void JsonConfigurationSource_BadData1(
            string value)
        {
            Assert.Throws<JsonReaderException>(() => { new JsonConfigurationSource(value); });
        }

        [Theory]
        [MemberData(nameof(GetBadJsonTestData2))]
        public void JsonConfigurationSource_BadData2(
            string value)
        {
            Assert.Throws<JsonSerializationException>(() => { new JsonConfigurationSource(value); });
        }
    }
}
