// <copyright file="TestSessionSpanTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Tags;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci.Tagging;

internal partial class TestSessionSpanTags : Trace.Tagging.CommonTags
{
    public ulong SessionId { get; set; }

    [Tag(TestTags.Command)]
    public string Command { get; set; }

    [Tag(TestTags.Status)]
    public string Status { get; set; }
}
