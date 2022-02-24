#include "../../../shared/src/native-src/pal.h"

#if _WIN32
#include <Windows.h>
#else
#include "dlfcn.h"
#endif

#include "log.h"

namespace datadog::shared::nativeloader
{

    void* LoadDynamicLibrary(std::string filePath)
    {
        Log::Debug("LoadDynamicLibrary: ", filePath);

#if _WIN32
        HMODULE dynLibPtr = LoadLibrary(::shared::ToWSTRING(filePath).c_str());
        if (dynLibPtr == nullptr)
        {
            LPVOID msgBuffer;
            DWORD errorCode = GetLastError();

            FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                          NULL, errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR) &msgBuffer, 0, NULL);

            if (msgBuffer != NULL)
            {
                Log::Warn("LoadDynamicLibrary: Error loading dynamic library '", filePath, "': ", (LPTSTR) msgBuffer);
                LocalFree(msgBuffer);
            }
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

    void* GetExternalFunction(void* instance, const char* funcName)
    {
        Log::Debug("GetExternalFunction: ", funcName);

        if (instance == nullptr)
        {
            Log::Warn("GetExternalFunction: The module instance is null.");
            return nullptr;
        }

#if _WIN32
        FARPROC dynFunc = GetProcAddress((HMODULE) instance, funcName);
        if (dynFunc == nullptr)
        {
            LPVOID msgBuffer;
            DWORD errorCode = GetLastError();

            FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                          NULL, errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR) &msgBuffer, 0, NULL);

            if (msgBuffer != NULL)
            {
                Log::Warn("GetExternalFunction: Error loading dynamic function '", funcName, "': ", (LPTSTR) msgBuffer);
                LocalFree(msgBuffer);
            }
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

    bool FreeDynamicLibrary(void* handle)
    {
        Log::Debug("FreeDynamicLibrary");

#if _WIN32
        return FreeLibrary((HMODULE) handle);
#else
        return dlclose(handle) == 0;
#endif
    }

} // namespace datadog::shared::nativeloader
