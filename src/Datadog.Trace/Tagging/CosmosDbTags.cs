// <copyright file="CosmosDbTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
                new Property<CosmosDbTags, string>(Trace.Tags.DbType, t => t.DbType, (t, v) => t.DbType = v),
                new Property<CosmosDbTags, string>(Trace.Tags.DbName, t => t.DatabaseId, (t, v) => t.DatabaseId = v),
                new Property<CosmosDbTags, string>(Trace.Tags.CosmosDbContainer, t => t.ContainerId, (t, v) => t.ContainerId = v),
                new Property<CosmosDbTags, string>(Trace.Tags.OutHost, t => t.Host, (t, v) => t.Host = v));

        public override string SpanKind => SpanKinds.Client;

        public string InstrumentationName => nameof(IntegrationIds.CosmosDb);

        public string DbType { get; set; }

        public string ContainerId { get; set; }

        public string DatabaseId { get; set; }

        public string Host { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => CosmosDbTagsProperties;
    }
}
