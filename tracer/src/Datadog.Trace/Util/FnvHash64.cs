// <copyright file="FnvHash64.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;

namespace Datadog.Trace.Util;

/// <summary>
/// Calculates the FNV 64 bit hash
/// <see href="http://www.isthe.com/chongo/tech/comp/fnv/index.html#FNV-1"/>
/// </summary>
internal static class FnvHash64
{
    /// <summary>
    /// Fixed parameter of the FNV algorithm for 64-bit primes
    /// <see href="http://www.isthe.com/chongo/tech/comp/fnv/index.html#FNV-param"/>
    /// </summary>
    private const ulong OffsetBasis = 14695981039346656037;

    /// <summary>
    /// Fixed parameter of the FNV algorithm for 64-bit primes
    /// <see href="http://www.isthe.com/chongo/tech/comp/fnv/index.html#FNV-param"/>
    /// </summary>
    private const ulong FnvPrime = 1099511628211;

    internal enum Version
    {
        V1,
        V1A,
    }

#if NETCOREAPP
    /// <summary>
    /// Generates the 64-bit FNV hash of <paramref name="data"/> using hash version <paramref name="version"/>
    /// </summary>
    /// <returns>The 64-bit FNV hash of the data, as a <c>ulong</c></returns>
    // Skip locals init to avoid initializing the stackalloc buffer
    public static ulong GenerateHash(string data, Version version)
        => GenerateHash(data, version, OffsetBasis);

    /// <summary>
    /// Generates the 64-bit FNV hash of <paramref name="data"/> using hash version <paramref name="version"/>
    /// Appends the hash to the existing value <paramref name="initialHash"/>. Equivalent to concatenating
    /// the two data values and subsequently calling GenerateHash.
    /// </summary>
    /// <returns>The 64-bit FNV hash of the data, as a <c>ulong</c></returns>
    // Skip locals init to avoid initializing the stackalloc buffer
    [System.Runtime.CompilerServices.SkipLocalsInit]
    public static ulong GenerateHash(string data, Version version, ulong initialHash)
    {
        // Use a relatively small size, unlikely to hit names this big
        const int MaxStackLimit = 256;
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(data.Length);

        if (maxByteCount > MaxStackLimit)
        {
            // To big, allocate on the heap
            return GenerateHash(Encoding.UTF8.GetBytes(data), version, initialHash);
        }

        Span<byte> bytes = stackalloc byte[MaxStackLimit];
        var byteCount = Encoding.UTF8.GetBytes(data, bytes);

        return GenerateHash(bytes.Slice(0, byteCount), version, initialHash);
    }
#else
    /// <summary>
    /// Generates the 64-bit FNV hash of <paramref name="data"/> using hash version <paramref name="version"/>.
    /// </summary>
    /// <returns>The 64-bit FNV hash of the data, as a <c>ulong</c></returns>
    public static ulong GenerateHash(string data, Version version)
        => GenerateHash(Encoding.UTF8.GetBytes(data), version);

    /// <summary>
    /// Generates the 64-bit FNV hash of <paramref name="data"/> using hash version <paramref name="version"/>.
    /// Appends the hash to the existing value <paramref name="initialHash"/>. Equivalent to concatenating
    /// the two data values and subsequently calling GenerateHash.
    /// </summary>
    /// <returns>The 64-bit FNV hash of the data, as a <c>ulong</c></returns>
    public static ulong GenerateHash(string data, Version version, ulong initialHash)
        => GenerateHash(Encoding.UTF8.GetBytes(data), version, initialHash);
#endif

    /// <summary>
    /// Generates the 64-bit FNV hash of <paramref name="data"/> using hash version <paramref name="version"/>.
    /// </summary>
    /// <returns>The 64-bit FNV hash of the data, as a <c>ulong</c></returns>
    public static ulong GenerateHash(byte[] data, Version version)
        => GenerateHash(data, version, OffsetBasis);

    /// <summary>
    /// Generates the 64-bit FNV hash of <paramref name="data"/> using hash version <paramref name="version"/>.
    /// Appends the hash to the existing value <paramref name="initialHash"/>. Equivalent to concatenating
    /// the two data values and subsequently calling GenerateHash.
    /// </summary>
    /// <returns>The 64-bit FNV hash of the data, as a <c>ulong</c></returns>
    public static ulong GenerateHash(byte[] data, Version version, ulong initialHash)
        => version == Version.V1
               ? GenerateV1Hash(data, initialHash)
               : GenerateV1AHash(data, initialHash);

#if NETCOREAPP
    /// <summary>
    /// Generates the 64-bit FNV hash of <paramref name="data"/> using hash version <paramref name="version"/>.
    /// </summary>
    /// <returns>The 64-bit FNV hash of the data, as a <c>ulong</c></returns>
    public static ulong GenerateHash(Span<byte> data, Version version)
        => GenerateHash(data, version, OffsetBasis);

    /// <summary>
    /// Generates the 64-bit FNV hash of <paramref name="data"/> using hash version <paramref name="version"/>.
    /// Appends the hash to the existing value <paramref name="initialHash"/>. Equivalent to concatenating
    /// the two data values and subsequently calling GenerateHash.
    /// </summary>
    /// <returns>The 64-bit FNV hash of the data, as a <c>ulong</c></returns>
    public static ulong GenerateHash(Span<byte> data, Version version, ulong initialHash)
        => version == Version.V1
               ? GenerateV1Hash(data, initialHash)
               : GenerateV1AHash(data, initialHash);
#endif

    private static ulong GenerateV1Hash(byte[] bytes, ulong hash)
    {
        // for each octet_of_data to be hashed
        foreach (var b in bytes)
        {
            // hash = hash * FNV_prime
            hash *= FnvPrime;
            // hash = hash xor octet_of_data
            hash ^= b;
        }

        return hash;
    }

    private static ulong GenerateV1AHash(byte[] bytes, ulong hash)
    {
        // for each octet_of_data to be hashed
        foreach (var b in bytes)
        {
            // hash = hash xor octet_of_data
            hash ^= b;
            // hash = hash * FNV_prime
            hash *= FnvPrime;
        }

        return hash;
    }

#if NETCOREAPP
    private static ulong GenerateV1Hash(ReadOnlySpan<byte> bytes, ulong hash)
    {
        // for each octet_of_data to be hashed
        foreach (var b in bytes)
        {
            // hash = hash * FNV_prime
            hash *= FnvPrime;
            // hash = hash xor octet_of_data
            hash ^= b;
        }

        return hash;
    }

    private static ulong GenerateV1AHash(ReadOnlySpan<byte> bytes, ulong hash)
    {
        // for each octet_of_data to be hashed
        foreach (var b in bytes)
        {
            // hash = hash xor octet_of_data
            hash ^= b;
            // hash = hash * FNV_prime
            hash *= FnvPrime;
        }

        return hash;
    }
#endif
}
