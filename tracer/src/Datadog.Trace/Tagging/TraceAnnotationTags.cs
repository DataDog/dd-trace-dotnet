// <copyright file="TraceAnnotationTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class TraceAnnotationTags : CommonTags
    {
        private const string ComponentName = "trace";

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => ComponentName;
    }
}
