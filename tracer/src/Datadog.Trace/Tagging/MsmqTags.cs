// <copyright file="MsmqTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Internal;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class MsmqTags : InstrumentationTags
    {
        public MsmqTags() => SpanKind = InternalSpanKinds.Consumer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsmqTags"/> class.
        /// </summary>
        /// <param name="spanKind">kind of span</param>
        public MsmqTags(string spanKind) => SpanKind = spanKind;

        [Tag(Tags.MsmqCommand)]
        public string Command { get; set; }

        /// <inheritdoc/>
        [Tag(Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Tags.InstrumentationName)]
        public string InstrumentationName => "msmq";

        [Tag(Tags.OutHost)]
        public string Host { get; set; }

        [Tag(Tags.MsmqQueuePath)]
        public string Path { get; set; }

        [Tag(Tags.MsmqMessageWithTransaction)]
        public string MessageWithTransaction { get; set; }

        [Tag(Tags.MsmqIsTransactionalQueue)]
        public string IsTransactionalQueue { get; set; }
    }

    internal partial class MsmqV1Tags : MsmqTags
    {
        private string _peerServiceOverride = null;

        // For the sake of unit tests, define a default constructor
        // though the Kafka integration should use the constructor that takes a spanKind
        // so the setter is only invoked once
        [Obsolete("Use constructor that takes a SpanKind")]
        public MsmqV1Tags()
        {
        }

        public MsmqV1Tags(string spanKind)
            : base(spanKind)
        {
        }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? (SpanKind.Equals(InternalSpanKinds.Client) || SpanKind.Equals(InternalSpanKinds.Producer) ?
                                                Host
                                                : null);
            private set => _peerServiceOverride = value;
        }

        [Tag(Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                           ? Tags.PeerService
                           : SpanKind.Equals(InternalSpanKinds.Client) || SpanKind.Equals(InternalSpanKinds.Producer)
                               ? Tags.OutHost
                               : null;
            }
        }
    }
}
