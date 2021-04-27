using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal abstract class AwsSdkTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] AwsSdkTagsProperties =
            InstrumentationTagsProperties.Concat(
                new ReadOnlyProperty<AwsSdkTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new ReadOnlyProperty<AwsSdkTags, string>(Trace.Tags.AwsAgentName, t => t.AgentName),
                new Property<AwsSdkTags, string>(Trace.Tags.AwsOperationName, t => t.Operation, (t, v) => t.Operation = v),
                new Property<AwsSdkTags, string>(Trace.Tags.AwsRequestId, t => t.RequestId, (t, v) => t.RequestId = v),
                new Property<AwsSdkTags, string>(Trace.Tags.AwsServiceName, t => t.Service, (t, v) => t.Service = v));

        public string InstrumentationName => "aws-sdk";

        public string AgentName => "dotnet-aws-sdk";

        public string Operation { get; set; }

        public string RequestId { get; set; }

        public string Service { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AwsSdkTagsProperties;
    }
}