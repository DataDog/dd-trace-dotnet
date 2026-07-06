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
    // Single-pass tree walk: collects types lazily and emits JSON in one traversal.
    static void OutputNode(
        const TypeTreeNode& node,
        std::unordered_map<ClassID, uint32_t>& typeToIndex,
        std::vector<std::string_view>& typeTable,
        uint32_t& nextIndex,
        IFrameStore* pFrameStore,
        std::string& out);

    static const char* GetRootCategoryCode(RootCategory category);

    static void AppendUInt64(std::string& out, uint64_t v);
    static void AppendUInt32(std::string& out, uint32_t v);

    // Append JSON-escaped string directly to output buffer.
    // Fast path: if no characters need escaping, appends the original in one operation.
    static void AppendEscapedJson(std::string& out, std::string_view str);
};
