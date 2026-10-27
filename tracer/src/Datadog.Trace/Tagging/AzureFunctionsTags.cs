// <copyright file="AzureFunctionsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging
{
    internal sealed partial class AzureFunctionsTags : InstrumentationTags
    {
        private const string ComponentName = nameof(Datadog.Trace.Configuration.IntegrationId.AzureFunctions);

        private readonly bool _isContainerApps;

        [TestingOnly]
        [Obsolete("Use the constructor that takes isContainerApps. This exists only so the type can be created reflectively.")]
        public AzureFunctionsTags()
            : this(isContainerApps: false)
        {
        }

        public AzureFunctionsTags(bool isContainerApps)
        {
            _isContainerApps = isContainerApps;
            TriggerType = "Unknown";
        }

        [Tag(Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Server;

        [Tag(Tags.InstrumentationName)]
        public string InstrumentationName => ComponentName;

        // Storage for both hosting environments. Exactly one set is ever populated - the hosting
        // environment is fixed for the life of the process - and the generated EnumerateTags skips
        // null tags, so the unused half costs a single null check and never reaches the wire.
        // Keeping the keys as [Tag] attributes preserves the source-generated, pre-serialized
        // MessagePack keys and the typed (lock-free) storage.
        [Tag(Trace.Tags.AzureFunctionName)]
        public string AasShortName { get; set; }

        [Tag(Trace.Tags.AzureFunctionMethod)]
        public string AasFullName { get; set; }

        [Tag(Trace.Tags.AzureFunctionBindingSource)]
        public string AasBindingSource { get; set; }

        [Tag(Trace.Tags.AzureFunctionTriggerType)]
        public string AasTriggerType { get; set; }

        [Tag(Trace.Tags.AzureContainerAppFunctionName)]
        public string AcaShortName { get; set; }

        [Tag(Trace.Tags.AzureContainerAppFunctionMethod)]
        public string AcaFullName { get; set; }

        [Tag(Trace.Tags.AzureContainerAppFunctionBindingSource)]
        public string AcaBindingSource { get; set; }

        [Tag(Trace.Tags.AzureContainerAppFunctionTriggerType)]
        public string AcaTriggerType { get; set; }

        // Facade over whichever set this instance targets, so callers don't need to know.
        internal string ShortName
        {
            get => _isContainerApps ? AcaShortName : AasShortName;

            set
            {
                if (_isContainerApps)
                {
                    AcaShortName = value;
                }
                else
                {
                    AasShortName = value;
                }
            }
        }

        internal string FullName
        {
            get => _isContainerApps ? AcaFullName : AasFullName;

            set
            {
                if (_isContainerApps)
                {
                    AcaFullName = value;
                }
                else
                {
                    AasFullName = value;
                }
            }
        }

        internal string BindingSource
        {
            get => _isContainerApps ? AcaBindingSource : AasBindingSource;

            set
            {
                if (_isContainerApps)
                {
                    AcaBindingSource = value;
                }
                else
                {
                    AasBindingSource = value;
                }
            }
        }

        internal string TriggerType
        {
            get => _isContainerApps ? AcaTriggerType : AasTriggerType;

            set
            {
                if (_isContainerApps)
                {
                    AcaTriggerType = value;
                }
                else
                {
                    AasTriggerType = value;
                }
            }
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

            // Works for any ITags: on AzureFunctionsTags the generated SetTag routes these keys to
            // the matching typed property, and anything else stores them in the tag dictionary.
            tags.SetTag(isContainerApps ? Trace.Tags.AzureContainerAppFunctionName : Trace.Tags.AzureFunctionName, shortName);
            tags.SetTag(isContainerApps ? Trace.Tags.AzureContainerAppFunctionMethod : Trace.Tags.AzureFunctionMethod, fullName);
            tags.SetTag(isContainerApps ? Trace.Tags.AzureContainerAppFunctionBindingSource : Trace.Tags.AzureFunctionBindingSource, bindingSource);
            tags.SetTag(isContainerApps ? Trace.Tags.AzureContainerAppFunctionTriggerType : Trace.Tags.AzureFunctionTriggerType, triggerType);
        }
    }
}
