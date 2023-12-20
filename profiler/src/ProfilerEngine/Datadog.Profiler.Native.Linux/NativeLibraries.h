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

#include <memory>
#include <future>
#include <thread>
#include <mutex>
#include <chrono>
#include <condition_variable>


using namespace std::chrono_literals;

class UnwindTablesStore
{
public:
    using UnwindTable = CodeCache;

    UnwindTablesStore();
    ~UnwindTablesStore();

    // to inherit from IService
    bool Start(); 
    bool Stop();

    const char* GetName() const;

    UnwindTable* FindByAddress(const void* address);

private:
    void ReloadUnwindTables();
    void LoadUnwindTables();

    inline static constexpr std::chrono::nanoseconds CollectingPeriod = 1s;

    class UnwindTables;
    std::unique_ptr<UnwindTables> _tables;
    std::mutex _tablesLock;

    std::thread _tablesReloader;
    std::promise<void> _updaterPromise;

    bool _mustStop;
};
