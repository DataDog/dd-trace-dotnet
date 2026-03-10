// <copyright file="AwsMessageAttributesHeadersAdapters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
            if (_ddAttributes != null && _ddAttributes.TryGetValue(name, out var b64))
            {
                return Convert.FromBase64String(b64);
            }

            return Array.Empty<byte>();
        }

        public void Add(string name, byte[] value)
        {
            throw new NotImplementedException("this is meant to read attributes only, not write them");
        }
    }
}
