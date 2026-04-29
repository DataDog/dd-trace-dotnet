// <copyright file="SimpleHash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// A vendored version of System.Reflection.Internal.Hash

namespace Datadog.Trace.Debugger;

internal static class SimpleHash
{
    /// <summary>
    /// The offset bias value used in the FNV-1a algorithm
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// Note however, that the <c>Combine</c> methods in this class
    /// do _not_ implement the FNV-1a algorithm.
    /// </summary>
    internal const int FnvOffsetBias = unchecked((int)2166136261);

    internal static int Combine(int data, int initialHash)
    {
        return unchecked((initialHash * (int)0xA5555529) + data);
    }

    internal static int Combine(uint data, int initialHash)
    {
        return unchecked((initialHash * (int)0xA5555529) + (int)data);
    }

    internal static int Combine(bool data, int initialHash)
    {
        return Combine(initialHash, data ? 1 : 0);
    }
}
