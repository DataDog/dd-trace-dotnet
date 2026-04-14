// <copyright file="MemoryExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Buffers;

namespace Datadog.Trace.Pdb
{
    internal static class MemoryExtensions
    {
        internal static IMemoryOwner<T> EnlargeBuffer<T>(this IMemoryOwner<T> memoryOwner, int currentSize)
        {
            var newMemory = MemoryPool<T>.Shared.Rent(currentSize * 2);
            memoryOwner.Memory.Span.CopyTo(newMemory.Memory.Span);
            memoryOwner.Dispose();
            return newMemory;
        }
    }
}
