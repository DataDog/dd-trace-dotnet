// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "CrashReporting.h"

#include <string>

#include <libunwind.h>

struct ModuleInfo
{
    uintptr_t startAddress;
    uintptr_t endAddress;
    uintptr_t baseAddress;
    std::string path;
};

class CrashReportingLinux : public CrashReporting
{
public:
    CrashReportingLinux(int32_t pid);

    ~CrashReportingLinux() override;

    int32_t Initialize() override;

private:
    std::vector<std::pair<int32_t, std::string>> GetThreads() override;
    std::vector<StackFrame> GetThreadFrames(int32_t tid, ResolveManagedCallstack resolveManagedCallstack, void* context) override;
    std::pair<std::string_view, uintptr_t> FindModule(uintptr_t ip);
    std::vector<ModuleInfo> GetModules();
    std::string GetSignalInfo(int32_t signal) override;
    std::string GetThreadName(int32_t tid);

    unw_addr_space_t _addressSpace;
    std::vector<ModuleInfo> _modules;
};
