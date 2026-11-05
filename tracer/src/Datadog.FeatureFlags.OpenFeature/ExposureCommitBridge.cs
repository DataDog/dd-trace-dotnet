// <copyright file="ExposureCommitBridge.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.FeatureFlags.OpenFeature;

// The tracer uses this marker to detect SDK versions that can commit a returned exposure batch.
internal static class ExposureCommitBridge
{
}
