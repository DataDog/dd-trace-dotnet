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

private:
    std::vector<int32_t> GetThreads() override;
    std::vector<std::pair<uintptr_t, std::string>> GetThreadFrames(int32_t tid, ResolveManagedMethod resolveManagedMethod) override;
    std::vector<ModuleInfo> GetModules();

    unw_addr_space_t _addressSpace;
    std::vector<ModuleInfo> _modules;
};

