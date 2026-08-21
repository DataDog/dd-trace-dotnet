// <copyright file="MockOtlpJsonIdNormalizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// The tracer's OTLP/HTTP JSON exporter (<c>OtlpTracesJsonSerializer</c>) encodes trace/span/parent-span
/// IDs as lowercase hex strings, but <c>Google.Protobuf</c>'s <c>JsonParser</c> expects the standard OTLP
/// JSON mapping, where <c>bytes</c> fields (including IDs) are base64. This rewrites those ID fields
/// in-place before parsing so both encodings decode into the same model.
/// </summary>
internal static class MockOtlpJsonIdNormalizer
{
    private static readonly string[] HexIdPropertyNames = { "traceId", "spanId", "parentSpanId" };

    public static string NormalizeHexIdsToBase64(string json)
    {
        var token = JToken.Parse(json);
        Normalize(token);
        return token.ToString(Formatting.None);
    }

    private static void Normalize(JToken token)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var property in obj.Properties().ToList())
                {
                    if (Array.IndexOf(HexIdPropertyNames, property.Name) >= 0
                        && property.Value.Type == JTokenType.String
                        && property.Value.Value<string>() is { Length: > 0 } hex)
                    {
                        var bytes = new byte[hex.Length / 2];
                        if (!HexString.TryParseBytes(hex, bytes))
                        {
                            throw new FormatException($"OTLP JSON property '{property.Name}' is not a well-formed hex string: '{hex}'.");
                        }

                        property.Value = new JValue(Convert.ToBase64String(bytes));
                    }
                    else
                    {
                        Normalize(property.Value);
                    }
                }

                break;

            case JArray array:
                foreach (var item in array)
                {
                    Normalize(item);
                }

                break;
        }
    }
}
