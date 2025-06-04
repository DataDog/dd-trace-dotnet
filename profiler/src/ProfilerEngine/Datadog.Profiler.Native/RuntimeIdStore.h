// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <functional>
#include <mutex>
#include <unordered_map>
#include "IRuntimeIdStore.h"
#include "ServiceBase.h"

class RuntimeIdStore
    :
    public IRuntimeIdStore,
    public ServiceBase
{
public:
    RuntimeIdStore() = default;

    const char* GetName() override;

    const char* GetId(AppDomainID appDomainId) override;

private:
    static const char* const ServiceName;
    static const char* const NativeLoaderFilename;
    static const char* const ExternalFunctionName;

    static void* LoadDynamicLibrary(std::string filePath);
    static void* GetExternalFunction(void* instance, const char* funcName);
    static bool FreeDynamicLibrary(void* handle);

    bool StartImpl() override;
    bool StopImpl() override;

    void* _instance = nullptr;
    std::function<const char*(AppDomainID)> _getIdFn;

    std::mutex _cacheLock;
    // This is a fallback case when the profiler runs without the native loader
    // This can still happen for linux.
    // Once the profiler/tracer/... are started using the native loader
    // we can remove this fallback
    std::unordered_map<AppDomainID, std::string> _runtimeIdPerAppdomain;
};
