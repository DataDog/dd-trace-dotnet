// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "TypeReferenceTree.h"
#include "IFrameStore.h"
#include <cstdint>
#include <string>
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

    // String table state threaded through the whole walk. Bundled into one object because
    // passing it as separate arguments meant several same-typed references side by side,
    // where a transposition would still compile.
    struct StringTable
    {
        explicit StringTable(IFrameStore* frameStore);

        // Length-prefixed type names in index order.
        std::vector<uint8_t> bytes;
        uint32_t count = 0;

        std::unordered_map<ClassID, uint32_t> typeToIndex;

        // Receives the resolved name of the type being registered (see RegisterType).
        std::string scratch;

        IFrameStore* pFrameStore;
    };

    // Returns the string table index for the given type, appending its fully
    // qualified name to the already encoded string table on first encounter.
    static uint32_t RegisterType(StringTable& types, ClassID typeID);

    static void WriteNode(const TypeTreeNode& node, StringTable& types, std::vector<uint8_t>& out);
};
