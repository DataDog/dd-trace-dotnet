// <copyright file="SamplingRuleSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling;

/// <summary>
/// Baseline serialization tests for sampling rule JSON models.
/// These capture the exact JSON format before any JSON library migration.
/// </summary>
public class SamplingRuleSerializationTests
{
    [Fact]
    public void SpanSamplingRuleConfig_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """[{"service":"my-service","name":"my-op","resource":"my-resource","tags":{"env":"prod","version":"1.0"},"sample_rate":0.5,"max_per_second":100.0}]""";
        var result = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.SpanSamplingRule.SpanSamplingRuleConfig>>(json);

        result.Should().ContainSingle();
        var rule = result[0];
        rule.ServiceNameGlob.Should().Be("my-service");
        rule.OperationNameGlob.Should().Be("my-op");
        rule.ResourceNameGlob.Should().Be("my-resource");
        rule.TagGlobs.Should().ContainKey("env").WhoseValue.Should().Be("prod");
        rule.TagGlobs.Should().ContainKey("version").WhoseValue.Should().Be("1.0");
        rule.SampleRate.Should().Be(0.5f);
        rule.MaxPerSecond.Should().Be(100.0f);

        // Re-serialize and verify round-trip
        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.SpanSamplingRule.SpanSamplingRuleConfig>>(reserialized);
        result2.Should().ContainSingle();
        result2[0].ServiceNameGlob.Should().Be("my-service");
        result2[0].SampleRate.Should().Be(0.5f);
        result2[0].MaxPerSecond.Should().Be(100.0f);
    }

    [Fact]
    public void SpanSamplingRuleConfig_DefaultValues_WhenFieldsMissing()
    {
        var json = @"[{}]";
        var result = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.SpanSamplingRule.SpanSamplingRuleConfig>>(json);

        result.Should().ContainSingle();
        var rule = result[0];
        rule.ServiceNameGlob.Should().Be("*");
        rule.OperationNameGlob.Should().Be("*");
        rule.ResourceNameGlob.Should().BeNull();
        rule.TagGlobs.Should().BeNull();
        rule.SampleRate.Should().Be(1.0f);
        rule.MaxPerSecond.Should().BeNull();
    }

    [Fact]
    public void SpanSamplingRuleConfig_NullMaxPerSecond_IsOmittedOrNull()
    {
        // language=json
        var json = """[{"service":"svc","name":"op","sample_rate":1.0}]""";
        var result = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.SpanSamplingRule.SpanSamplingRuleConfig>>(json);

        result.Should().ContainSingle();
        result[0].MaxPerSecond.Should().BeNull();
    }

    [Fact]
    public void LocalCustomSamplingRule_RuleConfigJsonModel_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """[{"sample_rate":0.75,"name":"op-name","service":"svc-name","resource":"res-name","tags":{"env":"staging","region":null}}]""";
        var result = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.LocalCustomSamplingRule.RuleConfigJsonModel>>(json);

        result.Should().ContainSingle();
        var rule = result[0];
        rule.SampleRate.Should().Be(0.75f);
        rule.OperationName.Should().Be("op-name");
        rule.Service.Should().Be("svc-name");
        rule.Resource.Should().Be("res-name");
        rule.Tags.Should().ContainKey("env").WhoseValue.Should().Be("staging");
        rule.Tags.Should().ContainKey("region").WhoseValue.Should().BeNull();

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.LocalCustomSamplingRule.RuleConfigJsonModel>>(reserialized);
        result2[0].SampleRate.Should().Be(0.75f);
        result2[0].OperationName.Should().Be("op-name");
    }

    [Fact]
    public void LocalCustomSamplingRule_RuleConfigJsonModel_NullOptionalFields()
    {
        // language=json
        var json = """[{"sample_rate":1.0}]""";
        var result = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.LocalCustomSamplingRule.RuleConfigJsonModel>>(json);

        result.Should().ContainSingle();
        var rule = result[0];
        rule.SampleRate.Should().Be(1.0f);
        rule.OperationName.Should().BeNull();
        rule.Service.Should().BeNull();
        rule.Resource.Should().BeNull();
        rule.Tags.Should().BeNull();
    }

    [Fact]
    public void RemoteCustomSamplingRule_RuleConfigJsonModel_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """[{"sample_rate":0.25,"provenance":"customer","name":"op","service":"svc","resource":"res","tags":[{"key":"env","value_glob":"prod*"}]}]""";
        var result = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.RemoteCustomSamplingRule.RuleConfigJsonModel>>(json);

        result.Should().ContainSingle();
        var rule = result[0];
        rule.SampleRate.Should().Be(0.25f);
        rule.Provenance.Should().Be("customer");
        rule.OperationName.Should().Be("op");
        rule.Service.Should().Be("svc");
        rule.Resource.Should().Be("res");
        rule.Tags.Should().ContainSingle();
        rule.Tags[0].Name.Should().Be("env");
        rule.Tags[0].Value.Should().Be("prod*");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.RemoteCustomSamplingRule.RuleConfigJsonModel>>(reserialized);
        result2[0].SampleRate.Should().Be(0.25f);
        result2[0].Tags[0].Name.Should().Be("env");
    }

    [Fact]
    public void RemoteCustomSamplingRule_RuleConfigJsonModel_MultipleRules()
    {
        // language=json
        var json = """[{"sample_rate":0.5,"service":"svc1"},{"sample_rate":1.0,"service":"svc2"}]""";
        var result = JsonConvert.DeserializeObject<List<Datadog.Trace.Sampling.RemoteCustomSamplingRule.RuleConfigJsonModel>>(json);

        result.Should().HaveCount(2);
        result[0].SampleRate.Should().Be(0.5f);
        result[0].Service.Should().Be("svc1");
        result[1].SampleRate.Should().Be(1.0f);
        result[1].Service.Should().Be("svc2");
    }
}
