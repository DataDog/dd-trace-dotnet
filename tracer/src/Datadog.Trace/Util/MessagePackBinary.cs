// <copyright file="MessagePackBinary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;

#if NETCOREAPP
namespace Datadog.Trace.Vendors.MessagePack;

/// <summary>
/// This is an extension for the original vendored MessagePackBinary file
/// </summary>
internal partial class MessagePackBinary
{
    public static int WriteRawReadOnlySpan(ref byte[] bytes, int offset, ReadOnlySpan<byte> rawMessagePackBlock)
    {
        var bytesCount = rawMessagePackBlock.Length;
        EnsureCapacity(ref bytes, offset, bytesCount);
        rawMessagePackBlock.CopyTo(new Span<byte>(bytes, offset, bytesCount));
        return bytesCount;
    }
}
#endif
