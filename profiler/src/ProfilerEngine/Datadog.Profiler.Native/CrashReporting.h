#pragma once

#include <stdint.h>
#include <vector>

typedef int (*ResolveManagedMethod)(uintptr_t ip, char* buffer, int bufferSize, int* requiredBufferSize);

class CrashReporting
{
public:
    CrashReporting();
    virtual ~CrashReporting();

    void ReportCrash(int32_t pid, ResolveManagedMethod resolveCallback);

private:
    virtual std::vector<int32_t> GetThreads(int32_t pid) = 0;
    virtual std::vector<std::pair<uintptr_t, std::string>> GetThreadFrames(int32_t pid, int32_t tid, ResolveManagedMethod resolveManagedMethod) = 0;
};
