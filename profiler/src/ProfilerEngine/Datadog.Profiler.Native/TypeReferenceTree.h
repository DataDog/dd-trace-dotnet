// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "ReferenceChainTypes.h"
#include <unordered_map>
#include <memory>
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
        auto it = children.find(childTypeID);
        if (it != children.end())
        {
            return it->second.get();
        }
        auto child = std::make_unique<TypeTreeNode>(childTypeID);
        auto* ptr = child.get();
        children[childTypeID] = std::move(child);
        return ptr;
    }

    // Get an existing child node (returns nullptr if not found).
    const TypeTreeNode* GetChild(ClassID childTypeID) const
    {
        auto it = children.find(childTypeID);
        return it != children.end() ? it->second.get() : nullptr;
    }
};


// A root node in the reference tree.
// Extends TypeTreeNode with root-specific metadata (category bitmask).
struct TypeRootNode
{
    TypeTreeNode node;
    uint8_t rootCategories;  // Bitmask of RootCategory values

    TypeRootNode(ClassID typeID) : node(typeID), rootCategories(0)
    {
    }

    void AddRoot(RootCategory category, uint64_t size)
    {
        node.AddInstance(size);
        rootCategories |= (1 << static_cast<uint8_t>(category));
    }

    bool HasRootCategory(RootCategory category) const
    {
        return (rootCategories & (1 << static_cast<uint8_t>(category))) != 0;
    }
};


// Complete type reference tree.
// Roots are the top-level entries; each root has a tree of children
// representing the type-level reference chains from that root.
class TypeReferenceTree
{
public:
    // Root nodes indexed by ClassID.
    // Multiple root instances of the same type merge into one TypeRootNode.
    std::unordered_map<ClassID, std::unique_ptr<TypeRootNode>> _roots;

    // Add or update a root.
    // Returns a pointer to the root's TypeTreeNode for use during traversal.
    TypeTreeNode* AddRoot(ClassID typeID, RootCategory category, uint64_t size)
    {
        auto it = _roots.find(typeID);
        if (it != _roots.end())
        {
            it->second->AddRoot(category, size);
            return &it->second->node;
        }
        auto root = std::make_unique<TypeRootNode>(typeID);
        root->AddRoot(category, size);
        auto* nodePtr = &root->node;
        _roots[typeID] = std::move(root);
        return nodePtr;
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
