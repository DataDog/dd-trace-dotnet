// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CommittedMemoryProbe.h"
#include "IMemoryReader.h"
#include "LogicalDescriptor.h"

#include <cstring>
#include <map>
#include <string>

namespace cdac
{
// The contract Target: the object the algorithmic contracts operate on. Exposes primitive memory
// reads plus accessors for the logical descriptor's globals and type layouts (the article's minimal
// vocabulary). Value-returning reads return 0/default on failure; the Has*/Try* guards are the
// mechanism that lets one reader survive field-level layout drift across builds.
class Target
{
public:
    Target(IMemoryReader& reader, LogicalDescriptor descriptor) :
        _reader(reader), _descriptor(std::move(descriptor))
    {
    }

    int PointerSize() const { return _reader.PointerSize(); }

    const std::map<std::string, std::string>& Contracts() const { return _descriptor.Contracts; }

    bool Refresh() { return _descriptor.Refresh(); }
    int PendingSubDescriptorCount() const { return _descriptor.PendingSubDescriptorCount(); }

    // --- primitive reads (value-returning; 0/default on failure) ---

    template <typename T>
    T Read(uintptr_t address)
    {
        T value{};
        _reader.Read(address, value);
        return value;
    }

    template <typename T>
    bool TryRead(uintptr_t address, T& value)
    {
        return _reader.Read(address, value);
    }

    uintptr_t ReadPointer(uintptr_t address)
    {
        uintptr_t value = 0;
        _reader.ReadPointer(address, value);
        return value;
    }

    // Committed bytes within [base, base + reserved), recovered by probing pages under the reader's
    // fault guard (the cDAC target is this same process). Used for loader/code blocks whose contract
    // only exposes a reserved VirtualSize.
    uint64_t ProbeCommitted(uintptr_t base, uint64_t reserved)
    {
        return eeheap::ProbeCommittedBytes(_reader, base, reserved);
    }

    // --- globals ---

    bool HasGlobal(const std::string& name) const
    {
        return _descriptor.Globals.find(name) != _descriptor.Globals.end();
    }

    uintptr_t ReadGlobalPointer(const std::string& name) const
    {
        auto it = _descriptor.Globals.find(name);
        return it == _descriptor.Globals.end() ? 0 : static_cast<uintptr_t>(it->second.NumericValue);
    }

    bool TryReadGlobalPointer(const std::string& name, uintptr_t& value) const
    {
        auto it = _descriptor.Globals.find(name);
        if (it == _descriptor.Globals.end())
        {
            value = 0;
            return false;
        }
        value = static_cast<uintptr_t>(it->second.NumericValue);
        return true;
    }

    template <typename T>
    T ReadGlobal(const std::string& name) const
    {
        auto it = _descriptor.Globals.find(name);
        T value{};
        if (it != _descriptor.Globals.end())
        {
            uint64_t numeric = it->second.NumericValue;
            std::memcpy(&value, &numeric, sizeof(T) <= sizeof(numeric) ? sizeof(T) : sizeof(numeric));
        }
        return value;
    }

    template <typename T>
    bool TryReadGlobal(const std::string& name, T& value) const
    {
        auto it = _descriptor.Globals.find(name);
        if (it == _descriptor.Globals.end())
        {
            value = T{};
            return false;
        }
        uint64_t numeric = it->second.NumericValue;
        value = T{};
        std::memcpy(&value, &numeric, sizeof(T) <= sizeof(numeric) ? sizeof(T) : sizeof(numeric));
        return true;
    }

    // Reads a string global (inline) or, if stored as a pointer, the UTF-8 C-string it points to.
    std::string ReadGlobalString(const std::string& name)
    {
        auto it = _descriptor.Globals.find(name);
        if (it == _descriptor.Globals.end())
        {
            return std::string{};
        }
        if (it->second.StringValue.has_value())
        {
            return it->second.StringValue.value();
        }
        return _reader.ReadCString(static_cast<uintptr_t>(it->second.NumericValue));
    }

    // --- types ---

    bool HasType(const std::string& name) const
    {
        return _descriptor.Types.find(name) != _descriptor.Types.end();
    }

    bool HasField(const std::string& typeName, const std::string& fieldName) const
    {
        auto it = _descriptor.Types.find(typeName);
        if (it == _descriptor.Types.end())
        {
            return false;
        }
        return it->second.Fields.find(fieldName) != it->second.Fields.end();
    }

    bool TryGetTypeSize(const std::string& typeName, int& size) const
    {
        auto it = _descriptor.Types.find(typeName);
        if (it == _descriptor.Types.end() || !it->second.Size.has_value())
        {
            size = 0;
            return false;
        }
        size = it->second.Size.value();
        return true;
    }

    // Returns the field offset, or -1 when the type/field is not defined by this runtime.
    int GetFieldOffset(const std::string& typeName, const std::string& fieldName) const
    {
        auto it = _descriptor.Types.find(typeName);
        if (it == _descriptor.Types.end())
        {
            return -1;
        }
        auto fit = it->second.Fields.find(fieldName);
        if (fit == it->second.Fields.end())
        {
            return -1;
        }
        return fit->second.Offset;
    }

    // Absolute address of a field within an instance at baseAddress (0 when the field is unknown).
    uintptr_t FieldAddress(uintptr_t baseAddress, const std::string& typeName, const std::string& fieldName) const
    {
        int offset = GetFieldOffset(typeName, fieldName);
        if (offset < 0)
        {
            return 0;
        }
        return baseAddress + static_cast<uintptr_t>(offset);
    }

    template <typename T>
    T ReadField(uintptr_t baseAddress, const std::string& typeName, const std::string& fieldName)
    {
        uintptr_t addr = FieldAddress(baseAddress, typeName, fieldName);
        if (addr == 0)
        {
            return T{};
        }
        return Read<T>(addr);
    }

    uintptr_t ReadFieldPointer(uintptr_t baseAddress, const std::string& typeName, const std::string& fieldName)
    {
        uintptr_t addr = FieldAddress(baseAddress, typeName, fieldName);
        if (addr == 0)
        {
            return 0;
        }
        return ReadPointer(addr);
    }

private:
    IMemoryReader& _reader;
    LogicalDescriptor _descriptor;
};
} // namespace cdac
