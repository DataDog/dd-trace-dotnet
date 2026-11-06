// <copyright file="OpenFeatureApiHooksCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

// API hooks are process-wide, and every synchronous evaluation in this assembly runs them, so tests that add
// them run alone.
[CollectionDefinition(nameof(OpenFeatureApiHooksCollection), DisableParallelization = true)]
public class OpenFeatureApiHooksCollection
{
}
