// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <vector>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog {

struct ProfileImpl
{
    ProfileImpl(ddog_prof_Profile prof) :
        _inner(prof)
    {
        _locations.resize(_locationsSize);
    }

    ~ProfileImpl()
    {
        ddog_prof_Profile_drop(&_inner);
    }

    operator ddog_prof_Profile*()
    {
        return &_inner;
    }

    operator std::tuple<std::vector<ddog_prof_Location>&, std::size_t&, ddog_prof_Profile*>()
    {
        return {_locations, _locationsSize, &_inner};
    }

private:
    std::vector<ddog_prof_Location> _locations;
    std::size_t _locationsSize = 512;

    ddog_prof_Profile _inner;
};

using profile_unique_ptr = std::unique_ptr<ProfileImpl>;
} // namespace libdatadog