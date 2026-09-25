// <copyright file="AwsMessageAttributesHeadersAdaptersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Amazon.SQS.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.Shared;

public class AwsMessageAttributesHeadersAdaptersTests
{
    // Fixed wire fixtures, independent of the .NET encoder: little-endian hash followed by
    // two zig-zag encoded millisecond timestamps (0/0 or 1700000000000/1700000001000).
    private const ulong Hash = 0x1004000000000000;
    private const string ShortPathway = "AAAAAAAABBAAAA==";
    private const string Pathway = "AAAAAAAABBCAoKv++WLQr6v++WI=";

    // Node.js pads contexts to 20 bytes, including when the timestamps need fewer bytes.
    private const string PaddedShortPathway = "AAAAAAAABBAAAAAAAAAAAAAAAAA=";
    private const string PaddedMixedPathway = "AAAAAAAABBAC0K+r/vliAAAAAAA=";
    private const string PaddedLongPathway = "AAAAAAAABBCAgICACNCvq/75YgA=";

    [Theory]
    [CombinatorialData]
    public void Extract_AcceptsSingleAndDoubleEncodedPathways(bool shortPathway, bool doubleEncoded, bool binaryAttribute)
    {
        var value = shortPathway ? ShortPathway : Pathway;
        if (doubleEncoded)
        {
            value = EncodeAgain(value);
        }

        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, value }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);
        var extracted = DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: false);

        var expected = shortPathway
                           ? new PathwayContext(new PathwayHash(Hash), 0, 0)
                           : new PathwayContext(new PathwayHash(Hash), 1_700_000_000_000_000_000, 1_700_000_001_000_000_000);
        extracted.Should().Be(expected);
    }

    [Theory]
    [CombinatorialData]
    public void Extract_AcceptsPaddedNodeJsPathways(
        [CombinatorialValues(PaddedShortPathway, PaddedMixedPathway, PaddedLongPathway)] string value,
        bool doubleEncoded,
        bool binaryAttribute,
        bool legacyHeadersEnabled)
    {
        var pathwayStartNs = value switch
        {
            PaddedShortPathway => 0,
            PaddedMixedPathway => 1_000_000,
            _ => 1_073_741_824_000_000,
        };
        var edgeStartNs = value == PaddedShortPathway ? 0 : 1_700_000_001_000_000_000;
        var expected = new PathwayContext(new PathwayHash(Hash), pathwayStartNs, edgeStartNs);
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, doubleEncoded ? EncodeAgain(value) : value },
                { DataStreamsPropagationHeaders.PropagationKey, Pathway }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);

        DataStreamsContextPropagator.Instance.Extract(adapter, legacyHeadersEnabled).Should().Be(expected);
    }

    [Theory]
    [CombinatorialData]
    public void Extract_PrefersValidBase64Header(bool doubleEncoded, bool binaryAttribute)
    {
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, doubleEncoded ? EncodeAgain(Pathway) : Pathway },
                { DataStreamsPropagationHeaders.PropagationKey, ShortPathway }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);

        DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: true)
                                   .Should()
                                   .Be(new PathwayContext(new PathwayHash(Hash), 1_700_000_000_000_000_000, 1_700_000_001_000_000_000));
    }

    [Theory]
    [CombinatorialData]
    public void Extract_InvalidPreferredHeader_FallsBackOnlyWhenLegacyHeadersEnabled(
        [CombinatorialValues("%%%", "AAAA", "AAAAAAAAAAAAAAA=", "AAAAAAAABBAAAAAAAAAAAAAAAAE=")] string value,
        bool doubleEncoded,
        bool binaryAttribute,
        bool legacyHeadersEnabled)
    {
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, doubleEncoded ? EncodeAgain(value) : value },
                { DataStreamsPropagationHeaders.PropagationKey, ShortPathway }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);
        var extracted = DataStreamsContextPropagator.Instance.Extract(adapter, legacyHeadersEnabled);

        if (legacyHeadersEnabled)
        {
            extracted.Should().Be(new PathwayContext(new PathwayHash(Hash), 0, 0));
        }
        else
        {
            extracted.Should().BeNull();
        }
    }

    [Theory]
    [CombinatorialData]
    public void Extract_RejectsMoreThanTwoEncodingLayers(bool binaryAttribute)
    {
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, EncodeAgain(EncodeAgain(ShortPathway)) }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);

        DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: false).Should().BeNull();
    }

    [Theory]
    [CombinatorialData]
    public void Extract_NullOrEmptyPreferredHeader_FallsBack(
        [CombinatorialValues(null, "")] string value,
        bool binaryAttribute)
    {
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, value },
                { DataStreamsPropagationHeaders.PropagationKey, ShortPathway }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);

        DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: true)
                                   .Should()
                                   .Be(new PathwayContext(new PathwayHash(Hash), 0, 0));
    }

    [Fact]
    public void Extract_MissingMessageAttributes_ReturnsNull()
    {
        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(null);

        DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: true).Should().BeNull();
    }

    [Theory]
    [CombinatorialData]
    public void Extract_InvalidLegacyHeader_ReturnsNull(bool binaryAttribute)
    {
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKey, "%%%" }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);

        DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: true).Should().BeNull();
    }

    [Theory]
    [CombinatorialData]
    public void Inject_PreservesExistingDotnetWireFormat(bool legacyHeadersEnabled)
    {
        var carrier = new StringBuilder("{");
        var adapter = AwsMessageAttributesHeadersAdapters.GetInjectionAdapter(carrier);
        var context = new PathwayContext(new PathwayHash(Hash), 1_700_000_000_000_000_000, 1_700_000_001_000_000_000);

        DataStreamsContextPropagator.Instance.Inject(context, adapter, legacyHeadersEnabled);
        carrier.Length--; // Remove the trailing comma written by the injection adapter.
        carrier.Append('}');
        var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(carrier.ToString());

        // Existing .NET consumers expect two Base64 layers for this header.
        headers[DataStreamsPropagationHeaders.PropagationKeyBase64].Should().Be(EncodeAgain(Pathway));
        if (legacyHeadersEnabled)
        {
            headers[DataStreamsPropagationHeaders.PropagationKey].Should().Be(Pathway);
        }
        else
        {
            headers.Should().NotContainKey(DataStreamsPropagationHeaders.PropagationKey);
        }
    }

    private static string EncodeAgain(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static Dictionary<string, MessageAttributeValue> CreateAttributes(Dictionary<string, string> headers, bool binaryAttribute)
    {
        var json = JsonConvert.SerializeObject(headers);
        var attribute = binaryAttribute
                            ? new MessageAttributeValue { DataType = "Binary", BinaryValue = new MemoryStream(Encoding.UTF8.GetBytes(json)) }
                            : new MessageAttributeValue { DataType = "String", StringValue = json };
        return new Dictionary<string, MessageAttributeValue> { { "_datadog", attribute } };
    }
}
