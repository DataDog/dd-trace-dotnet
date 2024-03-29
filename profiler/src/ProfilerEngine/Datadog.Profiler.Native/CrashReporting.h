#pragma once

#include <stdint.h>
#include <vector>
#include <string>
#include <memory>

typedef int (*ResolveManagedMethod)(uintptr_t ip, char* buffer, int bufferSize, int* requiredBufferSize);

class CrashReporting
{
public:
    CrashReporting(int32_t pid);
    virtual ~CrashReporting();

    static std::unique_ptr<CrashReporting> Create(int32_t pid);

    void ReportCrash(ResolveManagedMethod resolveCallback);

protected:
    int32_t _pid;

    virtual std::vector<int32_t> GetThreads() = 0;
    virtual std::vector<std::pair<uintptr_t, std::string>> GetThreadFrames(int32_t tid, ResolveManagedMethod resolveManagedMethod) = 0;
    virtual std::pair<std::string, uintptr_t> FindModule(uintptr_t ip) = 0;
};
