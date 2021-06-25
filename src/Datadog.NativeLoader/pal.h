#ifndef PAL_H_
#define PAL_H_

#include "string.h"

namespace datadog
{
namespace nativeloader
{

    HINSTANCE LoadDynamicLibrary(std::string filePath);

    void* GetExternalFunction(HINSTANCE instance, std::string funcName);

} // namespace nativeloader
} // namespace datadog

#endif