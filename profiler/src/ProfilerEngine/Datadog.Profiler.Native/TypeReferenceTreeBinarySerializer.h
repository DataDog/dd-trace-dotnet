// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "TypeReferenceTree.h"
#include "IFrameStore.h"
#include <cstdint>
#include <string_view>
#include <unordered_map>
#include <vector>

// Binary serializer for type reference tree (varint DFS format).
// Wire format documented in docs/reference-tree-serialization-formats.md.
// All integers are unsigned LEB128. The tree is walked in DFS pre-order,
// matching the same traversal as TypeReferenceTreeJsonSerializer.
class TypeReferenceTreeBinarySerializer
{
public:
    static std::vector<uint8_t> Serialize(const TypeReferenceTree& tree, IFrameStore* pFrameStore);

private:
    static void WriteVarint(std::vector<uint8_t>& out, uint64_t value);
    static void WriteBytes(std::vector<uint8_t>& out, const uint8_t* data, size_t len);
    static void WriteString(std::vector<uint8_t>& out, std::string_view str);

    static void WriteNode(
        const TypeTreeNode& node,
        std::unordered_map<ClassID, uint32_t>& typeToIndex,
        std::vector<std::string_view>& typeTable,
        uint32_t& nextIndex,
        IFrameStore* pFrameStore,
        std::vector<uint8_t>& out);
};
