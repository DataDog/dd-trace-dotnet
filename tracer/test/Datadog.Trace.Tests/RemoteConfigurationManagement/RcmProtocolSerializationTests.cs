// <copyright file="RcmProtocolSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

/// <summary>
/// Baseline serialization tests for RCM Protocol JSON models.
/// These capture the exact JSON format before any JSON library migration.
/// </summary>
public class RcmProtocolSerializationTests
{
    [Fact]
    public void RcmClientTracer_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "runtime_id":"abc-123",
                "language":"dotnet",
                "tracer_version":"2.50.0",
                "service":"my-service",
                "process_tags":["tag1","tag2"],
                "extra_services":["svc-a","svc-b"],
                "env":"staging",
                "app_version":"1.2.3",
                "tags":["env:staging","version:1.2.3"]
            }
            """;

        var result = JsonConvert.DeserializeObject<RcmClientTracer>(json);

        result.RuntimeId.Should().Be("abc-123");
        result.Language.Should().Be("dotnet");
        result.TracerVersion.Should().Be("2.50.0");
        result.Service.Should().Be("my-service");
        result.ProcessTags.Should().BeEquivalentTo(["tag1", "tag2"]);
        result.ExtraServices.Should().BeEquivalentTo(["svc-a", "svc-b"]);
        result.Env.Should().Be("staging");
        result.AppVersion.Should().Be("1.2.3");
        result.Tags.Should().BeEquivalentTo(["env:staging", "version:1.2.3"]);

        // JsonIgnore should not be serialized
        result.IsGitMetadataAddedToRequestTags.Should().BeFalse();

        // Round-trip
        var reserialized = JsonConvert.SerializeObject(result);
        reserialized.Should().NotContain("is_git_metadata_added");
        var result2 = JsonConvert.DeserializeObject<RcmClientTracer>(reserialized);
        result2.RuntimeId.Should().Be("abc-123");
        result2.Service.Should().Be("my-service");
        result2.Tags.Should().BeEquivalentTo(["env:staging", "version:1.2.3"]);
    }

    [Fact]
    public void RcmClientTracer_NullableFieldsMissing_DefaultsApplied()
    {
        // language=json
        var json = """{"runtime_id":"id","tracer_version":"v","service":"svc","env":"e","tags":[]}""";
        var result = JsonConvert.DeserializeObject<RcmClientTracer>(json);

        result.AppVersion.Should().BeNull();
        result.ProcessTags.Should().BeNull();
        result.ExtraServices.Should().BeNull();
        result.Tags.Should().BeEmpty();
    }

    [Fact]
    public void RcmConfigState_UlongApplyState_RoundTrips()
    {
        // ulong values — tests large unsigned integer handling
        // language=json
        var json = """{"id":"config-1","version":42,"product":"ASM","apply_state":18446744073709551615,"apply_error":"some error"}""";
        var result = JsonConvert.DeserializeObject<RcmConfigState>(json);

        result.Id.Should().Be("config-1");
        result.Version.Should().Be(42L);
        result.Product.Should().Be("ASM");
        result.ApplyState.Should().Be(ulong.MaxValue);
        result.ApplyError.Should().Be("some error");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<RcmConfigState>(reserialized);
        result2.ApplyState.Should().Be(ulong.MaxValue);
    }

    [Fact]
    public void RcmConfigState_NullApplyError_RoundTrips()
    {
        // language=json
        var json = """{"id":"cfg","version":1,"product":"APM_TRACING","apply_state":2}""";
        var result = JsonConvert.DeserializeObject<RcmConfigState>(json);

        result.ApplyError.Should().BeNull();
        result.ApplyState.Should().Be(2UL);
    }

    [Fact]
    public void RcmClientState_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "root_version":100,
                "targets_version":200,
                "config_states":[
                    {"id":"c1","version":1,"product":"ASM_FEATURES","apply_state":0},
                    {"id":"c2","version":2,"product":"ASM_DD","apply_state":3,"apply_error":"failed"}
                ],
                "has_error":true,
                "error":"something went wrong",
                "backend_client_state":"opaque-state-string"
            }
            """;

        var result = JsonConvert.DeserializeObject<RcmClientState>(json);

        result.RootVersion.Should().Be(100L);
        result.TargetsVersion.Should().Be(200L);
        result.ConfigStates.Should().HaveCount(2);
        result.ConfigStates[0].Id.Should().Be("c1");
        result.ConfigStates[1].ApplyError.Should().Be("failed");
        result.HasError.Should().BeTrue();
        result.Error.Should().Be("something went wrong");
        result.BackendClientState.Should().Be("opaque-state-string");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<RcmClientState>(reserialized);
        result2.RootVersion.Should().Be(100L);
        result2.ConfigStates.Should().HaveCount(2);
        result2.HasError.Should().BeTrue();
    }

    [Fact]
    public void RcmClient_AllFieldsPopulated_RoundTrips()
    {
        // byte[] (capabilities) is base64-encoded in JSON by Newtonsoft
        var capabilities = new byte[] { 0x01, 0x02, 0xFF };
        var capabilitiesBase64 = Convert.ToBase64String(capabilities);

        var json = $@"{{
            ""state"":{{""root_version"":1,""targets_version"":2,""config_states"":[],""has_error"":false,""error"":"""",""backend_client_state"":""""}},
            ""id"":""client-1"",
            ""products"":[""ASM_FEATURES"",""APM_TRACING""],
            ""is_tracer"":true,
            ""client_tracer"":{{""runtime_id"":""rt-1"",""language"":""dotnet"",""tracer_version"":""2.50.0"",""service"":""svc"",""env"":""prod"",""tags"":[]}},
            ""capabilities"":""{capabilitiesBase64}""
        }}";

        var result = JsonConvert.DeserializeObject<RcmClient>(json);

        result.Id.Should().Be("client-1");
        result.Products.Should().BeEquivalentTo(["ASM_FEATURES", "APM_TRACING"]);
        result.IsTracer.Should().BeTrue();
        result.Capabilities.Should().BeEquivalentTo(capabilities);
        result.ClientTracer.RuntimeId.Should().Be("rt-1");
        result.State.RootVersion.Should().Be(1L);

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<RcmClient>(reserialized);
        result2.Id.Should().Be("client-1");
        result2.Capabilities.Should().BeEquivalentTo(capabilities);
    }

    [Fact]
    public void RcmCachedTargetFile_WithHashes_RoundTrips()
    {
        // language=json
        var json = """
            {
                "path":"/datadog/config/apm_tracing",
                "length":1234,
                "hashes":[
                    {"algorithm":"sha256","hash":"abc123def456"},
                    {"algorithm":"sha512","hash":"789ghi"}
                ]
            }
            """;

        var result = JsonConvert.DeserializeObject<RcmCachedTargetFile>(json);

        result.Path.Should().Be("/datadog/config/apm_tracing");
        result.Length.Should().Be(1234L);
        result.Hashes.Should().HaveCount(2);
        result.Hashes[0].Algorithm.Should().Be("sha256");
        result.Hashes[0].Hash.Should().Be("abc123def456");
        result.Hashes[1].Algorithm.Should().Be("sha512");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<RcmCachedTargetFile>(reserialized);
        result2.Path.Should().Be("/datadog/config/apm_tracing");
        result2.Hashes.Should().HaveCount(2);
    }

    [Fact]
    public void RcmFile_ByteArrayRaw_RoundTrips()
    {
        var rawContent = Encoding.UTF8.GetBytes("{\"key\":\"value\"}");
        var rawBase64 = Convert.ToBase64String(rawContent);

        var json = $@"{{""path"":""/some/config/path"",""raw"":""{rawBase64}""}}";

        var result = JsonConvert.DeserializeObject<RcmFile>(json);

        result.Path.Should().Be("/some/config/path");
        result.Raw.Should().BeEquivalentTo(rawContent);

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<RcmFile>(reserialized);
        result2.Path.Should().Be("/some/config/path");
        result2.Raw.Should().BeEquivalentTo(rawContent);
    }

    [Fact]
    public void TufRoot_NestedStructure_RoundTrips()
    {
        // language=json
        var json = """
            {
                "signed":{
                    "targets":{
                        "datadog/1/ASM/config": {
                            "custom":{"v":42},
                            "hashes":{"sha256":"abcdef"},
                            "length":567
                        },
                        "datadog/2/APM_TRACING/config": {
                            "custom":{"v":0},
                            "hashes":{},
                            "length":0
                        }
                    },
                    "version":99,
                    "custom":{"opaque_backend_state":"backend-state-123"}
                }
            }
            """;

        var result = JsonConvert.DeserializeObject<TufRoot>(json);

        result.Signed.Should().NotBeNull();
        result.Signed.Version.Should().Be(99L);
        result.Signed.Custom.OpaqueBackendState.Should().Be("backend-state-123");
        result.Signed.Targets.Should().HaveCount(2);

        var target1 = result.Signed.Targets["datadog/1/ASM/config"];
        target1.Custom.V.Should().Be(42L);
        target1.Hashes.Should().ContainKey("sha256").WhoseValue.Should().Be("abcdef");
        target1.Length.Should().Be(567L);

        var target2 = result.Signed.Targets["datadog/2/APM_TRACING/config"];
        target2.Custom.V.Should().Be(0L);
        target2.Length.Should().Be(0L);

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<TufRoot>(reserialized);
        result2.Signed.Version.Should().Be(99L);
        result2.Signed.Targets.Should().HaveCount(2);
        result2.Signed.Targets["datadog/1/ASM/config"].Custom.V.Should().Be(42L);
    }

    [Fact]
    public void GetRcmRequest_FullPayload_RoundTrips()
    {
        // language=json
        var json = """
            {
                "client":{
                    "state":{"root_version":1,"targets_version":2,"config_states":[],"has_error":false,"error":"","backend_client_state":""},
                    "id":"client-1",
                    "products":["ASM"],
                    "is_tracer":true,
                    "client_tracer":{"runtime_id":"rt","language":"dotnet","tracer_version":"v","service":"svc","env":"e","tags":[]},
                    "capabilities":"AQ=="
                },
                "cached_target_files":[
                    {"path":"/p1","length":10,"hashes":[{"algorithm":"sha256","hash":"h1"}]}
                ]
            }
            """;

        var result = JsonConvert.DeserializeObject<GetRcmRequest>(json);

        result.Client.Id.Should().Be("client-1");
        result.Client.Products.Should().ContainSingle().Which.Should().Be("ASM");
        result.CachedTargetFiles.Should().ContainSingle();
        result.CachedTargetFiles[0].Path.Should().Be("/p1");
        result.CachedTargetFiles[0].Hashes[0].Algorithm.Should().Be("sha256");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<GetRcmRequest>(reserialized);
        result2.Client.Id.Should().Be("client-1");
        result2.CachedTargetFiles.Should().ContainSingle();
    }

    [Fact]
    public void GetRcmResponse_WithBase64Targets_RoundTrips()
    {
        // The targets field uses TufRootBase64Converter — value is base64-encoded JSON
        // language=json
        var tufRootJson = """{"signed":{"targets":{},"version":1,"custom":{"opaque_backend_state":"state"}}}""";
        var tufRootBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(tufRootJson));

        var rawFileContent = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        var rawBase64 = Convert.ToBase64String(rawFileContent);

        var json = $@"{{
            ""targets"":""{tufRootBase64}"",
            ""client_configs"":[""datadog/1/ASM/config"",""datadog/2/APM_TRACING/config""],
            ""target_files"":[
                {{""path"":""/target/file"",""raw"":""{rawBase64}""}}
            ]
        }}";

        var result = JsonConvert.DeserializeObject<GetRcmResponse>(json);

        result.Targets.Should().NotBeNull();
        result.Targets.Signed.Version.Should().Be(1L);
        result.Targets.Signed.Custom.OpaqueBackendState.Should().Be("state");
        result.ClientConfigs.Should().HaveCount(2);
        result.ClientConfigs[0].Should().Be("datadog/1/ASM/config");
        result.TargetFiles.Should().ContainSingle();
        result.TargetFiles[0].Path.Should().Be("/target/file");
        result.TargetFiles[0].Raw.Should().BeEquivalentTo(rawFileContent);

        // Round-trip: TufRootBase64Converter should re-encode on serialization
        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<GetRcmResponse>(reserialized);
        result2.Targets.Signed.Version.Should().Be(1L);
        result2.ClientConfigs.Should().HaveCount(2);
    }

    [Fact]
    public void GetRcmResponse_EmptyLists_RoundTrips()
    {
        // language=json
        var tufRootJson = """{"signed":{"targets":{},"version":0,"custom":{"opaque_backend_state":""}}}""";
        var tufRootBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(tufRootJson));

        var json = $@"{{""targets"":""{tufRootBase64}"",""client_configs"":[],""target_files"":[]}}";

        var result = JsonConvert.DeserializeObject<GetRcmResponse>(json);

        result.ClientConfigs.Should().BeEmpty();
        result.TargetFiles.Should().BeEmpty();
    }
}
