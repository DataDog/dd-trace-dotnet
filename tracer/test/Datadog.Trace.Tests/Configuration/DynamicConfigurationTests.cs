// <copyright file="DynamicConfigurationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class DynamicConfigurationTests
    {
        [Fact(Skip = "Disabled until service mapping is re-implemented in dynamic config")]
        public void ApplyServiceMappingToNewTraces()
        {
            var scope = Tracer.Instance.StartActive("Trace1");

            Tracer.Instance.CurrentTraceSettings.GetServiceName("test")
               .Should().Be($"{Tracer.Instance.DefaultServiceName}-test");

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_service_mapping", "test:ok")), Tracer.Instance.Settings);

            Tracer.Instance.CurrentTraceSettings.GetServiceName("test")
               .Should().Be($"{Tracer.Instance.DefaultServiceName}-test", "the old configuration should be used inside of the active trace");

            scope.Close();

            Tracer.Instance.CurrentTraceSettings.GetServiceName("test")
               .Should().Be("ok", "the new configuration should be used outside of the active trace");
        }

        [Fact]
        public void ApplyConfigurationTwice()
        {
            var tracer = TracerManager.Instance;

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_sampling_rate", 0.4)), tracer.Settings);

            var newTracer = TracerManager.Instance;

            newTracer.Should().NotBeSameAs(tracer);

            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_sampling_rate", 0.4)), tracer.Settings);

            TracerManager.Instance.Should().BeSameAs(newTracer);
        }

        [Fact]
        public void ApplyTagsToDirectLogs()
        {
            var tracerSettings = TracerSettings.Create(new() { { ConfigurationKeys.GlobalTags, "key1:value1" } });

            // emulate the one-time subscribe that TracerManager.Instance does
            var tracerManager = TracerManagerFactory.Instance.CreateTracerManager(tracerSettings, null);

            tracerManager.DirectLogSubmission.Formatter.Tags.Should().Be("key1:value1");

            var configBuilder = CreateConfig(("tracing_tags", new[] { "key2:value2" }));
            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(configBuilder, tracerSettings);

            tracerManager.DirectLogSubmission.Formatter.Tags.Should().Be("key2:value2");
        }

        [Fact]
        public void DoesNotOverrideDirectLogsTags()
        {
            var tracerSettings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.DirectLogSubmission.GlobalTags, "key1:value1" },
                { ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, "xunit" },
                { ConfigurationKeys.GlobalTags, "key2:value2" },
            });
            var tracerManager = TracerManagerFactory.Instance.CreateTracerManager(tracerSettings, null);

            tracerManager.DirectLogSubmission.Formatter.Tags.Should().Be("key1:value1");

            var configBuilder = CreateConfig(("tracing_tags", new[] { "key3:value3" }));
            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(configBuilder, tracerSettings);

            tracerManager.DirectLogSubmission.Formatter.Tags.Should().Be("key1:value1");
        }

        [Fact]
        public void DoesNotReplaceRuntimeMetricsWriter()
        {
            var tracerSettings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.RuntimeMetricsEnabled, "true" },
                { ConfigurationKeys.GlobalTags, "key1:value1" },
            });
            var tracerManager = TracerManagerFactory.Instance.CreateTracerManager(tracerSettings, null);

            var previousRuntimeMetrics = tracerManager.RuntimeMetrics;

            var configBuilder = CreateConfig(("tracing_tags", new[] { "key2:value2" }));
            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(configBuilder, tracerSettings);

            tracerManager.RuntimeMetrics.Should().Be(previousRuntimeMetrics);
        }

        [Fact]
        public void EnableTracing()
        {
            var tracerSettings = new TracerSettings();
            var tracerManager = TracerManagerFactory.Instance.CreateTracerManager(tracerSettings, null);

            // tracing is enabled by default
            tracerManager.PerTraceSettings.Settings.TraceEnabled.Should().BeTrue();

            // disable "remotely"
            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_enabled", false)), tracerSettings);
            tracerManager.PerTraceSettings.Settings.TraceEnabled.Should().BeFalse();

            // re-enable "remotely"
            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(CreateConfig(("tracing_enabled", true)), tracerSettings);
            tracerManager.PerTraceSettings.Settings.TraceEnabled.Should().BeTrue();
        }

        [Fact]
        public void SetSamplingRules()
        {
            // start with local sampling rules only
            var localSamplingRulesConfig = new[]
            {
                new { sample_rate = 0.5,  service = "Service3", resource = "Resource3", },
            };

            var localSamplingRulesJson = JsonConvert.SerializeObject(localSamplingRulesConfig);

            var tracerSettings = TracerSettings.Create(new()
            {
                { "DD_TRACE_SAMPLING_RULES", localSamplingRulesJson }
            });

            var tracerManager = TracerManagerFactory.Instance.CreateTracerManager(tracerSettings, null);

            tracerManager.PerTraceSettings.Settings.CustomSamplingRules.Should().Be(localSamplingRulesJson);
            tracerManager.PerTraceSettings.Settings.CustomSamplingRulesIsRemote.Should().BeFalse();

            var rules = ((TraceSampler)tracerManager.PerTraceSettings.TraceSampler)!.GetRules();

            rules.Should()
                 .BeEquivalentTo(
                      new ISamplingRule[]
                      {
                          new LocalCustomSamplingRule(
                              rate: 0.1f,
                              serviceNamePattern: "Service3",
                              operationNamePattern: null,
                              resourceNamePattern: "Resource3",
                              tagPatterns: null,
                              timeout: TimeSpan.FromSeconds(1),
                              patternFormat: "glob"),
                          new AgentSamplingRule()
                      });

            // set sampling rules "remotely"
            var remoteSamplingRulesConfig = new[]
            {
                new { sample_rate = 0.5, provenance = "customer", service = "Service1", resource = "Resource1", },
                new { sample_rate = 0.1, provenance = "dynamic", service = "Service2", resource = "Resource2", }
            };

            var configBuilder = CreateConfig(("tracing_sampling_rules", remoteSamplingRulesConfig));
            DynamicConfigurationManager.OnlyForTests_ApplyConfiguration(configBuilder, tracerSettings);

            var remoteSamplingRulesJson = JsonConvert.SerializeObject(remoteSamplingRulesConfig);
            tracerManager.PerTraceSettings.Settings.CustomSamplingRules.Should().Be(remoteSamplingRulesJson);
            tracerManager.PerTraceSettings.Settings.CustomSamplingRulesIsRemote.Should().BeTrue();

            rules = ((TraceSampler)tracerManager.PerTraceSettings.TraceSampler)!.GetRules();

            // new list should include the remote rules, not the local rules
            rules.Should()
                 .BeEquivalentTo(
                      new ISamplingRule[]
                      {
                          new RemoteCustomSamplingRule(
                              rate: 0.5f,
                              provenance: SamplingRuleProvenance.RemoteCustomer,
                              serviceNamePattern: "Service1",
                              operationNamePattern: null,
                              resourceNamePattern: "Resource1",
                              tagPatterns: null,
                              timeout: TimeSpan.FromSeconds(1)),
                          new RemoteCustomSamplingRule(
                              rate: 0.1f,
                              provenance: SamplingRuleProvenance.RemoteDynamic,
                              serviceNamePattern: "Service2",
                              operationNamePattern: null,
                              resourceNamePattern: "Resource2",
                              tagPatterns: null,
                              timeout: TimeSpan.FromSeconds(1)),
                          new AgentSamplingRule()
                      });
        }

        [Fact]
        public void DynamicConfigConfigurationSource_JTokenConstructor_ProducesSameResultAsStringConstructor()
        {
            // Arrange
            var configData = new
            {
                lib_config = new
                {
                    tracing_enabled = true,
                    log_injection_enabled = false,
                    tracing_sampling_rate = 0.75,
                    tracing_tags = new[] { "key1:value1", "key2:value2" },
                    dynamic_instrumentation_enabled = true,
                    exception_replay_enabled = true,
                    code_origin_enabled = true,
                }
            };

            var json = JsonConvert.SerializeObject(configData);
            var jToken = JToken.FromObject(configData);

            // Act
            var stringSource = new DynamicConfigConfigurationSource(json, ConfigurationOrigins.RemoteConfig);
            var jTokenSource = new DynamicConfigConfigurationSource(jToken, ConfigurationOrigins.RemoteConfig);

            var stringBuilder = new ConfigurationBuilder(stringSource, NullConfigurationTelemetry.Instance);
            var jTokenBuilder = new ConfigurationBuilder(jTokenSource, NullConfigurationTelemetry.Instance);

            // Assert - Both should produce identical configuration results
            stringBuilder.WithKeys(ConfigurationKeys.TraceEnabled).AsBool().Should()
                .Be(jTokenBuilder.WithKeys(ConfigurationKeys.TraceEnabled).AsBool());

            stringBuilder.WithKeys(ConfigurationKeys.LogsInjectionEnabled).AsBool().Should()
                .Be(jTokenBuilder.WithKeys(ConfigurationKeys.LogsInjectionEnabled).AsBool());

            stringBuilder.WithKeys(ConfigurationKeys.GlobalSamplingRate).AsDouble().Should()
                .Be(jTokenBuilder.WithKeys(ConfigurationKeys.GlobalSamplingRate).AsDouble());

            var stringTags = stringBuilder.WithKeys(ConfigurationKeys.GlobalTags).AsDictionary();
            var jTokenTags = jTokenBuilder.WithKeys(ConfigurationKeys.GlobalTags).AsDictionary();
            stringTags.Should().BeEquivalentTo(jTokenTags);

            stringBuilder.WithKeys(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled).AsBool().Should()
                .Be(jTokenBuilder.WithKeys(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled).AsBool());

            stringBuilder.WithKeys(ConfigurationKeys.Debugger.ExceptionReplayEnabled).AsBool().Should()
                .Be(jTokenBuilder.WithKeys(ConfigurationKeys.Debugger.ExceptionReplayEnabled).AsBool());

            stringBuilder.WithKeys(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled).AsBool().Should()
                .Be(jTokenBuilder.WithKeys(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled).AsBool());
        }

        private static DynamicConfigConfigurationSource CreateConfig(params (string Key, object Value)[] settings)
        {
            var libConfigObj = new JObject();
            foreach (var (key, value) in settings)
            {
                libConfigObj[key] = JToken.FromObject(value);
            }

            var configObj = new JObject
            {
                ["lib_config"] = libConfigObj
            };

            return new DynamicConfigConfigurationSource(configObj, ConfigurationOrigins.RemoteConfig);
        }
    }
}
