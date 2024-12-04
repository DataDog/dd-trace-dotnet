// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <stdint.h>
#include <vector>
#include <string>
#include <memory>
#include <optional>
#include <utility>

#include "cor.h"
#include "corprof.h"

#include "shared/src/native-src/dd_span.hpp"

extern "C"
{
#ifdef LINUX
#include "datadog/blazesym.h"
#endif
#include "datadog/common.h"
#include "datadog/profiling.h"
}

struct ResolveMethodData
{
    uint64_t symbolAddress;
    uint64_t moduleAddress;
    uint64_t ip;
    uint64_t sp;

    bool isSuspicious;

    char symbolName[1024];
};

#ifdef LINUX
class ElfBuildId
{
private:
    struct ElfBuildIdImpl {
        ElfBuildIdImpl() : ElfBuildIdImpl(nullptr) {}   
        ElfBuildIdImpl(const char* path) : _ptr{nullptr}, _size{0} {
            if (path != nullptr)
            {
                _ptr = blaze_read_elf_build_id(path, &_size);
            }
        };
        ~ElfBuildIdImpl()
        {
            auto* ptr = std::exchange(_ptr, nullptr);
            if (ptr != nullptr && _size != 0)
            {
                _size = 0;
                ::free(ptr);
            }
        }

        ElfBuildIdImpl(ElfBuildIdImpl const&) = delete;
        ElfBuildIdImpl(ElfBuildIdImpl&&) = delete;
        ElfBuildIdImpl& operator=(ElfBuildIdImpl const&) = delete;
        ElfBuildIdImpl& operator=(ElfBuildIdImpl&&) = delete;

        std::uint8_t* _ptr;
        std::size_t _size;
    };
public:
    ElfBuildId() : ElfBuildId(nullptr) {}
    ElfBuildId(const char* path)
    : _impl{std::make_shared<ElfBuildIdImpl>(path)} {}

    shared::span<std::uint8_t> AsSpan() const
    {
        return shared::span(_impl->_ptr, _impl->_size);
    }

private:
    std::shared_ptr<ElfBuildIdImpl> _impl;
};
#endif

struct StackFrame 
{
    uint64_t ip;    
    uint64_t sp;
    std::string method;
    uint64_t symbolAddress;
    uint64_t moduleAddress;
    bool isSuspicious;
#ifdef _WINDOWS
    bool hasPdbInfo;
    DWORD pdbAge;
    GUID pdbSig;
#else
    ElfBuildId buildId;
#endif
};

struct Tag
{
    char* key;
    char* value;
};

// typedef int (*ResolveManagedMethod)(uintptr_t ip, ResolveMethodData* methodData);

typedef int (*ResolveManagedCallstack)(int32_t threadId, void* context, ResolveMethodData** methodData, int32_t* numberOfFrames);

// {3B3BA8A9-F807-43BF-A3A9-55E369C0C532}
const IID IID_ICrashReporting = {0x3b3ba8a9, 0xf807, 0x43bf, { 0xa3, 0xa9, 0x55, 0xe3, 0x69, 0xc0, 0xc5, 0x32} };

MIDL_INTERFACE("3B3BA8A9-F807-43BF-A3A9-55E369C0C532")
ICrashReporting : public IUnknown
{
public:    
    virtual HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) = 0;
    virtual ULONG STDMETHODCALLTYPE AddRef() = 0;
    virtual ULONG STDMETHODCALLTYPE Release() = 0;
    virtual int32_t STDMETHODCALLTYPE Initialize() = 0;
    virtual int32_t STDMETHODCALLTYPE GetLastError(const char** message, int32_t* length) = 0;
    virtual int32_t STDMETHODCALLTYPE AddTag(const char* key, const char* value) = 0;
    virtual int32_t STDMETHODCALLTYPE SetSignalInfo(int32_t signal, const char* description) = 0;
    virtual int32_t STDMETHODCALLTYPE ResolveStacks(int32_t crashingThreadId, ResolveManagedCallstack resolveCallback, void* context, bool* isSuspicious) = 0;
    virtual int32_t STDMETHODCALLTYPE SetMetadata(const char* libraryName, const char* libraryVersion, const char* family, Tag* tags, int32_t tagCount) = 0;
    virtual int32_t STDMETHODCALLTYPE Send() = 0;
    virtual int32_t STDMETHODCALLTYPE WriteToFile(const char* url) = 0;
    virtual int32_t STDMETHODCALLTYPE CrashProcess() = 0;
};

class CrashReporting : public ICrashReporting 
{
public:
    CrashReporting(int32_t pid);
    virtual ~CrashReporting();

    static CrashReporting* Create(int32_t pid);

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;
    int32_t STDMETHODCALLTYPE GetLastError(const char** message, int32_t* length) override;
    int32_t STDMETHODCALLTYPE Initialize() override;
    int32_t STDMETHODCALLTYPE AddTag(const char* key, const char* value) override;
    int32_t STDMETHODCALLTYPE SetSignalInfo(int32_t signal, const char* description) override;
    int32_t STDMETHODCALLTYPE ResolveStacks(int32_t crashingThreadId, ResolveManagedCallstack resolveCallback, void* context, bool* isSuspicious) override;
    int32_t STDMETHODCALLTYPE SetMetadata(const char* libraryName, const char* libraryVersion, const char* family, Tag* tags, int32_t tagCount) override;
    int32_t STDMETHODCALLTYPE Send() override;
    int32_t STDMETHODCALLTYPE WriteToFile(const char* url) override;
    int32_t STDMETHODCALLTYPE CrashProcess() override;

protected:
    int32_t _pid;
    int32_t _signal;
    std::optional<ddog_Error> _error;
    ddog_crasht_CrashInfo _crashInfo;
    void SetLastError(ddog_Error error);
    virtual std::vector<std::pair<int32_t, std::string>> GetThreads() = 0;
    virtual std::vector<StackFrame> GetThreadFrames(int32_t tid, ResolveManagedCallstack resolveManagedCallstack, void* context) = 0;
    virtual std::string GetSignalInfo(int32_t signal) = 0;

    static std::vector<StackFrame> MergeFrames(const std::vector<StackFrame>& nativeFrames, const std::vector<StackFrame>& managedFrames);
private:
    int32_t ExportImpl(ddog_Endpoint* endpoint);
    int32_t _refCount;
};