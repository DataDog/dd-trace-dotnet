#pragma once
#include "string.h"

namespace datadog::shared::nativeloader
{

    void* LoadDynamicLibrary(std::string filePath);

    void* GetExternalFunction(void* instance, const char* funcName);

    bool FreeDynamicLibrary(void* handle);

    // GetEnvironmentValue returns the environment variable value for the given
    // name. Space is trimmed.
    WSTRING GetEnvironmentValue(const WSTRING& name);

    bool SetEnvironmentValue(const WSTRING& name, const WSTRING& value);

    // GetEnvironmentValues returns environment variable values for the given name
    // split by the delimiter. Space is trimmed and empty values are ignored.
    std::vector<WSTRING> GetEnvironmentValues(const WSTRING& name, const wchar_t delim);

    // GetEnvironmentValues calls GetEnvironmentValues with a semicolon delimiter.
    std::vector<WSTRING> GetEnvironmentValues(const WSTRING& name);

} // namespace datadog::shared::nativeloader