// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "TypeReferenceTree.h"
#include "IFrameStore.h"
#include <string>
#include <unordered_map>
#include <sstream>

// JSON serializer for type reference tree.
// Walks the tree structure directly — no cycle detection needed
// because the tree is naturally acyclic (instance-level cycles are
// stopped during traversal by VisitedObjectSet).
class TypeReferenceTreeJsonSerializer {
public:
    static std::string Serialize(const TypeReferenceTree& tree, IFrameStore* pFrameStore);

private:
    static void OutputNode(
        const TypeTreeNode& node,
        const std::unordered_map<ClassID, uint32_t>& typeToIndex,
        std::stringstream& ss);

    static void CollectTypes(
        const TypeTreeNode& node,
        std::unordered_map<ClassID, uint32_t>& typeToIndex,
        std::vector<std::string>& typeTable,
        uint32_t& nextIndex,
        IFrameStore* pFrameStore);

    static const char* GetRootCategoryCode(RootCategory category);
    static std::string EscapeJson(const std::string& str);
};
