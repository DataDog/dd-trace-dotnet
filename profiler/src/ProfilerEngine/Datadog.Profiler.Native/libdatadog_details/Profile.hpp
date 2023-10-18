#pragma once

#include <memory>
#include <vector>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog::detail {

using profile_type = ddog_prof_Profile;

struct ProfileDeleter;
using profile_unique_ptr = std::unique_ptr<profile_type, ProfileDeleter>;

struct ProfileDeleter
{
    void operator()(ddog_prof_Profile* o)
    {
        ddog_prof_Profile_drop(o);
    }
};

struct ProfileImpl
{
    using profile_raw_ptr = profile_type*;
    operator profile_raw_ptr()
    {
        return _inner.get();
    }

    std::vector<ddog_prof_Line> _lines;
    std::vector<ddog_prof_Location> _locations;
    std::size_t _locationsAndLinesSize = 512;

    profile_unique_ptr _inner;
};
} // namespace libdatadog::detail