#include "dynamic_library_base.h"

#if _WIN32
#include <Windows.h>
#else
#include "dlfcn.h"
#endif

#include "../../../shared/src/native-src/dd_filesystem.hpp"
#include "../../../shared/src/native-src/string.h"

DynamicLibraryBase::DynamicLibraryBase(const std::string& filePath) :
    _filePath{filePath}
{
}

void* DynamicLibraryBase::GetFunction(const std::string& funcName)
{
    //StaticLogger::Debug("GetExternalFunction: ", funcName);

    if (_instance == nullptr)
    {
        //_logger->Warn("GetExternalFunction: The module instance is null.");
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
            //StaticLogger::Warn("GetExternalFunction: Error loading dynamic function '", funcName,
            //                   "': ", (LPTSTR) msgBuffer);
            LocalFree(msgBuffer);
        }
    }
    return dynFunc;
#else
    void* dynFunc = dlsym(instance, funcName);
    if (dynFunc == nullptr)
    {
        char* errorMessage = dlerror();
        //::Warn("GetExternalFunction: Error loading dynamic function '", funcName, "': ", errorMessage);
    }
    return dynFunc;
#endif
}

bool DynamicLibraryBase::Load()
{
    //Log::Debug("LoadDynamicLibrary: ", filePath);

#if _WIN32
    _instance = LoadLibrary(::shared::ToWSTRING(_filePath).c_str());
    if (_instance == NULL)
    {
        LPVOID msgBuffer;
        DWORD errorCode = GetLastError();

        FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL,
                      errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR) &msgBuffer, 0, NULL);

        if (msgBuffer != NULL)
        {
            //Log::Warn("LoadDynamicLibrary: Error loading dynamic library '", filePath, "': ", (LPTSTR) msgBuffer);
            LocalFree(msgBuffer);
        }
    }

#else
    _instance = dlopen(filePath.c_str(), RTLD_LOCAL | RTLD_LAZY);
    if (_instance == nullptr)
    {
        char* errorMessage = dlerror();
        //Log::Warn("LoadDynamicLibrary: Error loading dynamic library '", filePath, "': ", errorMessage);
    }
#endif

    AfterLoad();
    return _instance != nullptr;
}

bool DynamicLibraryBase::Unload()
{
    BeforeUnload();

    //Log::Debug("FreeDynamicLibrary");

#if _WIN32
    auto result = FreeLibrary((HMODULE) _instance);
#else
    auto result = dlclose(handle) == 0;
#endif

    if (!result)
    {
        //Log::Warn("DynamicInstanceImpl::~DynamicInstanceImpl: Error unloading: ", m_filepath, " dynamic library.");
    }
    _instance = nullptr;
    // do stuff

    return result;
}

const std::string& DynamicLibraryBase::GetFilePath()
{
    return _filePath;
}
