// <copyright file="AppSecModelSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

using AsmAction = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;
using AsmParameter = Datadog.Trace.AppSec.Rcm.Models.Asm.Parameter;
using WafParameter = Datadog.Trace.AppSec.Waf.ReturnTypes.Managed.Parameter;

namespace Datadog.Trace.Tests.AppSec;

/// <summary>
/// Baseline serialization tests for AppSec JSON models.
/// These capture the exact JSON format before any JSON library migration.
/// </summary>
public class AppSecModelSerializationTests
{
    // ===== Asm Models =====

    [Fact]
    public void AsmAction_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """{"id":"block_request","type":"block_request","parameters":{"status_code":403,"type":"auto","location":"/" }}""";
        var result = JsonConvert.DeserializeObject<AsmAction>(json);

        result.Id.Should().Be("block_request");
        result.Type.Should().Be("block_request");
        result.Parameters.Should().NotBeNull();
        result.Parameters.StatusCode.Should().Be(403);
        result.Parameters.Type.Should().Be("auto");
        result.Parameters.Location.Should().Be("/");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<AsmAction>(reserialized);
        result2.Id.Should().Be("block_request");
        result2.Parameters.StatusCode.Should().Be(403);
    }

    [Fact]
    public void AsmAction_NullFields_RoundTrips()
    {
        // language=json
        var json = """{}""";
        var result = JsonConvert.DeserializeObject<AsmAction>(json);

        result.Id.Should().BeNull();
        result.Type.Should().BeNull();
        result.Parameters.Should().BeNull();
    }

    [Fact]
    public void RuleOverride_WithOnMatch_RoundTrips()
    {
        // Note: RulesTarget is JToken — tested as pass-through here, detailed in Step 2D
        // language=json
        var json = """{"id":"rule-1","enabled":false,"on_match":["block","stack_trace"]}""";
        var result = JsonConvert.DeserializeObject<RuleOverride>(json);

        result.Id.Should().Be("rule-1");
        result.Enabled.Should().BeFalse();
        result.OnMatch.Should().BeEquivalentTo(["block", "stack_trace"]);

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<RuleOverride>(reserialized);
        result2.Id.Should().Be("rule-1");
        result2.Enabled.Should().BeFalse();
        result2.OnMatch.Should().HaveCount(2);
    }

    [Fact]
    public void RuleOverride_NullableEnabled_RoundTrips()
    {
        // language=json
        var json = """{"id":"rule-2"}""";
        var result = JsonConvert.DeserializeObject<RuleOverride>(json);

        result.Enabled.Should().BeNull();
        result.OnMatch.Should().BeNull();
        result.RulesTarget.Should().BeNull();
    }

    // ===== AsmData Models =====

    [Fact]
    public void AsmDataPayload_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "rules_data":[
                    {"type":"ip_with_expiration","id":"blocked_ips","data":[
                        {"expiration":1700000000,"value":"192.168.1.1"},
                        {"value":"10.0.0.1"}
                    ]}
                ],
                "exclusion_data":[
                    {"type":"data_with_expiration","id":"exc_1","data":[
                        {"expiration":18446744073709551615,"value":"excluded"}
                    ]}
                ]
            }
            """;

        var result = JsonConvert.DeserializeObject<Datadog.Trace.AppSec.Rcm.Models.AsmData.Payload>(json);

        result.RulesData.Should().ContainSingle();
        result.RulesData[0].Type.Should().Be("ip_with_expiration");
        result.RulesData[0].Id.Should().Be("blocked_ips");
        result.RulesData[0].Data.Should().HaveCount(2);
        result.RulesData[0].Data[0].Expiration.Should().Be(1700000000UL);
        result.RulesData[0].Data[0].Value.Should().Be("192.168.1.1");
        result.RulesData[0].Data[1].Expiration.Should().BeNull();
        result.RulesData[0].Data[1].Value.Should().Be("10.0.0.1");

        result.ExclusionsData.Should().ContainSingle();
        // ulong max value
        result.ExclusionsData[0].Data[0].Expiration.Should().Be(ulong.MaxValue);

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<Datadog.Trace.AppSec.Rcm.Models.AsmData.Payload>(reserialized);
        result2.RulesData[0].Data.Should().HaveCount(2);
        result2.ExclusionsData[0].Data[0].Expiration.Should().Be(ulong.MaxValue);
    }

    [Fact]
    public void AsmDataPayload_NullArrays_RoundTrips()
    {
        // language=json
        var json = """{}""";
        var result = JsonConvert.DeserializeObject<Datadog.Trace.AppSec.Rcm.Models.AsmData.Payload>(json);

        result.RulesData.Should().BeNull();
        result.ExclusionsData.Should().BeNull();
    }

    // ===== AsmFeatures Models =====

    [Fact]
    public void AsmFeatures_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """{"asm":{"enabled":true},"auto_user_instrum":{"mode":"identification"}}""";
        var result = JsonConvert.DeserializeObject<Datadog.Trace.AppSec.Rcm.Models.AsmFeatures.AsmFeatures>(json);

        result.Asm.Should().NotBeNull();
        result.Asm.Enabled.Should().BeTrue();
        result.AutoUserInstrum.Should().NotBeNull();
        result.AutoUserInstrum.Mode.Should().Be("identification");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<Datadog.Trace.AppSec.Rcm.Models.AsmFeatures.AsmFeatures>(reserialized);
        result2.Asm.Enabled.Should().BeTrue();
        result2.AutoUserInstrum.Mode.Should().Be("identification");
    }

    [Fact]
    public void AsmFeatures_NullEnabled_RoundTrips()
    {
        // language=json
        var json = """{"asm":{}}""";
        var result = JsonConvert.DeserializeObject<Datadog.Trace.AppSec.Rcm.Models.AsmFeatures.AsmFeatures>(json);

        result.Asm.Enabled.Should().BeNull();
    }

    // ===== WAF ReturnTypes.Managed Models =====

    [Fact]
    public void WafMatch_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "rule":{"id":"ua0-600-10x","name":"Acunetix","tags":{"type":"security_scanner","category":"attack_attempt"}},
                "rule_matches":[
                    {
                        "operator":"match_regex",
                        "operator_value":"Acunetix",
                        "parameters":[
                            {
                                "address":"server.request.headers.no_cookies",
                                "highlight":["Acunetix"],
                                "key_path":["user-agent",0],
                                "value":"Acunetix Web Vulnerability Scanner"
                            }
                        ]
                    }
                ],
                "span_id":12345678901234,
                "security_response_id":"resp-abc"
            }
            """;

        var result = JsonConvert.DeserializeObject<WafMatch>(json);

        result.Rule.Id.Should().Be("ua0-600-10x");
        result.Rule.Name.Should().Be("Acunetix");
        result.Rule.Tags.Type.Should().Be("security_scanner");
        result.Rule.Tags.Category.Should().Be("attack_attempt");
        result.RuleMatches.Should().ContainSingle();
        result.RuleMatches[0].Operator.Should().Be("match_regex");
        result.RuleMatches[0].OperatorValue.Should().Be("Acunetix");
        result.RuleMatches[0].Parameters.Should().ContainSingle();
        result.RuleMatches[0].Parameters[0].Address.Should().Be("server.request.headers.no_cookies");
        result.RuleMatches[0].Parameters[0].Highlight.Should().ContainSingle().Which.Should().Be("Acunetix");
        result.RuleMatches[0].Parameters[0].Value.Should().Be("Acunetix Web Vulnerability Scanner");
        result.SpanId.Should().Be(12345678901234UL);
        result.SecurityResponseId.Should().Be("resp-abc");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<WafMatch>(reserialized);
        result2.Rule.Id.Should().Be("ua0-600-10x");
        result2.SpanId.Should().Be(12345678901234UL);
    }

    [Fact]
    public void WafMatch_NullValueHandlingIgnore_OmitsNullFields()
    {
        // WafMatch has NullValueHandling.Ignore on SpanId and SecurityResponseId
        var match = new WafMatch
        {
            Rule = new Rule { Id = "test", Name = "Test Rule", Tags = new Datadog.Trace.AppSec.Waf.ReturnTypes.Managed.Tags { Type = "t", Category = "c" } },
            RuleMatches = [],
            SpanId = null,
            SecurityResponseId = null,
        };

        var json = JsonConvert.SerializeObject(match);

        json.Should().NotContain("span_id");
        json.Should().NotContain("security_response_id");
    }

    [Fact]
    public void WafParameter_ObjectArrayKeyPath_RoundTrips()
    {
        // KeyPath is object[] — can contain strings and integers
        // language=json
        var json = """{"address":"server.request.query","highlight":["<script>"],"key_path":["param",0,"nested"],"value":"<script>alert(1)</script>"}""";
        var result = JsonConvert.DeserializeObject<WafParameter>(json);

        result.Address.Should().Be("server.request.query");
        result.Highlight.Should().ContainSingle().Which.Should().Be("<script>");
        result.KeyPath.Should().HaveCount(3);
        // Newtonsoft deserializes object[] elements as JToken values
        result.KeyPath[0].ToString().Should().Be("param");
        result.KeyPath[2].ToString().Should().Be("nested");
        result.Value.Should().Be("<script>alert(1)</script>");
    }

    [Fact]
    public void ResultData_WithFilters_RoundTrips()
    {
        // language=json
        var json = """
            {
                "ret_code":1,
                "flow":"block",
                "step":"step1",
                "rule":"rule-1",
                "filter":[
                    {"operator":"match_regex","operator_value":"pattern","binding_accessor":"ba","manifest_key":"mk","resolved_value":"rv","match_status":"matched"}
                ]
            }
            """;

        var result = JsonConvert.DeserializeObject<ResultData>(json);

        result.RetCode.Should().Be(1);
        result.Flow.Should().Be("block");
        result.Step.Should().Be("step1");
        result.Rule.Should().Be("rule-1");
        result.Filter.Should().ContainSingle();
        result.Filter[0].Operator.Should().Be("match_regex");
        result.Filter[0].MatchStatus.Should().Be("matched");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<ResultData>(reserialized);
        result2.RetCode.Should().Be(1);
        result2.Filter.Should().ContainSingle();
    }
}
