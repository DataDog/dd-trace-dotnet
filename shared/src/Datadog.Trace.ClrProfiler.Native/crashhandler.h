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
        static std::unique_ptr<CrashHandler> Create();

        ~CrashHandler();

    private:
        CrashHandler();
        bool Register();
        bool Unregister();

        std::wstring _crashHandler;
        WerContext _context;
    };
}