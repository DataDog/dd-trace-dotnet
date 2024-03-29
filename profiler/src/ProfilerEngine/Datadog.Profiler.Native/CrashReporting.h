#pragma once

#include <stdint.h>
#include <vector>
#include <string>
#include <memory>

struct ResolveMethodData
{
    uint64_t symbolAddress;
    uint64_t moduleAddress;
    char symbolName[1024];
};

struct StackFrame 
{
    uint64_t ip;    
    std::string method;
    uint64_t symbolAddress;
    uint64_t moduleAddress;
};

typedef int (*ResolveManagedMethod)(uintptr_t ip, ResolveMethodData* methodData);

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
    virtual std::vector<StackFrame> GetThreadFrames(int32_t tid, ResolveManagedMethod resolveManagedMethod) = 0;
};
