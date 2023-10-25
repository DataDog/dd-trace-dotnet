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
