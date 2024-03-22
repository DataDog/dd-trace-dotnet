// <copyright file="MemoryExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class MemoryExtensions
    {
        /// <summary>
        /// Return a new IMemoryOwner of size 'currentSize * 2' and dispose the old one
        /// </summary>
        internal static IMemoryOwner<T> EnlargeBuffer<T>(this IMemoryOwner<T> memoryOwner, int currentSize)
        {
            var newMemory = ArrayMemoryPool<T>.Shared.Rent(currentSize * 2);
            memoryOwner.Memory.Span.CopyTo(newMemory.Memory.Span);
            memoryOwner.Dispose();
            return newMemory;
        }

        /// <summary>
        /// Return a new array of size 'currentSize * 2' from the pool and return the old one to the pool
        /// </summary>
        internal static T[] EnlargeBuffer<T>(this T[] array, int currentSize)
        {
            var newArray = ArrayPool<T>.Shared.Rent(currentSize * 2);
            array.CopyTo(newArray, 0);
            ArrayPool<T>.Shared.Return(array, !typeof(T).IsValueType);
            return newArray;
        }
    }
}
