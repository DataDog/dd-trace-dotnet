// <copyright file="CachingBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Datadog.Trace.Tools.Analyzers.Helpers;

internal abstract class CachingBase<TEntry>
{
#pragma warning disable SA1401 // Fields should be private
    protected readonly int Mask;
#pragma warning restore SA1401
    // cache size is always ^2.
    // items are placed at [hash ^ mask]
    // new item will displace previous one at the same location.
    private readonly int _alignedSize;
    private TEntry[]? _entries;

    /// <param name="createBackingArray">Whether or not the backing array should be created immediately, or should
    /// be deferred until the first time that <see cref="Entries"/> is used.  Note: if <paramref
    /// name="createBackingArray"/> is <see langword="false"/> then the array will be created in a non-threadsafe
    /// fashion (effectively different threads might observe a small window of time when different arrays could be
    /// returned.  Derived types should only pass <see langword="false"/> here if that behavior is acceptable for
    /// their use case.</param>
    internal CachingBase(int size, bool createBackingArray = true)
    {
        _alignedSize = AlignSize(size);
        this.Mask = _alignedSize - 1;
        _entries = createBackingArray ? new TEntry[_alignedSize] : null;
    }

    // See docs for createBackingArray on the constructor for why using the non-threadsafe ??= is ok here.
    protected TEntry[] Entries => _entries ??= new TEntry[_alignedSize];

    private static int AlignSize(int size)
    {
        size--;
        size |= size >> 1;
        size |= size >> 2;
        size |= size >> 4;
        size |= size >> 8;
        size |= size >> 16;
        return size + 1;
    }
}
