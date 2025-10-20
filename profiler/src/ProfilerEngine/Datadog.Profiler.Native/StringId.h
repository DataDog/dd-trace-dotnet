#pragma once

#include <cstddef>

namespace libdatadog {
    
    // opaque type
    class StringId {
    public:
        StringId();
        ~StringId() = default;
        StringId(const StringId& other) = default;
        StringId(StringId&& other) noexcept = default;
        StringId& operator=(const StringId& other) = default;
        StringId& operator=(StringId&& other) noexcept = default;

        operator bool() const noexcept;

        template <typename T>
        explicit operator T*() noexcept {
            static_assert(sizeof(T) == BufferSize, "Size mismatch");
            static_assert(alignof(T) == Alignment, "Alignment mismatch");
            return reinterpret_cast<T*>(storage);
        }

    private:
        friend class SymbolsStore;
        static constexpr size_t BufferSize = 8;
        static constexpr size_t Alignment = 8; // alignment of the internal implementation
        alignas(Alignment) unsigned char storage[BufferSize];
    };
}