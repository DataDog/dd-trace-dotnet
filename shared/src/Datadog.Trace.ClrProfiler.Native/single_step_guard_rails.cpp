#include "cor_profiler.h"
#include "log.h"
#include "util.h"
#include "../../../shared/src/native-src/version.h"
#include "EnvironmentVariables.h"
#include "single_step_guard_rails.h"

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
            return HandleUnsupportedNetCoreVersion(eol, runtime);
        }

        // Unsupported EOL version, but _which_ version
        // unfortunately we can't trust the major_version etc values here
        const auto eol = ".NET Core 3.0 or lower";

        if (S_OK == pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo10), (void**) &tstVerProfilerInfo))
        {
            // we can't get this exact runtime version from cor profiler unfortunately
            tstVerProfilerInfo->Release();
            return HandleUnsupportedNetCoreVersion(eol, "3.0.0");
        }

        if (S_OK == pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo9), (void**) &tstVerProfilerInfo))
        {
            // no way of detecting 2.2 from here AFAIK, so we treat 2.1 and 2.2 the same
            tstVerProfilerInfo->Release();
            return HandleUnsupportedNetCoreVersion(eol, "2.1.0");
        }

        // Only remaining EOL version that we can instrument is 2.0.0
        return HandleUnsupportedNetCoreVersion(eol, "2.0.0");
    }

    // For .NET Framework, we require .NET Framework 4.6.1 or higher
    if (S_OK == pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo7), (void**) &tstVerProfilerInfo))
    {
        // supported
        tstVerProfilerInfo->Release();
        Log::Debug("SingleStepGuardRails::CheckRuntime: Supported .NET Framework runtime version detected, continuing with single step instrumentation");
        return S_OK;
    }

    return HandleUnsupportedNetFrameworkVersion(".NET Framework 4.6.0 or lower", "4.6.0");
}

HRESULT SingleStepGuardRails::HandleUnsupportedNetCoreVersion(const std::string& unsupportedDescription, const std::string& runtimeVersion)
{
    if(ShouldForceInstrumentationOverride(unsupportedDescription))
    {
        return S_OK;
    }

    Log::Warn(
        "CorProfiler::Initialize: Single-step instrumentation is not supported in '",
        unsupportedDescription,
        "'. Set ",
        EnvironmentVariables::ForceEolInstrumentation,
        "=1 to override this check and force instrumentation");

    return E_FAIL;
}

HRESULT SingleStepGuardRails::HandleUnsupportedNetFrameworkVersion(const std::string& unsupportedDescription, const std::string& runtimeVersion)
{
    if(ShouldForceInstrumentationOverride(unsupportedDescription))
    {
        return S_OK;
    }

    Log::Warn(
        "CorProfiler::Initialize: Single-step instrumentation is not supported in '",
        unsupportedDescription,
        "'. Set ",
        EnvironmentVariables::ForceEolInstrumentation,
        "=1 to override this check and force instrumentation");

    return E_FAIL;
}

bool SingleStepGuardRails::ShouldForceInstrumentationOverride(const std::string& eolDescription)
{
    // Are we supposed to override the EOL check?
    const auto forceEolInstrumentationVariable = GetEnvironmentValue(EnvironmentVariables::ForceEolInstrumentation);

    bool forceEolInstrumentation;
    if (!forceEolInstrumentationVariable.empty()
        && TryParseBooleanEnvironmentValue(forceEolInstrumentationVariable, forceEolInstrumentation)
        && forceEolInstrumentation)
    {
        m_isForcedExecution = true;
        Log::Info(
            "CorProfiler::Initialize: Unsupported framework version '",
            eolDescription,
            "' detected. Forcing instrumentation with single-step instrumentation due to ",
            EnvironmentVariables::ForceEolInstrumentation);
        return true;
    }

    return false;
}
} // namespace datadog::shared::nativeloader