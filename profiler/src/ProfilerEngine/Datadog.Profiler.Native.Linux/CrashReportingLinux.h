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
    CrashReportingLinux(int32_t pid, int32_t signal);

    ~CrashReportingLinux() override;

private:
    std::vector<int32_t> GetThreads() override;
    std::vector<StackFrame> GetThreadFrames(int32_t tid, ResolveManagedMethod resolveManagedMethod) override;
    std::pair<std::string, uintptr_t> FindModule(uintptr_t ip);
    std::vector<ModuleInfo> GetModules();
    std::string GetSignalInfo() override;

    unw_addr_space_t _addressSpace;
    std::vector<ModuleInfo> _modules;
};
