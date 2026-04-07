// <copyright file="PublicApiUsage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Telemetry.Metrics;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line", Justification = "It's easier to read")]
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "It's easier to read")]
[EnumExtensions]
internal enum PublicApiUsage
{
    [Description("name:eventtrackingsdk_trackcustomevent")]EventTrackingSdk_TrackCustomEvent,
    [Description("name:eventtrackingsdk_trackcustomevent_metadata")]EventTrackingSdk_TrackCustomEvent_Metadata,
    [Description("name:eventtrackingsdk_trackuserloginfailureevent")]EventTrackingSdk_TrackUserLoginFailureEvent,
    [Description("name:eventtrackingsdk_trackuserloginfailureevent_metadata")]EventTrackingSdk_TrackUserLoginFailureEvent_Metadata,
    [Description("name:eventtrackingsdk_trackuserloginsuccessevent")]EventTrackingSdk_TrackUserLoginSuccessEvent,
    [Description("name:eventtrackingsdk_trackuserloginsuccessevent_metadata")]EventTrackingSdk_TrackUserLoginSuccessEvent_Metadata,

    [Description("name:eventtrackingsdkv2_trackuserloginsuccess_userid")]EventTrackingSdkV2_TrackUserLoginSuccess_UserId,
    [Description("name:eventtrackingsdkv2_trackuserloginsuccess_userdetails")]EventTrackingSdkV2_TrackUserLoginSuccess_UserDetails,
    [Description("name:eventtrackingsdkv2_trackuserloginfailure_userid")]EventTrackingSdkV2_TrackUserLoginFailure_UserId,
    [Description("name:eventtrackingsdkv2_trackuserloginfailure_userdetails")]EventTrackingSdkV2_TrackUserLoginFailure_UserDetails,

    [Description("name:spancontextextractor_extract")] SpanContextExtractor_Extract,

    [Description("name:spancontextextractor_extractincludingdsm")] SpanContextExtractor_ExtractIncludingDsm,
    [Description("name:spancontextextractor_ctor")] SpanContextExtractor_Ctor,

    [Description("name:spancontextinjector_injectincludingdsm")] SpanContextInjector_InjectIncludingDsm,
    [Description("name:spancontextinjector_inject")] SpanContextInjector_Inject,
    [Description("name:spancontextinjector_ctor")] SpanContextInjector_Ctor,

    [Description("name:spanextensions_setuser")] SpanExtensions_SetUser,
    [Description("name:spanextensions_settag")] SpanExtensions_SetTag,
    [Description("name:spanextensions_settracesamplingpriority")] SpanExtensions_SetTraceSamplingPriority,

    [Description("name:tracer_configure")] Tracer_Configure,
    [Description("name:tracer_forceflushasync")] Tracer_ForceFlushAsync,
    [Description("name:tracer_startactive")] Tracer_StartActive,
    [Description("name:tracer_startactive_settings")] Tracer_StartActive_Settings,

    [Description("name:exportersettings_agenturi_get")] ExporterSettings_AgentUri_Get,
    [Description("name:exportersettings_agenturi_set")] ExporterSettings_AgentUri_Set,

    [Description("name:globalsettings_setdebugenabled")] GlobalSettings_SetDebugEnabled,

    [Description("name:immutableexportersettings_agenturi_get")] ImmutableExporterSettings_AgentUri_Get,

    [Description("name:integrationsettings_analyticsenabled_get")] IntegrationSettings_AnalyticsEnabled_Get,
    [Description("name:integrationsettings_analyticsenabled_set")] IntegrationSettings_AnalyticsEnabled_Set,
    [Description("name:integrationsettings_analyticssamplerate_get")] IntegrationSettings_AnalyticsSampleRate_Get,
    [Description("name:integrationsettings_analyticssamplerate_set")] IntegrationSettings_AnalyticsSampleRate_Set,
    [Description("name:integrationsettings_enabled_get")] IntegrationSettings_Enabled_Get,
    [Description("name:integrationsettings_enabled_set")] IntegrationSettings_Enabled_Set,
    [Description("name:integrationsettings_integrationname_get")] IntegrationSettings_IntegrationName_Get,

    [Description("name:integrationsettingscollection_indexer_name")] IntegrationSettingsCollection_Indexer_Name,

