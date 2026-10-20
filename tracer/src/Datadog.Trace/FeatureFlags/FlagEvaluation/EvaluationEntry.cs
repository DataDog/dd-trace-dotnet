// <copyright file="EvaluationEntry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal sealed class EvaluationEntry
{
    public EvaluationEntry(long evalTimeMs, bool observeFullEvaluationData, IReadOnlyDictionary<string, object?>? contextAttrs)
    {
        Count = 1;
        FirstEvaluationMs = evalTimeMs;
        LastEvaluationMs = evalTimeMs;
        ObserveFullEvaluationData = observeFullEvaluationData;
        ContextAttrs = observeFullEvaluationData ? contextAttrs : null;
    }

    public long Count { get; private set; }

    public long FirstEvaluationMs { get; private set; }

    public long LastEvaluationMs { get; private set; }

    public bool ObserveFullEvaluationData { get; private set; }

    public IReadOnlyDictionary<string, object?>? ContextAttrs { get; private set; }

    public void Observe(long evalTimeMs, bool observeFullEvaluationData)
    {
        Count++;
        ObserveFullEvaluationData &= observeFullEvaluationData;
        if (!ObserveFullEvaluationData)
        {
            ContextAttrs = null;
        }

        if (evalTimeMs < FirstEvaluationMs)
        {
            FirstEvaluationMs = evalTimeMs;
        }

        if (evalTimeMs > LastEvaluationMs)
        {
            LastEvaluationMs = evalTimeMs;
        }
    }
}
