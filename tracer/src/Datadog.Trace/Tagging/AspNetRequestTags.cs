// <copyright file="AspNetRequestTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal sealed partial class AspNetRequestTags : WebTags
    {
        private const string ComponentName = "aspnet";

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => ComponentName;

        [Tag(Tags.CodeOriginType)]
        public string? CodeOriginType { get; set; }

        [Tag(Tags.CodeOriginFrameIndex)]
        public string? CodeOriginFrameIndex { get; set; }

        [Tag(Tags.CodeOriginFrameMethod)]
        public string? CodeOriginFrameMethod { get; set; }

        [Tag(Tags.CodeOriginFrameType)]
        public string? CodeOriginFrameType { get; set; }

        [Tag(Tags.CodeOriginFrameFile)]
        public string? CodeOriginFrameFile { get; set; }

        [Tag(Tags.CodeOriginFrameLine)]
        public string? CodeOriginFrameLine { get; set; }

        [Tag(Tags.CodeOriginFrameColumn)]
        public string? CodeOriginFrameColumn { get; set; }
    }
}
