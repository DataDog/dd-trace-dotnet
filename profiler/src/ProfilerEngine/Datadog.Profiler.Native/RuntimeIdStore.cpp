// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RuntimeIdStore.h"

#include "Log.h"
#include "shared/src/native-src/string.h"
#include "shared/src/native-src/util.h"

#ifdef _WINDOWS
#include "OsSpecificApi.h"
#define LIBRARY_FILE_EXTENSION ".dll"
#elif LINUX
#define LIBRARY_FILE_EXTENSION ".so"
#elif MACOS
#define LIBRARY_FILE_EXTENSION ".dylib"
#else
Error("unknown platform");
#endif

const char* const RuntimeIdStore::ServiceName = "RuntimeID Store";
const char* const RuntimeIdStore::ExternalFunctionName = "GetRuntimeId";
const char* const RuntimeIdStore::NativeLoaderFilename = "Datadog.Trace.ClrProfiler.Native" LIBRARY_FILE_EXTENSION;

bool RuntimeIdStore::Start()
{
    // should be set by native loader
    shared::WSTRING nativeLoaderPath = shared::GetEnvironmentValue(WStr("DD_INTERNAL_NATIVE_LOADER_PATH"));
    if (!nativeLoaderPath.empty())
    {
        _instance = LoadDynamicLibrary(shared::ToString(nativeLoaderPath));
    }
    else
    {    // variable not set - try to infer the location instead
        Log::Debug("DD_INTERNAL_NATIVE_LOADER_PATH variable not found. Inferring native loader path");

        // the native loader is always available in the same directory
#ifdef _WINDOWS
        auto nativeLoaderFilename = NativeLoaderFilename;
#else
        auto currentModulePath = fs::path(shared::GetCurrentModuleFileName());
        // the native loader is in the parent directory
        auto nativeLoaderPath = currentModulePath.parent_path() / NativeLoaderFilename;
        auto nativeLoaderFilename = nativeLoaderPath.string();
#endif
        _instance = LoadDynamicLibrary(nativeLoaderFilename);
    }


    if (_instance == nullptr)
    {
        // Running without the native proxy. This is expected when debugging locally.
        Log::Warn("The RuntimeID store service couldn't load the native proxy.");
        return true;
    }

    auto* externalFunction = GetExternalFunction(_instance, ExternalFunctionName);

    if (externalFunction == nullptr)
    {
        return false;
    }

    // /!\ when casting the function pointer externalFunction, we must not forget the calling convention
    // /!\ otherwise, the profiler will crash.
    _getIdFn = reinterpret_cast<const char*(STDMETHODCALLTYPE*)(AppDomainID)>(externalFunction);
    return _getIdFn != nullptr;
}

bool RuntimeIdStore::Stop()
{
    if (_instance != nullptr)
    {
        bool success = FreeDynamicLibrary(_instance);
        _instance = nullptr;
        return success;
    }

    return true;
}

const char* RuntimeIdStore::GetName()
{
    return RuntimeIdStore::ServiceName;
}

const char* RuntimeIdStore::GetId(AppDomainID appDomainId)
{
    if (_getIdFn != nullptr)
    {
        return _getIdFn(appDomainId);
    }

    std::lock_guard<std::mutex> lock(_cacheLock);
    auto& rid = _runtimeIdPerAppdomain[appDomainId];

    if (rid.empty())
    {
        rid = ::shared::GenerateRuntimeId();
    }

    return rid.c_str();
}

void* RuntimeIdStore::LoadDynamicLibrary(std::string filePath)
{
    Log::Debug("LoadDynamicLibrary: ", filePath);

#if _WINDOWS
    HMODULE dynLibPtr = LoadLibrary(::shared::ToWSTRING(filePath).c_str());
    if (dynLibPtr == NULL)
    {
        auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
        Log::Warn("LoadDynamicLibrary: Error loading dynamic library '", filePath, "' ", message);
    }
    return dynLibPtr;
#else
    void* dynLibPtr = dlopen(filePath.c_str(), RTLD_LOCAL | RTLD_LAZY);
    if (dynLibPtr == nullptr)
    {
        char* errorMessage = dlerror();
        Log::Warn("LoadDynamicLibrary: Error loading dynamic library '", filePath, "': ", errorMessage);
    }
    return dynLibPtr;
#endif
}

void* RuntimeIdStore::GetExternalFunction(void* instance, const char* const funcName)
{
    Log::Debug("GetExternalFunction: ", funcName);

    if (instance == nullptr)
    {
        Log::Warn("GetExternalFunction: The module instance is null.");
        return nullptr;
    }

#if _WINDOWS
    FARPROC dynFunc = GetProcAddress((HMODULE)instance, funcName);
    if (dynFunc == NULL)
    {
        auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
        Log::Warn("GetExternalFunction: Error loading dynamic function '", funcName, "' ", message);
    }
    return dynFunc;
#else
    void* dynFunc = dlsym(instance, funcName);
    if (dynFunc == nullptr)
    {
        char* errorMessage = dlerror();
        Log::Warn("GetExternalFunction: Error loading dynamic function '", funcName, "': ", errorMessage);
    }
    return dynFunc;
#endif
}

bool RuntimeIdStore::FreeDynamicLibrary(void* handle)
{
    Log::Debug("FreeDynamicLibrary");

#if _WINDOWS
    return FreeLibrary((HMODULE)handle);
#else
    return dlclose(handle) == 0;
#endif
}
