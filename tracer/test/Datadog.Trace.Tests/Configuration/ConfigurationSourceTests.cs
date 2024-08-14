// <copyright file="ConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    [CollectionDefinition(nameof(ConfigurationSourceTests), DisableParallelization = true)]
    [Collection(nameof(ConfigurationSourceTests))]
    public class ConfigurationSourceTests : IDisposable
    {
        private static readonly Dictionary<string, string> TagsK1V1K2V2 = new() { { "k1", "v1" }, { "k2", "v2" } };
        private static readonly Dictionary<string, string> TagsK2V2 = new() { { "k2", "v2" } };
        private static readonly Dictionary<string, string> TagsWithColonsInValue = new() { { "k1", "v1" }, { "k2", "v2:with:colons" }, { "trailing", "colon:good:" } };
        private static readonly Dictionary<string, string> HeaderTagsWithOptionalMappings = new() { { "header1", "tag1" }, { "header2", "Content-Type" }, { "header3", "Content-Type" }, { "header4", "C!!!ont_____ent----tYp!/!e" }, { "validheaderwithoutcolon", string.Empty } };
        private static readonly Dictionary<string, string> HeaderTagsWithDots = new() { { "header3", "my.header.with.dot" }, { "my.new.header.with.dot", string.Empty } };
        private static readonly Dictionary<string, string> HeaderTagsSameTag = new() { { "header1", "tag1" }, { "header2", "tag1" } };

        private readonly Dictionary<string, string> _envVars;

        public ConfigurationSourceTests()
        {
            _envVars = GetTestData()
                      .Select(allArgs => allArgs.Key)
                      .Concat(GetGlobalTestData().Select(allArgs => allArgs.Key))
                      .Distinct()
                      .ToDictionary(key => key, key => Environment.GetEnvironmentVariable(key));
        }

        public static IEnumerable<(Func<GlobalSettings, object> Getter, object Expected)> GetGlobalDefaultTestData()
        {
            yield return (s => s.DebugEnabled, false);
            yield return (s => s.DiagnosticSourceEnabled, true);
        }

        public static IEnumerable<(string Key, string Value, Func<GlobalSettings, object> Getter, object Expected)> GetGlobalTestData()
        {
            yield return (ConfigurationKeys.DebugEnabled, "true", s => s.DebugEnabled, true);
            yield return (ConfigurationKeys.DebugEnabled, "false", s => s.DebugEnabled, false);
            yield return (ConfigurationKeys.DebugEnabled, "tRUe", s => s.DebugEnabled, true);
            yield return (ConfigurationKeys.DebugEnabled, "fALse", s => s.DebugEnabled, false);
            yield return (ConfigurationKeys.DebugEnabled, "1", s => s.DebugEnabled, true);
            yield return (ConfigurationKeys.DebugEnabled, "0", s => s.DebugEnabled, false);
            yield return (ConfigurationKeys.DebugEnabled, "yes", s => s.DebugEnabled, true);
            yield return (ConfigurationKeys.DebugEnabled, "no", s => s.DebugEnabled, false);
            yield return (ConfigurationKeys.DebugEnabled, "T", s => s.DebugEnabled, true);
            yield return (ConfigurationKeys.DebugEnabled, "F", s => s.DebugEnabled, false);
            yield return (ConfigurationKeys.DebugEnabled, "Y", s => s.DebugEnabled, true);
            yield return (ConfigurationKeys.DebugEnabled, "N", s => s.DebugEnabled, false);

            // garbage checks
            yield return (ConfigurationKeys.DebugEnabled, "what_even_is_this", s => s.DebugEnabled, false);
            yield return (ConfigurationKeys.DebugEnabled, "42", s => s.DebugEnabled, false);
            yield return (ConfigurationKeys.DebugEnabled, string.Empty, s => s.DebugEnabled, false);
        }

        public static IEnumerable<(Func<TracerSettings, object> SettingGetter, object ExpectedValue)> GetDefaultTestData()
        {
            yield return (s => s.TraceEnabled, true);
            yield return (s => s.Exporter.AgentUri, new Uri("http://127.0.0.1:8126/"));
            yield return (s => s.Environment, null);
            yield return (s => s.ServiceName, null);
            yield return (s => s.DisabledIntegrationNames.Count, 1); // The OpenTelemetry integration is disabled by defa)t
            yield return (s => s.LogsInjectionEnabled, false);
            yield return (s => s.GlobalTags.Count, 0);
#pragma warning disable 618 // App analytics is deprecated but supported
            yield return (s => s.AnalyticsEnabled, false);
#pragma warning restore 618
            yield return (s => s.CustomSamplingRules, null);
            yield return (s => s.MaxTracesSubmittedPerSecond, 100);
            yield return (s => s.TracerMetricsEnabled, false);
            yield return (s => s.Exporter.DogStatsdPort, 8125);
            yield return (s => s.PropagationStyleInject, new[] { "Datadog", "tracecontext" });
            yield return (s => s.PropagationStyleExtract, new[] { "Datadog", "tracecontext" });
            yield return (s => s.ServiceNameMappings, null);

            yield return (s => s.TraceId128BitGenerationEnabled, true);
            yield return (s => s.TraceId128BitLoggingEnabled, false);
        }

        public static IEnumerable<(string Key, string Value, Func<TracerSettings, object> Getter, object Expected)> GetTestData()
        {
            yield return (ConfigurationKeys.TraceEnabled, "true", s => s.TraceEnabled, true);
            yield return (ConfigurationKeys.TraceEnabled, "false", s => s.TraceEnabled, false);

            yield return (ConfigurationKeys.AgentHost, "test-host", s => s.Exporter.AgentUri, new Uri("http://test-host:8126/"));
            yield return (ConfigurationKeys.AgentPort, "9000", s => s.Exporter.AgentUri, new Uri("http://127.0.0.1:9000/"));

            yield return (ConfigurationKeys.Environment, "staging", s => s.Environment, "staging");

            yield return (ConfigurationKeys.ServiceVersion, "1.0.0", s => s.ServiceVersion, "1.0.0");

            yield return (ConfigurationKeys.ServiceName, "web-service", s => s.ServiceName, "web-service");
            yield return ("DD_SERVICE_NAME", "web-service", s => s.ServiceName, "web-service");

            yield return (ConfigurationKeys.DisabledIntegrations, "integration1;integration2;;INTEGRATION2", s => s.DisabledIntegrationNames.Count, 3); // The OpenTelemetry integration is disabled by defau)t

            yield return (ConfigurationKeys.GlobalTags, "k1:v1, k2:v2", s => s.GlobalTags, TagsK1V1K2V2);
            yield return (ConfigurationKeys.GlobalTags, "keyonly:,nocolon,:,:valueonly,k2:v2", s => s.GlobalTags, TagsK2V2);
            yield return ("DD_TRACE_GLOBAL_TAGS", "k1:v1, k2:v2", s => s.GlobalTags, TagsK1V1K2V2);
            yield return (ConfigurationKeys.GlobalTags, "k1:v1,k1:v2", s => s.GlobalTags.Count, 1);
            yield return (ConfigurationKeys.GlobalTags, "k1:v1, k2:v2:with:colons, :leading:colon:bad, trailing:colon:good:", s => s.GlobalTags, TagsWithColonsInValue);

#pragma warning disable 618 // App Analytics is deprecated but still supported
            yield return (ConfigurationKeys.GlobalAnalyticsEnabled, "true", s => s.AnalyticsEnabled, true);
            yield return (ConfigurationKeys.GlobalAnalyticsEnabled, "false", s => s.AnalyticsEnabled, false);
#pragma warning restore 618

            yield return (ConfigurationKeys.HeaderTags, "header1:tag1,header2:Content-Type,header3: Content-Type ,header4:C!!!ont_____ent----tYp!/!e,header6:9invalidtagname,:invalidtagonly,invalidheaderonly:,validheaderwithoutcolon,:", s => s.HeaderTags, HeaderTagsWithOptionalMappings);
            yield return (ConfigurationKeys.HeaderTags, "header1:tag1,header2:tag1", s => s.HeaderTags, HeaderTagsSameTag);
            yield return (ConfigurationKeys.HeaderTags, "header1:tag1,header1:tag2", s => s.HeaderTags.Count, 1);
            yield return (ConfigurationKeys.HeaderTags, "header3:my.header.with.dot,my.new.header.with.dot", s => s.HeaderTags, HeaderTagsWithDots);

            yield return (ConfigurationKeys.ServiceNameMappings, "elasticsearch:custom-name", s => s.ServiceNameMappings["elasticsearch"], "custom-name");
        }

        // JsonConfigurationSource needs to be tested with JSON data, which cannot be used with the other IConfigurationSource implementations.
        public static IEnumerable<(string Value, Func<TracerSettings, object> Getter, object Expected)> GetJsonTestData()
        {
            yield return new(@"{ ""DD_TRACE_GLOBAL_TAGS"": { ""k1"":""v1"", ""k2"": ""v2""} }", s => s.GlobalTags, TagsK1V1K2V2);
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

        public static IEnumerable<(string Value, Func<TracerSettings, object> Getter, object Expected)> GetBadJsonTestData3()
        {
            // Json doesn't represent dictionary of string to string
            yield return (@"{ ""DD_TRACE_GLOBAL_TAGS"": { ""name1"": { ""name2"": [ ""vers"" ] } } }", s => s.GlobalTags.Count, 0);
        }

        public void Dispose()
        {
            ResetEnvironment();
        }

        [Fact]
        public void DefaultSetting()
        {
            foreach (var (settingGetter, expectedValue) in GetDefaultTestData())
            {
                var settings = new TracerSettings();
                object actualValue = settingGetter(settings);
                Assert.Equal(expectedValue, actualValue);
            }
        }

        [Fact]
        public void NameValueConfigurationSource()
        {
            foreach (var (key, value, settingGetter, expectedValue) in GetTestData())
            {
                var collection = new NameValueCollection { { key, value } };
                IConfigurationSource source = new NameValueConfigurationSource(collection);
                var settings = new TracerSettings(source);
                object actualValue = settingGetter(settings);
                // Assert.Equal(expectedValue, actualValue);
                actualValue.Should().BeEquivalentTo(expectedValue);
            }
        }

        [Fact]
        public void EnvironmentConfigurationSource()
        {
            foreach (var (key, value, settingGetter, expectedValue) in GetTestData())
            {
                TracerSettings settings;

                if (key == "DD_SERVICE_NAME")
                {
                    // We need to ensure DD_SERVICE is empty.
                    Environment.SetEnvironmentVariable(ConfigurationKeys.ServiceName, null, EnvironmentVariableTarget.Process);
                    settings = GetTracerSettings(key, value);
                }
                else if (key == ConfigurationKeys.AgentHost || key == ConfigurationKeys.AgentPort)
                {
                    // We need to ensure all the agent URLs are empty.
                    Environment.SetEnvironmentVariable(ConfigurationKeys.AgentHost, null, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable(ConfigurationKeys.AgentPort, null, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable(ConfigurationKeys.AgentUri, null, EnvironmentVariableTarget.Process);

                    settings = GetTracerSettings(key, value);
                }
                else
                {
                    settings = GetTracerSettings(key, value);
                }

                var actualValue = settingGetter(settings);
                actualValue.Should().BeEquivalentTo(expectedValue, $"{key} should have correct value");
                ResetEnvironment();
            }

            static TracerSettings GetTracerSettings(string key, string value)
            {
                Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
                IConfigurationSource source = new EnvironmentConfigurationSource();
                return new TracerSettings(source);
            }
        }

        [Fact]
        public void JsonConfigurationSource()
        {
            foreach (var (key, value, settingGetter, expectedValue) in GetTestData())
            {
                var config = new Dictionary<string, string> { [key] = value };
                string json = JsonConvert.SerializeObject(config);
                IConfigurationSource source = new JsonConfigurationSource(json);
                var settings = new TracerSettings(source);

                object actualValue = settingGetter(settings);
                Assert.Equal(expectedValue, actualValue);
            }
        }

        [Fact]
        public void GlobalDefaultSetting()
        {
            foreach (var (settingGetter, expectedValue) in GetGlobalDefaultTestData())
            {
                var settings = new GlobalSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
                object actualValue = settingGetter(settings);
                Assert.Equal(expectedValue, actualValue);
            }
        }

        [Fact]
        public void GlobalNameValueConfigurationSource()
        {
            foreach (var (key, value, settingGetter, expectedValue) in GetGlobalTestData())
            {
                var collection = new NameValueCollection { { key, value } };
                IConfigurationSource source = new NameValueConfigurationSource(collection);
                var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
                object actualValue = settingGetter(settings);
                Assert.Equal(expectedValue, actualValue);
            }
        }

        [Fact]
        public void GlobalEnvironmentConfigurationSource()
        {
            foreach (var (key, value, settingGetter, expectedValue) in GetGlobalTestData())
            {
                IConfigurationSource source = new EnvironmentConfigurationSource();

                // save original value so we can restore later
                Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
                var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());

                object actualValue = settingGetter(settings);
                Assert.Equal(expectedValue, actualValue);
            }
        }

        // Special case for dictionary-typed settings in JSON
        [Fact]
        public void JsonConfigurationSourceWithJsonTypedSetting()
        {
            foreach (var (value, settingGetter, expectedValue) in GetJsonTestData())
            {
                IConfigurationSource source = new JsonConfigurationSource(value);
                var settings = new TracerSettings(source);

                var actualValue = settingGetter(settings);
                Assert.Equal(expectedValue, actualValue);
            }
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

        [Fact]
        public void JsonConfigurationSource_BadData3()
        {
            foreach (var (value, settingGetter, expectedValue) in GetBadJsonTestData3())
            {
                IConfigurationSource source = new JsonConfigurationSource(value);
                var settings = new TracerSettings(source);

                var actualValue = settingGetter(settings);
                Assert.Equal(expectedValue, actualValue);
            }
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

        private void ResetEnvironment()
        {
            foreach (var envVar in _envVars)
            {
                Environment.SetEnvironmentVariable(envVar.Key, envVar.Value, EnvironmentVariableTarget.Process);
            }
        }
    }
}
