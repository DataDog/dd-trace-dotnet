// <copyright file="MessagePackHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator;

internal class MessagePackHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<byte> GetValueInRawMessagePackIEnumerable(string value)
    {
        var bytes = new byte[StringEncoding.UTF8.GetMaxByteCount(value.Length) + 5];
        var offset = MessagePackBinary.WriteString(ref bytes, 0, value);
        return new ArraySegment<byte>(bytes, 0, offset);
    }
}
