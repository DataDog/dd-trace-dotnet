// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <memory>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog {

struct TagsImpl
{
public:
    TagsImpl(bool releaseOnClose = true) :
        _releaseOnClose{releaseOnClose}
    {
        _tags = ddog_Vec_Tag_new();
    }

    ~TagsImpl()
    {
        if (_releaseOnClose)
        {
            ddog_Vec_Tag_drop(_tags);
        }
    }

    explicit operator ddog_Vec_Tag*()
    {
        return &_tags;
    }

    TagsImpl(TagsImpl const&) = delete;
    TagsImpl& operator=(TagsImpl const&) = delete;

    ddog_Vec_Tag _tags;
    bool _releaseOnClose;
};
} // namespace libdatadog