// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "shared/src/native-src/dd_memory_resource.hpp"

class MmapMemoryResource : public shared::pmr::memory_resource
{
public:
    ~MmapMemoryResource() = default;

private:
    void* do_allocate(size_t _Bytes, size_t _Align) override;
    void do_deallocate(void* _Ptr, size_t _Bytes, size_t _Align) override;
    bool do_is_equal(const memory_resource& _That) const noexcept override;
};