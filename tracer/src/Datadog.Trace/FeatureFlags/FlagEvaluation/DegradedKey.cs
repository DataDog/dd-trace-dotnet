// <copyright file="DegradedKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

// Consent is absent: degraded output omits both subject identity and evaluation context.
// FeaturesKey is the canonical form of the split's EVALUATION features, so evaluations that differ
// only in those features count separately. The serial id is deliberately not part of any key.
internal readonly record struct DegradedKey(string FlagKey, string? Variant, string? AllocationKey, string? ErrorCode, string FeaturesKey);
