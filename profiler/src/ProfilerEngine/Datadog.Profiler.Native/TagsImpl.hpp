// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <memory>

extern "C"
{
#include "datadog_poc/common.h"
#include "datadog_poc/profiling.h"
}

namespace libdatadog {

struct TagsImpl
{
public:
    TagsImpl(bool releaseOnClose = true) :
        _releaseOnClose{releaseOnClose}
    {
        ddog_vec_tag_new(&_tags);
    }

    ~TagsImpl()
    {
        if (_releaseOnClose)
        {
            ddog_vec_tag_drop(&_tags);
        }
    }

    explicit operator ddog_vec_tag*()
    {
        return &_tags;
    }

    TagsImpl(TagsImpl const&) = delete;
    TagsImpl& operator=(TagsImpl const&) = delete;

    ddog_vec_tag _tags;
    bool _releaseOnClose;
};
} // namespace libdatadog