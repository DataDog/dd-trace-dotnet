// <copyright file="ProcessStartTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.ObjectModel;
using Datadog.Trace.Internal.SourceGenerators;

namespace Datadog.Trace.Internal.Tagging
{
    internal partial class ProcessCommandStartTags : InstrumentationTags
    {
        [Tag(Trace.Internal.Tags.ProcessComponent)]
        public static string Component => "process";

        [Tag(Trace.Internal.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Internal;

        [Tag(Trace.Internal.Tags.ProcessEnvironmentVariables)]
        public string EnvironmentVariables { get; set; }

        [Tag(Trace.Internal.Tags.ProcessCommandExec)]
        public string CommandExec { get; set; }

        [Tag(Trace.Internal.Tags.ProcessCommandShell)]
        public string CommandShell { get; set; }

        [Tag(Trace.Internal.Tags.ProcessTruncated)]
        public string Truncated { get; set; }
    }
}
