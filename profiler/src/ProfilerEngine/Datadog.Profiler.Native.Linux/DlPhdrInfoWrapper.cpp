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
DlPhdrInfoWrapper::DlPhdrInfoWrapper(struct dl_phdr_info const* info, std::size_t size, shared::pmr::memory_resource* allocator) :
    _info{0}, _phdr{nullptr}, _name{nullptr}, _size(size), _allocator{allocator}
{
    DeepCopy(_info, info);
}

std::pair<struct dl_phdr_info*, std::size_t> DlPhdrInfoWrapper::Get()
{
    return {&_info, _size};
}

using custom_deleter = std::function<void(void*)>;
template <class T, typename _Naked_T = std::remove_cv_t<T>>
std::unique_ptr<_Naked_T, custom_deleter> Duplicate(shared::pmr::memory_resource* allocator, T* src, std::size_t count)
{
    // Static assertions to that T does not need special treatment (ex:. calling ctor or dtor)
    static_assert(std::is_trivially_copyable_v<T>, 
        "T must be trivially copyable");
    static_assert(std::is_trivially_destructible_v<T>, 
        "T must be trivially destructible");

    if (src == nullptr)
    {
        return {nullptr};
    }

    const std::size_t size = sizeof(T) * count;
    auto* newPtr = static_cast<_Naked_T*>(allocator->allocate(size, alignof(T)));
    memcpy(newPtr, src, size);
    return {static_cast<_Naked_T*>(newPtr), [allocator, size](void* ptr) { allocator->deallocate(ptr, size, alignof(T)); }};
}

void DlPhdrInfoWrapper::DeepCopy(struct dl_phdr_info& destination, struct dl_phdr_info const* source)
{
    // first copy all fields
    destination = *source;
    // then update the pointer-ones
    _name = Duplicate(_allocator, source->dlpi_name, strlen(source->dlpi_name) + 1);
    destination.dlpi_name = _name.get();

    // copy header
    _phdr = Duplicate(_allocator, source->dlpi_phdr, source->dlpi_phnum);
    destination.dlpi_phdr = _phdr.get();

    // Those fields appeared in glibc 2.4 (with two others).
    // Since we compile with glibc 2.17, those fields are present (size of struct dl_phdr_info contains those fields),
    // so need to check the size/offset.
    // We do not know how to copy dlpi_tls_data field and libunwind does not use them, we can nullify/zeroify them
    destination.dlpi_tls_modid = 0;
    destination.dlpi_tls_data = nullptr;
}

bool DlPhdrInfoWrapper::IsSame(struct dl_phdr_info const* other) const
{
    return strcmp(_info.dlpi_name, other->dlpi_name) == 0 &&
           _info.dlpi_addr == other->dlpi_addr;
}