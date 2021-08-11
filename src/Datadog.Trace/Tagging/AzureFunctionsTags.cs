// <copyright file="AzureFunctionsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class AzureFunctionsTags : InstrumentationTags
    {
        public static readonly IEnumerable<Property<AzureFunctionsTags, string>> AzureFunctionsExtraTags = new List<Property<AzureFunctionsTags, string>>()
        {
            new ReadOnlyProperty<AzureFunctionsTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
            new Property<AzureFunctionsTags, string>(Trace.Tags.AzureFunctionTriggerType, t => t.TriggerType, (t, v) => t.TriggerType = v),
            new Property<AzureFunctionsTags, string>(Trace.Tags.AzureFunctionName, t => t.ShortName, (t, v) => t.ShortName = v),
            new Property<AzureFunctionsTags, string>(Trace.Tags.AzureFunctionMethod, t => t.FullName, (t, v) => t.FullName = v),
            new Property<AzureFunctionsTags, string>(Trace.Tags.AzureFunctionBindingSource, t => t.BindingSource, (t, v) => t.BindingSource = v)
        };

        public static readonly IProperty<string>[] AzureFunctionRootTagsProperties =
            InstrumentationTagsProperties.Concat(
                new ReadOnlyProperty<AzureFunctionsTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<AzureFunctionsTags, string>(Trace.Tags.AzureFunctionTriggerType, t => t.TriggerType, (t, v) => t.TriggerType = v),
                new Property<AzureFunctionsTags, string>(Trace.Tags.AzureFunctionName, t => t.ShortName, (t, v) => t.ShortName = v),
                new Property<AzureFunctionsTags, string>(Trace.Tags.AzureFunctionMethod, t => t.FullName, (t, v) => t.FullName = v),
                new Property<AzureFunctionsTags, string>(Trace.Tags.AzureFunctionBindingSource, t => t.BindingSource, (t, v) => t.BindingSource = v));

        public AzureFunctionsTags()
        {
            SpanKind = SpanKinds.Server;
            TriggerType = "Unknown";
        }

        public string InstrumentationName => nameof(Datadog.Trace.Configuration.IntegrationIds.AzureFunctions);

        public override string SpanKind { get; }

        public string ShortName { get; set; }

        public string FullName { get; set; }

        public string BindingSource { get; set; }

        public string TriggerType { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => AzureFunctionRootTagsProperties;
    }
}
