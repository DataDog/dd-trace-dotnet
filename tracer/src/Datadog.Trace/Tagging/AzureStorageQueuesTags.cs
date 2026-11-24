// <copyright file="AzureStorageQueuesTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal sealed partial class AzureStorageQueuesTags : InstrumentationTags
    {
        private string _spanKind;

        // Required by TagsListTests.CheckProperties, which instantiates every tags type via Activator
        public AzureStorageQueuesTags()
            : this(SpanKinds.Producer)
        {
        }

        public AzureStorageQueuesTags(string spanKind)
        {
            _spanKind = spanKind;
        }

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => nameof(Configuration.IntegrationId.AzureStorageQueues);

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => _spanKind;

        [Tag(Tags.MessagingSystem)]
        public string? MessagingSystem { get; set; }

        [Tag(Tags.MessagingOperation)]
        public string? MessagingOperation { get; set; }

        [Tag(Trace.Tags.MessagingDestinationName)]
        public string? MessagingDestinationName { get; set; }

        [Metric(Trace.Metrics.MessageQueueTimeMs)]
        public double? MessageQueueTimeMs { get; set; }
    }
}
