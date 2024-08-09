// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Tags.h"

#include "FfiHelper.h"
#include "Log.h"
#include "TagsImpl.hpp"

#include <cassert>

namespace libdatadog {

Tags::Tags(bool releaseOnClose) :
    _impl{std::make_unique<TagsImpl>(releaseOnClose)}
{
}
Tags::Tags(std::initializer_list<std::pair<std::string, std::string>> tags, bool releaseOnClose) :
    Tags(releaseOnClose)
{
    for (auto&& [name, value] : tags)
    {
        auto ec = Add(name, value);
        if (!ec)
        {
            Log::Debug("Failed to add tag with name '", name, "' and value '", value, "' while creating the Tags object. Reason: ", ec.message());
        }
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

libdatadog::Success Tags::Add(std::string const& name, std::string const& value)
{
    auto ffiName = FfiHelper::StringToCharSlice(name);
    auto ffiValue = FfiHelper::StringToCharSlice(value);

    auto pushResult = ddog_Vec_Tag_push(&_impl->_tags, ffiName, ffiValue);
    if (pushResult.tag == DDOG_VEC_TAG_PUSH_RESULT_ERR)
    {
        return make_error(pushResult.err);
    }
    return make_success();
}
} // namespace libdatadog