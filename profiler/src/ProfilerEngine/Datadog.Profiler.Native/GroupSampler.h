// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <unordered_set>

#include "GenericSampler.h"
#include "IConfiguration.h"

template <class TGroup>
struct UpscaleGroupInfo
{
public:
    TGroup Group;
    uint64_t RealCount;
    uint64_t SampledCount;
    uint64_t RealValue;
    uint64_t SampledValue;
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

    ~GroupSampler<TGroup>()
    {
        _sampler.Stop();
    }

public:
    struct GroupInfo
    {
        uint64_t RealCount;
        uint64_t SampledCount;
        uint64_t RealValue;
        uint64_t SampledValue;
    };

public:
    bool Sample(TGroup group, uint64_t value = 0)
    {
        std::unique_lock lock(_groupsMutex);

        // increment the real count and value for the given group
        GroupInfo* pInfo;
        AddInGroup(group, pInfo, value);

        auto [it, inserted] = _knownGroups.insert(std::move(group));
        if (inserted && _keepAtLeastOne)
        {
            // increment the sampled count and value for the given group
            pInfo->SampledCount++;
            pInfo->SampledValue += value;

            // This is the first time we see this group in this time window,
            // so force the sampling decision
            return _sampler.Keep();
        }

        auto sampled = _sampler.Sample();
        if (sampled)
        {
            // increment the sampled count and value for the given group
            pInfo->SampledCount++;
            pInfo->SampledValue += value;
        }

        return sampled;
    }

    // MUST be called under the lock
    void AddInGroup(TGroup group, GroupInfo*& groupInfo, uint64_t value = 0)
    {
        auto info = _groups.find(group);
        if (info != _groups.end())
        {
            groupInfo = &(info->second);
            info->second.RealCount++;
            info->second.RealValue += value;
            return;
        }

        // need to add the info of this new group
        GroupInfo gi;
        gi.RealCount = 1;
        gi.SampledCount = 0;
        gi.RealValue = value;
        gi.SampledValue = 0;
        auto slot = _groups.insert_or_assign(group, gi);
        groupInfo = &((*slot.first).second);
    }

    std::vector<UpscaleGroupInfo<TGroup>> GetGroups()
    {
        std::vector<UpscaleGroupInfo<TGroup>> upscaleGroups;
        std::unique_lock lock(_groupsMutex);

        upscaleGroups.reserve(_groups.size());

        for (auto& [name, info] : _groups)
        {
            auto sampledCount = info.SampledCount;
            if (sampledCount > 0)
            {
                upscaleGroups.push_back(UpscaleGroupInfo<TGroup>{name, info.RealCount, sampledCount, info.RealValue, info.SampledValue});
            }

            // reset groups count
            info.RealCount = 0;
            info.SampledCount = 0;
            info.RealValue = 0;
            info.SampledValue = 0;
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
