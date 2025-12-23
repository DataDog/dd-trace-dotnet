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
        private const string ShortNameTagName = Trace.Tags.AzureFunctionName;
        private const string FullNameTagName = Trace.Tags.AzureFunctionMethod;
        private const string BindingSourceTagName = Trace.Tags.AzureFunctionBindingSource;
        private const string TriggerTypeTagName = Trace.Tags.AzureFunctionTriggerType;
        private const string ExtensionVersionTagName = Trace.Tags.AzureFunctionExtensionVersion;
        private const string WorkerRuntimeTagName = Trace.Tags.AzureFunctionWorkerRuntime;

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

        [Tag(ExtensionVersionTagName)]
        public string ExtensionVersion { get; set; }

        [Tag(WorkerRuntimeTagName)]
        public string WorkerRuntime { get; set; }

        internal static void SetRootSpanTags(
            Span span,
            string shortName,
            string fullName,
            string bindingSource,
            string triggerType,
            string extensionVersion,
            string workerRuntime)
        {
            var tags = span.Tags;

            tags.SetTag(ShortNameTagName, shortName);
            tags.SetTag(FullNameTagName, fullName);
            tags.SetTag(BindingSourceTagName, bindingSource);
            tags.SetTag(TriggerTypeTagName, triggerType);
            tags.SetTag(ExtensionVersionTagName, extensionVersion);
            tags.SetTag(WorkerRuntimeTagName, workerRuntime);
        }
    }
}
