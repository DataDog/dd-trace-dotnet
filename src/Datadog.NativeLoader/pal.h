#ifndef PAL_H_
#define PAL_H_

#include "string.h"

namespace datadog
{
namespace nativeloader
{

    void* LoadDynamicLibrary(std::string filePath);

    void* GetExternalFunction(void* instance, std::string funcName);

} // namespace nativeloader
} // namespace datadog

#endif