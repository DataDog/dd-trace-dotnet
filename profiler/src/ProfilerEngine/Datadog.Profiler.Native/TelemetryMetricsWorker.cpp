// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TelemetryMetricsWorker.h"

#include "IConfiguration.h"
#include "ISsiManager.h"
#include "Log.h"
#include "ProfileExporter.h"
#include "Tags.h"
#include "TagsImpl.hpp"

#include "FfiHelper.h"

extern "C"
{
#include "datadog/common.h"
#include "datadog/telemetry.h"
}

namespace libdatadog {

struct TelemetryMetricsWorker::Impl
{
public:
    ddog_TelemetryWorkerHandle* _pHandle = nullptr;
    ddog_ContextKey _numberOfProfilesKey = {};
    ddog_ContextKey _numberOfRuntimeIdKey = {};
};

TelemetryMetricsWorker::TelemetryMetricsWorker(ISsiManager* ssiManager) :
    _serviceName{},
    _impl{std::make_unique<Impl>()},
    _hasSentProfiles{false},
    _ssiManager{ssiManager}
{
}

TelemetryMetricsWorker::~TelemetryMetricsWorker()
{
    Stop();
}

void TelemetryMetricsWorker::Stop()
{
    auto* handle = std::exchange(_impl->_pHandle, nullptr);
    // stop the worker if needed
    if (handle == nullptr)
    {
        return;
    }

    auto result = ddog_telemetry_handle_stop(handle);
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Debug("Failed to stop telemetry worker for (", _serviceName, "): ", error.message());
        return;
    }

    ddog_telemetry_handle_wait_for_shutdown_ms(handle, 10);
}

bool TelemetryMetricsWorker::Start(
    const IConfiguration* pConfiguration,
    const std::string& serviceName,
    const std::string& serviceVersion,
    const std::string& language,
    const std::string& languageVersion,
    const std::string& libraryVersion,
    const std::string& agentUrl,
    const std::string& runtimeId,
    const std::string& environment)
{
    if (_impl->_pHandle != nullptr)
    {
        assert(false);

        Log::Debug("It is not allowed to start telemetry worker for (", serviceName, ") more than once.");
        return false;
    }

    _serviceName = serviceName;

    if (pConfiguration->GetDeploymentMode() != DeploymentMode::SingleStepInstrumentation)
    {
        Log::Debug("No telemetry worker for (", serviceName, ") should be started if not deployed via Single Step Instrumentation");
        return false;
    }

    ddog_TelemetryWorkerBuilder* builder;
    auto service = FfiHelper::StringToCharSlice(serviceName);
    auto lang = FfiHelper::StringToCharSlice(language);
    auto lang_version = FfiHelper::StringToCharSlice(languageVersion);
    auto tracer_version = FfiHelper::StringToCharSlice(libraryVersion);

    auto result = ddog_telemetry_builder_instantiate(&builder, service, lang, lang_version, tracer_version);
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Error("Failed to instantiate telemetry builder for (", serviceName, "): ", error.message());
        return false;
    }

    auto endpoint_char = FfiHelper::StringToCharSlice(agentUrl);
    auto* endpoint = ddog_endpoint_from_url(endpoint_char);
    result = ddog_telemetry_builder_with_endpoint_config_endpoint(builder, endpoint);
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Debug("Failed to configure telemetry builder agent endpoint: ", error.message());
        return false;
    }

    ddog_endpoint_drop(endpoint);

    // other builder configuration
    auto runtime_id = FfiHelper::StringToCharSlice(runtimeId);
    result = ddog_telemetry_builder_with_str_runtime_id(builder, runtime_id);
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Debug("Failed to set telemetry builder runtime ID for (", serviceName, "): ", error.message());
        return false;
    }

    // these are optional
    if (!serviceVersion.empty())
    {
        auto service_version = FfiHelper::StringToCharSlice(serviceVersion);
        result = ddog_telemetry_builder_with_str_application_service_version(builder, service_version);
        if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
        {
            auto error = make_error(result);
            Log::Debug("Failed to set telemetry builder service version for (", serviceName, "): ", error.message());
            return false;
        }
    }

    if (!environment.empty())
    {
        auto env = FfiHelper::StringToCharSlice(environment);
        result = ddog_telemetry_builder_with_str_application_env(builder, env);
        if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
        {
            auto error = make_error(result);
            Log::Debug("Failed to set telemetry builder environment for (", serviceName, "): ", error.message());
            return false;
        }
    }

    // start the worker
    // NOTE: builder is consummed after the call so no need to drop it
    result = ddog_telemetry_builder_run(builder, &_impl->_pHandle);
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Debug("Failed to run builder for (", serviceName, "): ", error.message());
        return false;
    }

    result = ddog_telemetry_handle_start(_impl->_pHandle);
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Debug("Failed to start telemetry for (", serviceName, "): ", error.message());
        return false;
    }

    // metrics definition

    std::string enablementTagValue =
        (pConfiguration->GetEnablementStatus() == EnablementStatus::ManuallyEnabled)
            ? "manually_enabled"
        : (pConfiguration->GetEnablementStatus() == EnablementStatus::SsiEnabled)
            ? "ssi_enabled"
            : "not_enabled";

    auto tags = libdatadog::Tags({{"installation", "ssi"},
                                  {"enablement_choice", enablementTagValue}},
                                 false);

    // TODO: see if we need to also emit ssi_heuristic.number_of_runtime_id
    auto numberOfProfilesMetricName = FfiHelper::StringToCharSlice("ssi_heuristic.number_of_profiles");

    _impl->_numberOfProfilesKey = ddog_telemetry_handle_register_metric_context(
        _impl->_pHandle, numberOfProfilesMetricName, DDOG_METRIC_TYPE_COUNT, *static_cast<ddog_Vec_Tag const*>(*tags._impl), true, DDOG_METRIC_NAMESPACE_PROFILERS);

    tags = libdatadog::Tags({{"installation", "ssi"},
                             {"enablement_choice", enablementTagValue}},
                            false);

    auto numberOfRuntimeIdMetricName = FfiHelper::StringToCharSlice("ssi_heuristic.number_of_runtime_id");

    _impl->_numberOfRuntimeIdKey = ddog_telemetry_handle_register_metric_context(
        _impl->_pHandle, numberOfRuntimeIdMetricName, DDOG_METRIC_TYPE_COUNT, *static_cast<ddog_Vec_Tag const*>(*tags._impl), true, DDOG_METRIC_NAMESPACE_PROFILERS);

    return true;
}

