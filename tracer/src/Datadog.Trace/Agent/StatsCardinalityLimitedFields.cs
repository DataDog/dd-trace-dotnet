// <copyright file="StatsCardinalityLimitedFields.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Agent;

[Flags]
internal enum StatsCardinalityLimitedFields
{
    None = 0,
    Resource = 1,
    HttpEndpoint = 2,
    PeerTags = 4,
    AdditionalMetricTags = 8,

    // Not a field, but we flag it separately anyway
    WholeKey = 16,

    All = WholeKey | Resource | HttpEndpoint | PeerTags | AdditionalMetricTags,
}
