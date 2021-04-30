using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class KafkaTags : InstrumentationTags
    {
        private const string ComponentName = "kafka";

        private static readonly IProperty<string>[] KafkaTagsProperties =
            InstrumentationTagsProperties.Concat(
                new ReadOnlyProperty<KafkaTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<KafkaTags, string>(Trace.Tags.Partition, t => t.Partition, (t, v) => t.Partition = v),
                new Property<KafkaTags, string>(Trace.Tags.Offset, t => t.Offset, (t, v) => t.Offset = v),
                new Property<KafkaTags, string>(Trace.Tags.Tombstone, t => t.Tombstone, (t, v) => t.Tombstone = v),
                new Property<KafkaTags, string>(Trace.Tags.RecordQueueTimeMs, t => t.RecordQueueTimeMs, (t, v) => t.RecordQueueTimeMs = v));

        public KafkaTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        public override string SpanKind { get; }

        public string InstrumentationName => ComponentName;

        public string Partition { get; set; }

        public string Offset { get; set; }

        public string Exchange { get; set; }

        public string Tombstone { get; set; }

        public string RecordQueueTimeMs { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => KafkaTagsProperties;
    }
}
