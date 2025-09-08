// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "../../../shared/src/native-src/string.h"

class environment final
{
public:
    inline static const shared::WSTRING log_path = WStr("DD_TRACE_LOG_PATH");
    inline static const shared::WSTRING log_directory = WStr("DD_TRACE_LOG_DIRECTORY");
    inline static const shared::WSTRING log_buffering_enabled = WStr("DD_TRACE_LOG_BUFFERING_ENABLED");
    inline static const shared::WSTRING debug_log_enabled = WStr("DD_TRACE_DEBUG");
    inline static const shared::WSTRING include_process_names = WStr("DD_PROFILER_PROCESSES");
    inline static const shared::WSTRING exclude_process_names = WStr("DD_PROFILER_EXCLUDE_PROCESSES");
    inline static const shared::WSTRING internal_runtime_id = WStr("DD_INTERNAL_CIVISIBILITY_RUNTIMEID");
    // DD_INJECTION_ENABLED is non-empty when SSI is enabled
    inline static const shared::WSTRING single_step_instrumentation_enabled = WStr("DD_INJECTION_ENABLED");
    inline static const shared::WSTRING force_eol_instrumentation = WStr("DD_INJECT_FORCE");
    inline static const shared::WSTRING single_step_instrumentation_telemetry_forwarder_path = WStr("DD_TELEMETRY_FORWARDER_PATH");

    // Sets whether the current process must run in CI Visibility mode or not.
    inline static const shared::WSTRING ci_visibility_enabled = WStr("DD_CIVISIBILITY_ENABLED");

    // Indicates whether the Datadog SDK is running from the Azure App Services site extension
    inline static const shared::WSTRING is_azure_app_services_extension = WStr("DD_AZURE_APP_SERVICES");

    // The app_pool_id in the context of azure app services
    inline static const shared::WSTRING azure_app_services_app_pool_id = WStr("APP_POOL_ID");

    // The DOTNET_CLI_TELEMETRY_PROFILE in the context of azure app services
    inline static const shared::WSTRING azure_app_services_cli_telemetry_profiler_value = WStr("DOTNET_CLI_TELEMETRY_PROFILE");

    // The FUNCTIONS_WORKER_RUNTIME in Azure Functions.
    // Valid values: "dotnet" (in-process functions) or "dotnet-isolated" (isolated functions).
    // Used as a flag to determine that we are running within a functions app.
    inline static const shared::WSTRING azure_app_services_functions_worker_runtime = WStr("FUNCTIONS_WORKER_RUNTIME");

    // Enables instrumentation of Azure Functions. Default value is true.
    inline static const shared::WSTRING azure_functions_instrumentation_enabled = WStr("DD_TRACE_AZURE_FUNCTIONS_ENABLED");

    inline static const ::shared::WSTRING config_filepath = WStr("DD_NATIVELOADER_CONFIGFILE");

    inline static const ::shared::WSTRING write_instrumentation_to_disk = WStr("DD_WRITE_INSTRUMENTATION_TO_DISK");
    inline static const ::shared::WSTRING copy_original_modules_to_disk = WStr("DD_COPY_ORIGINALS_MODULES_TO_DISK");
};