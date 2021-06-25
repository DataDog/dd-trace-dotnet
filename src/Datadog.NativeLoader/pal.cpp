#include "pal.h"

#include <Windows.h>
#include "logging.h"

namespace datadog
{
namespace nativeloader
{

    HINSTANCE LoadDynamicLibrary(std::string filePath)
    {
        Debug("LoadLibrary: ", filePath);
        return LoadLibrary(ToWSTRING(filePath).c_str());
    }

    void* GetExternalFunction(HINSTANCE instance, std::string funcName)
    {
        Debug("GetExternalFunction: ", funcName);
        return (void*)GetProcAddress(instance, funcName.c_str());
    }

} // namespace nativeloader
} // namespace datadog
