#pragma once
#include <memory>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog::detail {

struct TagsImpl
{
public:
    TagsImpl()
    {
        _tags = ddog_Vec_Tag_new();
    }

    ~TagsImpl()
    {
        ddog_Vec_Tag_drop(_tags);
    }

    explicit operator ddog_Vec_Tag* ()
    {
        return &_tags;
    }

    TagsImpl(TagsImpl const&) = delete;
    TagsImpl& operator=(TagsImpl const&) = delete;

    ddog_Vec_Tag _tags;
};
} // namespace libdatadog::detail