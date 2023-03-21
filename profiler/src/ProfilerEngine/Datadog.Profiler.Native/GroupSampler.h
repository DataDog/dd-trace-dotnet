// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <unordered_set>

#include "GenericSampler.h"
#include "IConfiguration.h"
#include "IUpscaleProvider.h"

template <class TGroup>
struct UpscaleGroupInfo
{
public:
    TGroup Group;
    uint64_t RealCount;
    uint64_t SampledCount;
};

// Template class that support "sampling by group".
// At least one element in the group will ALWAYS be sampled if keepAtleastOne is true
template <class TGroup>
class GroupSampler : public GenericSampler
{
public:
    GroupSampler<TGroup>(int32_t samplesLimit, std::chrono::seconds uploadInterval, bool keepAtLeastOne = true)
        :
        GenericSampler(samplesLimit, uploadInterval),
        _keepAtLeastOne{keepAtLeastOne}
    {
    }

public:
    struct GroupInfo
    {
        uint64_t Real;
        uint64_t Sampled;
    };

public:
    bool Sample(TGroup group)
    {
        std::unique_lock lock(_groupsMutex);

        // increment the real count for the given group
        GroupInfo* pInfo;
        AddInGroup(group, pInfo);

        auto [it, inserted] = _knownGroups.insert(std::move(group));
        if (inserted && _keepAtLeastOne)
        {
            // increment the sampled count for the given group
            pInfo->Sampled++;

            // This is the first time we see this group in this time window,
            // so force the sampling decision
            return _sampler.Keep();
        }

        auto sampled = _sampler.Sample();
        if (sampled)
        {
            // increment the sampled count for the given group
            pInfo->Sampled++;
        }

        return sampled;
    }

    // MUST be called under the lock
    void AddInGroup(TGroup group, GroupInfo*& groupInfo)
    {
        auto info = _groups.find(group);
        if (info != _groups.end())
        {
            groupInfo = &(info->second);
            info->second.Real++;

            return;
        }

        // need to add the info of this new group
        GroupInfo gi;
        gi.Real = 1;
        gi.Sampled = 0;
        auto slot = _groups.insert_or_assign(group, gi);
        groupInfo = &((*slot.first).second);
    }

    std::vector<UpscaleGroupInfo<TGroup>> GetGroups()
    {
        std::unique_lock lock(_groupsMutex);

        std::vector<UpscaleGroupInfo<TGroup>> upscaleGroups;

        for (auto& bucket : _groups)
        {
            auto count = bucket.second.Sampled;
            if (count > 0)
            {
                UpscaleGroupInfo<TGroup> info;
                info.Group = bucket.first;
                info.SampledCount = count;
                info.RealCount = bucket.second.Real;
                upscaleGroups.push_back(info);
            }

            // reset groups count
            bucket.second.Real = 0;
            bucket.second.Sampled = 0;
        }

        return upscaleGroups;
    }

protected:
    void OnRollWindow() override
    {
        std::unique_lock lock(_groupsMutex);
        _knownGroups.clear();
    }

private:
    // _knownGroups is used to detect when a new group appear during a given window
    std::unordered_set<TGroup> _knownGroups;

    // _groups keeps track of the sampled/real count per group
    std::unordered_map<TGroup, GroupInfo> _groups;
    bool _keepAtLeastOne;

    std::mutex _groupsMutex;
};
