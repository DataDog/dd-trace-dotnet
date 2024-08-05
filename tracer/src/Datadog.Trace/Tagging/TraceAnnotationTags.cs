// <copyright file="TraceAnnotationTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.SourceGenerators;

namespace Datadog.Trace.Internal.Tagging
{
    internal partial class TraceAnnotationTags : CommonTags
    {
        private const string ComponentName = "trace";

        [Tag(Trace.Internal.Tags.InstrumentationName)]
        public string InstrumentationName => ComponentName;
    }
}