struct TelemetryMetricsWorker::Key
{
public:
    ddog_ContextKey* _internal;
};

void TelemetryMetricsWorker::IncNumberOfProfiles(bool hasSentProfiles)
{
    _hasSentProfiles |= hasSentProfiles;
    auto k = Key{._internal = &_impl->_numberOfProfilesKey};
    AddPoint(&k, 1, hasSentProfiles, _ssiManager->GetSkipProfileHeuristic());
}

void TelemetryMetricsWorker::IncNumberOfApplications()
{
    auto k = Key{._internal = &_impl->_numberOfRuntimeIdKey};
    AddPoint(&k, 1, _hasSentProfiles, _ssiManager->GetSkipProfileHeuristic());
}

void TelemetryMetricsWorker::AddPoint(Key* key, double value, bool hasSentProfiles, SkipProfileHeuristicType heuristics)
{
    if (_impl->_pHandle == nullptr)
    {
        assert(false);

        Log::Debug("Impossible to add telemetry point: worker is not started.");
        return;
    }

    std::string heuristicTag;
    if (heuristics == SkipProfileHeuristicType::AllTriggered)
    {
        heuristicTag = "triggered";
    }
    else
    {
        if ((heuristics & SkipProfileHeuristicType::NoSpan) == SkipProfileHeuristicType::NoSpan)
        {
            heuristicTag += "no_span";
        }

        if ((heuristics & SkipProfileHeuristicType::ShortLived) == SkipProfileHeuristicType::ShortLived)
        {
            if (!heuristicTag.empty())
            {
                heuristicTag += "_";
            }

            heuristicTag = "short_lived";
        }
    }

    auto tags = libdatadog::Tags({{"has_sent_profiles", hasSentProfiles ? "true" : "false"},
                                  {"heuristic_hypothetical_decision", std::move(heuristicTag)}},
                                 false);

    auto result = ddog_telemetry_handle_add_point_with_tags(_impl->_pHandle, key->_internal, value, *static_cast<ddog_Vec_Tag const*>(*tags._impl));
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Debug("Failed to add telemetry point: ", error.message());
        return;
    }
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