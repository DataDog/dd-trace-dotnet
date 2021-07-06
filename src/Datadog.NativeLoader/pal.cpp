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
        return dlopen(filePath.c_str(), RTLD_LOCAL | RTLD_LAZY);
    }

    void* GetExternalFunction(void* instance, std::string funcName)
    {
        Debug("GetExternalFunction: ", funcName);
        return dlsym(instance, funcName.c_str());
    }
#endif

} // namespace nativeloader
} // namespace datadog
