// <copyright file="NativeStringHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;

namespace Datadog.Trace.Util;

internal static class NativeStringHelper
{
    public static unsafe string GetString(nint pointer, nuint length)
    {
#if NETCOREAPP3_0_OR_GREATER
        var span = new ReadOnlySpan<byte>((byte*)pointer, (int)length);
        return Encoding.UTF8.GetString(span);
#else
        return Encoding.UTF8.GetString((byte*)pointer, (int)length);
#endif
    }
}
