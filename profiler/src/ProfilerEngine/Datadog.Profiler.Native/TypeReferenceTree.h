// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "ReferenceChainTypes.h"
#include <unordered_map>
#include <memory>
#include <string>
#include <cstdint>

// Maximum depth for tree traversal to prevent pathological cases
// (e.g., million-element linked lists creating million-level trees).
// Beyond this depth, the additional retention information is minimal.
static constexpr uint32_t MaxTreeDepth = 128;

// A node in the type reference tree.
// Each node represents a type AT A SPECIFIC POSITION in a reference chain.
// The same ClassID can appear at multiple positions (different nodes).
// For example: TypeA -> TypeB -> TypeA -> TypeC produces 4 nodes.
struct TypeTreeNode
{
    ClassID typeID;
    uint64_t instanceCount;  // How many instances at this tree position
    uint64_t totalSize;      // Aggregate size of instances at this position

    // Children keyed by ClassID.
    // Multiple instances flowing through the same type path merge into one child node.
    std::unordered_map<ClassID, std::unique_ptr<TypeTreeNode>> children;

    TypeTreeNode(ClassID id) : typeID(id), instanceCount(0), totalSize(0)
    {
    }

    void AddInstance(uint64_t size)
    {
        instanceCount++;
        totalSize += size;
    }

    // Get or create a child node for the given type.
    TypeTreeNode* GetOrCreateChild(ClassID childTypeID)
    {
        auto [it, inserted] = children.try_emplace(childTypeID, nullptr);
        if (inserted)
        {
            it->second = std::make_unique<TypeTreeNode>(childTypeID);
        }
        return it->second.get();
    }

    // Get an existing child node (returns nullptr if not found).
    const TypeTreeNode* GetChild(ClassID childTypeID) const
    {
        auto it = children.find(childTypeID);
        return it != children.end() ? it->second.get() : nullptr;
    }
};


// Key for roots: (type, category) so the same type can appear as distinct roots per category
struct RootKey
{
    ClassID typeID;
    RootCategory category;

    bool operator==(const RootKey& o) const
    {
        return typeID == o.typeID && category == o.category;
    }
};

struct RootKeyHash
{
    size_t operator()(const RootKey& k) const
    {
        size_t h1 = std::hash<ClassID>{}(k.typeID);
        size_t h2 = std::hash<uint8_t>{}(static_cast<uint8_t>(k.category));
        return h1 ^ (h2 << 16);
    }
};

// A root node in the reference tree.
// Each root is uniquely identified by (type, category) so byte[] as Pinning
// and byte[] as Stack are distinct entries.
struct TypeRootNode
{
    TypeTreeNode node;
    RootCategory category;
    std::string fieldName;  // For static roots: the declaring field name (e.g., "_staticOrders")

    TypeRootNode(ClassID typeID, RootCategory cat) : node(typeID), category(cat)
    {
    }

    void AddInstance(uint64_t size, const std::string& field = "")
    {
        node.AddInstance(size);
        if (!field.empty() && fieldName.empty())
        {
            fieldName = field;
        }
    }
};


// Complete type reference tree.
// Roots are keyed by (ClassID, RootCategory) so the same type can appear
// as distinct roots for different categories (e.g. byte[] as Pinning vs Stack).
class TypeReferenceTree
{
public:
    std::unordered_map<RootKey, std::unique_ptr<TypeRootNode>, RootKeyHash> _roots;

    // Add or update a root for the given (type, category).
    // Returns a pointer to the root's TypeTreeNode for use during traversal.
    TypeTreeNode* AddRoot(ClassID typeID, RootCategory category, uint64_t size, const std::string& fieldName = "")
    {
        RootKey key{typeID, category};
        auto [it, inserted] = _roots.try_emplace(key, nullptr);
        if (inserted)
        {
            it->second = std::make_unique<TypeRootNode>(typeID, category);
        }
        it->second->AddInstance(size, fieldName);
        return &it->second->node;
    }

    bool IsEmpty() const
    {
        return _roots.empty();
    }

    void Clear()
    {
        _roots.clear();
    }
};
