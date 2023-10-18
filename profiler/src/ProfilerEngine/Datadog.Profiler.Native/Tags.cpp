// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Tags.h"
#include "libdatadog_details/Tags.hpp"
#include "libdatadog_details/error_code.hpp"

#include "FfiHelper.h"

namespace libdatadog {

Tags::Tags() :
    _impl{std::make_unique<detail::TagsImpl>()}
{
}
Tags::Tags(std::initializer_list<std::pair<std::string, std::string>> tags) :
    Tags()
{
    for (auto&& [name, value] : tags)
    {
        Add(name, value);
    }
}

Tags::~Tags() = default;

Tags::Tags(Tags&& tags) noexcept
{
    _impl.swap(tags._impl);
}

Tags& Tags::operator=(Tags&& tags) noexcept
{
    _impl = std::move(tags._impl);
    return *this;
}

libdatadog::error_code Tags::Add(std::string const& name, std::string const& value)
{
    auto ffiName = FfiHelper::StringToCharSlice(name);
    auto ffiValue = FfiHelper::StringToCharSlice(value);

    auto pushResult = ddog_Vec_Tag_push(&_impl->_tags, ffiName, ffiValue);
    if (pushResult.tag == DDOG_VEC_TAG_PUSH_RESULT_ERR)
    {
        return detail::make_error(pushResult.err);
    }
    return detail::make_success();
}
} // namespace libdatadog