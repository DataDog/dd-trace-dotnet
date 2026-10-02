// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstddef>
#include <cstdint>
#include <string>

// Minimal memory-reading abstraction used by the cDAC backend (matches the article's IMemoryReader).
// An algorithmic contract never needs more than "read N bytes at address X"; ReadMemory MUST return
// false (never throw / crash) when the address is unmapped so a corrupt pointer degrades to "skip".
class IMemoryReader
{
public:
    virtual ~IMemoryReader() = default;

    // Pointer width of the target (8 for 64-bit, 4 for 32-bit). For in-process this is sizeof(void*).
    virtual int PointerSize() const = 0;

    // Reads exactly size bytes from address into buffer. Returns false on any failure (unmapped page,
    // guard page, size == 0 with non-empty intent, etc.). On false, buffer contents are unspecified.
    virtual bool ReadMemory(uintptr_t address, uint8_t* buffer, size_t size) = 0;

    // --- convenience helpers (non-virtual, all return false on failure) ---

    template <typename T>
    bool Read(uintptr_t address, T& value)
    {
        return ReadMemory(address, reinterpret_cast<uint8_t*>(&value), sizeof(T));
    }

    bool ReadPointer(uintptr_t address, uintptr_t& value)
    {
        if (PointerSize() == 8)
        {
            uint64_t v = 0;
            if (!Read(address, v))
            {
                return false;
            }
            value = static_cast<uintptr_t>(v);
            return true;
        }

        uint32_t v = 0;
        if (!Read(address, v))
        {
            return false;
        }
        value = static_cast<uintptr_t>(v);
        return true;
    }

    // Reads a NUL-terminated UTF-8 C-string (best effort, bounded). Returns empty on failure.
    std::string ReadCString(uintptr_t address, size_t maxLength = 4096)
    {
        std::string result;
        if (address == 0)
        {
            return result;
        }

        result.reserve(64);
        for (size_t i = 0; i < maxLength; i++)
        {
            char c = 0;
            if (!Read(address + i, c))
            {
                return std::string{};
            }
            if (c == '\0')
            {
                break;
            }
            result.push_back(c);
        }
        return result;
    }
};
