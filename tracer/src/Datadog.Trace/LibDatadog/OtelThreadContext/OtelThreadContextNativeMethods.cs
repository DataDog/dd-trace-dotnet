// <copyright file="OtelThreadContextNativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.LibDatadog.OtelThreadContext;

internal sealed class OtelThreadContextNativeMethods : IOtelThreadContextNativeMethods
{
    public static readonly OtelThreadContextNativeMethods Instance = new();

    public unsafe void Update(ReadOnlySpan<byte> traceId, ReadOnlySpan<byte> spanId, ReadOnlySpan<byte> localRootSpanId)
    {
        fixed (byte* traceIdPointer = traceId)
        {
            fixed (byte* spanIdPointer = spanId)
            {
                fixed (byte* localRootSpanIdPointer = localRootSpanId)
                {
                    NativeInterop.OtelThreadContext.Update(traceIdPointer, spanIdPointer, localRootSpanIdPointer);
                }
            }
        }
    }

    public IntPtr Detach() => NativeInterop.OtelThreadContext.Detach();

    public void Free(IntPtr context) => NativeInterop.OtelThreadContext.Free(context);
}
