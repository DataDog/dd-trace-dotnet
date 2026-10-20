// <copyright file="AzureFunctionsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal sealed partial class AzureFunctionsTags : InstrumentationTags
    {
        private const string AasPrefix = "aas.";
        private const string AcaPrefix = "aca.";
        private const string ComponentName = nameof(Datadog.Trace.Configuration.IntegrationId.AzureFunctions);
        // Tag keys carry an environment-dependent prefix, so they can't be [Tag] attributes
        // (those require compile-time constants). These flow through the base tag dictionary.
        private readonly string _shortNameTagName;
        private readonly string _fullNameTagName;
        private readonly string _bindingSourceTagName;
        private readonly string _triggerTypeTagName;

        public AzureFunctionsTags(bool isContainerApps)
        {
            var prefix = isContainerApps ? AcaPrefix : AasPrefix;

            _shortNameTagName = prefix + Trace.Tags.AzureFunctionName;
            _fullNameTagName = prefix + Trace.Tags.AzureFunctionMethod;
            _bindingSourceTagName = prefix + Trace.Tags.AzureFunctionBindingSource;
            _triggerTypeTagName = prefix + Trace.Tags.AzureFunctionTriggerType;

            TriggerType = "Unknown";
        }

        [Tag(Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Server;

        [Tag(Tags.InstrumentationName)]
        public string InstrumentationName => ComponentName;

        public string ShortName
        {
            get => GetTag(_shortNameTagName);
            set => SetTag(_shortNameTagName, value);
        }

        public string FullName
        {
            get => GetTag(_fullNameTagName);
            set => SetTag(_fullNameTagName, value);
        }

        public string BindingSource
        {
            get => GetTag(_bindingSourceTagName);
            set => SetTag(_bindingSourceTagName, value);
        }

        public string TriggerType
        {
            get => GetTag(_triggerTypeTagName);
            set => SetTag(_triggerTypeTagName, value);
        }

        internal static void SetRootSpanTags(
            ITags tags,
            bool isContainerApps,
            string shortName,
            string fullName,
            string bindingSource,
            string triggerType)
        {
            if (tags is AspNetCoreTags aspNetTags)
            {
                aspNetTags.InstrumentationName = ComponentName;
            }
            else if (tags.GetTag(Tags.InstrumentationName) is null)
            {
                // not already set, so should be safe to set it as not readonly
                tags.SetTag(Tags.InstrumentationName, ComponentName);
            }

            if (tags is AzureFunctionsTags azureFunctionsTags)
            {
                azureFunctionsTags.ShortName = shortName;
                azureFunctionsTags.FullName = fullName;
                azureFunctionsTags.BindingSource = bindingSource;
                azureFunctionsTags.TriggerType = triggerType;
            }
            else
            {
                var prefix = isContainerApps ? AcaPrefix : AasPrefix;

                tags.SetTag(prefix + Trace.Tags.AzureFunctionName, shortName);
                tags.SetTag(prefix + Trace.Tags.AzureFunctionMethod, fullName);
                tags.SetTag(prefix + Trace.Tags.AzureFunctionBindingSource, bindingSource);
                tags.SetTag(prefix + Trace.Tags.AzureFunctionTriggerType, triggerType);
            }
        }
    }
}
