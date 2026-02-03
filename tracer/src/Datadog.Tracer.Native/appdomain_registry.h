#pragma once

#include <mutex>
#include <shared_mutex>
#include <unordered_set>

#include "corprof.h"

namespace trace
{

class AppDomainRegistry
{
public:
    bool Contains(AppDomainID id) const
    {
        std::shared_lock<std::shared_mutex> lock(_mutex);
        return _app_domains.find(id) != _app_domains.end();
    }

    void Add(AppDomainID id)
    {
        std::unique_lock<std::shared_mutex> lock(_mutex);
        _app_domains.insert(id);
    }

    size_t Remove(AppDomainID id)
    {
        std::unique_lock<std::shared_mutex> lock(_mutex);
        return _app_domains.erase(id);
    }

    size_t Size() const
    {
        std::shared_lock<std::shared_mutex> lock(_mutex);
        return _app_domains.size();
    }

private:
    mutable std::shared_mutex _mutex;
    std::unordered_set<AppDomainID> _app_domains;
};

} // namespace trace