    [Description("name:immutableintegrationsettings_analyticsenabled_get")] ImmutableIntegrationSettings_AnalyticsEnabled_Get,
    [Description("name:immutableintegrationsettings_analyticssamplerate_get")] ImmutableIntegrationSettings_AnalyticsSampleRate_Get,
    [Description("name:immutableintegrationsettings_enabled_get")] ImmutableIntegrationSettings_Enabled_Get,
    [Description("name:immutableintegrationsettings_integrationname_get")] ImmutableIntegrationSettings_IntegrationName_Get,
    [Description("name:immutableintegrationsettingscollection_indexer_name")] ImmutableIntegrationSettingsCollection_Indexer_Name,

    [Description("name:tracersettings_ctor")] TracerSettings_Ctor,
    [Description("name:tracersettings_ctor_usedefaultsources")] TracerSettings_Ctor_UseDefaultSources,
    [Description("name:tracersettings_analyticsenabled_get")] TracerSettings_AnalyticsEnabled_Get,
    [Description("name:tracersettings_analyticsenabled_set")] TracerSettings_AnalyticsEnabled_Set,
    [Description("name:tracersettings_customsamplingrules_get")] TracerSettings_CustomSamplingRules_Get,
    [Description("name:tracersettings_customsamplingrules_set")] TracerSettings_CustomSamplingRules_Set,
    [Description("name:tracersettings_diagnosticsourceenabled_get")] TracerSettings_DiagnosticSourceEnabled_Get,
    [Description("name:tracersettings_diagnosticsourceenabled_set")] TracerSettings_DiagnosticSourceEnabled_Set,
    [Description("name:tracersettings_disabledintegrationnames_get")] TracerSettings_DisabledIntegrationNames_Get,
    [Description("name:tracersettings_disabledintegrationnames_set")] TracerSettings_DisabledIntegrationNames_Set,
    [Description("name:tracersettings_environment_get")] TracerSettings_Environment_Get,
    [Description("name:tracersettings_environment_set")] TracerSettings_Environment_Set,
    [Description("name:tracersettings_exporter_get")] TracerSettings_Exporter_Get,
    [Description("name:tracersettings_exporter_set")] TracerSettings_Exporter_Set,
    [Description("name:tracersettings_globalsamplingrate_get")] TracerSettings_GlobalSamplingRate_Get,
    [Description("name:tracersettings_globalsamplingrate_set")] TracerSettings_GlobalSamplingRate_Set,
    [Description("name:tracersettings_globaltags_get")] TracerSettings_GlobalTags_Get,
    [Description("name:tracersettings_globaltags_set")] TracerSettings_GlobalTags_Set,
    [Description("name:tracersettings_grpctags_get")] TracerSettings_GrpcTags_Get,
    [Description("name:tracersettings_grpctags_set")] TracerSettings_GrpcTags_Set,
    [Description("name:tracersettings_headertags_get")] TracerSettings_HeaderTags_Get,
    [Description("name:tracersettings_headertags_set")] TracerSettings_HeaderTags_Set,
    [Description("name:tracersettings_integrations_get")] TracerSettings_Integrations_Get,
    [Description("name:tracersettings_kafkacreateconsumerscopeenabled_get")] TracerSettings_KafkaCreateConsumerScopeEnabled_Get,
    [Description("name:tracersettings_kafkacreateconsumerscopeenabled_set")] TracerSettings_KafkaCreateConsumerScopeEnabled_Set,
    [Description("name:tracersettings_logsinjectionenabled_get")] TracerSettings_LogsInjectionEnabled_Get,
    [Description("name:tracersettings_logsinjectionenabled_set")] TracerSettings_LogsInjectionEnabled_Set,
    [Description("name:tracersettings_maxtracessubmittedpersecond_get")] TracerSettings_MaxTracesSubmittedPerSecond_Get,
    [Description("name:tracersettings_maxtracessubmittedpersecond_set")] TracerSettings_MaxTracesSubmittedPerSecond_Set,
    [Description("name:tracersettings_servicename_get")] TracerSettings_ServiceName_Get,
    [Description("name:tracersettings_servicename_set")] TracerSettings_ServiceName_Set,
    [Description("name:tracersettings_serviceversion_get")] TracerSettings_ServiceVersion_Get,
    [Description("name:tracersettings_serviceversion_set")] TracerSettings_ServiceVersion_Set,
    [Description("name:tracersettings_startupdiagnosticlogenabled_get")] TracerSettings_StartupDiagnosticLogEnabled_Get,
    [Description("name:tracersettings_startupdiagnosticlogenabled_set")] TracerSettings_StartupDiagnosticLogEnabled_Set,
    [Description("name:tracersettings_statscomputationenabled_get")] TracerSettings_StatsComputationEnabled_Get,
    [Description("name:tracersettings_statscomputationenabled_set")] TracerSettings_StatsComputationEnabled_Set,
    [Description("name:tracersettings_traceenabled_get")] TracerSettings_TraceEnabled_Get,
    [Description("name:tracersettings_traceenabled_set")] TracerSettings_TraceEnabled_Set,
    [Description("name:tracersettings_tracermetricsenabled_get")] TracerSettings_TracerMetricsEnabled_Get,
    [Description("name:tracersettings_tracermetricsenabled_set")] TracerSettings_TracerMetricsEnabled_Set,
    [Description("name:tracersettings_sethttpclienterrorstatuscodes")] TracerSettings_SetHttpClientErrorStatusCodes,
    [Description("name:tracersettings_sethttpservererrorstatuscodes")] TracerSettings_SetHttpServerErrorStatusCodes,
    [Description("name:tracersettings_setservicenamemappings")] TracerSettings_SetServiceNameMappings,
    [Description("name:tracersettings_fromdefaultsources")] TracerSettings_FromDefaultSources,

