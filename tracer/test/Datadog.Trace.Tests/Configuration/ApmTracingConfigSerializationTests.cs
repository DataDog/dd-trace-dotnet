// <copyright file="ApmTracingConfigSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

/// <summary>
/// Baseline serialization tests for APM Tracing configuration JSON models.
/// These capture the exact JSON format before any JSON library migration.
/// </summary>
public class ApmTracingConfigSerializationTests
{
    [Fact]
    public void ApmTracingConfigDto_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "lib_config":{
                    "tracing_enabled":true,
                    "log_injection_enabled":false,
                    "tracing_sampling_rate":0.75,
                    "tracing_debug":true,
                    "runtime_metrics_enabled":true,
                    "tracing_service_mapping":"svc1:svc2",
                    "data_streams_enabled":false,
                    "dynamic_instrumentation_enabled":true,
                    "exception_replay_enabled":false,
                    "code_origin_enabled":true
                },
                "service_target":{
                    "service":"my-service",
                    "env":"production"
                },
                "k8s_target_v2":{
                    "cluster_targets":[
                        {"cluster_name":"prod-cluster","enabled":true,"enabled_namespaces":["default","monitoring"]}
                    ]
                }
            }
            """;

        var result = JsonConvert.DeserializeObject<ApmTracingConfigDto>(json);

        result.LibConfig.Should().NotBeNull();
        result.LibConfig.TracingEnabled.Should().BeTrue();
        result.LibConfig.LogInjectionEnabled.Should().BeFalse();
        result.LibConfig.TracingSamplingRate.Should().Be(0.75);
        result.LibConfig.DebugEnabled.Should().BeTrue();
        result.LibConfig.RuntimeMetricsEnabled.Should().BeTrue();
        result.LibConfig.ServiceMapping.Should().Be("svc1:svc2");
        result.LibConfig.DataStreamsEnabled.Should().BeFalse();
        result.LibConfig.DynamicInstrumentationEnabled.Should().BeTrue();
        result.LibConfig.ExceptionReplayEnabled.Should().BeFalse();
        result.LibConfig.CodeOriginEnabled.Should().BeTrue();

        result.ServiceTarget.Should().NotBeNull();
        result.ServiceTarget.Service.Should().Be("my-service");
        result.ServiceTarget.Env.Should().Be("production");

        result.K8sTargetV2.Should().NotBeNull();
        result.K8sTargetV2.ClusterTargets.Should().ContainSingle();
        result.K8sTargetV2.ClusterTargets[0].ClusterName.Should().Be("prod-cluster");
        result.K8sTargetV2.ClusterTargets[0].Enabled.Should().BeTrue();
        result.K8sTargetV2.ClusterTargets[0].EnabledNamespaces.Should().BeEquivalentTo(["default", "monitoring"]);

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<ApmTracingConfigDto>(reserialized);
        result2.LibConfig.TracingEnabled.Should().BeTrue();
        result2.ServiceTarget.Service.Should().Be("my-service");
    }

    [Fact]
    public void ApmTracingConfigDto_NullOptionalFields_RoundTrips()
    {
        // language=json
        var json = """{"lib_config":{}}""";
        var result = JsonConvert.DeserializeObject<ApmTracingConfigDto>(json);

        result.LibConfig.Should().NotBeNull();
        result.LibConfig.TracingEnabled.Should().BeNull();
        result.LibConfig.LogInjectionEnabled.Should().BeNull();
        result.LibConfig.TracingSamplingRate.Should().BeNull();
        result.LibConfig.TracingSamplingRules.Should().BeNull();
        result.LibConfig.TracingHeaderTags.Should().BeNull();
        result.LibConfig.TracingTags.Should().BeNull();
        result.LibConfig.DebugEnabled.Should().BeNull();
        result.LibConfig.RuntimeMetricsEnabled.Should().BeNull();
        result.LibConfig.ServiceMapping.Should().BeNull();
        result.LibConfig.DataStreamsEnabled.Should().BeNull();
        result.LibConfig.SpanSamplingRules.Should().BeNull();
        result.LibConfig.DynamicInstrumentationEnabled.Should().BeNull();
        result.LibConfig.ExceptionReplayEnabled.Should().BeNull();
        result.LibConfig.CodeOriginEnabled.Should().BeNull();
        result.ServiceTarget.Should().BeNull();
        result.K8sTargetV2.Should().BeNull();
    }

    [Fact]
    public void LibConfig_ObjectFields_PreserveJsonStructure_RoundTrips()
    {
        // tracing_sampling_rules, tracing_header_tags, tracing_tags, span_sampling_rules
        // are typed as `object?` — they preserve whatever JSON structure is provided
        // language=json
        var json = """
            {
                "lib_config":{
                    "tracing_sampling_rules":[{"sample_rate":0.5,"service":"*"}],
                    "tracing_header_tags":[{"header":"x-custom","tag_name":"http.x_custom"}],
                    "tracing_tags":{"env":"prod"},
                    "span_sampling_rules":[{"service":"svc","sample_rate":1.0}]
                }
            }
            """;

        var result = JsonConvert.DeserializeObject<ApmTracingConfigDto>(json);

        result.LibConfig.TracingSamplingRules.Should().NotBeNull();
        result.LibConfig.TracingHeaderTags.Should().NotBeNull();
        result.LibConfig.TracingTags.Should().NotBeNull();
        result.LibConfig.SpanSamplingRules.Should().NotBeNull();

        // These are deserialized as JToken objects by Newtonsoft
        var reserialized = JsonConvert.SerializeObject(result);
        reserialized.Should().Contain("tracing_sampling_rules");
        reserialized.Should().Contain("tracing_header_tags");
    }
}
