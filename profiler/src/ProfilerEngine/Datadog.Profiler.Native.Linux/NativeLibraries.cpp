#include "NativeLibraries.h"

#include "OpSysTools.h"
#include "Log.h"

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
}

// implement destructor to clean
bool UnwindTablesStore::Start()
{
    _mustStop = false;
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

std::shared_ptr<UnwindTablesStore::UnwindTable> UnwindTablesStore::FindByAddress(const void* address)
{
    std::unique_lock lock(_tablesLock);
    
    using store_type = typeof(_tables);
    store_type::difference_type count, step;
    store_type::iterator it, first;
    
    first = _tables.begin();
    count = std::distance(_tables.cbegin(), _tables.cend());

    while (count > 0)
    {
        it = first;
        step = count / 2;
        std::advance(it, step);
 
        auto const& current = *it;

        if (current->contains(address))
        {
            return current;
        }

        if (current->minAddress() < address)
        {
            first = ++it;
            count -= step + 1;
        }
        else
            count = step;
    }
 
    return nullptr;
}

extern "C" unsigned int dd_get_nb_opened_libraries() __attribute__((weak));

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
    _tables = Symbols::parseLibraries(false);
    
    std::ostringstream oss;
    const int native_lib_count = _tables.size();
    Log::Debug("=======================");
    for (auto const& table : _tables)
    {
        oss << "* " << table->name() << " | " << std::hex << (std::uintptr_t)(table->minAddress()) << " - " << (std::uintptr_t)(table->maxAddress()) << "\n";
    }
    Log::Debug(oss.str());
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