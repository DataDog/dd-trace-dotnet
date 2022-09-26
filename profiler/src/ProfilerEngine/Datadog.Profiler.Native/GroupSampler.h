// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <unordered_set>

#include "GenericSampler.h"
#include "IConfiguration.h"


// Template class that support "sampling by group".
// At least one element in the group will ALWAYS be sampled
template <class TGroup>
class GroupSampler : public GenericSampler
{
public:
    GroupSampler<TGroup>(IConfiguration* configuration)
        :
        GenericSampler(configuration)
    {
    }

    bool Sample(TGroup group)
    {
        std::unique_lock lock(_knownGroupsMutex);

        if (_knownGroups.find(group) == _knownGroups.end())
        {
            // This is the first time we see this group in this time window,
            // force the sampling decision
            _knownGroups.insert(group);

            return _sampler.Keep();
        }

        // We've already seen this group, let the sampler decide
        return _sampler.Sample();
    }

protected:
    virtual void OnRollWindow() override
    {
        std::unique_lock lock(_knownGroupsMutex);
        _knownGroups.clear();
    }

private:
    std::unordered_set<std::string> _knownGroups;
    std::mutex _knownGroupsMutex;
};
