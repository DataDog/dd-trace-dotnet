// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <functional>
#include <mutex>
#include <unordered_map>
#include "IRuntimeIdStore.h"
#include "IService.h"

class RuntimeIdStore : public IService, public IRuntimeIdStore
{
public:
    RuntimeIdStore() = default;

    const char* GetName() override;
    bool Start() override;
    bool Stop() override;

    const std::string& GetId(AppDomainID appDomainId) override;

private:
    static const char* const ServiceName;
    static const char* const NativeLoaderFilename;
    static const char* const ExternalFunctionName;

    static void* LoadDynamicLibrary(std::string filePath);
    static void* GetExternalFunction(void* instance, const char* const funcName);
    static bool FreeDynamicLibrary(void* handle);

    void* _instance = nullptr;
    std::function<const std::string&(AppDomainID)> _getIdFn;

    std::mutex _cacheLock;
    // This is a fallback case when the profiler runs without the native loader
    // This can still happen for linux.
    // Once the profiler/tracer/... are started using the native loader
    // we can remove this fallback
    std::unordered_map<AppDomainID, std::string> _runtimeIdPerAppdomain;
};
