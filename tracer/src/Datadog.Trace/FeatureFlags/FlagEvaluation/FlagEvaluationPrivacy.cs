// <copyright file="FlagEvaluationPrivacy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal static class FlagEvaluationPrivacy
{
    internal static string? TargetingKeyForOutput(string? key, bool consent)
    {
        if (key is null || !IsValidText(key))
        {
            return null;
        }

        return consent || key.Length == 0 ? key : "sha256_" + Sha256Helper.ComputeHashAsHexString(key);
    }

    // Keep this allowlist aligned with FlagEvalEVPHook.ToMetadataErrorCode in Datadog.FeatureFlags.OpenFeature.
    // The tracer validates output independently, even if the provider was bypassed.
    internal static string? ErrorCodeForOutput(string? code) => code switch
    {
        null or "" => null,
        "FLAG_NOT_FOUND" or "INVALID_CONTEXT" or "PARSE_ERROR" or "PROVIDER_FATAL" or
        "PROVIDER_NOT_READY" or "TARGETING_KEY_MISSING" or "TYPE_MISMATCH" or "GENERAL" => code,
        _ => "GENERAL",
    };

    internal static bool IsValidText(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (!char.IsSurrogate(c))
            {
                continue;
            }

            // Reject unpaired surrogates without raising application-visible exceptions.
            if (!char.IsHighSurrogate(c) || ++i == text.Length || !char.IsLowSurrogate(text[i]))
            {
                return false;
            }
        }

        return true;
    }
}
