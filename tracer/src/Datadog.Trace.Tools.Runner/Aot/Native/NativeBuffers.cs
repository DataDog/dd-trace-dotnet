// <copyright file="NativeBuffers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Helpers that write results into native buffers the way the CLR metadata APIs do.
/// </summary>
internal static unsafe class NativeBuffers
{
    public const int RecordNotFound = unchecked((int)0x80131130);
    public const int Truncation = 0x00131106;

    /// <summary>
    /// Copies a string into a WCHAR buffer and reports the full length including the terminator, like the CLR.
    /// </summary>
    public static HResult WriteString(string? value, char* buffer, uint capacity, uint* length)
    {
        value ??= string.Empty;
        if (length != null)
        {
            *length = (uint)value.Length + 1;
        }

        if (buffer == null || capacity == 0)
        {
            return HResult.S_OK;
        }

        var count = Math.Min(value.Length, (int)capacity - 1);
        for (var i = 0; i < count; i++)
        {
            buffer[i] = value[i];
        }

        buffer[count] = '\0';
        return count < value.Length ? Truncation : HResult.S_OK;
    }

    public static string? ReadString(char* value) => value == null ? null : new string(value);

    public static string ReadString(char* value, int length) => value == null ? string.Empty : new string(value, 0, length);

    public static byte[] ReadBytes(IntPtr pointer, int length)
    {
        if (pointer == IntPtr.Zero || length <= 0)
        {
            return Array.Empty<byte>();
        }

        var result = new byte[length];
        Marshal.Copy(pointer, result, 0, length);
        return result;
    }

    public static void Set<T>(T* pointer, T value)
        where T : unmanaged
    {
        if (pointer != null)
        {
            *pointer = value;
        }
    }
}
#endif
