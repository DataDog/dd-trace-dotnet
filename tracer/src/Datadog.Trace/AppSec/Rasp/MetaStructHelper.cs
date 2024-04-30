// <copyright file="MetaStructHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.MessagePack.Formatters;

#nullable enable

namespace Datadog.Trace.AppSec.Rasp;

internal static class MetaStructHelper
{
    public static Dictionary<string, object> StackTraceInfoToDictionary(string? type, string language, string id, string? message, List<Dictionary<string, object>> frames)
    {
        var dict = new Dictionary<string, object>(3);

        if (type is not null)
        {
            dict["type"] = type;
        }

        dict["language"] = language;
        dict["id"] = id;

        if (message is not null)
        {
            dict["message"] = message;
        }

        dict["frames"] = frames;

        return dict;
    }

    public static Dictionary<string, object> StackFrameToDictionary(uint id, string? text, string? file, uint? line, uint? column, string? ns, string? className, string? function)
    {
        var dict = new Dictionary<string, object>(7)
        {
            { "id", id }
        };

        if (text is not null)
        {
            dict["text"] = text;
        }

        if (file is not null)
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

        if (ns is not null)
        {
            dict["namespace"] = ns;
        }

        if (className is not null)
        {
            dict["class_name"] = className;
        }

        if (function is not null)
        {
            dict["function"] = function;
        }

        return dict;
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
}
