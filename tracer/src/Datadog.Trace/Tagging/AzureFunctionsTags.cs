// <copyright file="AzureFunctionsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class AzureFunctionsTags : InstrumentationTags
    {
        private const string InstrumentationTagName = Trace.Tags.InstrumentationName;
        private const string LanguageTagName = Trace.Tags.Language;
        private const string ShortNameTagName = Trace.Tags.AzureFunctionName;
        private const string FullNameTagName = Trace.Tags.AzureFunctionMethod;
        private const string BindingSourceTagName = Trace.Tags.AzureFunctionBindingSource;
        private const string TriggerTypeTagName = Trace.Tags.AzureFunctionTriggerType;

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Server;

        [Tag(InstrumentationTagName)]
        public string InstrumentationName => nameof(Datadog.Trace.Configuration.IntegrationId.AzureFunctions);

        [Tag(LanguageTagName)]
        public string Language => TracerConstants.Language;

        [Tag(ShortNameTagName)]
        public string ShortName { get; set; }

        [Tag(FullNameTagName)]
        public string FullName { get; set; }

        [Tag(BindingSourceTagName)]
        public string BindingSource { get; set; }

        [Tag(TriggerTypeTagName)]
        public string TriggerType { get; set; } = "Unknown";

        /// <summary>
        /// Used to set the current tags on a given root span
        /// </summary>
        internal void SetRootTags(Span span)
        {
            span.SetTag(InstrumentationTagName, InstrumentationName);
            span.SetTag(LanguageTagName, Language);
            span.SetTag(ShortNameTagName, ShortName);
            span.SetTag(FullNameTagName, FullName);
            span.SetTag(BindingSourceTagName, BindingSource);
            span.SetTag(TriggerTypeTagName, TriggerType);
        }
    }
}
