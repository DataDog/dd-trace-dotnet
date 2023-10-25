// <copyright file="ProcessStartTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.ObjectModel;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class ProcessCommandStartTags : InstrumentationTags
    {
        [Tag(Trace.Tags.ProcessComponent)]
        public static string Component => "process";

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Internal;

        [Tag(Trace.Tags.ProcessEnvironmentVariables)]
        public string EnvironmentVariables { get; set; }

        [Tag(Trace.Tags.ProcessCommandExec)]
        public string CommandExec { get; set; }

        [Tag(Trace.Tags.ProcessCommandShell)]
        public string CommandShell { get; set; }

        [Tag(Trace.Tags.ProcessTruncated)]
        public string Truncated { get; set; }
    }
}
