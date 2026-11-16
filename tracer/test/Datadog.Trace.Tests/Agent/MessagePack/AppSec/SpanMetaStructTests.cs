// <copyright file="SpanMetaStructTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.OpenTelemetry.Common;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using FluentAssertions;
using Moq;
using Xunit;

#nullable enable

namespace Datadog.Trace.Tests.Agent.MessagePack;

[Collection(nameof(TracerInstanceTestCollection))]
public class SpanMetaStructTests
{
    private const string MetaStructStr = "meta_struct";
    private const string DdStackKey = "_dd.stack.exploit";
    private const string AppsecKey = "appsec";
    private const string StringKey = "StringKey";
    private const string StringValue = "value";
    private static readonly IFormatterResolver FormatterResolver = SpanFormatterResolver.Instance;
    private static Dictionary<string, object> stackFrame1 = MetaStructHelper.StackFrameToDictionary(1, "text", "file.cs", 33, null, "testnamespace", "class1", "method1");
    private static Dictionary<string, object> stackFrame2 = MetaStructHelper.StackFrameToDictionary(2, "text2", "file2.cs", 55, null, "testnamespace", "class2", "method2");
    private static Dictionary<string, object> stackFrame3 = MetaStructHelper.StackFrameToDictionary(3, "text3", "file3.cs", 77, null, "testnamespace", "class3", "method3");
    private static Dictionary<string, object> stackFrame4 = MetaStructHelper.StackFrameToDictionary(4, "text4", "file4.cs", 77, null, "testnamespace", "class4", "method4");

    private static List<object> stackData =
                new List<object>()
                {
                    MetaStructHelper.StackTraceInfoToDictionary("typevalue", "dotnet", "213123213123213", null, new List<Dictionary<string, object>>() { stackFrame1, stackFrame2 }),
                    MetaStructHelper.StackTraceInfoToDictionary("type2222", "dotnet", "test55555", null, new List<Dictionary<string, object>>() { stackFrame3, stackFrame4 }),
                };

