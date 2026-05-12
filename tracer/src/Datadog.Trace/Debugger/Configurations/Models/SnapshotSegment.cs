// <copyright file="SnapshotSegment.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Debugger.Configurations.Models;

internal sealed record SnapshotSegment
{
    public SnapshotSegment()
    {
    }

    public SnapshotSegment(string dsl, string json, string str)
    {
        Dsl = dsl;
        Json = json == null ? null : JsonHelper.ParseJObject(json);
        Str = str;
    }

    public string Str { get; set; }

    public string Dsl { get; set; }

    public JObject Json { get; set; }

    // The record-synthesized members would hash/compare Json by reference (JObject inherits
    // object.GetHashCode/Equals), which means two SnapshotSegment instances parsed from the
    // same JSON would be considered different. Compare and hash by content instead.
    public bool Equals(SnapshotSegment other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Str, other.Str, StringComparison.Ordinal)
            && string.Equals(Dsl, other.Dsl, StringComparison.Ordinal)
            && (ReferenceEquals(Json, other.Json) || JToken.DeepEquals(Json, other.Json));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Str, Dsl, Json?.ToString());
    }
}
