// <copyright file="Subject.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Aerospike;

namespace Datadog.Trace.FeatureFlags.Exposure;

internal class Subject(string id, IDictionary<string, object?> attributes)
{
    public string Id { get; } = id;

    public IDictionary<string, object?> Attributes { get; } = attributes;
}
