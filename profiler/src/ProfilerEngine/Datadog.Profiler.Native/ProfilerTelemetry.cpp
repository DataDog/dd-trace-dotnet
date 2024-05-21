// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProfileExporter.h"
#include "ProfilerTelemetry.h"
#include "Log.h"

#include "FfiHelper.h"
extern "C"
{
#include "datadog/common.h"
#include "datadog/telemetry.h"
}

ProfilerTelemetry::ProfilerTelemetry(IConfiguration* pConfiguration)
   : _pConfiguration(pConfiguration)
{
    _pExporter = nullptr;
}

void ProfilerTelemetry::SetExporter(IExporter* pExporter)
{
    _pExporter = pExporter;
}

std::string ProfilerTelemetry::GetDeploymentModeTag()
{
    return _isSsiDeployed ? "ssi" : "manual";
}

std::string ProfilerTelemetry::GetHeuristicTag(SkipProfileHeuristicType heuristics)
{
    std::string tags;
    if (heuristics == SkipProfileHeuristicType::AllTriggered)
    {
        return "AllTriggered";
    }

    if ((heuristics & SkipProfileHeuristicType::ShortLived) == SkipProfileHeuristicType::ShortLived)
    {
        tags = "ShortLived";
    }

    if ((heuristics & SkipProfileHeuristicType::NoSpan) == SkipProfileHeuristicType::NoSpan)
    {
        if (!tags.empty())
        {
            tags += " | ";
        }

        tags += "NoSpan";
    }

    return tags;
}

void ProfilerTelemetry::ProcessStart(DeploymentMode deployment)
{
    _isSsiDeployed = (deployment == DeploymentMode::SingleStepInstrumentation);

    Log::Debug("ProcessStart(", GetDeploymentModeTag(), ")");
}

void ProfilerTelemetry::ProcessEnd(uint64_t duration, uint64_t sentProfiles, SkipProfileHeuristicType heuristics)
{
    // provides:
    // - enablement choice (manual or SSI)
    auto enablementChoice = GetDeploymentModeTag();
    // - duration of the process
    // - heuristics that were not triggered
    auto skippedHeuristics = GetHeuristicTag(heuristics);
    Log::Debug("ProcessEnd(", enablementChoice, ", ", duration, ", ", sentProfiles, ", ", skippedHeuristics, ")");

    if (_pExporter != nullptr)
    {
        // could have been done in the exporter
        // _pExporter->SendProcessSsiMetrics(duration, _isSsiDeployed, heuristics);
    }
}



#define TRY(expr)                                                                                  \
  {                                                                                                \
    ddog_MaybeError err = expr;                                                                    \
    if (err.tag == DDOG_OPTION_ERROR_SOME_ERROR) {                                               \
      fprintf(stderr, "ERROR: %.*s", (int)err.some.message.len, (char *)err.some.message.ptr);                     \
      return;                                                                                    \
    }                                                                                              \
  }


void ProfilerTelemetry::SendMetrics(
    uint64_t duration,
    uint64_t sentProfiles,
    SkipProfileHeuristicType heuristics)
{
    // setup builder
    ddog_TelemetryWorkerBuilder* builder;
    auto serviceName = _pConfiguration->GetServiceName();
    ddog_CharSlice service = DDOG_CHARSLICE_C_BARE("service name"); //

    ddog_CharSlice lang = DDOG_CHARSLICE_C_BARE("dotnet");          // ProfileExporter::LanguageFamily
    ddog_CharSlice lang_version = DDOG_CHARSLICE_C_BARE("8.0");     // use IRuntimeInfo.GetDotnet(Major/Minor)Version
    ddog_CharSlice tracer_version = DDOG_CHARSLICE_C_BARE("0.0.0"); // ProfileExporter::LibraryVersion

    TRY(ddog_telemetry_builder_instantiate(&builder, service, lang, lang_version, tracer_version));

    // TODO: what is this url?
    ddog_CharSlice endpoint_char = DDOG_CHARSLICE_C_BARE("url de l'agent");
    struct ddog_Endpoint* endpoint = ddog_endpoint_from_url(endpoint_char);
    TRY(ddog_telemetry_builder_with_endpoint_config_endpoint(builder, endpoint));
    ddog_endpoint_drop(endpoint);

    // other builder configuration
    // TODO: in the IIS case, we might have more than one runtimeID: do we need to use the
    // Is it possible to use ApplicationStore to enumerate the runtimeIDs?
    // --> it would be easier to move all the telemetry logic to the exporter + ApplicationStore access will be lock protected
    ddog_CharSlice runtime_id = DDOG_CHARSLICE_C_BARE("fa1f0ed0-8a3a-49e8-8f23-46fb44e24579");
    ddog_CharSlice service_version = DDOG_CHARSLICE_C_BARE("DD_VERSION");
    ddog_CharSlice env = DDOG_CHARSLICE_C_BARE("DD_ENV");

    TRY(ddog_telemetry_builder_with_str_runtime_id(builder, runtime_id));
    TRY(ddog_telemetry_builder_with_str_application_service_version(builder, service_version));
    TRY(ddog_telemetry_builder_with_str_application_env(builder, env));

    // start the telemetry worker
    ddog_TelemetryWorkerHandle* handle;
    // builder is consummed after the call to build
    TRY(ddog_telemetry_builder_run_metric_logs(builder, &handle));
    TRY(ddog_telemetry_handle_start(handle));

    // metric definition
    ddog_CharSlice metric_name = DDOG_CHARSLICE_C_BARE("ssi_heuristic.number_of_profiles");
    auto enablementChoice = GetDeploymentModeTag();
    ddog_Vec_Tag tags = ddog_Vec_Tag_new();
    auto res = ddog_Vec_Tag_push(&tags,
        libdatadog::FfiHelper::StringToCharSlice(std::string("enablement_choice")),
        libdatadog::FfiHelper::StringToCharSlice(enablementChoice)
        );

    if (res.tag == DDOG_VEC_TAG_PUSH_RESULT_ERR)
    {
        auto error = libdatadog::make_error(res.err);
        Log::Debug("Failed to add label 'enablement_choice' with value '", enablementChoice, "'. Reason: ", error.message());
    }

    res = ddog_Vec_Tag_push(&tags, DDOG_CHARSLICE_C_BARE("has_sent_profiles"), DDOG_CHARSLICE_C_BARE("???"));

    if (res.tag == DDOG_VEC_TAG_PUSH_RESULT_ERR)
    {
        auto error = libdatadog::make_error(res.err);
        Log::Debug("Failed to add label 'has_sent_profiles' with value '???'. Reason: ", error.message());
    }

    res = ddog_Vec_Tag_push(&tags, DDOG_CHARSLICE_C_BARE("heuristic_hypothetical_decision"), DDOG_CHARSLICE_C_BARE("???"));

    if (res.tag == DDOG_VEC_TAG_PUSH_RESULT_ERR)
    {
        auto error = libdatadog::make_error(res.err);
        Log::Debug("Failed to add label 'heuristic_hypothetical_decision' with value '???'. Reason: ", error.message());
    }

    // tags is consummed
    struct ddog_ContextKey test_temetry = ddog_telemetry_handle_register_metric_context(
        handle, metric_name, DDOG_METRIC_TYPE_COUNT, tags, true, DDOG_METRIC_NAMESPACE_PROFILERS);

    TRY(ddog_telemetry_handle_add_point(handle, &test_temetry, sentProfiles));

    // stop the telemetry worker
    TRY(ddog_telemetry_handle_stop(handle));
    ddog_telemetry_handle_wait_for_shutdown_ms(handle, 10);
}