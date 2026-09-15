#pragma once

#include "Synchronized.hpp"

namespace trace
{

template <typename TModules>
class ModuleLoadLock
{
public:
    template <typename TAfterLock>
    ModuleLoadLock(Synchronized<TModules>& moduleIds, TAfterLock&& afterLock) : _modules(moduleIds.Get())
    {
        afterLock();
    }

    TModules& Modules()
    {
        return _modules.Ref();
    }

private:
    typename Synchronized<TModules>::Scope _modules;
};

} // namespace trace
