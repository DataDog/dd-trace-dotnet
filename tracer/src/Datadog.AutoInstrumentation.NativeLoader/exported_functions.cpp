#include "exported_functions.h"
#include "cor_profiler.h"

// This function is exported for interoperability with native libraries (e.g.: Profiler)
extern "C" const std::string& STDMETHODCALLTYPE GetRuntimeId(AppDomainID appDomain)
{
    return datadog::shared::nativeloader::CorProfiler::GetRuntimeId(appDomain);
}

// This function is exported mainly for interoperability with managed libraries (e.g.: Tracer managed code)
extern "C" char* STDMETHODCALLTYPE GetCurrentAppDomainRuntimeId()
{
    auto appDomain = datadog::shared::nativeloader::CorProfiler::GetCurrentAppDomainId();
    auto& rid = GetRuntimeId(appDomain);
    return const_cast<char*>(rid.data());
}
