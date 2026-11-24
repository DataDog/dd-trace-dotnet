// <copyright file="OpenFeatureApiHooksCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

// API hooks and the provider's static flags are process-wide; tests that change them run alone.
[CollectionDefinition(nameof(OpenFeatureApiHooksCollection), DisableParallelization = true)]
public class OpenFeatureApiHooksCollection
{
}
