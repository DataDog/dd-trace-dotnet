#pragma once

#include "Synchronized.hpp"

#include <utility>

namespace trace
{

template <typename TModules>
class ModuleLoadLock
{
public:
    template <typename TBeforeLock>
    ModuleLoadLock(Synchronized<TModules>& moduleIds, TBeforeLock&& beforeLock) :
        _modules(Acquire(moduleIds, std::forward<TBeforeLock>(beforeLock)))
    {
    }

    TModules& Modules()
    {
        return _modules.Ref();
    }

private:
    template <typename TBeforeLock>
    static typename Synchronized<TModules>::Scope Acquire(Synchronized<TModules>& moduleIds, TBeforeLock&& beforeLock)
    {
        std::forward<TBeforeLock>(beforeLock)();
        return moduleIds.Get();
    }

    typename Synchronized<TModules>::Scope _modules;
};

} // namespace trace
