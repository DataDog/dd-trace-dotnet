// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TelemetryMetricsWorker.h"
#include "IConfiguration.h"
#include "Log.h"
#include "ProfileExporter.h"

#include "FfiHelper.h"
extern "C"
{
#include "datadog/common.h"
#include "datadog/telemetry.h"
}

namespace libdatadog {

TelemetryMetricsWorker::TelemetryMetricsWorker()
{
    _pHandle = nullptr;
}

TelemetryMetricsWorker::~TelemetryMetricsWorker()
{
    if (_pHandle == nullptr)
    {
        return;
    }

    Stop();
}

void TelemetryMetricsWorker::Stop()
{
    // stop the worker if needed
    if (_pHandle == nullptr)
    {
        return;
    }

    ddog_MaybeError result = ddog_telemetry_handle_stop(_pHandle);
    if (result.tag == DDOG_OPTION_VEC_U8_SOME_VEC_U8)
    {
        Log::Error("Failed to stop telemetry worker for (", _serviceName, "): ", std::string((char*)result.some.ptr, result.some.len));
        return;
    }

    ddog_telemetry_handle_wait_for_shutdown_ms(_pHandle, 10);

    _pHandle = nullptr;
}

bool TelemetryMetricsWorker::Start(
    IConfiguration* pConfiguration,
    const std::string& serviceName,
    const std::string& serviceVersion,
    const std::string& language,
    const std::string& languageVersion,
    const std::string& libraryVersion,
    const std::string& agentUrl,
    const std::string& runtimeId,
    const std::string& environment
)
{
    _serviceName = serviceName;

    if (!pConfiguration->IsSsiDeployed())
    {
        Log::Error("No telemetry worker for (", serviceName, ") should be started if not deployed via Single Step Instrumentation");
        return false;
    }

    ddog_TelemetryWorkerBuilder* builder;
    ddog_CharSlice service = FfiHelper::StringToCharSlice(serviceName);
    ddog_CharSlice lang = FfiHelper::StringToCharSlice(language);                   // ProfileExporter::LanguageFamily
    ddog_CharSlice lang_version = FfiHelper::StringToCharSlice(languageVersion);    // use IRuntimeInfo.GetDotnet(Major/Minor)Version
    ddog_CharSlice tracer_version = FfiHelper::StringToCharSlice(libraryVersion);   // ProfileExporter::LibraryVersion

    ddog_MaybeError result = ddog_telemetry_builder_instantiate(&builder, service, lang, lang_version, tracer_version);
    if (result.tag == DDOG_OPTION_VEC_U8_SOME_VEC_U8)  // TODO: create a macro for these similar repeated check/log
    {
        Log::Error("Failed to instantiate telemetry builder for (", serviceName, "): ", std::string((char*)result.some.ptr, result.some.len));
        return false;
    }

    std::string agentEndpoint = ProfileExporter::BuildAgentEndpoint(pConfiguration);
    ddog_CharSlice endpoint_char = FfiHelper::StringToCharSlice(agentEndpoint);
    struct ddog_Endpoint* endpoint = ddog_endpoint_from_url(endpoint_char);
    result = ddog_telemetry_builder_with_endpoint_config_endpoint(builder, endpoint);
    if (result.tag == DDOG_OPTION_VEC_U8_SOME_VEC_U8)
    {
        Log::Error("Failed to configure telemetry builder agent endpoint: ", std::string((char*)result.some.ptr, result.some.len));
        return false;
    }
    ddog_endpoint_drop(endpoint);

    // other builder configuration
    ddog_CharSlice runtime_id = FfiHelper::StringToCharSlice(runtimeId);
    result = ddog_telemetry_builder_with_str_runtime_id(builder, runtime_id);
    if (result.tag == DDOG_OPTION_VEC_U8_SOME_VEC_U8)
    {
        Log::Error("Failed to set telemetry builder runtime ID for (", serviceName, "): ", std::string((char*)result.some.ptr, result.some.len));
        return false;
    }

    // these are optional
    if (!serviceVersion.empty())
    {
        ddog_CharSlice service_version = FfiHelper::StringToCharSlice(serviceVersion);
        result = ddog_telemetry_builder_with_str_application_service_version(builder, service_version);
        if (result.tag == DDOG_OPTION_VEC_U8_SOME_VEC_U8)
        {
            Log::Error("Failed to set telemetry builder service version for (", serviceName, "): ", std::string((char*)result.some.ptr, result.some.len));
            return false;
        }
    }
    if (!environment.empty())
    {
        ddog_CharSlice env = FfiHelper::StringToCharSlice(environment);
        result = ddog_telemetry_builder_with_str_application_env(builder, env);
        if (result.tag == DDOG_OPTION_VEC_U8_SOME_VEC_U8)
        {
            Log::Error("Failed to set telemetry builder environment for (", serviceName, "): ", std::string((char*)result.some.ptr, result.some.len));
            return false;
        }
    }

    // start the worker
    // NOTE: builder is consummed after the call so no need to drop it
    result = ddog_telemetry_handle_start(_pHandle);
    if (result.tag == DDOG_OPTION_VEC_U8_SOME_VEC_U8)
    {
        Log::Error("Failed to start telemetry for (", serviceName, "): ", std::string((char*)result.some.ptr, result.some.len));
        return false;
    }

    // metrics definition
    ddog_CharSlice numberOfProfilesMetricName = FfiHelper::StringToCharSlice(std::string("ssi_heuristic.number_of_profiles"));

    ddog_Vec_Tag tags = ddog_Vec_Tag_new();

    // TODO: check possible error
    std::string installationTagValue = (pConfiguration->IsSsiDeployed()) ? std::string("ssi") : std::string("manual");
    ddog_Vec_Tag_push(&tags,
        libdatadog::FfiHelper::StringToCharSlice(std::string("installation")),
        libdatadog::FfiHelper::StringToCharSlice(installationTagValue)
    );

    std::string enablementTagValue =
        (pConfiguration->IsProfilerEnabled())
            ? std::string("manually_enabled") :
        (pConfiguration->IsSsiEnabled())
            ? std::string("ssi_enabled") :
        std::string("not_enabled");
    ddog_Vec_Tag_push(&tags,
        libdatadog::FfiHelper::StringToCharSlice(std::string("enablement_choice")),
        libdatadog::FfiHelper::StringToCharSlice(enablementTagValue)
    );


    // TODO: these have dynamic values depending on the heuristics and the number of sent profiles
    //ddog_Vec_Tag_push(&tags, DDOG_CHARSLICE_C_BARE("has_sent_profiles"), DDOG_CHARSLICE_C_BARE("???"));
    //ddog_Vec_Tag_push(&tags, DDOG_CHARSLICE_C_BARE("heuristic_hypothetical_decision"), DDOG_CHARSLICE_C_BARE("???"));

    // tags is consummed
    _numberOfProfilesKey = ddog_telemetry_handle_register_metric_context(
        _pHandle, numberOfProfilesMetricName, DDOG_METRIC_TYPE_COUNT, tags, true, DDOG_METRIC_NAMESPACE_PROFILERS);

    return true;
}

// telemetry SSI metrics definition
//  "profilers": {
//      "ssi_heuristic.number_of_profiles": {
//          "tags": [
//              "installation",
//              "enablement_choice",
//              "has_sent_profiles",
//              "heuristic_hypothetical_decision"
//              ] ,
//          "metric_type": "count",
//          "data_type" : "profiles",
//          "description" : "The number of profiles that would have been hypothetically emitted if SSI was enabled for profiling",
//          "send_to_user" : false,
//          "user_tags" : []
//      },
//      "ssi_heuristic.number_of_runtime_id": {
//          "tags": [
//              "installation",
//              "enablement_choice",
//              "has_sent_profiles",
//              "heuristic_hypothetical_decision"
//              ] ,
//          "metric_type": "count",
//          "data_type" : "profiles",
//          "description" : "The number of runtimes that would have been hypothetically profiled if SSI was enabled for profiling",
//          "send_to_user" : false,
//          "user_tags" : []
//      }
//  }

} // namespace libdatadog