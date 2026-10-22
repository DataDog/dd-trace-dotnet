// <copyright file="AzureFunctionsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal sealed partial class AzureFunctionsTags : InstrumentationTags
    {
        private const string ComponentName = nameof(Datadog.Trace.Configuration.IntegrationId.AzureFunctions);

        // The tag key depends on the hosting environment, which is only known at runtime, so these
        // can't be [Tag] attributes (those require a compile-time constant key). They flow through
        // the base tag dictionary instead. Selecting a key is a reference copy to an interned
        // constant - nothing is built per instance on the span-creation path.
        private readonly string _shortNameTagName;
        private readonly string _fullNameTagName;
        private readonly string _bindingSourceTagName;
        private readonly string _triggerTypeTagName;

        public AzureFunctionsTags(bool isContainerApps)
        {
            _shortNameTagName = isContainerApps ? Trace.Tags.AzureContainerAppFunctionName : Trace.Tags.AzureFunctionName;
            _fullNameTagName = isContainerApps ? Trace.Tags.AzureContainerAppFunctionMethod : Trace.Tags.AzureFunctionMethod;
            _bindingSourceTagName = isContainerApps ? Trace.Tags.AzureContainerAppFunctionBindingSource : Trace.Tags.AzureFunctionBindingSource;
            _triggerTypeTagName = isContainerApps ? Trace.Tags.AzureContainerAppFunctionTriggerType : Trace.Tags.AzureFunctionTriggerType;

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

            // Applies to any ITags, AzureFunctionsTags included: its typed properties are backed by
            // the same tag-dictionary entries these keys address, so there is no need to special
            // case it - and no second source of truth for the prefix.
            tags.SetTag(isContainerApps ? Trace.Tags.AzureContainerAppFunctionName : Trace.Tags.AzureFunctionName, shortName);
            tags.SetTag(isContainerApps ? Trace.Tags.AzureContainerAppFunctionMethod : Trace.Tags.AzureFunctionMethod, fullName);
            tags.SetTag(isContainerApps ? Trace.Tags.AzureContainerAppFunctionBindingSource : Trace.Tags.AzureFunctionBindingSource, bindingSource);
            tags.SetTag(isContainerApps ? Trace.Tags.AzureContainerAppFunctionTriggerType : Trace.Tags.AzureFunctionTriggerType, triggerType);
        }
    }
}
