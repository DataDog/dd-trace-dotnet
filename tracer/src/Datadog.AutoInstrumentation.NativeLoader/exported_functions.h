#pragma once

#include <corhlpr.h>
#include <corprof.h>

#include <string>

extern "C" const char* STDMETHODCALLTYPE GetCurrentAppDomainRuntimeId();
extern "C" const std::string& STDMETHODCALLTYPE GetRuntimeId(AppDomainID appDomain);
