#pragma once
#include "../../../shared/src/native-src/com_ptr.h"
#include <string>
#include "cor_profiler.h"

namespace datadog::shared::nativeloader
{
class SingleStepGuardRails
{
private:
    bool m_isRunningInSingleStep;
    bool m_isForcedExecution = false;

    inline static const std::string MinNetFrameworkVersion = "4.6.1";
    inline static const std::string MaxNetFrameworkVersion = "4.8.1";
    inline static const std::string MinNetCoreVersion = "3.1.0";
    inline static const std::string MaxNetCoreVersion = "8.0.0";

    bool ShouldForceInstrumentationOverride(const std::string& eolDescription);
    HRESULT HandleUnsupportedNetCoreVersion(const std::string& unsupportedDescription, const std::string& runtimeVersion);
    HRESULT HandleUnsupportedNetFrameworkVersion(const std::string& unsupportedDescription, const std::string& runtimeVersion);
public:
    inline static const std::string NetFrameworkRuntime = ".NET Framework";
    inline static const std::string NetCoreRuntime = ".NET Core";

    SingleStepGuardRails();
    ~SingleStepGuardRails();
    HRESULT CheckRuntime(const RuntimeInformation& runtimeInformation, IUnknown* pICorProfilerInfoUnk);
};
} // namespace datadog::shared::nativeloader