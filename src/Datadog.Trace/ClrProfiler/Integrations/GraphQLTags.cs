// <copyright file="GraphQLTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal class GraphQLTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] GraphQLTagsProperties =
            InstrumentationTagsProperties.Concat(
                new ReadOnlyProperty<GraphQLTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<GraphQLTags, string>(Trace.Tags.GraphQLSource, t => t.Source, (t, v) => t.Source = v),
                new Property<GraphQLTags, string>(Trace.Tags.GraphQLOperationName, t => t.OperationName, (t, v) => t.OperationName = v),
                new Property<GraphQLTags, string>(Trace.Tags.GraphQLOperationType, t => t.OperationType, (t, v) => t.OperationType = v),
                new ReadOnlyProperty<GraphQLTags, string>(Trace.Tags.Language, t => t.Language));

        public override string SpanKind => SpanKinds.Server;

        public string InstrumentationName => GraphQLIntegration.IntegrationName;

        public string Language => TracerConstants.Language;

        public string Source { get; set; }

        public string OperationName { get; set; }

        public string OperationType { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => GraphQLTagsProperties;
    }
}
