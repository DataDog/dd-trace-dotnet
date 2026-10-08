// <copyright file="EnumRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// HCORENUM implementation: each enumeration is a snapshot of tokens consumed in chunks, like RegMeta's HENUMInternal.
/// </summary>
internal sealed unsafe class EnumRegistry
{
    private readonly Dictionary<nint, Snapshot> _enumerators = new();
    private readonly object _sync = new();
    private nint _next = 0x1000;

    public HResult Fill<T>(HCORENUM* handle, Func<IEnumerable<int>> tokens, T* output, uint max, uint* fetched)
        where T : unmanaged
    {
        Snapshot? enumerator;
        lock (_sync)
        {
            if (handle == null)
            {
                return HResult.E_INVALIDARG;
            }

            if (handle->Value == 0 || !_enumerators.TryGetValue(handle->Value, out enumerator))
            {
                enumerator = new Snapshot(tokens().ToArray());
                var id = _next++;
                _enumerators[id] = enumerator;
                *handle = new HCORENUM(id);
            }
        }

        var count = 0;
        while (count < max && enumerator.Position < enumerator.Tokens.Length)
        {
            ((int*)output)[count++] = enumerator.Tokens[enumerator.Position++];
        }

        NativeBuffers.Set(fetched, (uint)count);
        return count > 0 ? HResult.S_OK : HResult.S_FALSE;
    }

    public void Close(HCORENUM handle)
    {
        lock (_sync)
        {
            _enumerators.Remove(handle.Value);
        }
    }

    public HResult Count(HCORENUM handle, uint* count)
    {
        lock (_sync)
        {
            NativeBuffers.Set(count, _enumerators.TryGetValue(handle.Value, out var enumerator) ? (uint)enumerator.Tokens.Length : 0u);
            return HResult.S_OK;
        }
    }

    public HResult Reset(HCORENUM handle, uint position)
    {
        lock (_sync)
        {
            if (_enumerators.TryGetValue(handle.Value, out var enumerator))
            {
                enumerator.Position = (int)position;
            }

            return HResult.S_OK;
        }
    }

    private sealed class Snapshot(int[] tokens)
    {
        public int[] Tokens { get; } = tokens;

        public int Position { get; set; }
    }
}
#endif
