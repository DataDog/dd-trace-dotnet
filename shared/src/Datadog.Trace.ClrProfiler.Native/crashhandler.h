#pragma once

#include "util.h"
#include "werapi.h"

namespace datadog::shared::nativeloader
{
    // Be careful when updating the WerContext structure, it is read across processes using ReadProcessMemory
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

        std::wstring _crashHandler;
        WerContext _context;
    };
}