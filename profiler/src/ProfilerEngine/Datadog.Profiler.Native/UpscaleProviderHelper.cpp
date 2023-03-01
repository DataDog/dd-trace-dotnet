// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GroupSampler.h"
#include "UpscaleProviderHelper.h"

bool UpscaleProviderHelper::GetGroups(GroupSampler<std::string>& sampler, std::vector<UpscaleGroupInfo>& upscaleGroups)
{
    upscaleGroups.clear();

    std::vector<std::pair<std::string, GroupSampler<std::string>::GroupInfo>> groups;
    if (!sampler.GetGroups(groups))
    {
        return false;
    }

    for (auto& bucket : groups)
    {
        auto count = bucket.second.Sampled;
        if (count > 0)
        {
            UpscaleGroupInfo info;
            info.Name = bucket.first;
            info.SampledCount = count;
            info.RealCount = bucket.second.Real;
            upscaleGroups.push_back(info);
        }

        // reset groups count
        bucket.second.Real = 0;
        bucket.second.Sampled = 0;
    }

    return (upscaleGroups.size() > 0);
}
