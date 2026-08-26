// <copyright file="ApiKeyFingerprint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags.Agentless;

internal static class ApiKeyFingerprint
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int EncodedLength = 43;

    public static string Create(string apiKey)
    {
#if NETCOREAPP
        Span<byte> digest = stackalloc byte[32];
        Sha256Helper.ComputeHash(apiKey, digest);
        return Encode(digest);
#else
        return Encode(Sha256Helper.ComputeHash(apiKey));
#endif
    }

    private static string Encode(Span<byte> digest)
    {
        Span<char> encoded = stackalloc char[EncodedLength];
        encoded.Fill('0');

        // The digest is an unsigned big-endian integer. Repeated long division avoids the
        // signed/little-endian differences in BigInteger across the supported target frameworks.
        for (var outputIndex = EncodedLength - 1; outputIndex >= 0; outputIndex--)
        {
            var remainder = 0;
            var hasValue = false;
            for (var digestIndex = 0; digestIndex < digest.Length; digestIndex++)
            {
                var current = (remainder * 256) + digest[digestIndex];
                digest[digestIndex] = (byte)(current / 62);
                remainder = current % 62;
                hasValue |= digest[digestIndex] != 0;
            }

            encoded[outputIndex] = Alphabet[remainder];
            if (!hasValue)
            {
                break;
            }
        }

        return "rijn_" + encoded.ToString();
    }
}
