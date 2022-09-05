// <copyright file="ProcessStartTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class ProcessCommandStartTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Consumer;

        [Tag(Trace.Tags.ProcessEnvironmentVars)]
        public string EnviromentVars { get; set; }

        [Tag(Trace.Tags.ProcessTruncated)]
        public string IsTruncated { get; set; }
    }
}
