// <copyright file="AwsSdkTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal abstract partial class AwsSdkTags : InstrumentationTags, IHasStatusCode
    {
        [Tag(Internal.Tags.InstrumentationName)]
        public string InstrumentationName => "aws-sdk";

        [Tag(Internal.Tags.AwsAgentName)]
        public string AgentName => "dotnet-aws-sdk";

        [Tag(Internal.Tags.AwsOperationName)]
        public string Operation { get; set; }

#pragma warning disable CS0618
        [Tag(Internal.Tags.AwsRegion)]
#pragma warning restore CS0618
        public string AwsRegion => Region;

        [Tag(Internal.Tags.Region)]
        public string Region { get; set; }

        [Tag(Internal.Tags.AwsRequestId)]
        public string RequestId { get; set; }

#pragma warning disable CS0618
        [Tag(Internal.Tags.AwsServiceName)]
#pragma warning restore CS0618
        public string AwsService => Service;

        [Tag(Internal.Tags.AwsService)]
        public string Service { get; set; }

        [Tag(Internal.Tags.HttpMethod)]
        public string HttpMethod { get; set; }

        [Tag(Internal.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        [Tag(Internal.Tags.HttpStatusCode)]
        public string HttpStatusCode { get; set; }
    }
}
