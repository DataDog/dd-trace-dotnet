// <copyright file="MsmqTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class MsmqTags : InstrumentationTags
    {
        public MsmqTags() => SpanKind = SpanKinds.Consumer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsmqTags"/> class.
        /// </summary>
        /// <param name="spanKind">kind of span</param>
        public MsmqTags(string spanKind) => SpanKind = spanKind;

        [TagName(Trace.Tags.MsmqCommand)]
        public string Command { get; set; }

        /// <inheritdoc/>
        [TagName(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        [TagName(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => "msmq";

        [TagName(Trace.Tags.MsmqQueuePath)]
        public string Path { get; set; }

        [TagName(Trace.Tags.MsmqMessageWithTransaction)]
        public string MessageWithTransaction { get; set; }

        [TagName(Trace.Tags.MsmqIsTransactionalQueue)]
        public string IsTransactionalQueue { get; set; }
    }
}
