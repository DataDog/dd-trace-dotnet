// <copyright file="ContextOmissionReason.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

[Flags]
internal enum ContextOmissionReason
{
    None = 0,
    MaxContextFields = 1,
    MaxKeyLength = 2,
    MaxValueLength = 4,
    MaxListElements = 8,
    MaxStructureProperties = 16,
    MaxSnapshotDepth = 32,
    MaxVisitedNodes = 64,
    UnsupportedValue = 128,
    SnapshotError = 256,
}
