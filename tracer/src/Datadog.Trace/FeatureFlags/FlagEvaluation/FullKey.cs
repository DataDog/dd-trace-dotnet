// <copyright file="FullKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal readonly record struct FullKey(DegradedKey Dimensions, string? TargetingKey, string ContextKey, bool ObserveFullEvaluationData);
