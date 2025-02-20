// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "CrashReporting.h"
#include "ScopedHandle.h"
#include <functional>

struct ModuleInfo
{
    uintptr_t startAddress;
    uintptr_t endAddress;
    std::string path;
    BuildId buildId;
};

// PE32 and PE64 have different optional headers, which complexify the logic to fetch them
// This struct contains the common fields between the two types of headers
struct IMAGE_NT_HEADERS_GENERIC
{
    DWORD Signature;
    IMAGE_FILE_HEADER FileHeader;
    WORD    Magic;
};


class CrashReportingWindows : public CrashReporting
{
public:
    CrashReportingWindows(int32_t pid);

    ~CrashReportingWindows() override;

    int32_t STDMETHODCALLTYPE Initialize() override;

    BuildId ExtractBuildId(uintptr_t baseAddress);

    void SetMemoryReader(std::function<std::vector<BYTE>(uintptr_t, SIZE_T)> readMemory);

private:
    std::vector<std::pair<int32_t, std::string>> GetThreads() override;
    std::vector<StackFrame> GetThreadFrames(int32_t tid, ResolveManagedCallstack resolveManagedCallstack, void* context) override;
    std::string GetSignalInfo(int32_t signal) override;
    std::vector<ModuleInfo> GetModules();
    const ModuleInfo* FindModule(uintptr_t ip);

    static std::vector<BYTE> ReadRemoteMemory(HANDLE process, uintptr_t address, SIZE_T size);

    ScopedHandle _process;
    std::vector<ModuleInfo> _modules;
    std::function<std::vector<BYTE>(uintptr_t, SIZE_T)> _readMemory;
};
