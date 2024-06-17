// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include <link.h>

#include <memory>
#include <stdlib.h>
#include <tuple>

// This class copy-wraps dl_phdr_info
// Why? We do not own dl_phdr_info objects when called-back by dl_iterate_phdr.
// This struct contains pointers which will be freed when dlclose is called.
// So we have to deep-copy them to avoid a potential crash:
// dlclose is happening when libunwind is calling our custom dl_iterate_phdr.
// The pointers will be invalidated and a crash can happen.
class DlPhdrInfoWrapper
{
public:
    DlPhdrInfoWrapper(struct dl_phdr_info const* info, std::size_t size);

    std::pair<struct dl_phdr_info*, std::size_t> Get();

    bool IsSame(struct dl_phdr_info const * other) const;

private:
    void DeepCopy(struct dl_phdr_info& destination, struct dl_phdr_info const * source);

    struct dl_phdr_info _info;
    std::unique_ptr<ElfW(Phdr)[]> _phdr;
    std::unique_ptr<char, decltype(&std::free)> _name;
    std::size_t _size;
};