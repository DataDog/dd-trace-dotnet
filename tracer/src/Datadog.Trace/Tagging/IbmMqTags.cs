// <copyright file="IbmMqTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Internal.SourceGenerators;

namespace Datadog.Trace.Internal.Tagging
{
    internal partial class IbmMqTags : InstrumentationTags
    {
        public IbmMqTags() => SpanKind = SpanKinds.Consumer;

        public IbmMqTags(string spanKind) => SpanKind = spanKind;

        /// <inheritdoc/>
        [Tag(Trace.Internal.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Trace.Internal.Tags.InstrumentationName)]
        public string InstrumentationName => "ibmmq";

        [Tag(Trace.Internal.Tags.TopicName)]
        public string? TopicName { get; set; }
    }
}
