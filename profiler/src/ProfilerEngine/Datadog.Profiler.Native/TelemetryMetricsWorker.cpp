// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TelemetryMetricsWorker.h"

#include "IConfiguration.h"
#include "ISsiManager.h"
#include "Log.h"
#include "ProfileExporter.h"
#include "ScopeFinalizer.h"
#include "Tags.h"
#include "TagsImpl.hpp"

#include "FfiHelper.h"
#include "FileHelper.h"

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

std::string TelemetryMetricsWorker::TelemetryMetricsEndPoint = "/telemetry/proxy/api/v2/apmtelemetry";

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

    if (_ssiManager->GetDeploymentMode() != DeploymentMode::SingleStepInstrumentation)
    {
        Log::Debug("No telemetry worker for (", serviceName, ") should be started if not deployed via Single Step Instrumentation");
        return false;
    }

    ddog_TelemetryWorkerBuilder* builder;
    auto service = to_char_slice(serviceName);
    auto lang = to_char_slice(language);
    auto lang_version = to_char_slice(languageVersion);
    auto tracer_version = to_char_slice(libraryVersion);

    auto result = ddog_telemetry_builder_instantiate(&builder, service, lang, lang_version, tracer_version);
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Debug("Failed to instantiate telemetry builder for (", serviceName, "): ", error.message());
        return false;
    }

    std::string endpointUrl;
    ddog_CharSlice endpoint_char;
    auto& outputDirectory = pConfiguration->GetProfilesOutputDirectory();
    if ((pConfiguration->IsTelemetryToDiskEnabled()) && (!outputDirectory.empty()))
    {
#ifdef _WIN32
        std::string separator = "\\";
 #else
        std::string separator = "//";
#endif
        std::string pathname = FileHelper::GenerateFilename("telemetry", ".json", serviceName, runtimeId);
        endpointUrl = "file://" + outputDirectory.generic_string() + separator + pathname;
        endpoint_char = to_char_slice(endpointUrl);
    }
    else
    {
        endpointUrl = agentUrl + TelemetryMetricsEndPoint;
        endpoint_char = to_char_slice(endpointUrl);
    }

    auto* endpoint = ddog_endpoint_from_url(endpoint_char);
    on_leave { ddog_endpoint_drop(endpoint); };

    result = ddog_telemetry_builder_with_endpoint_config_endpoint(builder, endpoint);
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Debug("Failed to configure telemetry builder agent endpoint: ", error.message());
        return false;
    }

    // other builder configuration
    auto runtime_id = to_char_slice(runtimeId);
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
        auto service_version = to_char_slice(serviceVersion);
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
        auto env = to_char_slice(environment);
        result = ddog_telemetry_builder_with_str_application_env(builder, env);
        if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
        {
            auto error = make_error(result);
            Log::Debug("Failed to set telemetry builder environment for (", serviceName, "): ", error.message());
            return false;
        }
    }

    // start the worker
    // NOTE: builder is consumed after the call so no need to drop it
    // TODO: we need to update libdatadog to avoid taking the ownership of the builder
    //       and explicitly drop it in all cases
    result = ddog_telemetry_builder_run_metric_logs(builder, &_impl->_pHandle); // don't send lifecycle telemetry
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
        : ((pConfiguration->GetEnablementStatus() == EnablementStatus::SsiEnabled) || (pConfiguration->GetEnablementStatus() == EnablementStatus::Auto))
            ? "ssi_enabled"
            : "not_enabled";

    auto tags = libdatadog::Tags({{"installation", "ssi"},
                                  {"enablement_choice", enablementTagValue}},
                                 false);

    auto numberOfProfilesMetricName = to_char_slice("ssi_heuristic.number_of_profiles");

    _impl->_numberOfProfilesKey = ddog_telemetry_handle_register_metric_context(
        _impl->_pHandle, numberOfProfilesMetricName, DDOG_METRIC_TYPE_COUNT, *static_cast<ddog_Vec_Tag const*>(*tags._impl), true, DDOG_METRIC_NAMESPACE_PROFILERS);

    tags = libdatadog::Tags({{"installation", "ssi"},
                             {"enablement_choice", enablementTagValue}},
                            false);

    auto numberOfRuntimeIdMetricName = to_char_slice("ssi_heuristic.number_of_runtime_id");

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

static std::string GetHeuristicsTag(SkipProfileHeuristicType heuristics)
{
    if (heuristics == SkipProfileHeuristicType::AllTriggered)
    {
        return "triggered";
    }

    std::string str;
    if ((heuristics & SkipProfileHeuristicType::NoSpan) == SkipProfileHeuristicType::NoSpan)
    {
        str = "no_span";
    }

    if ((heuristics & SkipProfileHeuristicType::ShortLived) == SkipProfileHeuristicType::ShortLived)
    {
        if (!str.empty())
        {
            str += "_";
        }
        str += "short_lived";
    }

    return str;
}
void TelemetryMetricsWorker::AddPoint(Key* key, double value, bool hasSentProfiles, SkipProfileHeuristicType heuristics)
{
    if (_impl->_pHandle == nullptr)
    {
        assert(false);

        Log::Debug("Impossible to add telemetry point: worker is not started.");
        return;
    }

    auto tags = libdatadog::Tags({{"has_sent_profiles", hasSentProfiles ? "true" : "false"},
                                  {"heuristic_hypothetical_decision", GetHeuristicsTag(heuristics)}},
                                 false);

    auto result = ddog_telemetry_handle_add_point_with_tags(_impl->_pHandle, key->_internal, value, *static_cast<ddog_Vec_Tag const*>(*tags._impl));
    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto error = make_error(result);
        Log::Debug("Failed to add telemetry point: ", error.message());
        return;
    }
}
} // namespace libdatadog