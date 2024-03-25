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
    public static byte[] StackToByteArray(string? type, string language, string id, string? message, List<StackFrame> frames)
    {
        return ObjectToByteArray(StackToDictionary(type, language, id, message, frames));
    }

    public static Dictionary<string, object> StackToDictionary(string? type, string language, string id, string? message, List<StackFrame> frames)
    {
        var dict = new Dictionary<string, object>(3);

        if (type != null)
        {
            dict["type"] = type;
        }

        dict["language"] = language;
        dict["id"] = id;

        if (message != null)
        {
            dict["message"] = message;
        }

        var frameList = new List<object>(frames.Count);

        foreach (var frame in frames)
        {
            frameList.Add(frame.ToDictionary());
        }

        dict["frames"] = frameList;

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
