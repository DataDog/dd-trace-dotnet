// <copyright file="AwsMessageAttributesHeadersAdapters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;

internal static class AwsMessageAttributesHeadersAdapters
{
    public static IBinaryHeadersCollection GetInjectionAdapter(StringBuilder carrier)
    {
        return new StringBuilderJsonAdapter(carrier);
    }

    public static IBinaryHeadersCollection GetExtractionAdapter(IDictionary? messageAttributes)
    {
        return new MessageAttributesAdapter(messageAttributes);
    }

    /// <summary>
    /// The adapter to use to append stuff to a string builder where a json is being built
    /// </summary>
    private readonly struct StringBuilderJsonAdapter : IBinaryHeadersCollection
    {
        private readonly StringBuilder _carrier;

        public StringBuilderJsonAdapter(StringBuilder carrier)
        {
            _carrier = carrier;
        }

        public byte[] TryGetLastBytes(string name)
        {
            throw new NotImplementedException("this adapter can only be use to write to a StringBuilder, not to read data");
        }

        public void Add(string key, byte[] value)
        {
            // FIXME BREAKING CHANGE:
            // We are preserving the existing bug with the extra Base64 layer on the Base64 pathway header
            // We will address this as a follow up
            _carrier
               .Append(value: '"')
               .Append(key)
               .Append("\":\"")
               .Append(Convert.ToBase64String(value))
               .Append("\",");
        }
    }

    /// <summary>
    /// The adapter to use to read attributes packed in a json string under the _datadog key
    /// </summary>
    private readonly struct MessageAttributesAdapter : IBinaryHeadersCollection
    {
        private readonly Dictionary<string, string>? _ddAttributes;

        public MessageAttributesAdapter(IDictionary? messageAttributes)
        {
            // IDictionary returns null if the key is not present
            var json = messageAttributes?[ContextPropagation.InjectionKey]?.DuckCast<IMessageAttributeValue>();
            if (json != null)
            {
                string? jsonString = null;
                if (json.StringValue != null)
                {
                    jsonString = json.StringValue;
                }
                else if (json.BinaryValue != null)
                {
                    // SNS encodes the json string in base64
                    if (json.BinaryValue.TryGetBuffer(out var bytes))
                    {
                        jsonString = StringEncoding.UTF8.GetString(bytes.Array!, bytes.Offset, bytes.Count);
                    }
                    else
                    {
                        using var reader = new System.IO.StreamReader(
                            json.BinaryValue,
                            Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: false,
                            bufferSize: 1024,
                            leaveOpen: true);
                        jsonString = reader.ReadToEnd();
                    }
                }

                if (jsonString != null)
                {
                    _ddAttributes = JsonHelper.DeserializeObject<Dictionary<string, string>>(jsonString);
                }
            }
        }

        public byte[] TryGetLastBytes(string name)
        {
            if (_ddAttributes != null && _ddAttributes.TryGetValue(name, out var b64) && !StringUtil.IsNullOrEmpty(b64))
            {
                var decodedBytes = Convert.FromBase64String(b64);
                if (name != DataStreamsPropagationHeaders.PropagationKeyBase64)
                {
                    // The legacy binary header has one Base64 layer for transport in JSON.
                    return decodedBytes;
                }

                if (IsCompletePathwayEncoding(decodedBytes))
                {
                    // Other tracers encode the pathway once. The binary propagator expects the Base64 text,
                    // so preserve that layer when the decoded value is a complete binary pathway.
                    return Encoding.UTF8.GetBytes(b64);
                }

                // Existing .NET producers encode the Base64 header again when writing this JSON.
                // Validate the inner encoding before passing it to the shared propagator: Base64 text
                // can itself resemble a binary pathway if the decoder ignores trailing bytes.
#if NETCOREAPP3_1_OR_GREATER
                Span<byte> pathwayBytes = stackalloc byte[PathwayContextEncoder.MaxEncodedSize];
#else
                Span<byte> pathwayBytes = new byte[PathwayContextEncoder.MaxEncodedSize];
#endif
                var status = Base64.DecodeFromUtf8(decodedBytes, pathwayBytes, out _, out var bytesWritten);
                if (status == OperationStatus.Done && IsCompletePathwayEncoding(pathwayBytes.Slice(0, bytesWritten)))
                {
                    return decodedBytes;
                }

                throw new FormatException("Invalid AWS Data Streams pathway context encoding.");
            }

            return Array.Empty<byte>();
        }

        public void Add(string name, byte[] value)
        {
            throw new NotImplementedException("this is meant to read attributes only, not write them");
        }

        private static bool IsCompletePathwayEncoding(Span<byte> bytes)
        {
            // Check the binary layout to distinguish a decoded pathway from another Base64 layer:
            // an 8-byte hash followed by two ZigZag varint timestamps (1-9 bytes each, 10-26 bytes total).
            // Read both timestamps to validate their encoding and locate the trailing padding checked below.
            if (bytes.Length is < 10 or > PathwayContextEncoder.MaxEncodedSize
             || VarEncodingHelper.ReadVarLongZigZag(bytes.Slice(8), out var pathwayBytes) is null
             || VarEncodingHelper.ReadVarLongZigZag(bytes.Slice(8 + pathwayBytes), out var edgeBytes) is null)
            {
                return false;
            }

            // Node.js pads shorter pathways to 20 bytes. Allow zero padding when identifying the
            // AWS encoding layer, without changing the shared pathway decoder's trailing-byte handling.
            for (var i = 8 + pathwayBytes + edgeBytes; i < bytes.Length; i++)
            {
                if (bytes[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
