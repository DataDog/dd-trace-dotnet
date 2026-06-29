// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <map>
#include <optional>
#include <string>
#include <vector>

namespace cdac
{
// A single field of a descriptor type: its byte offset and (optional) declared type name.
struct FieldInfo
{
    std::string Name;
    int Offset = 0;
    std::optional<std::string> TypeName;
};

// A descriptor type: its name, optional determinate size ("!") and its fields by name.
struct TypeInfo
{
    std::string Name;
    std::optional<int> Size;
    std::map<std::string, FieldInfo> Fields;
};

// A named global value: a numeric/pointer value (already indirection-resolved) and/or a string.
struct GlobalValue
{
    std::string Name;
    std::optional<std::string> TypeName;
    uint64_t NumericValue = 0;
    std::optional<std::string> StringValue;
};

// A named sub-descriptor pointer SLOT (i.e. pointer_data[index]), not the sub-descriptor itself.
// The actual sub-descriptor address is obtained by dereferencing Slot, and may be null until the
// runtime populates it (e.g. the GC sub-descriptor before the GC initializes).
struct SubDescriptorSlot
{
    std::string Name;
    uint64_t Slot = 0;
};

// Raw, unparsed contents of a single contract descriptor read from target memory.
struct RawContractDescriptor
{
    uintptr_t Address = 0;
    int PointerSize = 8;
    std::string Json;
    std::vector<uintptr_t> PointerData;
    bool IsLittleEndian = true;
};

// The parsed (indirection-resolved) contents of one physical descriptor.
struct ParsedDescriptor
{
    std::map<std::string, TypeInfo> Types;
    std::map<std::string, GlobalValue> Globals;
    std::map<std::string, std::string> Contracts;
    std::vector<SubDescriptorSlot> SubDescriptorSlots;
};
} // namespace cdac
