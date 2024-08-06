// <copyright file="AwsSdkTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.SourceGenerators;

namespace Datadog.Trace.Internal.Tagging
{
    internal abstract partial class AwsSdkTags : InstrumentationTags, IHasStatusCode
    {
        [Tag(Trace.Internal.Tags.InstrumentationName)]
        public string InstrumentationName => "aws-sdk";

        [Tag(Trace.Internal.Tags.AwsAgentName)]
        public string AgentName => "dotnet-aws-sdk";

        [Tag(Trace.Internal.Tags.AwsOperationName)]
        public string Operation { get; set; }

#pragma warning disable CS0618
        [Tag(Trace.Internal.Tags.AwsRegion)]
#pragma warning restore CS0618
        public string AwsRegion => Region;

        [Tag(Trace.Internal.Tags.Region)]
        public string Region { get; set; }

        [Tag(Trace.Internal.Tags.AwsRequestId)]
        public string RequestId { get; set; }

#pragma warning disable CS0618
        [Tag(Trace.Internal.Tags.AwsServiceName)]
#pragma warning restore CS0618
        public string AwsService => Service;

        [Tag(Trace.Internal.Tags.AwsService)]
        public string Service { get; set; }

        [Tag(Trace.Internal.Tags.HttpMethod)]
        public string HttpMethod { get; set; }

        [Tag(Trace.Internal.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        [Tag(Trace.Internal.Tags.HttpStatusCode)]
        public string HttpStatusCode { get; set; }
    }
}
