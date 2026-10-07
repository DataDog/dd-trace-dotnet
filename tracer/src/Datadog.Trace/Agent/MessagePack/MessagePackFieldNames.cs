// <copyright file="MessagePackFieldNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Agent.MessagePack
{
    /// <summary>
    /// MessagePack protocol field names for span serialization.
    /// These constants are marked with [MessagePackField] to generate pre-serialized byte arrays.
    /// </summary>
    internal static class MessagePackFieldNames
    {
        // Span fields
        [MessagePackField]
        public const string TraceId = "trace_id";

        [MessagePackField]
        public const string TraceIdHigh = "trace_id_high";

        [MessagePackField]
        public const string SpanId = "span_id";

        [MessagePackField]
        public const string Name = "name";

        [MessagePackField]
        public const string Resource = "resource";

        [MessagePackField]
        public const string Service = "service";

        [MessagePackField]
        public const string Type = "type";

        [MessagePackField]
        public const string Start = "start";

        [MessagePackField]
        public const string Duration = "duration";

        [MessagePackField]
        public const string ParentId = "parent_id";

        [MessagePackField]
        public const string Error = "error";

        [MessagePackField]
        public const string MetaStruct = "meta_struct";

        [MessagePackField]
        public const string SpanLinks = "span_links";

        [MessagePackField]
        public const string TraceState = "tracestate";

        [MessagePackField]
        public const string TraceFlags = "flags";

        [MessagePackField]
        public const string Events = "events";

        [MessagePackField]
        public const string SpanEvents = "span_events";

        [MessagePackField]
        public const string TimeUnixNano = "time_unix_nano";

        [MessagePackField]
        public const string Attributes = "attributes";

        [MessagePackField]
        public const string TypeField = "type";

        [MessagePackField]
        public const string StringValueField = "string_value";

        [MessagePackField]
        public const string BoolValueField = "bool_value";

        [MessagePackField]
        public const string IntValueField = "int_value";

        [MessagePackField]
        public const string DoubleValueField = "double_value";

        [MessagePackField]
        public const string ArrayValueField = "array_value";

        [MessagePackField]
        public const string ValuesField = "values";

        [MessagePackField]
        public const string Meta = "meta";

        [MessagePackField]
        public const string Metrics = "metrics";

        // Tags.cs is also linked into projects without source generators.
        // Keep the generation attributes here while using the original tag values.
        [MessagePackField]
        public const string Env = Tags.Env;

        [MessagePackField]
        public const string Version = Tags.Version;

        [MessagePackField]
        public const string Language = Tags.Language;

        [MessagePackField]
        public const string GitCommitSha = Tags.GitCommitSha;

        [MessagePackField]
        public const string GitRepositoryUrl = Tags.GitRepositoryUrl;

        [MessagePackField]
        public const string RuntimeFamily = Tags.RuntimeFamily;

        [MessagePackField]
        public const string ProcessTags = Tags.ProcessTags;

        [MessagePackField]
        public const string AzureAppServicesResourceId = Tags.AzureAppServicesResourceId;

        [MessagePackField]
        public const string AzureAppServicesResourceGroup = Tags.AzureAppServicesResourceGroup;

        [MessagePackField]
        public const string AzureAppServicesSiteName = Tags.AzureAppServicesSiteName;

        [MessagePackField]
        public const string AzureAppServicesExtensionVersion = Tags.AzureAppServicesExtensionVersion;

        [MessagePackField]
        public const string AzureAppServicesInstanceName = Tags.AzureAppServicesInstanceName;

        [MessagePackField]
        public const string AzureAppServicesInstanceId = Tags.AzureAppServicesInstanceId;

        [MessagePackField]
        public const string AzureAppServicesOperatingSystem = Tags.AzureAppServicesOperatingSystem;

        [MessagePackField]
        public const string AzureAppServicesRuntime = Tags.AzureAppServicesRuntime;

        [MessagePackField]
        public const string AzureAppServicesSiteKind = Tags.AzureAppServicesSiteKind;

        [MessagePackField]
        public const string AzureAppServicesSiteType = Tags.AzureAppServicesSiteType;

        [MessagePackField]
        public const string AzureAppServicesSubscriptionId = Tags.AzureAppServicesSubscriptionId;

        [MessagePackField]
        public const string Origin = Tags.Origin;

        [MessagePackField]
        public const string AppSecRuleFileVersion = Tags.AppSecRuleFileVersion;

        [MessagePackField]
        public const string RuntimeId = Tags.RuntimeId;

        [MessagePackField]
        public const string BaseService = Tags.BaseService;

        [MessagePackField]
        public const string LastParentId = Tags.LastParentId;

        // Note: Cannot use [MessagePackField] on TracerConstants.Language directly because TracerConstants.cs
        // is linked/shared with dd_dotnet tool which doesn't have access to source generator infrastructure
        // We thus duplicate the value here.
        [MessagePackField]
        public const string DotnetLanguageValue = "dotnet";
    }
}
