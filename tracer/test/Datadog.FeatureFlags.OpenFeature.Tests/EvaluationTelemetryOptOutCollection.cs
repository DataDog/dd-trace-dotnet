// <copyright file="EvaluationTelemetryOptOutCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

// These tests change the OpenFeature API context and transaction propagator.
[CollectionDefinition(nameof(EvaluationTelemetryOptOutCollection), DisableParallelization = true)]
public class EvaluationTelemetryOptOutCollection
{
}
