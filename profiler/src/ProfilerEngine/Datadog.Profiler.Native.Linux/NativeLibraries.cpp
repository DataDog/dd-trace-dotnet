#include "NativeLibraries.h"

#include "OpSysTools.h"

#include "../async-profiler/symbols.h"

#include <memory>
#include <utility>

void NativeLibraries::Initialize()
{
    UpdateCache();
}

NativeLibraries::NativeLibraries()
{
    Initialize();
}

NativeLibraries* NativeLibraries::Instance()
{
    static std::unique_ptr<NativeLibraries> _instance = std::make_unique<NativeLibraries>();

    return _instance.get();
}

// with locking
NativeLibraries::ScopedCodeCacheArray NativeLibraries::GetCache()
{
    return ScopedCodeCacheArray(_native_libs, _m);
}

void Clean(CodeCacheArray&& array)
{
    for (auto i = 0; i < array.count(); i++)
    {
        delete array[i];
    }
}

void NativeLibraries::UpdateCache()
{
    // check if we need to update the cache first
    // if not
    //   just return

    // otherwise
    std::unique_lock lock(_m);
    Symbols::parseLibraries(&_native_libs, false);

    // TODO look for avoiding copies
    // TODO lock
    // CodeCacheArray cc;
    // std::swap(cc, _native_libs);
    // Clean(std::move(cc));
}

NativeLibraries::ScopedCodeCacheArray::ScopedCodeCacheArray(CodeCacheArray& arrayz, std::mutex& m) :
    _lock{m},
    _native_libs{arrayz}
{
}

CodeCache* NativeLibraries::ScopedCodeCacheArray::findLibraryByAddress(const void* pc)
{
    const int native_lib_count = _native_libs.count();
    for (int i = 0; i < native_lib_count; i++)
    {
        if (_native_libs[i]->contains(pc))
        {
            return _native_libs[i];
        }
    }
    return nullptr;
}

class UnwindTablesStore::UnwindTables : public CodeCacheArray
{
};

UnwindTablesStore::UnwindTablesStore() = default;

UnwindTablesStore::~UnwindTablesStore()
{
    Stop();
    
    std::unique_lock lock(_tablesLock);
    for (auto i = 0; i < _tables->count(); i++)
    {
        auto* table = (*_tables)[i];
        delete table;
    }
    _tables.reset();
}

// implement destructor to clean
bool UnwindTablesStore::Start()
{
    _mustStop = false;
    _tables = std::make_unique<UnwindTables>();
    LoadUnwindTables();
    _tablesReloader = std::thread(&UnwindTablesStore::ReloadUnwindTables, this);
    
    return true;
}

bool UnwindTablesStore::Stop()
{
    if (_mustStop)
    {
        return true;
    }

    _mustStop = true;
    _updaterPromise.set_value();
    _tablesReloader.join();

    return true;
}

const char* UnwindTablesStore::GetName() const
{
    return "Unwind Tables store";
}

UnwindTablesStore::UnwindTable* UnwindTablesStore::FindByAddress(const void* address)
{
    // TODO binarySearch if native_lib_count is big?
    std::unique_lock lock(_tablesLock);
    const int native_lib_count = _tables->count();
    for (int i = 0; i < native_lib_count; i++)
    {
        if ((*_tables)[i]->contains(address))
        {
            return reinterpret_cast<UnwindTable*>((*_tables)[i]);
        }
    }
    return nullptr;
}

extern "C" unsigned int dd_get_nb_opened_libraries() __attribute__((weak));

// TODO rename to ReloadUnwindTables
void UnwindTablesStore::ReloadUnwindTables()
{
    const auto future = _updaterPromise.get_future();
    std::uint32_t previous_nb_opened_libraries = 0;

    while (future.wait_for(CollectingPeriod) == std::future_status::timeout)
    {
        bool shouldReload = true;
        if (dd_get_nb_opened_libraries != nullptr)
        {
            auto nb_opened_libraries = dd_get_nb_opened_libraries();
            shouldReload = nb_opened_libraries != previous_nb_opened_libraries;
            previous_nb_opened_libraries = nb_opened_libraries;
        }
        if (shouldReload)
        {
            LoadUnwindTables();
        }
    }
}

void UnwindTablesStore::LoadUnwindTables()
{
    std::unique_lock lock(_tablesLock);
    Symbols::parseLibraries(_tables.get(), false);
}
//void UnwindTablesStore::UpdateUnwindTables()
//{
//    const auto future = _updaterPromise.get_future();
//
//    {
//        std::unique_lock lock(_tablesLock);
//        Symbols::parseLibraries(_tables.get(), false);
//    }
//
//    while (future.wait_for(CollectingPeriod) == std::future_status::timeout)
//    {
//        std::unique_lock lock(_tablesLock);
//        Symbols::parseLibraries(_tables.get(), false);
//    }
//}