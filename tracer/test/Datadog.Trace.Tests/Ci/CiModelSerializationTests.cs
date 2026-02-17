// <copyright file="CiModelSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

/// <summary>
/// Baseline serialization tests for CI Visibility JSON models.
/// These capture the exact JSON format before any JSON library migration.
/// </summary>
public class CiModelSerializationTests
{
    // ===== Coverage Models (Tests) =====

    [Fact]
    public void TestCoverage_AllFieldsPopulated_RoundTrips()
    {
        var bitmapBytes = new byte[] { 0x01, 0x02, 0xFF };
        var bitmapBase64 = Convert.ToBase64String(bitmapBytes);

        var json = $@"{{
            ""test_session_id"":18446744073709551615,
            ""test_suite_id"":12345678901234,
            ""span_id"":9876543210,
            ""files"":[
                {{""filename"":""/src/MyClass.cs"",""bitmap"":""{bitmapBase64}""}}
            ]
        }}";

        var result = JsonConvert.DeserializeObject<TestCoverage>(json);

        result.SessionId.Should().Be(ulong.MaxValue);
        result.SuiteId.Should().Be(12345678901234UL);
        result.SpanId.Should().Be(9876543210UL);
        result.Files.Should().ContainSingle();
        result.Files[0].FileName.Should().Be("/src/MyClass.cs");
        result.Files[0].Bitmap.Should().BeEquivalentTo(bitmapBytes);

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<TestCoverage>(reserialized);
        result2.SessionId.Should().Be(ulong.MaxValue);
        result2.Files[0].Bitmap.Should().BeEquivalentTo(bitmapBytes);
    }

    [Fact]
    public void TestCoverage_NullFiles_RoundTrips()
    {
        // language=json
        var json = """{"test_session_id":1,"test_suite_id":2,"span_id":3}""";
        var result = JsonConvert.DeserializeObject<TestCoverage>(json);

        result.SessionId.Should().Be(1UL);
        result.Files.Should().BeNull();
    }

    // ===== Coverage Models (Global) =====

    [Fact]
    public void GlobalCoverageInfo_NestedStructure_RoundTrips()
    {
        var execBitmap = Convert.ToBase64String(new byte[] { 0xFF });
        var exedBitmap = Convert.ToBase64String(new byte[] { 0x0F });

        var json = $@"{{
            ""components"":[
                {{
                    ""name"":""MyAssembly"",
                    ""files"":[
                        {{""path"":""/src/Class1.cs"",""executableBitmap"":""{execBitmap}"",""executedBitmap"":""{exedBitmap}""}},
                        {{""path"":""/src/Class2.cs""}}
                    ]
                }}
            ]
        }}";

        var result = JsonConvert.DeserializeObject<GlobalCoverageInfo>(json);

        result.Components.Should().ContainSingle();
        result.Components[0].Name.Should().Be("MyAssembly");
        result.Components[0].Files.Should().HaveCount(2);
        result.Components[0].Files[0].Path.Should().Be("/src/Class1.cs");
        result.Components[0].Files[0].ExecutableBitmap.Should().BeEquivalentTo(new byte[] { 0xFF });
        result.Components[0].Files[0].ExecutedBitmap.Should().BeEquivalentTo(new byte[] { 0x0F });
        result.Components[0].Files[1].ExecutableBitmap.Should().BeNull();
        result.Components[0].Files[1].ExecutedBitmap.Should().BeNull();
    }

    // ===== IPC Messages =====

    [Fact]
    public void SessionCodeCoverageMessage_RoundTrips()
    {
        // language=json
        var json = """{"value":85.5}""";
        var result = JsonConvert.DeserializeObject<SessionCodeCoverageMessage>(json);

        result.Value.Should().Be(85.5);

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<SessionCodeCoverageMessage>(reserialized);
        result2.Value.Should().Be(85.5);
    }

    [Fact]
    public void SetSessionTagMessage_StringValue_RoundTrips()
    {
        // language=json
        var json = """{"name":"tag-name","value":"tag-value"}""";
        var result = JsonConvert.DeserializeObject<SetSessionTagMessage>(json);

        result.Name.Should().Be("tag-name");
        result.Value.Should().Be("tag-value");
        result.NumberValue.Should().BeNull();

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<SetSessionTagMessage>(reserialized);
        result2.Name.Should().Be("tag-name");
        result2.Value.Should().Be("tag-value");
    }

    [Fact]
    public void SetSessionTagMessage_NumberValue_RoundTrips()
    {
        // language=json
        var json = """{"name":"duration","nvalue":42.5}""";
        var result = JsonConvert.DeserializeObject<SetSessionTagMessage>(json);

        result.Name.Should().Be("duration");
        result.Value.Should().BeNull();
        result.NumberValue.Should().Be(42.5);
    }

    // ===== CI Models =====

    [Fact]
    public void SkippableTest_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "name":"TestMethod1",
                "suite":"MyTests.UnitTests",
                "parameters":"{  \"metadata\": {},  \"arguments\": { \"arg1\": \"val1\" }}",
                "configurations":{"os.platform":"Windows","os.version":"10.0","os.architecture":"x64","runtime.name":".NET","runtime.version":"8.0.0","runtime.architecture":"x64","custom":{"browser":"chrome"}}
            }
            """;

        var result = JsonConvert.DeserializeObject<SkippableTest>(json);

        result.Name.Should().Be("TestMethod1");
        result.Suite.Should().Be("MyTests.UnitTests");
        result.RawParameters.Should().NotBeNull();
        result.Configurations.Should().NotBeNull();
        result.Configurations.Value.OSPlatform.Should().Be("Windows");
        result.Configurations.Value.OSVersion.Should().Be("10.0");
        result.Configurations.Value.OSArchitecture.Should().Be("x64");
        result.Configurations.Value.RuntimeName.Should().Be(".NET");
        result.Configurations.Value.RuntimeVersion.Should().Be("8.0.0");
        result.Configurations.Value.RuntimeArchitecture.Should().Be("x64");
        result.Configurations.Value.Custom.Should().ContainKey("browser").WhoseValue.Should().Be("chrome");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<SkippableTest>(reserialized);
        result2.Name.Should().Be("TestMethod1");
        result2.Configurations.Value.OSPlatform.Should().Be("Windows");
    }

    [Fact]
    public void SkippableTest_MinimalFields_RoundTrips()
    {
        // language=json
        var json = """{"name":"Test1","suite":"Suite1"}""";
        var result = JsonConvert.DeserializeObject<SkippableTest>(json);

        result.Name.Should().Be("Test1");
        result.Suite.Should().Be("Suite1");
        result.RawParameters.Should().BeNull();
        result.Configurations.Should().BeNull();
    }

    [Fact]
    public void TestParameters_WithArguments_RoundTrips()
    {
        // language=json
        var json = """{"metadata":{"test.source":"unit"},"arguments":{"input":"hello","count":42}}""";
        var result = JsonConvert.DeserializeObject<TestParameters>(json);

        result.Metadata.Should().ContainKey("test.source");
        result.Arguments.Should().ContainKey("input");
        result.Arguments.Should().ContainKey("count");

        // Verify ToJSON() produces valid JSON that round-trips
        var toJson = result.ToJSON();
        var result2 = JsonConvert.DeserializeObject<TestParameters>(toJson);
        result2.Metadata.Should().ContainKey("test.source");
    }

    [Fact]
    public void TestParameters_NullFields_RoundTrips()
    {
        // language=json
        var json = """{}""";
        var result = JsonConvert.DeserializeObject<TestParameters>(json);

        result.Metadata.Should().BeNull();
        result.Arguments.Should().BeNull();
    }

    [Fact]
    public void TestsConfigurations_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "os.platform":"Linux",
                "os.version":"5.15",
                "os.architecture":"arm64",
                "runtime.name":".NET",
                "runtime.version":"8.0.0",
                "runtime.architecture":"arm64",
                "custom":{"env":"ci","parallel":"true"}
            }
            """;

        var result = JsonConvert.DeserializeObject<TestsConfigurations>(json);

        result.OSPlatform.Should().Be("Linux");
        result.OSVersion.Should().Be("5.15");
        result.OSArchitecture.Should().Be("arm64");
        result.RuntimeName.Should().Be(".NET");
        result.RuntimeVersion.Should().Be("8.0.0");
        result.RuntimeArchitecture.Should().Be("arm64");
        result.Custom.Should().HaveCount(2);
        result.Custom.Should().ContainKey("env").WhoseValue.Should().Be("ci");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<TestsConfigurations>(reserialized);
        result2.OSPlatform.Should().Be("Linux");
        result2.Custom.Should().HaveCount(2);
    }
}