    [Description("name:immutabletracersettings_analyticsenabled_get")] ImmutableTracerSettings_AnalyticsEnabled_Get,
    [Description("name:immutabletracersettings_customsamplingrules_get")] ImmutableTracerSettings_CustomSamplingRules_Get,
    [Description("name:immutabletracersettings_environment_get")] ImmutableTracerSettings_Environment_Get,
    [Description("name:immutabletracersettings_exporter_get")] ImmutableTracerSettings_Exporter_Get,
    [Description("name:immutabletracersettings_globalsamplingrate_get")] ImmutableTracerSettings_GlobalSamplingRate_Get,
    [Description("name:immutabletracersettings_globaltags_get")] ImmutableTracerSettings_GlobalTags_Get,
    [Description("name:immutabletracersettings_grpctags_get")] ImmutableTracerSettings_GrpcTags_Get,
    [Description("name:immutabletracersettings_headertags_get")] ImmutableTracerSettings_HeaderTags_Get,
    [Description("name:immutabletracersettings_integrations_get")] ImmutableTracerSettings_Integrations_Get,
    [Description("name:immutabletracersettings_kafkacreateconsumerscopeenabled_get")] ImmutableTracerSettings_KafkaCreateConsumerScopeEnabled_Get,
    [Description("name:immutabletracersettings_logsinjectionenabled_get")] ImmutableTracerSettings_LogsInjectionEnabled_Get,
    [Description("name:immutabletracersettings_maxtracessubmittedpersecond_get")] ImmutableTracerSettings_MaxTracesSubmittedPerSecond_Get,
    [Description("name:immutabletracersettings_servicename_get")] ImmutableTracerSettings_ServiceName_Get,
    [Description("name:immutabletracersettings_serviceversion_get")] ImmutableTracerSettings_ServiceVersion_Get,
    [Description("name:immutabletracersettings_startupdiagnosticlogenabled_get")] ImmutableTracerSettings_StartupDiagnosticLogEnabled_Get,
    [Description("name:immutabletracersettings_statscomputationenabled_get")] ImmutableTracerSettings_StatsComputationEnabled_Get,
    [Description("name:immutabletracersettings_traceenabled_get")] ImmutableTracerSettings_TraceEnabled_Get,
    [Description("name:immutabletracersettings_tracermetricsenabled_get")] ImmutableTracerSettings_TracerMetricsEnabled_Get,

    [Description("name:opentracingtracerfactory_createtracer")] OpenTracingTracerFactory_CreateTracer,
    [Description("name:opentracingtracerfactory_wraptracer")] OpenTracingTracerFactory_WrapTracer,
}
