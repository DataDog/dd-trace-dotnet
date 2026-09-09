#pragma once

#include "Synchronized.hpp"

#include <type_traits>
#include <utility>

namespace trace
{

template <typename TModules, typename TSnapshot>
class ModuleLoadContext
{
public:
    template <typename TSnapshotFactory>
    ModuleLoadContext(Synchronized<TModules>& moduleIds, TSnapshotFactory&& snapshotFactory) :
        _snapshot(std::forward<TSnapshotFactory>(snapshotFactory)()), _modules(moduleIds)
    {
    }

    TModules& Modules()
    {
        return _modules.Ref();
    }

    const TSnapshot& Snapshot() const
    {
        return _snapshot;
    }

private:
    // Member initialization order is intentional: snapshotting can acquire m_probes_mutex, so it must happen before
    // _modules acquires module_ids.
    TSnapshot _snapshot;
    typename Synchronized<TModules>::Scope _modules;
};

template <typename TModules, typename TSnapshotFactory>
ModuleLoadContext(Synchronized<TModules>&, TSnapshotFactory&&)
    -> ModuleLoadContext<TModules, std::decay_t<std::invoke_result_t<TSnapshotFactory>>>;

} // namespace trace
