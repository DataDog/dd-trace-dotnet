#pragma once

#include <algorithm>
#include <atomic>
#include <memory>
#include <mutex>
#include <shared_mutex>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include "corprof.h"

namespace trace
{

struct ModuleState
{
    std::atomic<AppDomainID> app_domain_id{0};
    std::atomic<bool> is_internal{false};
    std::atomic<bool> is_ngen{false};
    std::atomic<bool> ngen_inliner_added{false};

    ModuleState() = default;
    ModuleState(AppDomainID appDomainId, bool isInternal, bool isNgen)
    {
        app_domain_id.store(appDomainId, std::memory_order_relaxed);
        is_internal.store(isInternal, std::memory_order_relaxed);
        is_ngen.store(isNgen, std::memory_order_relaxed);
    }

    ModuleState(const ModuleState& other)
    {
        app_domain_id.store(other.app_domain_id.load(std::memory_order_relaxed), std::memory_order_relaxed);
        is_internal.store(other.is_internal.load(std::memory_order_relaxed), std::memory_order_relaxed);
        is_ngen.store(other.is_ngen.load(std::memory_order_relaxed), std::memory_order_relaxed);
        ngen_inliner_added.store(other.ngen_inliner_added.load(std::memory_order_relaxed), std::memory_order_relaxed);
    }

    ModuleState& operator=(const ModuleState& other)
    {
        if (this != &other)
        {
            app_domain_id.store(other.app_domain_id.load(std::memory_order_relaxed), std::memory_order_relaxed);
            is_internal.store(other.is_internal.load(std::memory_order_relaxed), std::memory_order_relaxed);
            is_ngen.store(other.is_ngen.load(std::memory_order_relaxed), std::memory_order_relaxed);
            ngen_inliner_added.store(other.ngen_inliner_added.load(std::memory_order_relaxed), std::memory_order_relaxed);
        }
        return *this;
    }

    AppDomainID AppDomainId() const
    {
        return app_domain_id.load(std::memory_order_relaxed);
    }

    bool IsInternal() const
    {
        return is_internal.load(std::memory_order_relaxed);
    }

    bool IsNgen() const
    {
        return is_ngen.load(std::memory_order_relaxed);
    }
};

class ModuleRegistry
{
public:
    using ModuleStatePtr = std::shared_ptr<ModuleState>;

    bool Contains(ModuleID id) const
    {
        std::shared_lock<std::shared_mutex> lock(_mutex);
        return _modules_set.find(id) != _modules_set.end();
    }

    ModuleStatePtr TryGet(ModuleID id) const
    {
        std::shared_lock<std::shared_mutex> lock(_mutex);
        const auto it = _module_states.find(id);
        if (it == _module_states.end())
        {
            return nullptr;
        }

        return it->second;
    }

    std::vector<ModuleID> Snapshot() const
    {
        std::shared_lock<std::shared_mutex> lock(_mutex);
        return _modules_list;
    }

    size_t Size() const
    {
        std::shared_lock<std::shared_mutex> lock(_mutex);
        return _modules_list.size();
    }

    ModuleStatePtr Add(ModuleID id, const ModuleState& state)
    {
        return UpsertState(id, state, true);
    }

    ModuleStatePtr TrackState(ModuleID id, const ModuleState& state)
    {
        return UpsertState(id, state, false);
    }

    void Remove(ModuleID id)
    {
        std::unique_lock<std::shared_mutex> lock(_mutex);
        _modules_set.erase(id);
        _module_states.erase(id);

        const auto it = std::find(_modules_list.begin(), _modules_list.end(), id);
        if (it != _modules_list.end())
        {
            _modules_list.erase(it);
        }
    }

private:
    ModuleStatePtr UpsertState(ModuleID id, const ModuleState& state, bool track_membership)
    {
        std::unique_lock<std::shared_mutex> lock(_mutex);
        if (track_membership)
        {
            if (_modules_set.insert(id).second)
            {
                _modules_list.push_back(id);
            }
        }

        auto it = _module_states.find(id);
        if (it == _module_states.end())
        {
            auto state_ptr = std::make_shared<ModuleState>(state);
            _module_states.emplace(id, state_ptr);
            return state_ptr;
        }

        MergeState(*it->second, state);
        return it->second;
    }

    static void MergeState(ModuleState& target, const ModuleState& incoming)
    {
        const auto incoming_app_domain_id = incoming.app_domain_id.load(std::memory_order_relaxed);
        if (incoming_app_domain_id != 0)
        {
            target.app_domain_id.store(incoming_app_domain_id, std::memory_order_relaxed);
        }

        if (incoming.is_internal.load(std::memory_order_relaxed))
        {
            target.is_internal.store(true, std::memory_order_relaxed);
        }

        if (incoming.is_ngen.load(std::memory_order_relaxed))
        {
            target.is_ngen.store(true, std::memory_order_relaxed);
        }

        if (incoming.ngen_inliner_added.load(std::memory_order_relaxed))
        {
            target.ngen_inliner_added.store(true, std::memory_order_relaxed);
        }
    }

    mutable std::shared_mutex _mutex;
    std::vector<ModuleID> _modules_list;
    std::unordered_set<ModuleID> _modules_set;
    std::unordered_map<ModuleID, ModuleStatePtr> _module_states;
};

} // namespace trace
