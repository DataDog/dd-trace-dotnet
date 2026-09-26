// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string_view>
#include <vector>

// Minimal protobuf wire-format writer used to serialize the pprof (Google
// perftools) profile message without any third-party dependency.
//
// The functions append into a caller-owned std::vector<uint8_t> so that output
// and scratch buffers can be pooled/reused across serialization cycles.
//
// Only the wire types needed by pprof are implemented:
//   - wire type 0 (varint)             -> int64/uint64 scalar fields
//   - wire type 2 (length-delimited)   -> strings, sub-messages and packed repeated fields
namespace protobuf {

inline constexpr uint32_t WireVarint = 0;
inline constexpr uint32_t WireLengthDelimited = 2;

inline void WriteVarint(std::vector<uint8_t>& buffer, uint64_t value)
{
    while (value >= 0x80)
    {
        buffer.push_back(static_cast<uint8_t>(value) | 0x80);
        value >>= 7;
    }
    buffer.push_back(static_cast<uint8_t>(value));
}

inline void WriteTag(std::vector<uint8_t>& buffer, uint32_t fieldNumber, uint32_t wireType)
{
    WriteVarint(buffer, (static_cast<uint64_t>(fieldNumber) << 3) | wireType);
}

// varint scalar field (int64/uint64). pprof does not use zig-zag encoding.
inline void WriteVarintField(std::vector<uint8_t>& buffer, uint32_t fieldNumber, uint64_t value)
{
    WriteTag(buffer, fieldNumber, WireVarint);
    WriteVarint(buffer, value);
}

inline void WriteInt64Field(std::vector<uint8_t>& buffer, uint32_t fieldNumber, int64_t value)
{
    WriteVarintField(buffer, fieldNumber, static_cast<uint64_t>(value));
}

inline void WriteStringField(std::vector<uint8_t>& buffer, uint32_t fieldNumber, std::string_view value)
{
    WriteTag(buffer, fieldNumber, WireLengthDelimited);
    WriteVarint(buffer, value.size());
    buffer.insert(buffer.end(), value.begin(), value.end());
}

// Embed an already-serialized sub-message as a length-delimited field.
inline void WriteMessageField(std::vector<uint8_t>& buffer, uint32_t fieldNumber, const std::vector<uint8_t>& message)
{
    WriteTag(buffer, fieldNumber, WireLengthDelimited);
    WriteVarint(buffer, message.size());
    buffer.insert(buffer.end(), message.begin(), message.end());
}

// Packed repeated varint field (used for Sample.location_id and Sample.value).
inline void WritePackedVarints(std::vector<uint8_t>& buffer, std::vector<uint8_t>& scratch,
                               uint32_t fieldNumber, const uint64_t* values, size_t count)
{
    if (count == 0)
    {
        return;
    }

    scratch.clear();
    for (size_t i = 0; i < count; ++i)
    {
        WriteVarint(scratch, values[i]);
    }

    WriteTag(buffer, fieldNumber, WireLengthDelimited);
    WriteVarint(buffer, scratch.size());
    buffer.insert(buffer.end(), scratch.begin(), scratch.end());
}

inline void WritePackedInt64(std::vector<uint8_t>& buffer, std::vector<uint8_t>& scratch,
                             uint32_t fieldNumber, const int64_t* values, size_t count)
{
    WritePackedVarints(buffer, scratch, fieldNumber, reinterpret_cast<const uint64_t*>(values), count);
}

} // namespace protobuf
