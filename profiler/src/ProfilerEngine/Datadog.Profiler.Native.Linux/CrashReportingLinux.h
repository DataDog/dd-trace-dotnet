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
    std::vector<int32_t> GetThreads() override;
    std::vector<StackFrame> GetThreadFrames(int32_t tid, ResolveManagedMethod resolveManagedMethod) override;
    std::pair<std::string, uintptr_t> FindModule(uintptr_t ip);
    std::vector<ModuleInfo> GetModules();
    std::string GetSignalInfo(int32_t signal) override;

    unw_addr_space_t _addressSpace;
    std::vector<ModuleInfo> _modules;
};
