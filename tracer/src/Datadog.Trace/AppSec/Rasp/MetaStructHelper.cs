// <copyright file="MetaStructHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Iast;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.MessagePack.Resolvers;

#nullable enable

namespace Datadog.Trace.AppSec.Rasp;

internal static class MetaStructHelper
{
    public static Dictionary<string, object> StackTraceInfoToDictionary(string? type, string language, string id, string? message, List<Dictionary<string, object>> frames)
    {
        var dict = new Dictionary<string, object>(3)
        {
            { "language", language },
            { "id", id },
            { "frames", frames }
        };

        if (type is { Length: > 0 })
        {
            dict["type"] = type;
        }

        if (message is { Length: > 0 })
        {
            dict["message"] = message;
        }

        return dict;
    }

    public static Dictionary<string, object> StackFrameToDictionary(uint id, string? text, string? file, uint? line, uint? column, string? ns, string? className, string? function)
    {
        var dict = new Dictionary<string, object>(7)
        {
            { "id", id }
        };

        if (text is { Length: > 0 })
        {
            dict["text"] = text;
        }

        if (file is { Length: > 0 })
        {
            dict["file"] = file;
        }

        if (line.HasValue)
        {
            dict["line"] = line.Value;
        }

        if (column.HasValue)
        {
            dict["column"] = column.Value;
        }

        if (ns is { Length: > 0 })
        {
            dict["namespace"] = ns;
        }

        if (className is { Length: > 0 })
        {
            dict["class_name"] = className;
        }

        if (function is { Length: > 0 })
        {
            dict["function"] = function;
        }

        return dict;
    }

    public static Dictionary<string, object> VulnerabilityBatchToDictionary(VulnerabilityBatch vulnerabilityBatch)
    {
        var result = new Dictionary<string, object>(2);

        var truncationMaxValueLength = vulnerabilityBatch.GetTruncationMaxValueLength();
        var redactionEnabled = vulnerabilityBatch.IsRedactionEnabled();

        if (vulnerabilityBatch.Vulnerabilities.Count > 0)
        {
            var vulnerabilitiesList = new List<Dictionary<string, object>>(vulnerabilityBatch.Vulnerabilities.Count);
            foreach (var vulnerability in vulnerabilityBatch.Vulnerabilities)
            {
                var vulnerabilityDictionary = new Dictionary<string, object>();

                vulnerabilityDictionary["type"] = vulnerability.Type;
                vulnerabilityDictionary["hash"] = vulnerability.Hash;

                if (vulnerability.Location.HasValue)
                {
                    var locationDict = new Dictionary<string, object>();
                    var location = vulnerability.Location.Value;

                    if (location.SpanId.HasValue)
                    {
                        locationDict["spanId"] = location.SpanId.Value;
                    }

                    if (location.Path is { Length: > 0 })
                    {
                        locationDict["path"] = location.Path;
                    }

                    if (location.Method is { Length: > 0 })
                    {
                        locationDict["method"] = location.Method;
                    }

                    if (location.Line.HasValue)
                    {
                        locationDict["line"] = location.Line.Value;
                    }

                    vulnerabilityDictionary["location"] = locationDict;
                }

                if (vulnerability.Evidence.HasValue)
                {
                    vulnerabilityDictionary["evidence"] = new EvidenceConverterDictionary(truncationMaxValueLength, redactionEnabled).EvidenceToDictionary(vulnerability.Evidence.Value);
                }

                vulnerabilitiesList.Add(vulnerabilityDictionary);
            }

            result["vulnerabilities"] = vulnerabilitiesList;
        }

        if (vulnerabilityBatch.Sources != null)
        {
            var sourcesList = new List<Dictionary<string, object>>(vulnerabilityBatch.Sources.Count);
            foreach (var source in vulnerabilityBatch.Sources)
            {
                sourcesList.Add(SourceConverterDictionary.ToDictionary(source, truncationMaxValueLength));
            }

            result["sources"] = sourcesList;
        }

        return result;
    }

    public static byte[] ObjectToByteArray(object? value)
    {
        // 256 is the size that the serializer would reserve initially for empty arrays, so we create
        // the buffer with that size to avoid this first resize. If a bigger size is required later, the serializer
        // will resize it.

        var buffer = new byte[256];
        var bytesCopied = PrimitiveObjectFormatter.Instance.Serialize(ref buffer, 0, value, null);
        Array.Resize(ref buffer, bytesCopied);

        return buffer;
    }

    public static object ByteArrayToObject(byte[] value)
    {
        var formatterResolver = StandardResolver.Instance;
        return PrimitiveObjectFormatter.Instance.Deserialize(value, 0, formatterResolver, out _);
    }
}
