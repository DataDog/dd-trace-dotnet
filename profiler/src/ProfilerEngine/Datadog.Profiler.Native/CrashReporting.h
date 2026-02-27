// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <stdint.h>
#include <iomanip>
#include <vector>
#include <string>
#include <sstream>
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

struct BuildId
{
public:
#ifdef LINUX
    static BuildId From(const char* path);
#else
    static BuildId From(GUID guid, DWORD age);
#endif

    BuildId() :
        _buildId{}
    {
    }

    operator std::string_view() const
    {
        return _buildId;
    }

    BuildId(BuildId const&) = delete;
    BuildId& operator=(BuildId const&) = delete;
    BuildId(BuildId&&) = default;
    BuildId& operator=(BuildId&&) = default;

private:
    BuildId(std::string buildId) : _buildId{std::move(buildId)}
    {}

    std::string _buildId;
};

struct StackFrame 
{
    uint64_t ip;    
    uint64_t sp;
    std::string method;
    uint64_t symbolAddress;
    uint64_t moduleAddress;
    bool isSuspicious;
    std::string_view buildId;
    std::string_view modulePath;
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
    // only for tests
    virtual int32_t STDMETHODCALLTYPE Panic() = 0;
    virtual int32_t STDMETHODCALLTYPE SetSignalInfo(int32_t signal, const char* description) = 0;
    virtual int32_t STDMETHODCALLTYPE ResolveStacks(int32_t crashingThreadId, void* crashingThreadContext, ResolveManagedCallstack resolveCallback, void* context, bool* isSuspicious) = 0;
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
    int32_t STDMETHODCALLTYPE Panic() override;
    int32_t STDMETHODCALLTYPE SetSignalInfo(int32_t signal, const char* description) override;
    int32_t STDMETHODCALLTYPE ResolveStacks(int32_t crashingThreadId, void* crashingThreadContext, ResolveManagedCallstack resolveCallback, void* context, bool* isSuspicious) override;
    int32_t STDMETHODCALLTYPE SetMetadata(const char* libraryName, const char* libraryVersion, const char* family, Tag* tags, int32_t tagCount) override;
    int32_t STDMETHODCALLTYPE Send() override;
    int32_t STDMETHODCALLTYPE WriteToFile(const char* url) override;
    int32_t STDMETHODCALLTYPE CrashProcess() override;

protected:
    uint32_t _pid;
    int32_t _signal;
    std::optional<ddog_Error> _error;
    ddog_crasht_Handle_CrashInfoBuilder _builder;
    void SetLastError(ddog_Error error);
    virtual std::vector<std::pair<int32_t, std::string>> GetThreads() = 0;
    virtual std::vector<StackFrame> GetThreadFrames(int32_t tid, void* threadContext, ResolveManagedCallstack resolveManagedCallstack, void* context) = 0;
    virtual std::string GetSignalInfo(int32_t signal) = 0;

#ifdef DD_TEST
public:
#endif
    static std::vector<StackFrame> MergeFrames(const std::vector<StackFrame>& nativeFrames, const std::vector<StackFrame>& managedFrames);
private:
    int32_t ExportImpl(ddog_Endpoint* endpoint);
    
    template <typename T>
    std::pair<decltype(T::ok), bool> ExtractResult(T v);

    int32_t _refCount;
};