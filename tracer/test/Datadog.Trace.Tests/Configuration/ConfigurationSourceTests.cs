// <copyright file="ConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    [CollectionDefinition(nameof(ConfigurationSourceTests), DisableParallelization = true)]
    [Collection(nameof(ConfigurationSourceTests))]
    public class ConfigurationSourceTests
    {
        private static readonly Dictionary<string, string> TagsK1V1K2V2 = new Dictionary<string, string> { { "k1", "v1" }, { "k2", "v2" } };
        private static readonly Dictionary<string, string> TagsK2V2 = new Dictionary<string, string> { { "k2", "v2" } };
        private static readonly Dictionary<string, string> HeaderTagsWithOptionalMappings = new Dictionary<string, string> { { "header1", "tag1" }, { "header2", "content-type" }, { "header3", "content-type" }, { "header4", "c___ont_____ent----typ_/_e" }, { "validheaderonly", string.Empty }, { "validheaderwithoutcolon", string.Empty } };
        private static readonly Dictionary<string, string> HeaderTagsWithDots = new Dictionary<string, string> { { "header3", "my.header.with.dot" }, { "my.new.header.with.dot", string.Empty } };
        private static readonly Dictionary<string, string> HeaderTagsSameTag = new Dictionary<string, string> { { "header1", "tag1" }, { "header2", "tag1" } };

        public static IEnumerable<object[]> GetGlobalDefaultTestData()
        {
            yield return new object[] { CreateGlobalFunc(s => s.DebugEnabled), false };
        }

        public static IEnumerable<object[]> GetGlobalTestData()
        {
            yield return new object[] { ConfigurationKeys.DebugEnabled, 1, CreateGlobalFunc(s => s.DebugEnabled), true };
            yield return new object[] { ConfigurationKeys.DebugEnabled, 0, CreateGlobalFunc(s => s.DebugEnabled), false };
            yield return new object[] { ConfigurationKeys.DebugEnabled, true, CreateGlobalFunc(s => s.DebugEnabled), true };
            yield return new object[] { ConfigurationKeys.DebugEnabled, false, CreateGlobalFunc(s => s.DebugEnabled), false };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "true", CreateGlobalFunc(s => s.DebugEnabled), true };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "false", CreateGlobalFunc(s => s.DebugEnabled), false };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "tRUe", CreateGlobalFunc(s => s.DebugEnabled), true };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "fALse", CreateGlobalFunc(s => s.DebugEnabled), false };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "1", CreateGlobalFunc(s => s.DebugEnabled), true };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "0", CreateGlobalFunc(s => s.DebugEnabled), false };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "yes", CreateGlobalFunc(s => s.DebugEnabled), true };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "no", CreateGlobalFunc(s => s.DebugEnabled), false };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "T", CreateGlobalFunc(s => s.DebugEnabled), true };
            yield return new object[] { ConfigurationKeys.DebugEnabled, "F", CreateGlobalFunc(s => s.DebugEnabled), false };

            // garbage checks
            yield return new object[] { ConfigurationKeys.DebugEnabled, "what_even_is_this", CreateGlobalFunc(s => s.DebugEnabled), false };
            yield return new object[] { ConfigurationKeys.DebugEnabled, 42, CreateGlobalFunc(s => s.DebugEnabled), false };
            yield return new object[] { ConfigurationKeys.DebugEnabled, string.Empty, CreateGlobalFunc(s => s.DebugEnabled), false };
        }

        public static IEnumerable<object[]> GetDefaultTestData()
        {
            yield return new object[] { CreateFunc(s => s.TraceEnabled), true };
            yield return new object[] { CreateFunc(s => s.Exporter.AgentUri), new Uri("http://127.0.0.1:8126/") };
            yield return new object[] { CreateFunc(s => s.Environment), null };
            yield return new object[] { CreateFunc(s => s.ServiceName), null };
            yield return new object[] { CreateFunc(s => s.DisabledIntegrationNames.Count), 0 };
            yield return new object[] { CreateFunc(s => s.LogsInjectionEnabled), false };
            yield return new object[] { CreateFunc(s => s.GlobalTags.Count), 0 };
#pragma warning disable 618 // App analytics is deprecated but supported
            yield return new object[] { CreateFunc(s => s.AnalyticsEnabled), false };
#pragma warning restore 618
            yield return new object[] { CreateFunc(s => s.CustomSamplingRules), null };
            yield return new object[] { CreateFunc(s => s.MaxTracesSubmittedPerSecond), 100 };
            yield return new object[] { CreateFunc(s => s.TracerMetricsEnabled), false };
            yield return new object[] { CreateFunc(s => s.Exporter.DogStatsdPort), 8125 };
        }

        public static IEnumerable<object[]> GetTestData()
        {
            yield return new object[] { ConfigurationKeys.TraceEnabled, "true", CreateFunc(s => s.TraceEnabled), true };
            yield return new object[] { ConfigurationKeys.TraceEnabled, "false", CreateFunc(s => s.TraceEnabled), false };

            yield return new object[] { ConfigurationKeys.AgentHost, "test-host", CreateFunc(s => s.Exporter.AgentUri), new Uri("http://test-host:8126/") };
            yield return new object[] { ConfigurationKeys.AgentPort, "9000", CreateFunc(s => s.Exporter.AgentUri), new Uri("http://127.0.0.1:9000/") };

            yield return new object[] { ConfigurationKeys.Environment, "staging", CreateFunc(s => s.Environment), "staging" };

            yield return new object[] { ConfigurationKeys.ServiceVersion, "1.0.0", CreateFunc(s => s.ServiceVersion), "1.0.0" };

            yield return new object[] { ConfigurationKeys.ServiceName, "web-service", CreateFunc(s => s.ServiceName), "web-service" };
            yield return new object[] { "DD_SERVICE_NAME", "web-service", CreateFunc(s => s.ServiceName), "web-service" };

            yield return new object[] { ConfigurationKeys.DisabledIntegrations, "integration1;integration2;;INTEGRATION2", CreateFunc(s => s.DisabledIntegrationNames.Count), 2 };

            yield return new object[] { ConfigurationKeys.GlobalTags, "k1:v1, k2:v2", CreateFunc(s => s.GlobalTags), TagsK1V1K2V2 };
            yield return new object[] { ConfigurationKeys.GlobalTags, "keyonly:,nocolon,:,:valueonly,k2:v2", CreateFunc(s => s.GlobalTags), TagsK2V2 };
            yield return new object[] { "DD_TRACE_GLOBAL_TAGS", "k1:v1, k2:v2", CreateFunc(s => s.GlobalTags), TagsK1V1K2V2 };
            yield return new object[] { ConfigurationKeys.GlobalTags, "k1:v1,k1:v2", CreateFunc(s => s.GlobalTags.Count), 1 };

#pragma warning disable 618 // App Analytics is deprecated but still supported
            yield return new object[] { ConfigurationKeys.GlobalAnalyticsEnabled, "true", CreateFunc(s => s.AnalyticsEnabled), true };
            yield return new object[] { ConfigurationKeys.GlobalAnalyticsEnabled, "false", CreateFunc(s => s.AnalyticsEnabled), false };
#pragma warning restore 618

            yield return new object[] { ConfigurationKeys.HeaderTags, "header1:tag1,header2:Content-Type,header3: Content-Type ,header4:C!!!ont_____ent----tYp!/!e,header6:9invalidtagname,:invalidtagonly,validheaderonly:,validheaderwithoutcolon,:", CreateFunc(s => s.HeaderTags), HeaderTagsWithOptionalMappings };
            yield return new object[] { ConfigurationKeys.HeaderTags, "header1:tag1,header2:tag1", CreateFunc(s => s.HeaderTags), HeaderTagsSameTag };
            yield return new object[] { ConfigurationKeys.HeaderTags, "header1:tag1,header1:tag2", CreateFunc(s => s.HeaderTags.Count), 1 };
            yield return new object[] { ConfigurationKeys.HeaderTags, "header3:my.header.with.dot,my.new.header.with.dot", CreateFunc(s => s.HeaderTags), HeaderTagsWithDots };
        }

        // JsonConfigurationSource needs to be tested with JSON data, which cannot be used with the other IConfigurationSource implementations.
        public static IEnumerable<object[]> GetJsonTestData()
        {
            yield return new object[] { @"{ ""DD_TRACE_GLOBAL_TAGS"": { ""k1"":""v1"", ""k2"": ""v2""} }", CreateFunc(s => s.GlobalTags), TagsK1V1K2V2 };
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

        public static IEnumerable<object[]> GetBadJsonTestData3()
        {
            // Json doesn't represent dictionary of string to string
            yield return new object[] { @"{ ""DD_TRACE_GLOBAL_TAGS"": { ""name1"": { ""name2"": [ ""vers"" ] } } }", CreateFunc(s => s.GlobalTags.Count), 0 };
        }

        public static Func<TracerSettings, object> CreateFunc(Func<TracerSettings, object> settingGetter)
        {
            return settingGetter;
        }

        public static Func<GlobalSettings, object> CreateGlobalFunc(Func<GlobalSettings, object> settingGetter)
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
            // save original value so we can restore later
            var originalValue = Environment.GetEnvironmentVariable(key);

            TracerSettings settings;

            if (key == "DD_SERVICE_NAME")
            {
                // We need to ensure DD_SERVICE is empty.
                string originalServiceName = Environment.GetEnvironmentVariable(ConfigurationKeys.ServiceName);
                Environment.SetEnvironmentVariable(ConfigurationKeys.ServiceName, null, EnvironmentVariableTarget.Process);

                settings = GetTracerSettings(key, value);

                // after load settings we can restore the original DD_SERVICE
                Environment.SetEnvironmentVariable(ConfigurationKeys.ServiceName, originalServiceName, EnvironmentVariableTarget.Process);
            }
            else if (key == ConfigurationKeys.AgentHost || key == ConfigurationKeys.AgentPort)
            {
                // We need to ensure DD_TRACE_AGENT_URL is empty.
                string originalAgentUri = Environment.GetEnvironmentVariable(ConfigurationKeys.AgentUri);
                Environment.SetEnvironmentVariable(ConfigurationKeys.AgentUri, null, EnvironmentVariableTarget.Process);

                settings = GetTracerSettings(key, value);

                // after load settings we can restore the original DD_TRACE_AGENT_URL
                Environment.SetEnvironmentVariable(ConfigurationKeys.AgentUri, originalAgentUri, EnvironmentVariableTarget.Process);
            }
            else
            {
                settings = GetTracerSettings(key, value);
            }

            // restore original value
            Environment.SetEnvironmentVariable(key, originalValue, EnvironmentVariableTarget.Process);

            object actualValue = settingGetter(settings);
            Assert.Equal(expectedValue, actualValue);

            static TracerSettings GetTracerSettings(string key, string value)
            {
                Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
                IConfigurationSource source = new EnvironmentConfigurationSource();
                return new TracerSettings(source);
            }
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void JsonConfigurationSource(
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

        [Theory]
        [MemberData(nameof(GetGlobalDefaultTestData))]
        public void GlobalDefaultSetting(Func<GlobalSettings, object> settingGetter, object expectedValue)
        {
            var settings = new GlobalSettings();
            object actualValue = settingGetter(settings);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [MemberData(nameof(GetGlobalTestData))]
        public void GlobalNameValueConfigurationSource(
            string key,
            string value,
            Func<GlobalSettings, object> settingGetter,
            object expectedValue)
        {
            var collection = new NameValueCollection { { key, value } };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new GlobalSettings(source);
            object actualValue = settingGetter(settings);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [MemberData(nameof(GetGlobalTestData))]
        public void GlobalEnvironmentConfigurationSource(
            string key,
            string value,
            Func<GlobalSettings, object> settingGetter,
            object expectedValue)
        {
            IConfigurationSource source = new EnvironmentConfigurationSource();

            // save original value so we can restore later
            var originalValue = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
            var settings = new GlobalSettings(source);
            Environment.SetEnvironmentVariable(key, originalValue, EnvironmentVariableTarget.Process);

            object actualValue = settingGetter(settings);
            Assert.Equal(expectedValue, actualValue);
        }

        // Special case for dictionary-typed settings in JSON
        [Theory]
        [MemberData(nameof(GetJsonTestData))]
        public void JsonConfigurationSourceWithJsonTypedSetting(
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

        [Theory]
        [MemberData(nameof(GetBadJsonTestData3))]
        public void JsonConfigurationSource_BadData3(
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
        [InlineData(false, "tag_1")]
        [InlineData(true, "tag.1")]
        public void TestHeaderTagsNormalization(bool headerTagsNormalizationFixEnabled, string expectedHeader)
        {
            var expectedValue = new Dictionary<string, string> { { "header", expectedHeader } };
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled, headerTagsNormalizationFixEnabled.ToString() },
                { ConfigurationKeys.HeaderTags, "header:tag.1" },
            };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);

            Assert.Equal(expectedValue, settings.HeaderTags);
        }
    }
}
