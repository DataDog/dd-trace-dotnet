// <copyright file="NativeStringHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;

namespace Datadog.Trace.LibDatadog;

internal class NativeStringHelpers
{
    public static string ReadUtf8NativeString(VecU8 nativeMessage)
    {
        string message;
        unsafe
        {
#if NETCOREAPP
            var messageBytes = new ReadOnlySpan<byte>((void*)nativeMessage.Ptr, (int)nativeMessage.Length);
            message = Encoding.UTF8.GetString(messageBytes);
#else
            message = Encoding.UTF8.GetString((byte*)nativeMessage.Ptr, (int)nativeMessage.Length);
#endif
        }

        return message;
    }
}
