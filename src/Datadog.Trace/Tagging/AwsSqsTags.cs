using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AwsSqsTags : AwsSdkTags
    {
        protected static readonly IProperty<string>[] AwsSqsTagsProperties =
            AwsSdkTagsProperties.Concat(
                new Property<AwsSqsTags, string>(Trace.Tags.AwsQueueName, t => t.QueueName, (t, v) => t.QueueName = v),
                new Property<AwsSqsTags, string>(Trace.Tags.AwsQueueUrl, t => t.QueueUrl, (t, v) => t.QueueUrl = v));

        public string QueueName { get; set; }

        public string QueueUrl { get; set; }

        public override string SpanKind => SpanKinds.Client;

        protected override IProperty<string>[] GetAdditionalTags() => AwsSqsTagsProperties;
    }
}
