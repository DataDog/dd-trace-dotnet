// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include <link.h>

#include <memory>
#include <stdlib.h>
#include <string.h>
#include <functional>

#include "shared/src/native-src/dd_memory_resource.hpp"

// This class copy-wraps dl_phdr_info
// Why? We do not own dl_phdr_info objects when called-back by dl_iterate_phdr.
// This struct contains pointers which will be freed when dlclose is called.
// So we have to deep-copy them to avoid a potential crash:
// dlclose is happening when libunwind is calling our custom dl_iterate_phdr.
// The pointers will be invalidated and a crash can happen.
class DlPhdrInfoWrapper
{
public:
    DlPhdrInfoWrapper(struct dl_phdr_info const* info, std::size_t size, shared::pmr::memory_resource* allocator);

    std::pair<struct dl_phdr_info*, std::size_t> Get();

    bool IsSame(struct dl_phdr_info const * other) const;

private:
    struct dl_phdr_info _info;
    void DeepCopy(struct dl_phdr_info& destination, struct dl_phdr_info const * source);

    using custom_deleter = std::function<void(void*)>;
    std::unique_ptr<ElfW(Phdr), custom_deleter> _phdr;
    std::unique_ptr<char, custom_deleter> _name;
    std::size_t _size;
    shared::pmr::memory_resource* _allocator;
};