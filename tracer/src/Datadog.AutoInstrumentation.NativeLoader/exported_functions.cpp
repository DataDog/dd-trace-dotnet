#include "exported_functions.h"
#include "cor_profiler.h"

// This function is exported for interoperability with native libraries (e.g.: Profiler)
extern "C" const char* STDMETHODCALLTYPE GetRuntimeId(AppDomainID appDomain)
{
    return datadog::shared::nativeloader::CorProfiler::GetRuntimeId(appDomain);
}

// This function is exported mainly for interoperability with managed libraries (e.g.: Tracer managed code)
// This method must be called by a managed thread
extern "C" const char* STDMETHODCALLTYPE GetCurrentAppDomainRuntimeId()
{
    auto appDomain = datadog::shared::nativeloader::CorProfiler::GetCurrentAppDomainId();
    return GetRuntimeId(appDomain);
}
