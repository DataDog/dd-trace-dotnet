#include "cor_profiler.h"
#include "log.h"
#include "util.h"
#include "../../../shared/src/native-src/version.h"
#include "EnvironmentVariables.h"
#include "single_step_guard_rails.h"
#include "process_helper.h"

#ifdef _WIN32
#include <Windows.h>
#endif

using namespace shared;

namespace datadog::shared::nativeloader
{
SingleStepGuardRails::SingleStepGuardRails()
{
    // This variable is non-empty when we're in single step
    Log::Debug("SingleStepGuardRails::CheckRuntime: Checking for Single step instrumentation environment using ",
               EnvironmentVariables::SingleStepInstrumentationEnabled);
    const auto isSingleStepVariable = GetEnvironmentValue(EnvironmentVariables::SingleStepInstrumentationEnabled);

    m_isRunningInSingleStep = !isSingleStepVariable.empty(); 
}

SingleStepGuardRails::~SingleStepGuardRails()
{
}

/**
 * Check if we're running in Single Step, and if so, whether we should bail out
 * 
 * @param runtimeInformation 
 * @param pICorProfilerInfoUnk 
 * @return 
 */
HRESULT SingleStepGuardRails::CheckRuntime(const RuntimeInformation& runtimeInformation, IUnknown* pICorProfilerInfoUnk)
{
    //
    // Check if we're running in Single Step, and if so, whether we should bail out
    //
    if(!m_isRunningInSingleStep)
    {
        // not single step, don't do anything else
        return S_OK;
    }
    
    Log::Debug("SingleStepGuardRails::CheckRuntime: Single step instrumentation detected, checking for EOL environment");
    // We're doing single-step instrumentation, check if we're in an EOL environment
    
    IUnknown* tstVerProfilerInfo;
    std::string unsupportedRuntimeVersion;
    std::string unsupportedSummary;

    if (runtimeInformation.is_core())
    {
        Log::Debug("SingleStepGuardRails::CheckRuntime: Running in .NET Core - checking available version");

        // For .NET Core, we require .NET Core 3.1 or higher
        // We could use runtime_information, but I don't know how reliable the values we get from that are?
        if (S_OK == pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo11), (void**) &tstVerProfilerInfo))
        {
            tstVerProfilerInfo->Release();

            // Is it a known-faulty release?
            if(runtimeInformation.major_version == 6
                && runtimeInformation.minor_version == 0
                && runtimeInformation.build_version < 13)
            {
                // known faulty release with bug in the runtime that can cause crashes when we're attached:
                // https://github.com/dotnet/runtime/pull/78670
                Log::Debug("SingleStepGuardRails::CheckRuntime: Known faulty .NET runtime version detected, aborting instrumentation");

                const auto eol = ".NET 6.0.12 and earlier have known crashing bugs";
                const auto runtime = std::to_string(runtimeInformation.major_version) + "." +
                                     std::to_string(runtimeInformation.minor_version) + "." +
                                     std::to_string(runtimeInformation.build_version);
                return HandleUnsupportedNetCoreVersion(eol, runtime, false); // not eol, just incompatible
            }

            // .NET Core 3.1+, but is it _too_ high?
            if(runtimeInformation.major_version <= 8)
            {
                // supported
                Log::Debug("SingleStepGuardRails::CheckRuntime: Supported .NET runtime version detected, continuing with single step instrumentation");
                return S_OK;
            }

            const auto eol = ".NET 9 or higher";

            const auto runtime = std::to_string(runtimeInformation.major_version) + "." +
                                 std::to_string(runtimeInformation.minor_version) + "." +
                                 std::to_string(runtimeInformation.build_version);
            return HandleUnsupportedNetCoreVersion(eol, runtime, false); // not eol, just incompatible
        }

        // Unsupported EOL version, but _which_ version
        // unfortunately we can't trust the major_version etc values here
        const auto eol = ".NET Core 3.0 or lower";

