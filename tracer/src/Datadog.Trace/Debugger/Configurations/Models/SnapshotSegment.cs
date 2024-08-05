// <copyright file="SnapshotSegment.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Internal.Debugger.Configurations.Models;

internal record SnapshotSegment
{
    public SnapshotSegment()
    {
    }

    public SnapshotSegment(string dsl, string json, string str)
    {
        Dsl = dsl;
        Json = json == null ? null : JObject.Parse(json);
        Str = str;
    }

    public string Str { get; set; }

    public string Dsl { get; set; }

    public JObject Json { get; set; }
}
