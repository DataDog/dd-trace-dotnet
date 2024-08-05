// <copyright file="AzureFunctionsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.SourceGenerators;

namespace Datadog.Trace.Internal.Tagging
{
    internal partial class AzureFunctionsTags : InstrumentationTags
    {
        private const string ComponentName = nameof(Datadog.Trace.Internal.Configuration.IntegrationId.AzureFunctions);
        private const string ShortNameTagName = Trace.Internal.Tags.AzureFunctionName;
        private const string FullNameTagName = Trace.Internal.Tags.AzureFunctionMethod;
        private const string BindingSourceTagName = Trace.Internal.Tags.AzureFunctionBindingSource;
        private const string TriggerTypeTagName = Trace.Internal.Tags.AzureFunctionTriggerType;

        [Tag(Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Server;

        [Tag(Tags.InstrumentationName)]
        public string InstrumentationName => ComponentName;

        [Tag(ShortNameTagName)]
        public string ShortName { get; set; }

        [Tag(FullNameTagName)]
        public string FullName { get; set; }

        [Tag(BindingSourceTagName)]
        public string BindingSource { get; set; }

        [Tag(TriggerTypeTagName)]
        public string TriggerType { get; set; } = "Unknown";

        internal static void SetRootSpanTags(
            Span span,
            string shortName,
            string fullName,
            string bindingSource,
            string triggerType)
        {
            var tags = span.Tags;
            if (span.Tags is AspNetCoreTags aspNetTags)
            {
                aspNetTags.InstrumentationName = ComponentName;
            }
            else if (tags.GetTag(Tags.InstrumentationName) is { })
            {
                // not already set, so should be safe to set it as not readonly
                tags.SetTag(Tags.InstrumentationName, ComponentName);
            }

            tags.SetTag(ShortNameTagName, shortName);
            tags.SetTag(FullNameTagName, fullName);
            tags.SetTag(BindingSourceTagName, bindingSource);
            tags.SetTag(TriggerTypeTagName, triggerType);
        }
    }
}
