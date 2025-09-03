// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "../../../shared/src/native-src/string.h"

class EnvironmentVariables final
{
public:
    inline static const shared::WSTRING LogPath = WStr("DD_TRACE_LOG_PATH");
    inline static const shared::WSTRING LogDirectory = WStr("DD_TRACE_LOG_DIRECTORY");
    inline static const shared::WSTRING LogBufferingEnabled = WStr("DD_TRACE_LOG_BUFFERING_ENABLED");
    inline static const shared::WSTRING DebugLogEnabled = WStr("DD_TRACE_DEBUG");
    inline static const shared::WSTRING IncludeProcessNames = WStr("DD_PROFILER_PROCESSES");
    inline static const shared::WSTRING ExcludeProcessNames = WStr("DD_PROFILER_EXCLUDE_PROCESSES");
    inline static const shared::WSTRING InternalRuntimeId = WStr("DD_INTERNAL_CIVISIBILITY_RUNTIMEID");
    // DD_INJECTION_ENABLED is non-empty when SSI is enabled
    inline static const shared::WSTRING SingleStepInstrumentationEnabled = WStr("DD_INJECTION_ENABLED");
    inline static const shared::WSTRING ForceEolInstrumentation = WStr("DD_INJECT_FORCE");
    inline static const shared::WSTRING SingleStepInstrumentationTelemetryForwarderPath = WStr("DD_TELEMETRY_FORWARDER_PATH");

    // Sets whether the current process must run in CI Visibility mode or not.
    inline static const shared::WSTRING CiVisibilityEnabled = WStr("DD_CIVISIBILITY_ENABLED");

    // Indicates whether the profiler is running in the context
    // of Azure App Services with the extension installed
    inline static const shared::WSTRING IsAzureAppServicesExtension = WStr("DD_AZURE_APP_SERVICES");

    // The app_pool_id in the context of azure app services
    inline static const shared::WSTRING AzureAppServicesAppPoolId = WStr("APP_POOL_ID");

    // The DOTNET_CLI_TELEMETRY_PROFILE in the context of azure app services
    inline static const shared::WSTRING AzureAppServicesCliTelemetryProfilerValue = WStr("DOTNET_CLI_TELEMETRY_PROFILE");

    // The FUNCTIONS_WORKER_RUNTIME in the context of azure app services
    // Used as a flag to determine that we are running within a functions app.
    inline static const shared::WSTRING AzureAppServicesFunctionsWorkerRuntime = WStr("FUNCTIONS_WORKER_RUNTIME");

    // Determine whether to instrument within azure functions.
    // Default is true.
    inline static const shared::WSTRING AzureFunctionsInstrumentationEnabled = WStr("DD_TRACE_AZURE_FUNCTIONS_ENABLED");
};