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
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;

internal static class AwsMessageAttributesHeadersAdapters
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsMessageAttributesHeadersAdapters));

    public static IBinaryHeadersCollection GetInjectionAdapter(StringBuilder carrier)
    {
        Console.WriteLine("AwsMessageAttributesHeadersAdapters.GetInjectionAdapter: Creating injection adapter for StringBuilder");
        return new StringBuilderJsonAdapter(carrier);
    }

    public static IBinaryHeadersCollection GetExtractionAdapter(IDictionary? messageAttributes)
    {
        Console.WriteLine("AwsMessageAttributesHeadersAdapters.GetExtractionAdapter: Creating extraction adapter. MessageAttributes count: {0}", messageAttributes?.Count ?? 0);
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
            Console.WriteLine("StringBuilderJsonAdapter.Add: Adding key '{0}' with {1} bytes", key, value.Length);
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
            Console.WriteLine("MessageAttributesAdapter.Constructor: Starting extraction from message attributes");
            Console.WriteLine("MessageAttributesAdapter.Constructor: messageAttributes is null: {0}", messageAttributes == null);

            if (messageAttributes != null)
            {
                Console.WriteLine("MessageAttributesAdapter.Constructor: messageAttributes count: {0}", messageAttributes.Count);
                Console.WriteLine("MessageAttributesAdapter.Constructor: messageAttributes type: {0}", messageAttributes.GetType().FullName);

                // Log all keys in the dictionary
                foreach (var key in messageAttributes.Keys)
                {
                    Console.WriteLine("MessageAttributesAdapter.Constructor: Key found: '{0}' (type: {1})", key, key?.GetType().FullName);
                }
            }

            // IDictionary returns null if the key is not present
            var datadogAttribute = messageAttributes?[ContextPropagation.InjectionKey];
            Console.WriteLine("MessageAttributesAdapter.Constructor: Found _datadog attribute: {0}", datadogAttribute != null);
            Console.WriteLine("MessageAttributesAdapter.Constructor: datadogAttribute type: {0}", datadogAttribute?.GetType().FullName);
            Console.WriteLine("MessageAttributesAdapter.Constructor: datadogAttribute value: {0}", datadogAttribute);

            var json = datadogAttribute?.DuckCast<IMessageAttributeValue>();
            Console.WriteLine("MessageAttributesAdapter.Constructor: json: {0}, StringValue: {1}", json, json?.StringValue);
            Console.WriteLine("MessageAttributesAdapter.Constructor: Cast to IMessageAttributeValue: {0}", (json != null));
            Console.WriteLine("MessageAttributesAdapter.Constructor: json type: {0}", (json?.GetType().FullName));

            if (json != null)
            {
                Console.WriteLine("MessageAttributesAdapter.Constructor: json.StringValue is null: {0}", json.StringValue == null);
                Console.WriteLine("MessageAttributesAdapter.Constructor: json.StringValue type: {0}", json.StringValue?.GetType().FullName);
                Console.WriteLine("MessageAttributesAdapter.Constructor: json.StringValue length: {0}", json.StringValue?.Length ?? 0);

                // Log specific IMessageAttributeValue fields
                Console.WriteLine("MessageAttributesAdapter.Constructor: === IMessageAttributeValue Field Details ===");
                Console.WriteLine("MessageAttributesAdapter.Constructor: DataType: '{0}'", json.DataType);
                Console.WriteLine("MessageAttributesAdapter.Constructor: StringValue: '{0}'", json.StringValue);
                Console.WriteLine("MessageAttributesAdapter.Constructor: BinaryValue: '{0}'", json.BinaryValue);

                // Log all properties of the IMessageAttributeValue
                try
                {
                    var properties = json.GetType().GetProperties();
                    Console.WriteLine("MessageAttributesAdapter.Constructor: === All Properties via Reflection ===");
                    foreach (var prop in properties)
                    {
                        try
                        {
                            var value = prop.GetValue(json);
                            Console.WriteLine(
                                "MessageAttributesAdapter.Constructor: Property '{0}' = '{1}' (type: {2})",
                                prop.Name,
                                value ?? "null",
                                value?.GetType().FullName ?? "null");
                        }
                        catch (Exception propEx)
                        {
                            Console.WriteLine("MessageAttributesAdapter.Constructor: Failed to get property '{0}': {1}", prop.Name, propEx.Message);
                        }
                    }
                }
                catch (Exception propsEx)
                {
                    Console.WriteLine("MessageAttributesAdapter.Constructor: Failed to enumerate properties: {0}", propsEx.Message);
                }
            }

            if (json != null && json.StringValue != null)
            {
                Console.WriteLine("MessageAttributesAdapter.Constructor: StringValue length: {0}", json.StringValue.Length);
                Console.WriteLine("MessageAttributesAdapter.Constructor: StringValue content: '{0}'", json.StringValue);
                try
                {
                    _ddAttributes = JsonConvert.DeserializeObject<Dictionary<string, string>>(json.StringValue);
                    Console.WriteLine("MessageAttributesAdapter.Constructor: Deserialized {0} attributes", _ddAttributes?.Count);

                    if (_ddAttributes != null)
                    {
                        foreach (var kvp in _ddAttributes)
                        {
                            Console.WriteLine("MessageAttributesAdapter.Constructor: Attribute '{0}' = '{1}'", kvp.Key, kvp.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "MessageAttributesAdapter.Constructor: Failed to deserialize JSON string: {0}", json.StringValue);
                    Console.WriteLine("MessageAttributesAdapter.Constructor: Deserialization exception: {0}", ex.Message);
                    Console.WriteLine("MessageAttributesAdapter.Constructor: Exception type: {0}", ex.GetType().FullName);
                    Console.WriteLine("MessageAttributesAdapter.Constructor: Stack trace: {0}", ex.StackTrace);
                    _ddAttributes = null;
                }
            }
            else
            {
                Console.WriteLine("MessageAttributesAdapter.Constructor: No StringValue found in message attribute");
                _ddAttributes = null;
            }
        }

        public byte[] TryGetLastBytes(string name)
        {
            Console.WriteLine("MessageAttributesAdapter.TryGetLastBytes: Looking for key '{0}'", name);

            if (_ddAttributes != null && _ddAttributes.TryGetValue(name, out var b64))
            {
                Console.WriteLine("MessageAttributesAdapter.TryGetLastBytes: Found key '{0}' with base64 value length: {1}", name, b64.Length);
                try
                {
                    var bytes = Convert.FromBase64String(b64);
                    Console.WriteLine("MessageAttributesAdapter.TryGetLastBytes: Successfully decoded {0} bytes for key '{1}'", bytes.Length, name);
                    return bytes;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "MessageAttributesAdapter.TryGetLastBytes: Failed to decode base64 string for key '{0}'", name);
                    return Array.Empty<byte>();
                }
            }

            Console.WriteLine("MessageAttributesAdapter.TryGetLastBytes: Key '{0}' not found or _ddAttributes is null", name);
            return Array.Empty<byte>();
        }

        public void Add(string name, byte[] value)
        {
            throw new NotImplementedException("this is meant to read attributes only, not write them");
        }
    }
}
