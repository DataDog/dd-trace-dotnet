#pragma once

#include "../async-profiler/codeCache.h"

#include <cstdint>
#include <mutex>

class CodeCache;

class NativeLibraries
{
private:

    void Initialize();

public:
    NativeLibraries();
    static NativeLibraries* Instance();

    // void ParseLibrary(std::string const& libraryPath);

    class ScopedCodeCacheArray;
    ScopedCodeCacheArray GetCache();

    void UpdateCache();

    class ScopedCodeCacheArray
    {
    public:
        ScopedCodeCacheArray(CodeCacheArray& array, std::mutex & m);

        CodeCache* findLibraryByAddress(const void* pc);

    private:
        CodeCacheArray& _native_libs;
        std::lock_guard<std::mutex> _lock;
    };


private:
    CodeCacheArray _native_libs;
    std::mutex _m;
    // thread that would just wait to parse and enrich the codecachearray
};
