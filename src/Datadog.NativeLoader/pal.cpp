#include "pal.h"

#if _WIN32
#include <Windows.h>
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
        return (void*)GetProcAddress(instance, funcName.c_str());
    }
#else
    void* LoadDynamicLibrary(std::string filePath)
    {
        Debug("LoadLibrary: ", filePath);
        return nullptr;
    }

    void* GetExternalFunction(void* instance, std::string funcName)
    {
        Debug("GetExternalFunction: ", funcName);
        return nullptr;
    }
#endif

} // namespace nativeloader
} // namespace datadog
