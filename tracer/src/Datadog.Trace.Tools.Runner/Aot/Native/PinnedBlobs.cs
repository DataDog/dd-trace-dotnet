// <copyright file="PinnedBlobs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Keeps unmanaged copies of metadata blobs alive until disposed, because the metadata APIs return pointers into
/// memory the caller never frees.
/// </summary>
internal sealed class PinnedBlobs : IDisposable
{
    private readonly Dictionary<(object Owner, long Key), (IntPtr Pointer, int Length)> _blobs = new();
    private readonly object _sync = new();

    public (IntPtr Pointer, int Length) Get(object owner, long key, Func<byte[]> factory)
    {
        lock (_sync)
        {
            if (!_blobs.TryGetValue((owner, key), out var blob))
            {
                var bytes = factory();
                var pointer = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                blob = (pointer, bytes.Length);
                _blobs[(owner, key)] = blob;
            }

            return blob;
        }
    }

    public IntPtr Copy(byte[] bytes)
    {
        var pointer = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        lock (_sync)
        {
            _blobs[(this, pointer.ToInt64())] = (pointer, bytes.Length);
        }

        return pointer;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            foreach (var blob in _blobs.Values)
            {
                Marshal.FreeHGlobal(blob.Pointer);
            }

            _blobs.Clear();
        }
    }
}
#endif
