// <copyright file="EvaluationTelemetryOptOutCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

// These tests change process-wide state: the OpenFeature API context and transaction propagator,
// and DD_FEATURE_FLAGS_EVALUATION_TELEMETRY_PER_SUBJECT_OPT_OUT_ENABLED, which every
// parameterless DatadogProvider reads at construction.
[CollectionDefinition(nameof(EvaluationTelemetryOptOutCollection), DisableParallelization = true)]
public class EvaluationTelemetryOptOutCollection
{
}
