#pragma once

#include <corhlpr.h>
#include <corprof.h>

extern "C" const char* STDMETHODCALLTYPE GetCurrentAppDomainRuntimeId();
extern "C" const char* STDMETHODCALLTYPE GetRuntimeId(AppDomainID appDomain);
