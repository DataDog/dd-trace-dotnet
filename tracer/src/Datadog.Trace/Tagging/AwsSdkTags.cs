// <copyright file="AwsSdkTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal abstract partial class AwsSdkTags : InstrumentationTags, IHasStatusCode
    {
        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => "aws-sdk";

        [Tag(Trace.Tags.AwsAgentName)]
        public string AgentName => "dotnet-aws-sdk";

        [Tag(Trace.Tags.AwsOperationName)]
        public string Operation { get; set; }

        [Tag(Trace.Tags.AwsRegion)]
        public string Region { get; set; }

        [Tag(Trace.Tags.AwsRequestId)]
        public string RequestId { get; set; }

        [Tag(Trace.Tags.AwsServiceName)]
        public string Service { get; set; }

        [Tag(Trace.Tags.HttpMethod)]
        public string HttpMethod { get; set; }

        [Tag(Trace.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        [Tag(Trace.Tags.HttpStatusCode)]
        public string HttpStatusCode { get; set; }
    }
}
