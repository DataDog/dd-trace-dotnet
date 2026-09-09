#pragma once

#include "Synchronized.hpp"

#include <utility>

namespace trace
{

template <typename TModuleIds, typename TSnapshotFactory, typename TCallback>
decltype(auto) WithModuleLockAfterSnapshot(TModuleIds& moduleIds, TSnapshotFactory&& snapshotFactory,
                                           TCallback&& callback)
{
    auto snapshot = std::forward<TSnapshotFactory>(snapshotFactory)();
    auto modules = moduleIds.Get();
    return std::forward<TCallback>(callback)(modules.Ref(), snapshot);
}

} // namespace trace