    // appsec event: {"triggers":[{"rule":{"id":"crs-913-120","name":"Known security scanner filename/argument","tags":{"category":"attack_attempt","type":"security_scanner"}},"rule_matches":[{"operator":"phrase_match","operator_value":"","parameters":[{"address":"server.request.path_params","highlight":["appscan_fingerprint"],"key_path":["id"],"value":"appscan_fingerprint"}]}]}]},
    private static List<object> appsecData =
                new()
                {
                    new Dictionary<string, object>()
                    {
                        {
                            "rule", new Dictionary<string, object>
                            {
                                { "id", "crs-913-120" },
                                { "name", "Known security scanner filename/argument" },
                                {
                                    "tags", new Dictionary<string, object>
                                    {
                                        { "category", "attack_attempt" },
                                        { "type", "security_scanner" }
                                    }
                                }
                            }
                        },
                        {
                            "rule_matches", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "operator", "phrase_match" },
                                    { "operator_value", string.Empty },
                                    {
                                        "parameters", new List<object>
                                        {
                                            new Dictionary<string, object>
                                            {
                                                { "address", "server.request.path_params" },
                                                { "highlight", new List<string> { "appscan_fingerprint" } },
                                                { "key_path", new List<string>() { "id" } },
                                                { "value", "appscan_fingerprint" }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

    public static TheoryData<List<Tuple<string, object?>>> Data
    => new()
       {
            new()
            {
                   new(StringKey, StringValue),
                   new(DdStackKey, stackData),
                   new(AppsecKey, appsecData),
            },
            new()
            {
                   new(StringKey, 4545),
                   new("thirdKey", new List<object> { "test", 44, new List<string> { "test" } }),
                   new(DdStackKey, true),
            },
            new()
            {
                   new(DdStackKey, null)
            }
       };

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public static async Task OtlpExportsMetaStructAndSecurityMetadata(bool enabled, bool wafExecuted, bool localRoot)
    {
        var previousTracer = Tracer.Instance;
        var previousSecurity = Security.Instance;
        await using var tracer = TracerHelper.CreateWithFakeAgent(TracerSettings.Create(new()));
        var settings = new SecuritySettings(new DictionaryConfigurationSource(new Dictionary<string, string> { [ConfigurationKeys.AppSec.Enabled] = "0" }), NullConfigurationTelemetry.Instance);
        var state = new ConfigurationState(settings, NullConfigurationTelemetry.Instance, wafIsNull: true);
        using var security = new Security(settings, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>(), configurationState: state);
        try
        {
            Tracer.UnsafeSetTracerInstance(tracer);
            Security.Instance = security;
            state.AppsecEnabled = enabled;
            using var root = (Scope)tracer.StartActive("web");
            var traceContext = root.Span.Context.TraceContext.Should().BeOfType<TraceContext>().Which;
            traceContext.WafExecuted = wafExecuted;
            var span = localRoot ? root.Span : new Span(new SpanContext(root.Span.Context, traceContext, "child"), DateTimeOffset.UtcNow);
            var appsecReport = MetaStructHelper.ObjectToByteArray(new Dictionary<string, List<object>> { { "triggers", appsecData } });
            var iastReport = MetaStructHelper.ObjectToByteArray(new Dictionary<string, object> { ["vulnerabilities"] = new List<object>() });
            span.SetMetaStruct(AppsecKey, appsecReport);
            span.SetMetaStruct("iast", iastReport);
            var attributes = new List<KeyValue>();
            var chunk = new TraceChunkModel(new SpanCollection([span]));
            OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), chunk.GetSpanModel(0), limit: 128);

            attributes.Should().ContainSingle(kv => kv.Key == AppsecKey).Which.Value.Should().BeEquivalentTo(appsecReport);
            attributes.Should().ContainSingle(kv => kv.Key == "iast").Which.Value.Should().BeEquivalentTo(iastReport);
            attributes.Should().NotContain(kv => kv.Key == Tags.AppSecJson || kv.Key == Tags.IastJson);
            if (enabled && wafExecuted && localRoot)
            {
                attributes.Should().ContainSingle(kv => kv.Key == Metrics.AppSecEnabled).Which.Value.Should().Be(1d);
                attributes.Should().ContainSingle(kv => kv.Key == Tags.RuntimeFamily).Which.Value.Should().Be("dotnet");
            }
            else
            {
                attributes.Should().NotContain(kv => kv.Key == Metrics.AppSecEnabled || kv.Key == Tags.RuntimeFamily);
            }

            // The WAF is not initialized here, so there is no ruleset version to report.
            security.WafRuleFileVersion.Should().BeNull();
            attributes.Should().NotContain(kv => kv.Key == Tags.AppSecRuleFileVersion);
        }
        finally
        {
            Tracer.UnsafeSetTracerInstance(previousTracer);
            Security.Instance = previousSecurity;
        }
    }

    [MemberData(nameof(Data), DisableDiscoveryEnumeration = true)]
    [Theory]
    public static void GivenAEncodedSpanWithMetaStruct_WhenDecoding_ThenMetaStructIsCorrectlyDecoded(List<Tuple<string, object?>> dataToEncode)
    {
        var span = new Span(new SpanContext(5, 6, samplingPriority: null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };
        List<string> visitedKeys = new();

        // We add the elements to the meta struct
        foreach (var item in dataToEncode)
        {
            span.SetMetaStruct(item.Item1, MetaStructHelper.ObjectToByteArray(item.Item2));
        }

        var spanBytes = new byte[] { };
        var spanBuffer = new SpanBuffer(10000, new SpanBufferMessagePackSerializer(FormatterResolver));
        // We serialize the span
        var serializationResult = spanBuffer.TryWrite(new([span]), ref spanBytes);
        serializationResult.Should().Be(SpanBuffer.WriteStatus.Success);

        // Find the offset of the header "meta_struct" in the byte array
        int offset = FindMetaStructSection(spanBytes);
        offset.Should().BeGreaterThan(0);
        offset += MetaStructStr.Length;

        // Read the map header
        var headerLength = MessagePackBinary.ReadMapHeader(spanBytes, offset, out var bytesRead);
        offset += bytesRead;

        // We check every item in the map
        for (var i = 0; i < headerLength; i++)
        {
            offset = CheckDictionaryItem(spanBytes, offset, dataToEncode, visitedKeys);
        }

        visitedKeys.Count.Should().Be(dataToEncode.Count);
    }

    private static int CheckDictionaryItem(byte[] bytes, int offset, List<Tuple<string, object?>> originalData, List<string> visitedKeys)
    {
        // Read the key
        string result = MessagePackBinary.ReadString(bytes, offset, out var size);
        var originalKey = originalData.Find(x => x.Item1 == result);
        originalKey.Should().NotBeNull();
        visitedKeys.Should().NotContain(result);
        visitedKeys.Add(result);
        var expectedValue = originalKey!.Item2;
        offset += size;

        // Read the value of this key
        var newArray = MessagePackBinary.ReadBytes(bytes, offset, out size);
        var newResult = PrimitiveObjectFormatter.Instance.Deserialize(newArray, 0, FormatterResolver, out var readSize);

        // We should read the whole array
        readSize.Should().Be(newArray.Length);
        newResult.Should().BeEquivalentTo(expectedValue);
        offset += size;
        return offset;
    }

    private static int FindMetaStructSection(byte[] bytes)
    {
        var offset = -1;
        int maxIndex = bytes.Length - MetaStructStr.Length;

        for (var i = 0; i <= maxIndex; i++)
        {
            bool matchFound = true;
            for (int j = 0; j < MetaStructStr.Length; j++)
            {
                if (bytes[i + j] != MetaStructStr[j])
                {
                    matchFound = false;
                    break;
                }
            }

            if (matchFound)
            {
                offset = i;
                break;
            }
        }

        return offset;
    }
}
