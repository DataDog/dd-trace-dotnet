#pragma once
#include "../../../shared/src/native-src/string.h"
#include <vector>

namespace datadog::shared::nativeloader
{

    void* LoadDynamicLibrary(std::string filePath);

    void* GetExternalFunction(void* instance, const char* funcName);

    bool FreeDynamicLibrary(void* handle);

} // namespace datadog::shared::nativeloader