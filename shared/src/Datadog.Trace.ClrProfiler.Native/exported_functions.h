#pragma once

#include <corhlpr.h>
#include <corprof.h>

#ifndef _WIN32
#undef EXTERN_C
#define EXTERN_C extern "C" __attribute__((visibility("default")))
#endif

EXTERN_C const char* STDMETHODCALLTYPE GetCurrentAppDomainRuntimeId();
EXTERN_C const char* STDMETHODCALLTYPE GetRuntimeId(AppDomainID appDomain);
