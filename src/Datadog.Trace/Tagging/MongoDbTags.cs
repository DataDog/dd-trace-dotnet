using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class MongoDbTags : CommonTags
    {
        internal static readonly IProperty<string>[] MongoDbTagsProperties =
            CommonTagsProperties.Concat(
                new Property<MongoDbTags, string>(Trace.Tags.DbName, t => t.DbName, (t, v) => t.DbName = v),
                new Property<MongoDbTags, string>(Trace.Tags.MongoDbQuery, t => t.Query, (t, v) => t.Query = v),
                new Property<MongoDbTags, string>(Trace.Tags.MongoDbCollection, t => t.Collection, (t, v) => t.Collection = v),
                new Property<MongoDbTags, string>(Trace.Tags.OutHost, t => t.Host, (t, v) => t.Host = v),
                new Property<MongoDbTags, string>(Trace.Tags.OutPort, t => t.Port, (t, v) => t.Port = v));

        internal static readonly IProperty<double?>[] MongoDbMetricsProperties =
            CommonMetricsProperties.Concat(
                new Property<MongoDbTags, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v));

        public string DbName { get; set; }

        public string Query { get; set; }

        public string Collection { get; set; }

        public string Host { get; set; }

        public string Port { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => MongoDbTagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => MongoDbMetricsProperties;
    }
}
