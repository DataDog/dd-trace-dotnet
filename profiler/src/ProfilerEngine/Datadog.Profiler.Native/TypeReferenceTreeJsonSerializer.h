// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "TypeReferenceTree.h"
#include "IFrameStore.h"
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

// JSON serializer for type reference tree.
// Walks the tree structure directly — no cycle detection needed
// because the tree is naturally acyclic (instance-level cycles are
// stopped during traversal by VisitedObjectSet).
class TypeReferenceTreeJsonSerializer
{
public:
    static std::string Serialize(const TypeReferenceTree& tree, IFrameStore* pFrameStore);

private:
    // Type table state threaded through the whole walk. Bundled into one object because
    // passing it as separate arguments meant several same-typed references side by side,
    // where a transposition would still compile.
    struct TypeTable
    {
        explicit TypeTable(IFrameStore* frameStore);

        // Entries in index order, already escaped and comma separated.
        std::string json;
        uint32_t count = 0;

        std::unordered_map<ClassID, uint32_t> typeToIndex;

        // Receives the resolved name of the type being registered (see RegisterType).
        std::string scratch;

        IFrameStore* pFrameStore;
    };

    // Returns the type table index for the given type, appending its fully qualified
    // name to the already escaped type table entries on first encounter.
    static uint32_t RegisterType(TypeTable& types, ClassID typeID);

    // Single-pass tree walk: collects types lazily and emits JSON in one traversal.
    static void OutputNode(const TypeTreeNode& node, TypeTable& types, std::string& out);

    static const char* GetRootCategoryCode(RootCategory category);

    static void AppendUInt64(std::string& out, uint64_t v);
    static void AppendUInt32(std::string& out, uint32_t v);

    // Append JSON-escaped string directly to output buffer.
    // Fast path: if no characters need escaping, appends the original in one operation.
    static void AppendEscapedJson(std::string& out, std::string_view str);
};
