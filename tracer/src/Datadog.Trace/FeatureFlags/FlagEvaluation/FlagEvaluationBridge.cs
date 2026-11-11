// <copyright file="FlagEvaluationBridge.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

// Linked into the provider so both sides use the same internal wire format.
internal static class FlagEvaluationBridge
{
    // Keep consent separate from ContextOmissionReason's low bits. Packing them together
    // keeps EnqueueEVP at eight arguments: a ninth forces CallTarget to allocate and box.
    internal const int ObserveFullEvaluationData = 1 << 30;
}
