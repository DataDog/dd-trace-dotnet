// <copyright file="AwsSdkTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal abstract class AwsSdkTags : InstrumentationTags, IHasStatusCode
    {
        protected static readonly IProperty<string>[] AwsSdkTagsProperties =
            InstrumentationTagsProperties.Concat(
                new ReadOnlyProperty<AwsSdkTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new ReadOnlyProperty<AwsSdkTags, string>(Trace.Tags.AwsAgentName, t => t.AgentName),
                new Property<AwsSdkTags, string>(Trace.Tags.AwsOperationName, t => t.Operation, (t, v) => t.Operation = v),
                new Property<AwsSdkTags, string>(Trace.Tags.AwsRegion, t => t.Region, (t, v) => t.Region = v),
                new Property<AwsSdkTags, string>(Trace.Tags.AwsRequestId, t => t.RequestId, (t, v) => t.RequestId = v),
                new Property<AwsSdkTags, string>(Trace.Tags.AwsServiceName, t => t.Service, (t, v) => t.Service = v),
                new Property<AwsSdkTags, string>(Trace.Tags.HttpMethod, t => t.HttpMethod, (t, v) => t.HttpMethod = v),
                new Property<AwsSdkTags, string>(Trace.Tags.HttpStatusCode, t => t.HttpStatusCode, (t, v) => t.HttpStatusCode = v),
                new Property<AwsSdkTags, string>(Trace.Tags.HttpUrl, t => t.HttpUrl, (t, v) => t.HttpUrl = v));

        public string InstrumentationName => "aws-sdk";

        public string AgentName => "dotnet-aws-sdk";

        public string Operation { get; set; }

        public string Region { get; set; }

        public string RequestId { get; set; }

        public string Service { get; set; }

        public string HttpMethod { get; set; }

        public string HttpUrl { get; set; }

        public string HttpStatusCode { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AwsSdkTagsProperties;
    }
}
