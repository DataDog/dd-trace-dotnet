using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class SqlTags : CommonTags
    {
        private static new readonly IProperty<string>[] TagsProperties =
            CommonTags.TagsProperties.Concat(
                new Property<SqlTags, string>(Trace.Tags.DbType, t => t.DbType, (t, v) => t.DbType = v),
                new Property<SqlTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName, (t, v) => t.InstrumentationName = v),
                new Property<SqlTags, string>(Trace.Tags.DbName, t => t.DbName, (t, v) => t.DbName = v),
                new Property<SqlTags, string>(Trace.Tags.DbUser, t => t.DbUser, (t, v) => t.DbUser = v),
                new Property<SqlTags, string>(Trace.Tags.OutHost, t => t.OutHost, (t, v) => t.OutHost = v));

        private static new readonly IProperty<double?>[] MetricsProperties =
            CommonTags.MetricsProperties.Concat(
                new Property<SqlTags, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v));

        public string DbType { get; set; }

        public string InstrumentationName { get; set; }

        public string DbName { get; set; }

        public string DbUser { get; set; }

        public string OutHost { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => TagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => MetricsProperties;
    }
}