        if (S_OK == pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo10), (void**) &tstVerProfilerInfo))
        {
            // we can't get this exact runtime version from cor profiler unfortunately
            tstVerProfilerInfo->Release();
            return HandleUnsupportedNetCoreVersion(eol, "3.0.0", true);
        }

        if (S_OK == pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo9), (void**) &tstVerProfilerInfo))
        {
            // no way of detecting 2.2 from here AFAIK, so we treat 2.1 and 2.2 the same
            tstVerProfilerInfo->Release();
            return HandleUnsupportedNetCoreVersion(eol, "2.1.0", true);
        }

        // Only remaining EOL version that we can instrument is 2.0.0
        return HandleUnsupportedNetCoreVersion(eol, "2.0.0", true);
    }

    // For .NET Framework, we require .NET Framework 4.6.1 or higher
    if (S_OK == pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo7), (void**) &tstVerProfilerInfo))
    {
        // supported
        tstVerProfilerInfo->Release();
        Log::Debug("SingleStepGuardRails::CheckRuntime: Supported .NET Framework runtime version detected, continuing with single step instrumentation");
        return S_OK;
    }

    return HandleUnsupportedNetFrameworkVersion(".NET Framework 4.6.0 or lower", "4.6.0", true);
}

HRESULT SingleStepGuardRails::HandleUnsupportedNetCoreVersion(const std::string& unsupportedDescription, const std::string& runtimeVersion, const bool isEol)
{
    if(ShouldForceInstrumentationOverride(unsupportedDescription, isEol))
    {
        return S_OK;
    }

    SendAbortTelemetry(NetCoreRuntime, runtimeVersion, isEol);
    return E_FAIL;
}

HRESULT SingleStepGuardRails::HandleUnsupportedNetFrameworkVersion(const std::string& unsupportedDescription, const std::string& runtimeVersion, const bool isEol)
{
    if(ShouldForceInstrumentationOverride(unsupportedDescription, isEol))
    {
        return S_OK;
    }

    SendAbortTelemetry(NetFrameworkRuntime, runtimeVersion, isEol);
    return E_FAIL;
}

bool SingleStepGuardRails::ShouldForceInstrumentationOverride(const std::string& eolDescription, const bool isEol)
{
    // Should only be called when we have an incompatible runtime
    Log::Warn(
        "SingleStepGuardRails::ShouldForceInstrumentationOverride: Found incompatible runtime ", eolDescription);

    // Are we supposed to override the EOL check?
    const auto forceEolInstrumentationVariable = GetEnvironmentValue(EnvironmentVariables::ForceEolInstrumentation);

    bool forceEolInstrumentation;
    if (!forceEolInstrumentationVariable.empty()
        && TryParseBooleanEnvironmentValue(forceEolInstrumentationVariable, forceEolInstrumentation)
        && forceEolInstrumentation)
    {
        m_isForcedExecution = true;
        
        Log::Info(
            "SingleStepGuardRails::ShouldForceInstrumentationOverride: ",
            EnvironmentVariables::ForceEolInstrumentation,
            " enabled, allowing unsupported runtimes and continuing");
        return true;
    }

    const std::string reason = isEol ? "eol_runtime" : "incompatible_runtime";
    Log::Warn(
        "SingleStepGuardRails::HandleUnsupportedNetCoreVersion: Aborting application instrumentation due to ", reason);
    return false;
}

void SingleStepGuardRails::RecordBootstrapError(const RuntimeInformation& runtimeInformation,
    const std::string& errorType) const
{
    if(!m_isRunningInSingleStep)
    {
        return;
    }

    Log::Error(
        "SingleStepGuardRails::RecordBootstrapError: Error during instrumentation of application, aborting. Error: ",
        errorType);

    const auto runtimeName = runtimeInformation.is_core() ? NetCoreRuntime : NetFrameworkRuntime;
    const auto runtimeVersion = runtimeInformation.description();

    RecordBootstrapError(runtimeName, runtimeVersion, errorType);
}

void SingleStepGuardRails::RecordBootstrapError(const std::string& runtimeName, const std::string& runtimeVersion,
                                                const std::string& errorType) const
{
    if(!m_isRunningInSingleStep)
    {
        return;
    }

    const std::string points = "[{\"name\": \"library_entrypoint.error\", \"tags\": [\"error_type:"
                              + errorType + "\"]}]";
    SendTelemetry(runtimeName, runtimeVersion, points);
}

void SingleStepGuardRails::RecordBootstrapSuccess(const RuntimeInformation& runtimeInformation) const
{
    if(!m_isRunningInSingleStep)
    {
        return;
    }

    Log::Info("SingleStepGuardRails::RecordBootstrapSuccess: Application instrumentation bootstrapping complete");

    const auto runtimeName = runtimeInformation.is_core() ? NetCoreRuntime : NetFrameworkRuntime;
    const auto runtimeVersion = runtimeInformation.description();

    const std::string isForced = m_isForcedExecution ? "true" : "false";
    const std::string points = "[{\"name\": \"library_entrypoint.complete\", \"tags\": [\"injection_forced:"
                              + isForced + "\"]}]";

    SendTelemetry(runtimeName, runtimeVersion, points);
}

