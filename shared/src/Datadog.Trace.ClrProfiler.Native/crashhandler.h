#pragma once

#include "util.h"
#include "werapi.h"

namespace datadog::shared::nativeloader
{
    struct WerContext
    {
        WCHAR* Environ;
        int32_t EnvironLength;
    };

    class CrashHandler
    {
    public:
        bool Register();
        bool Unregister();

    private:
        std::wstring _crashHandler;
        WerContext _context;
    };
}