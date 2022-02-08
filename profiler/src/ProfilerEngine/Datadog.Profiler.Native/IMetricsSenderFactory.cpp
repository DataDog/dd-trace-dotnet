// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IMetricsSenderFactory.h"
#include "DogstatsdService.h"
#include "EnvironmentVariables.h"
#include "Log.h"

#include "shared/src/native-src/string.h"

#include <string>

std::shared_ptr<IMetricsSender> IMetricsSenderFactory::Create()
{
    const shared::WSTRING operationalMetricsEnabledStr = shared::GetEnvironmentValue(EnvironmentVariables::OperationalMetricsEnabled);

    if (operationalMetricsEnabledStr.empty())
    {
        Log::Info("No \"", EnvironmentVariables::OperationalMetricsEnabled, "\" environment variable has been found.",
                  " Default: Operational metrics disabled.");

        return nullptr;
    }

    bool operationalMetricsEnabled = false;

    if (!shared::TryParseBooleanEnvironmentValue(operationalMetricsEnabledStr, operationalMetricsEnabled))
    {
        Log::Info("Invalid value \"", operationalMetricsEnabledStr, "\" for \"",
                  EnvironmentVariables::OperationalMetricsEnabled, "\" environment variable.",
                  " Default: Operational metrics disabled.");
        return nullptr;
    }

    Log::Info("Operational metrics ", (operationalMetricsEnabled ? "enabled" : "disabled"),
              " ('", EnvironmentVariables::OperationalMetricsEnabled, "' = ", operationalMetricsEnabledStr, ")");

    if (!operationalMetricsEnabled)
    {
        return nullptr;
    }

    std::string profilerVersion = shared::ToString(shared::GetEnvironmentValue(EnvironmentVariables::Version));
    std::string serviceName = shared::ToString(shared::GetEnvironmentValue(EnvironmentVariables::ServiceName));
    std::string environment = shared::ToString(shared::GetEnvironmentValue(EnvironmentVariables::Environment));
    auto tags = IMetricsSender::Tags{{"profiler_version", profilerVersion}, {"service_name", serviceName}, {"environment", environment}};

    // Operational metrics are activated only in reliability environment.
    // We assume that the datadog agent is installed localhost and is listening on port 8125
    return std::make_shared<DogstatsdService>("127.0.0.1", 8125, tags);
}