// <copyright file="AwsStepFunctionsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AwsStepFunctionsTags : AwsSdkTags
    {
        [Obsolete("Use constructor that takes a SpanKind")]
        public AwsStepFunctionsTags()
            : this(SpanKinds.Client)
        {
        }

        public AwsStepFunctionsTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.StateMachineName)]
        public string? StateMachineName { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }

    internal partial class AwsStepFunctionsV1Tags : AwsStepFunctionsTags
    {
        private string? _peerServiceOverride = null;

        [Obsolete("Use constructor that takes a SpanKind")]
        public AwsStepFunctionsV1Tags()
            : this(SpanKinds.Client)
        {
        }

        public AwsStepFunctionsV1Tags(string spanKind)
            : base(spanKind)
        {
        }

        [Tag(Trace.Tags.PeerService)]
        public override string? PeerService
        {
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride ?? StateMachineName;
            }
            set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string? PeerServiceSource
        {
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride is not null
                           ? "peer.service"
                           : Trace.Tags.StateMachineName;
            }
        }
    }
}
