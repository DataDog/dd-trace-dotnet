// <copyright file="EvaluationTrace.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags.Exposure.Model;

namespace Datadog.Trace.FeatureFlags;

internal sealed class EvaluationTrace(
    Evaluation details,
    IReadOnlyList<DependencyEvaluation> dependencyEvaluations,
    IReadOnlyList<ExposureEvent> dependencyExposures)
{
    public Evaluation Details { get; } = details;

    public IReadOnlyList<DependencyEvaluation> DependencyEvaluations { get; } = dependencyEvaluations;

    public IReadOnlyList<ExposureEvent> DependencyExposures { get; } = dependencyExposures;

    public static EvaluationTrace Empty(Evaluation details) => new(details, Array.Empty<DependencyEvaluation>(), Array.Empty<ExposureEvent>());
}
