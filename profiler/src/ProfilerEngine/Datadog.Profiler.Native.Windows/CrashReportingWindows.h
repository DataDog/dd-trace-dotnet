// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "CrashReporting.h"
#include "ScopedHandle.h"

struct ModuleInfo
{
    uintptr_t startAddress;
    uintptr_t endAddress;
    std::string path;
};

class CrashReportingWindows : public CrashReporting
{
public:
    CrashReportingWindows(int32_t pid);

    ~CrashReportingWindows() override;

    int32_t STDMETHODCALLTYPE Initialize() override;

private:
    std::vector<std::pair<int32_t, std::string>> GetThreads() override;
    std::vector<StackFrame> GetThreadFrames(int32_t tid, ResolveManagedCallstack resolveManagedCallstack, void* context) override;
    std::string GetSignalInfo(int32_t signal) override;
    std::vector<ModuleInfo> GetModules();
    std::pair<std::string_view, uintptr_t> FindModule(uintptr_t ip);

    ScopedHandle _process;
    std::vector<ModuleInfo> _modules;
};
