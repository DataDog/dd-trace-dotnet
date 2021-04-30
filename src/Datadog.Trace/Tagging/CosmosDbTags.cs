using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class CosmosDbTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] CosmosDbTagsProperties =
            InstrumentationTagsProperties.Concat(
                new ReadOnlyProperty<CosmosDbTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<CosmosDbTags, string>(Trace.Tags.CosmosDbContainer, t => t.ContainerId, (t, v) => t.ContainerId = v),
                new Property<CosmosDbTags, string>(Trace.Tags.CosmosDbDatabase, t => t.DatabaseId, (t, v) => t.DatabaseId = v),
                new Property<CosmosDbTags, string>(Trace.Tags.OutHost, t => t.Host, (t, v) => t.Host = v));

        public override string SpanKind => SpanKinds.Client;

        public string InstrumentationName => nameof(IntegrationIds.CosmosDb);

        public string ContainerId { get; set; }

        public string DatabaseId { get; set; }

        public string Host { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => CosmosDbTagsProperties;
    }
}
