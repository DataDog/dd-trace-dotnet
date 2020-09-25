using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class GraphQLTags : CommonTags
    {
        internal static readonly IProperty<string>[] GraphQLTagsProperties =
            CommonTagsProperties.Concat(
                new Property<GraphQLTags, string>(Trace.Tags.GraphQLSource, t => t.Source, (t, v) => t.Source = v),
                new Property<GraphQLTags, string>(Trace.Tags.GraphQLOperationName, t => t.OperationName, (t, v) => t.OperationName = v),
                new Property<GraphQLTags, string>(Trace.Tags.GraphQLOperationType, t => t.OperationType, (t, v) => t.OperationType = v),
                new Property<GraphQLTags, string>(Trace.Tags.SpanKind, t => t.SpanKind, (t, v) => t.SpanKind = v),
                new Property<GraphQLTags, string>(Trace.Tags.Language, t => t.Language, (t, v) => t.Language = v));

        internal static readonly IProperty<double?>[] GraphQLMetricsProperties =
            CommonMetricsProperties.Concat(
                new Property<GraphQLTags, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v));

        public string SpanKind { get; set; }

        public string Language { get; set; }

        public string Source { get; set; }

        public string OperationName { get; set; }

        public string OperationType { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => GraphQLTagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => GraphQLMetricsProperties;
    }
}
