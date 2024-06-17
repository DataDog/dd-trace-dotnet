// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "DlPhdrInfoWrapper.h"

#include <string.h>

// This class copy-wraps dl_phdr_info
// Why? We do not own dl_phdr_info objects when called-back by dl_iterate_phdr.
// This struct contains pointers which will be freed when dlclose is called.
// So we have to deep-copy them to avoid a potential crash:
// dlclose is happening when libunwind is calling our custom dl_iterate_phdr.
// The pointers will be invalidated and a crash can happen.
DlPhdrInfoWrapper::DlPhdrInfoWrapper(struct dl_phdr_info const* info, std::size_t size) :
    _info{0}, _phdr{nullptr}, _name{nullptr, &std::free}, _size(size)
{
    DeepCopy(_info, info);
}

std::pair<struct dl_phdr_info*, std::size_t> DlPhdrInfoWrapper::Get()
{
    return {&_info, _size};
}

void DlPhdrInfoWrapper::DeepCopy(struct dl_phdr_info& destination, struct dl_phdr_info const* source)
{
    // first copy all fields
    destination = *source;
    // then update the pointer-ones
    _name = {strdup(source->dlpi_name), &std::free};
    destination.dlpi_name = _name.get();

    _phdr = std::make_unique<ElfW(Phdr)[]>(source->dlpi_phnum);
    memcpy(_phdr.get(), source->dlpi_phdr, sizeof(ElfW(Phdr)) * source->dlpi_phnum);
    destination.dlpi_phdr = _phdr.get();

    // Those fields appeared in glibc 2.4 (with two others).
    // Since we compile with glibc 2.17, those fields are present (size of struct dl_phdr_info contains those fields),
    // so need to check the size/offset.
    // We do not know how to copy dlpi_tls_data field and libunwind does not use them, we can nullify/zeroify them
    destination.dlpi_tls_modid = 0;
    destination.dlpi_tls_data = nullptr;
}

bool DlPhdrInfoWrapper::IsSame(struct dl_phdr_info const * other) const
{
    return strcmp(_info.dlpi_name, other->dlpi_name) == 0 &&
           _info.dlpi_addr == other->dlpi_addr;
}