#pragma once
#include "C:\git\dd-trace-dotnet\profiler\src\ProfilerEngine\Datadog.Profiler.Native\CrashReporting.h"
#include <string>

#include <libunwind.h>

class CrashReportingLinux : public CrashReporting
{
public:
    CrashReportingLinux();

    ~CrashReportingLinux() override;

private:
    std::vector<int32_t> GetThreads(int32_t pid) override;
    std::vector<std::pair<uintptr_t, std::string>> GetThreadFrames(int32_t pid, int32_t tid, ResolveManagedMethod resolveManagedMethod) override;

    unw_addr_space _addressSpace;
};

