// <copyright file="FeatureEvaluationPrivacy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Datadog.Trace.FeatureFlags;

internal static class FeatureEvaluationPrivacy
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static string? ProtectTargetingKey(string? targetingKey, bool observeFullEvaluationData)
    {
        if (string.IsNullOrEmpty(targetingKey))
        {
            return targetingKey;
        }

        byte[]? utf8 = null;
        try
        {
            // Validate before either mode uses the value. Invalid UTF-16 has no exact strict UTF-8
            // representation, so it must be omitted even when full-data consent is present.
            utf8 = StrictUtf8.GetBytes(targetingKey);
            if (observeFullEvaluationData)
            {
                return targetingKey;
            }

#if NET6_0_OR_GREATER
            var digest = SHA256.HashData(utf8);
#else
            using var sha256 = SHA256.Create();
            var digest = sha256.ComputeHash(utf8);
#endif
            var result = new StringBuilder("sha256_", 71);
            foreach (var value in digest)
            {
                result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }
        catch (EncoderFallbackException)
        {
            return null;
        }
        finally
        {
            if (utf8 is not null)
            {
                Array.Clear(utf8, 0, utf8.Length);
            }
        }
    }

    internal static string? ProtectErrorDetails(string? stableErrorCode, string? diagnosticError, bool observeFullEvaluationData)
    {
        if (stableErrorCode is null)
        {
            return null;
        }

        return observeFullEvaluationData && !string.IsNullOrEmpty(diagnosticError)
                   ? diagnosticError
                   : stableErrorCode;
    }
}
