#include "pal.h"

#if _WIN32
#include <Windows.h>
#else
#include "dlfcn.h"
#endif

#include "logging.h"

namespace datadog
{
namespace nativeloader
{

#if _WIN32
    void* LoadDynamicLibrary(std::string filePath)
    {
        Debug("LoadLibrary: ", filePath);
        return LoadLibrary(ToWSTRING(filePath).c_str());
    }

    void* GetExternalFunction(void* instance, std::string funcName)
    {
        Debug("GetExternalFunction: ", funcName);
        return (void*)GetProcAddress((HMODULE)instance, funcName.c_str());
    }
#else
    void* LoadDynamicLibrary(std::string filePath)
    {
        Debug("LoadLibrary: ", filePath);
        void* dynLibPtr = dlopen(filePath.c_str(), RTLD_LOCAL | RTLD_LAZY);
        if (dynLibPtr == nullptr)
        {
            char* errorMessage = dlerror();
            Warn("Error loading dynamic library: ", errorMessage);
        }
        return dynLibPtr;
    }

    void* GetExternalFunction(void* instance, std::string funcName)
    {
        Debug("GetExternalFunction: ", funcName);
        void* dynFunc = dlsym(instance, funcName.c_str());
        if (dynFunc == nullptr)
        {
            char* errorMessage = dlerror();
            Warn("Error loading dynamic function: ", errorMessage);
        }
        return dynFunc;
    }
#endif

} // namespace nativeloader
} // namespace datadog
