// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <type_traits>
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

    template <size_t Index>
    auto& get() &
    {
        static_assert(Index < 3, "Index out of bounds for ProfileImpl");
        if constexpr (Index == 0)
            return _locations;
        else if constexpr (Index == 1)
            return _locationsSize;
        else if constexpr (Index == 2)
            return _inner; // cppcheck-suppress missingReturn
    }

private:
    std::vector<ddog_prof_Location> _locations;
    std::size_t _locationsSize = 512;

    ddog_prof_Profile _inner;
};

using profile_unique_ptr = std::unique_ptr<ProfileImpl>;
} // namespace libdatadog

// --------------------------------------------------------------------------
// boilerplate code to allow structured binding for a ProfileImpl instance
// auto& [x, t, z] = obj;

namespace std {
template <>
struct tuple_size<libdatadog::ProfileImpl> : std::integral_constant<size_t, 3>
{
};

template <>
struct tuple_element<0, libdatadog::ProfileImpl>
{
    using type = std::vector<ddog_prof_Location>;
};
template <>
struct tuple_element<1, libdatadog::ProfileImpl>
{
    using type = std::size_t;
};
template <>
struct tuple_element<2, libdatadog::ProfileImpl>
{
    using type = ddog_prof_Profile;
};
} // namespace std