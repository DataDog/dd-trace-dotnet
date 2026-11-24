// <copyright file="ContextSnapshotLimits.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal static class ContextSnapshotLimits
{
    public const int Fields = 256;
    public const int TextLength = 256;
    public const int ContainerItems = 256;
    public const int Depth = 4;
    public const int Visits = 1_280;
}