void SingleStepGuardRails::SendAbortTelemetry(const std::string& runtimeName, const std::string& runtimeVersion, const bool isEol) const
{
    if(!m_isRunningInSingleStep)
    {
        return;
    }

    const std::string reason = isEol ? "eol_runtime" : "incompatible_runtime";  // possible reasons [”eol_runtime”,””incompatible_runtime”, ”integration”, ”package_manager”]

    const std::string abort = "{\"name\": \"library_entrypoint.abort\", \"tags\": [\"reason:"
                              + reason + "\"]}";
    const std::string abort_runtime = "{\"name\": \"library_entrypoint.abort.runtime\"}";
    
    const std::string points = "[" + abort + "," + abort_runtime + "]";

    SendTelemetry(runtimeName, runtimeVersion, points);
}

void SingleStepGuardRails::SendTelemetry(const std::string& runtimeName, const std::string& runtimeVersion,
                                         const std::string& points) const
{
    if(!m_isRunningInSingleStep)
    {
        Log::Debug("SingleStepGuardRails::SendTelemetry Not running in single step mode");
        return;
    }

    auto forwarderPath = GetEnvironmentValue(EnvironmentVariables::SingleStepInstrumentationTelemetryForwarderPath);
    if (forwarderPath.empty())
    {
        Log::Info("SingleStepGuardRails::SendTelemetry: Unable to send telemetry, ",
                  EnvironmentVariables::SingleStepInstrumentationTelemetryForwarderPath, " is not set");
        return;
    }

    std::error_code ec; // fs::exists might throw if no error_code parameter is provided
    if (!fs::exists(forwarderPath, ec))
    {
        Log::Info("SingleStepGuardRails::SendTelemetry: Unable to send telemetry, ",
                  EnvironmentVariables::SingleStepInstrumentationTelemetryForwarderPath, " path does not exist:",
                  forwarderPath);
        return;
    }

    const std::string metadata =
        "{\"metadata\":{\"runtime_name\": \"" + runtimeName
        + "\",\"runtime_version\": \"" + runtimeVersion
        + "\",\"language_name\": \"dotnet\",\"language_version\": \"" + runtimeVersion
        + "\",\"tracer_version\": \"" + PROFILER_VERSION
        + "\",\"pid\":" + std::to_string(GetPID())
        + "},\"points\": " + points + "}";

    const auto processPath = ToString(forwarderPath);

    // The telemetry forwarder expects the first argument to be the execution type:
    const std::string initialArg = "library_entrypoint";
    const std::vector args = {initialArg};

    Log::Debug("SingleStepGuardRails::SendTelemetry: Invoking: ", processPath, " with ", initialArg, "and metadata " , metadata);

    // Increment the reference count to prevent the loader from being unloaded while sending telemetry

#ifdef _WIN32
    HMODULE handle;

    // GetModuleHandleEx increments the reference count if called without GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT
    if (!GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
        (LPCWSTR)&datadog::shared::nativeloader::CorProfiler::GetRuntimeId, &handle))
    {
        handle = 0;
    }

#else
    // Use dlopen to increment the reference count of the module
    void* handle = 0;
    Dl_info info;
    if (dladdr((void*)&datadog::shared::nativeloader::CorProfiler::GetRuntimeId, &info))
    {
        handle = dlopen(info.dli_fname, RTLD_LAZY);
    }
#endif

    if (handle != 0)
    {
        std::thread([processPath, args, metadata, handle]()
        {
            const auto success = ProcessHelper::RunProcess(processPath, args, metadata);

            if (success)
            {
                Log::Debug("SingleStepGuardRails::SendTelemetry: Telemetry sent to forwarder");
            }
            else
            {
                Log::Warn("SingleStepGuardRails::SendTelemetry: Error calling telemetry forwarder");
            }

#ifdef _WIN32
            // On Windows, we can safely unload the module
            FreeLibraryAndExitThread(handle, 0);
#endif
        }).detach();
    }
    else
    {
        Log::Warn("SingleStepGuardRails::SendTelemetry: Skipping the telemetry forwarder because it can't be safely invoked");
    }
}
} // namespace datadog::shared::nativeloader
