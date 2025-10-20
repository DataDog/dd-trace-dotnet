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
    ProfileImpl(ddog_prof_ProfileAdapter  profile, ddog_prof_ScratchPadHandle scratchpad) :
        _inner(profile),
        _scratchpad(scratchpad)
    {
        _locations.resize(_locationsSize);
    }

    ~ProfileImpl()
    {
        ddog_prof_ProfileAdapter_drop(&_inner);
        ddog_prof_ScratchPad_drop(&_scratchpad);
    }

    operator ddog_prof_ProfileAdapter*()
    {
        return &_inner;
    }

    template <size_t Index>
    auto& get() &
    {
        static_assert(Index < 4, "Index out of bounds for ProfileImpl");
        if constexpr (Index == 0)
            return _locations;
        else if constexpr (Index == 1)
            return _locationsSize;
        else if constexpr (Index == 2)
            return _inner; // cppcheck-suppress missingReturn
        else if constexpr (Index == 3)
            return _scratchpad;
    }

private:
    std::vector<ddog_prof_LocationId> _locations;
    std::size_t _locationsSize = 512;

    ddog_prof_ProfileAdapter  _inner;
    ddog_prof_ScratchPadHandle _scratchpad;
};

using profile_unique_ptr = std::unique_ptr<ProfileImpl>;
} // namespace libdatadog

// --------------------------------------------------------------------------
// boilerplate code to allow structured binding for a ProfileImpl instance
// auto& [x, t, z] = obj;

namespace std {
template <>
struct tuple_size<libdatadog::ProfileImpl> : std::integral_constant<size_t, 4>
{
};

template <>
struct tuple_element<0, libdatadog::ProfileImpl>
{
    using type = std::vector<ddog_prof_LocationId>;
};
template <>
struct tuple_element<1, libdatadog::ProfileImpl>
{
    using type = std::size_t;
};
template <>
struct tuple_element<2, libdatadog::ProfileImpl>
{
    using type = ddog_prof_ProfileAdapter;
};
template <>
struct tuple_element<3, libdatadog::ProfileImpl>
{
    using type = ddog_prof_ScratchPadHandle;
};
} // namespace std