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

        [Tag(Trace.Tags.ProcessCommandLine)]
        public string CommandLine { get; set; }

        [Tag(Trace.Tags.ProcessDomain)]
        public string Domain { get; set; }

        [Tag(Trace.Tags.ProcessPassword)]
        public string Password { get; set; }

        [Tag(Trace.Tags.ProcessUserName)]
        public string UserName { get; set; }
    }
}
