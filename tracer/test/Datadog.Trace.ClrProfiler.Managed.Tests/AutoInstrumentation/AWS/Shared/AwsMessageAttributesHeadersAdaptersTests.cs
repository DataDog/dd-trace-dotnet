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

    // Node.js pads these 10-, 15-, and 19-byte pathways to 20 bytes.
    private const string TenBytePathwayPaddedToTwenty = "AAAAAAAABBAAAAAAAAAAAAAAAAA=";
    private const string FifteenBytePathwayPaddedToTwenty = "AAAAAAAABBAC0K+r/vliAAAAAAA=";
    private const string NineteenBytePathwayPaddedToTwenty = "AAAAAAAABBCAgICACNCvq/75YgA=";

    private const string InvalidBase64 = "%%%";
    private const string InsufficientPathwayBytes = "AAAA"; // Only three decoded bytes; a pathway needs at least ten.
    private const string UnterminatedEdgeTimestamp = "AAAAAAAAAAAAgA=="; // The last byte is a varint continuation byte.
    private const string NonZeroTrailingByte = "AAAAAAAAAAAAAAE="; // A ten-byte pathway followed by 0x01.
    private const string NonZeroNodeJsPadding = "AAAAAAAABBAAAAAAAAAAAAAAAAE="; // The last padding byte is 0x01.

    [Theory]
    [CombinatorialData]
    public void Extract_AcceptsSingleAndDoubleEncodedPathways(
        bool shortPathway,
        bool doubleEncoded,
        bool binaryAttribute,
        [CombinatorialValues(0, 1, 6)] int paddingBytes)
    {
        var bytes = Convert.FromBase64String(shortPathway ? ShortPathway : Pathway);
        // Exercise unpadded input, one trailing zero, and six trailing zeros. For the 20-byte
        // Pathway fixture, six reaches the 26-byte limit. This covers zero padding beyond Node.js's format.
        Array.Resize(ref bytes, bytes.Length + paddingBytes);
        var value = Convert.ToBase64String(bytes);
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

    [Fact]
    public void Extract_DoubleEncodedShortPathway_DoesNotTreatBase64TextAsBinaryPathway()
    {
        // After removing the outer Base64 layer, the bytes are the ASCII text "AAAAAAAABBAAAA==".
        // A permissive binary decoder can mistake the first eight characters for a hash and the
        // two 'B' characters for timestamps, ignoring the remaining text. Extract the inner pathway instead.
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, EncodeAgain(ShortPathway) }
            },
            binaryAttribute: false);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);

        DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: false)
                                   .Should()
                                   .Be(new PathwayContext(new PathwayHash(Hash), 0, 0));
    }

    [Theory]
    [InlineData(TenBytePathwayPaddedToTwenty, 0, 0, false)]
    [InlineData(TenBytePathwayPaddedToTwenty, 0, 0, true)]
    [InlineData(FifteenBytePathwayPaddedToTwenty, 1_000_000, 1_700_000_001_000_000_000, false)]
    [InlineData(FifteenBytePathwayPaddedToTwenty, 1_000_000, 1_700_000_001_000_000_000, true)]
    [InlineData(NineteenBytePathwayPaddedToTwenty, 1_073_741_824_000_000, 1_700_000_001_000_000_000, false)]
    [InlineData(NineteenBytePathwayPaddedToTwenty, 1_073_741_824_000_000, 1_700_000_001_000_000_000, true)]
    public void Extract_AcceptsPaddedNodeJsPathways(
        string value,
        long pathwayStartNs,
        long edgeStartNs,
        bool doubleEncoded)
    {
        var expected = new PathwayContext(new PathwayHash(Hash), pathwayStartNs, edgeStartNs);
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, doubleEncoded ? EncodeAgain(value) : value }
            },
            binaryAttribute: true);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);

        DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: false).Should().Be(expected);
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
    public void Extract_InvalidPreferredHeader_DoesNotFallBackToLegacyHeader(
        [CombinatorialValues(InvalidBase64, InsufficientPathwayBytes, UnterminatedEdgeTimestamp, NonZeroTrailingByte, NonZeroNodeJsPadding)] string value,
        bool doubleEncoded,
        bool binaryAttribute)
    {
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, doubleEncoded ? EncodeAgain(value) : value },
                { DataStreamsPropagationHeaders.PropagationKey, ShortPathway }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);
        DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: true).Should().BeNull();
    }

    [Theory]
    [CombinatorialData]
    public void Extract_RejectsMoreThanTwoEncodingLayers(bool binaryAttribute)
    {
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, EncodeAgain(EncodeAgain(ShortPathway)) },
                { DataStreamsPropagationHeaders.PropagationKey, ShortPathway }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);

        DataStreamsContextPropagator.Instance.Extract(adapter, isDataStreamsLegacyHeadersEnabled: true).Should().BeNull();
    }

    [Theory]
    [CombinatorialData]
    public void Extract_NullOrEmptyPreferredHeader_FallsBackOnlyWhenLegacyHeadersEnabled(
        [CombinatorialValues(null, "")] string value,
        bool binaryAttribute,
        bool legacyHeadersEnabled)
    {
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKeyBase64, value },
                { DataStreamsPropagationHeaders.PropagationKey, ShortPathway }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);

        var expected = legacyHeadersEnabled ? new PathwayContext(new PathwayHash(Hash), 0, 0) : (PathwayContext?)null;

        DataStreamsContextPropagator.Instance.Extract(adapter, legacyHeadersEnabled).Should().Be(expected);
    }

    [Theory]
    [CombinatorialData]
    public void Extract_LegacyHeaderOnly_HonorsLegacyHeadersSetting(bool binaryAttribute, bool legacyHeadersEnabled)
    {
        var attributes = CreateAttributes(
            new Dictionary<string, string>
            {
                { DataStreamsPropagationHeaders.PropagationKey, ShortPathway }
            },
            binaryAttribute);

        var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(attributes);
        var expected = legacyHeadersEnabled ? new PathwayContext(new PathwayHash(Hash), 0, 0) : (PathwayContext?)null;

        DataStreamsContextPropagator.Instance.Extract(adapter, legacyHeadersEnabled).Should().Be(expected);
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
                { DataStreamsPropagationHeaders.PropagationKey, InvalidBase64 }
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
