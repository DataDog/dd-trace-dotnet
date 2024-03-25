#pragma once

#include <stdint.h>
#include <vector>
#include <string>
#include <memory>

typedef int (*ResolveManagedMethod)(uintptr_t ip, char* buffer, int bufferSize, int* requiredBufferSize);

class CrashReporting
{
public:
    CrashReporting();
    virtual ~CrashReporting();

    static std::unique_ptr<CrashReporting> Create();

    void ReportCrash(int32_t pid, ResolveManagedMethod resolveCallback);

private:
    virtual std::vector<int32_t> GetThreads(int32_t pid) = 0;
    virtual std::vector<std::pair<uintptr_t, std::string>> GetThreadFrames(int32_t pid, int32_t tid, ResolveManagedMethod resolveManagedMethod) = 0;
};
