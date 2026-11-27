// <copyright file="NativeStrings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.LibDatadog.RemoteConfig;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// UTF-8 copies of strings in native memory, which live until disposed, i.e. for the duration of a native call.
/// </summary>
internal sealed class NativeStrings : IDisposable
{
    private readonly List<CharSlice> _slices = [];
    private readonly List<nint> _arrays = [];

    public CharSlice Add(string? value)
    {
        var slice = new CharSlice(value ?? string.Empty);
        _slices.Add(slice);
        return slice;
    }

    public unsafe CharSlices AddAll(IEnumerable<string>? values)
    {
        var collection = values as IReadOnlyCollection<string> ?? values?.ToList();
        if (collection is null || collection.Count == 0)
        {
            return default;
        }

        var array = Marshal.AllocHGlobal(collection.Count * sizeof(CharSlice));
        _arrays.Add(array);
        var i = 0;
        foreach (var value in collection)
        {
            ((CharSlice*)array)[i++] = Add(value);
        }

        return new CharSlices(array, (nuint)i);
    }

    public void Dispose()
    {
        foreach (var slice in _slices)
        {
            slice.Dispose();
        }

        foreach (var array in _arrays)
        {
            Marshal.FreeHGlobal(array);
        }
    }
}
