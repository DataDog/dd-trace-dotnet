#include "dynamic_library_base.h"

#if _WIN32
#include <Windows.h>
#else
#include <dlfcn.h>
#endif

#include "../../../shared/src/native-src/dd_filesystem.hpp"
#include "../../../shared/src/native-src/logger.h"
#include "../../../shared/src/native-src/string.h"

namespace datadog::shared
{

DynamicLibraryBase::DynamicLibraryBase(const std::string& filePath, Logger* logger) :
    _filePath{filePath}, _instance{nullptr}, _logger{logger}
{
}

void* DynamicLibraryBase::GetFunction(const std::string& funcName)
{
    _logger->Debug("GetFunction: ", funcName);

    if (_instance == nullptr)
    {
        _logger->Warn("GetFunction: The module instance is null.");
        return nullptr;
    }

#if _WIN32
    FARPROC dynFunc = GetProcAddress((HMODULE) _instance, funcName.c_str());
    if (dynFunc == NULL)
    {
        LPVOID msgBuffer;
        DWORD errorCode = GetLastError();

        FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL,
                      errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR) &msgBuffer, 0, NULL);

        if (msgBuffer != NULL)
        {
            _logger->Warn("GetFunction: Error loading dynamic function '", funcName, "': ", (LPTSTR) msgBuffer);
            LocalFree(msgBuffer);
        }
    }
    return dynFunc;
#else
    void* dynFunc = dlsym(_instance, funcName.c_str());
    if (dynFunc == nullptr)
    {
        char* errorMessage = dlerror();
        _logger->Warn("GetFunction: Error loading dynamic function '", funcName, "': ", errorMessage);
    }
    return dynFunc;
#endif
}

bool DynamicLibraryBase::Load()
{
    _logger->Debug("Load: ", _filePath);

#if _WIN32
    _instance = LoadLibraryEx(::shared::ToWSTRING(_filePath).c_str(),
                              NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR);
    if (_instance == NULL)
    {
        LPVOID msgBuffer;
        DWORD errorCode = GetLastError();

        FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL,
                      errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR) &msgBuffer, 0, NULL);

        if (msgBuffer != NULL)
        {
            _logger->Warn("Load: Error loading dynamic library '", _filePath, "': ", (LPTSTR) msgBuffer);
            LocalFree(msgBuffer);
        }
    }

#else
    _instance = dlopen(_filePath.c_str(), RTLD_LOCAL | RTLD_LAZY);
    if (_instance == nullptr)
    {
        char* errorMessage = dlerror();
        _logger->Warn("Load: Error loading dynamic library '", _filePath, "': ", errorMessage);
    }
#endif

    OnInitialized();
    return _instance != nullptr;
}

bool DynamicLibraryBase::Unload()
{
    _logger->Debug("Unloading ", _filePath);
    if (_instance == nullptr)
    {
        _logger->Warn("Unload: Unable to unload dynamic library '", _filePath,
                      ". Reason: An issue occured while loading it. See previous message.");
        return false;
    }

#if _WIN32
    auto result = FreeLibrary((HMODULE) _instance);
#else
#if LINUX
    auto [is_buggy, glibc_version] = ::shared::HasBuggyDlclose();
    if (is_buggy)
    {
        _logger->Warn("Unload: Skipping unload of dynamic library '", _filePath,
                      "' due to buggy dlclose implementation on this system.",
                      "GLIBC version 2.34-2.36 has a TLS-reuse bug that can cause crashes when unloading"
                      " shared libraries. Consider updating the installed version of glibc. Found GLIBC version: ", glibc_version);
        return false;
    }

#endif // if LINUX
    auto result = dlclose(_instance) == 0;
#endif

    if (!result)
    {
        _logger->Warn("Unload: DynamicInstanceImpl::~DynamicInstanceImpl: Error unloading: ", _filePath, " dynamic library.");
    }
    _instance = nullptr;

    return result;
}

const std::string& DynamicLibraryBase::GetFilePath()
{
    return _filePath;
}

} // namespace datadog::shared