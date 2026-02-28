// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "VisitedObjectSet.h"
#include "TypeReferenceTree.h"
#include "TypeReferenceTreeJsonSerializer.h"
#include "IFrameStore.h"

#include <string>
#include <unordered_map>
#include <sstream>

// ============================================================================
// Mock IFrameStore for unit tests
// ============================================================================

class MockFrameStore : public IFrameStore
{
public:
    void RegisterType(ClassID classId, const std::string& typeName)
    {
        _typeNames[classId] = typeName;
    }

    std::pair<bool, FrameInfoView> GetFrame(uintptr_t instructionPointer) override
    {
        return {false, {"", "", "", 0}};
    }

    bool GetTypeName(ClassID classId, std::string& name) override
    {
        auto it = _typeNames.find(classId);
        if (it != _typeNames.end())
        {
            name = it->second;
            return true;
        }
        return false;
    }

    bool GetTypeName(ClassID classId, std::string_view& name) override
    {
        auto it = _typeNames.find(classId);
        if (it != _typeNames.end())
        {
            name = it->second;
            return true;
        }
        return false;
    }

private:
    std::unordered_map<ClassID, std::string> _typeNames;
};

// ============================================================================
// VisitedObjectSet Tests
// ============================================================================

TEST(VisitedObjectSetTest, InitialStateIsEmpty)
{
    VisitedObjectSet visited;
    ASSERT_EQ(visited.Size(), 0);
    ASSERT_FALSE(visited.IsVisited(0x1000));
}

TEST(VisitedObjectSetTest, MarkAndCheckVisited)
{
    VisitedObjectSet visited;

    visited.MarkVisited(0x1000);
    ASSERT_TRUE(visited.IsVisited(0x1000));
    ASSERT_FALSE(visited.IsVisited(0x2000));
    ASSERT_EQ(visited.Size(), 1);
}

TEST(VisitedObjectSetTest, MarkMultipleAddresses)
{
    VisitedObjectSet visited;

    visited.MarkVisited(0x1000);
    visited.MarkVisited(0x2000);
    visited.MarkVisited(0x3000);

    ASSERT_TRUE(visited.IsVisited(0x1000));
    ASSERT_TRUE(visited.IsVisited(0x2000));
    ASSERT_TRUE(visited.IsVisited(0x3000));
    ASSERT_FALSE(visited.IsVisited(0x4000));
    ASSERT_EQ(visited.Size(), 3);
}

TEST(VisitedObjectSetTest, DuplicateMarkDoesNotIncreaseSize)
{
    VisitedObjectSet visited;

    visited.MarkVisited(0x1000);
    visited.MarkVisited(0x1000);

    ASSERT_TRUE(visited.IsVisited(0x1000));
    ASSERT_EQ(visited.Size(), 1);
}

TEST(VisitedObjectSetTest, ClearRemovesAll)
{
    VisitedObjectSet visited;

    visited.MarkVisited(0x1000);
    visited.MarkVisited(0x2000);
    ASSERT_EQ(visited.Size(), 2);

    visited.Clear();
    ASSERT_EQ(visited.Size(), 0);
    ASSERT_FALSE(visited.IsVisited(0x1000));
    ASSERT_FALSE(visited.IsVisited(0x2000));
}

// ============================================================================
// TypeTreeNode Tests
// ============================================================================

TEST(TypeTreeNodeTest, InitialState)
{
    TypeTreeNode node(100);
    ASSERT_EQ(node.typeID, 100);
    ASSERT_EQ(node.instanceCount, 0);
    ASSERT_EQ(node.totalSize, 0);
    ASSERT_TRUE(node.children.empty());
}

TEST(TypeTreeNodeTest, AddInstance)
{
    TypeTreeNode node(100);
    node.AddInstance(64);
    node.AddInstance(128);

    ASSERT_EQ(node.instanceCount, 2);
    ASSERT_EQ(node.totalSize, 192);
}

TEST(TypeTreeNodeTest, GetOrCreateChildCreatesNew)
{
    TypeTreeNode node(100);
    TypeTreeNode* child = node.GetOrCreateChild(200);

    ASSERT_NE(child, nullptr);
    ASSERT_EQ(child->typeID, 200);
    ASSERT_EQ(child->instanceCount, 0);
    ASSERT_EQ(node.children.size(), 1);
}

TEST(TypeTreeNodeTest, GetOrCreateChildReturnsExisting)
{
    TypeTreeNode node(100);
    TypeTreeNode* child1 = node.GetOrCreateChild(200);
    child1->AddInstance(64);

    TypeTreeNode* child2 = node.GetOrCreateChild(200);

    ASSERT_EQ(child1, child2); // Same pointer
    ASSERT_EQ(child2->instanceCount, 1); // Still has the instance we added
    ASSERT_EQ(node.children.size(), 1); // Still only one child
}

TEST(TypeTreeNodeTest, MultipleChildrenCreated)
{
    TypeTreeNode node(100);
    TypeTreeNode* childA = node.GetOrCreateChild(200);
    TypeTreeNode* childB = node.GetOrCreateChild(300);

    ASSERT_NE(childA, childB);
    ASSERT_EQ(childA->typeID, 200);
    ASSERT_EQ(childB->typeID, 300);
    ASSERT_EQ(node.children.size(), 2);
}

TEST(TypeTreeNodeTest, GetChildReturnsExisting)
{
    TypeTreeNode node(100);
    node.GetOrCreateChild(200)->AddInstance(64);

    const TypeTreeNode* child = node.GetChild(200);
    ASSERT_NE(child, nullptr);
    ASSERT_EQ(child->typeID, 200);
    ASSERT_EQ(child->instanceCount, 1);
}

TEST(TypeTreeNodeTest, GetChildReturnsNullForMissing)
{
    TypeTreeNode node(100);
    const TypeTreeNode* child = node.GetChild(999);
    ASSERT_EQ(child, nullptr);
}

// ============================================================================
// TypeRootNode Tests
// ============================================================================

TEST(TypeRootNodeTest, InitialState)
{
    TypeRootNode root(100);
    ASSERT_EQ(root.node.typeID, 100);
    ASSERT_EQ(root.node.instanceCount, 0);
    ASSERT_EQ(root.rootCategories, 0);
}

TEST(TypeRootNodeTest, AddRootUpdatesAllFields)
{
    TypeRootNode root(100);
    root.AddRoot(RootCategory::Stack, 64);

    ASSERT_EQ(root.node.instanceCount, 1);
    ASSERT_EQ(root.node.totalSize, 64);
    ASSERT_TRUE(root.HasRootCategory(RootCategory::Stack));
    ASSERT_FALSE(root.HasRootCategory(RootCategory::Handle));
}

TEST(TypeRootNodeTest, MultipleCategoriesBitmask)
{
    TypeRootNode root(100);
    root.AddRoot(RootCategory::Stack, 100);
    root.AddRoot(RootCategory::Handle, 200);
    root.AddRoot(RootCategory::Pinning, 300);

    ASSERT_EQ(root.node.instanceCount, 3);
    ASSERT_EQ(root.node.totalSize, 600);
    ASSERT_TRUE(root.HasRootCategory(RootCategory::Stack));
    ASSERT_TRUE(root.HasRootCategory(RootCategory::Handle));
    ASSERT_TRUE(root.HasRootCategory(RootCategory::Pinning));
    ASSERT_FALSE(root.HasRootCategory(RootCategory::Finalizer));
    ASSERT_FALSE(root.HasRootCategory(RootCategory::COM));
}

// ============================================================================
// TypeReferenceTree Tests
// ============================================================================

TEST(TypeReferenceTreeTest, InitialStateIsEmpty)
{
    TypeReferenceTree tree;
    ASSERT_TRUE(tree.IsEmpty());
}

TEST(TypeReferenceTreeTest, AddRootMakesNonEmpty)
{
    TypeReferenceTree tree;
    TypeTreeNode* node = tree.AddRoot(100, RootCategory::Stack, 64);

    ASSERT_FALSE(tree.IsEmpty());
    ASSERT_NE(node, nullptr);
    ASSERT_EQ(node->typeID, 100);
    ASSERT_EQ(node->instanceCount, 1);
    ASSERT_EQ(node->totalSize, 64);
}

TEST(TypeReferenceTreeTest, AddRootSameTypeMerges)
{
    TypeReferenceTree tree;
    TypeTreeNode* node1 = tree.AddRoot(100, RootCategory::Stack, 64);
    TypeTreeNode* node2 = tree.AddRoot(100, RootCategory::Handle, 128);

    // Same root node, merged
    ASSERT_EQ(node1, node2);
    ASSERT_EQ(node1->instanceCount, 2);
    ASSERT_EQ(node1->totalSize, 192);

    // Both categories recorded
    auto it = tree._roots.find(100);
    ASSERT_NE(it, tree._roots.end());
    ASSERT_TRUE(it->second->HasRootCategory(RootCategory::Stack));
    ASSERT_TRUE(it->second->HasRootCategory(RootCategory::Handle));
}

TEST(TypeReferenceTreeTest, AddRootDifferentTypesCreatesSeparateRoots)
{
    TypeReferenceTree tree;
    TypeTreeNode* nodeA = tree.AddRoot(100, RootCategory::Stack, 64);
    TypeTreeNode* nodeB = tree.AddRoot(200, RootCategory::Handle, 128);

    ASSERT_NE(nodeA, nodeB);
    ASSERT_EQ(tree._roots.size(), 2);
}

TEST(TypeReferenceTreeTest, ClearRemovesAll)
{
    TypeReferenceTree tree;
    tree.AddRoot(100, RootCategory::Stack, 64);
    tree.AddRoot(200, RootCategory::Handle, 128);

    tree.Clear();
    ASSERT_TRUE(tree.IsEmpty());
    ASSERT_TRUE(tree._roots.empty());
}

TEST(TypeReferenceTreeTest, TreeStructurePreservesPath)
{
    // Simulate: TypeA (root) -> TypeB -> TypeA -> TypeC
    TypeReferenceTree tree;
    TypeTreeNode* rootA = tree.AddRoot(100, RootCategory::Stack, 64);

    // Root TypeA -> TypeB
    TypeTreeNode* childB = rootA->GetOrCreateChild(200);
    childB->AddInstance(48);

    // TypeB -> TypeA (different position in tree!)
    TypeTreeNode* childA2 = childB->GetOrCreateChild(100);
    childA2->AddInstance(64);

    // TypeA (child of B) -> TypeC
    TypeTreeNode* childC = childA2->GetOrCreateChild(300);
    childC->AddInstance(32);

    // Verify the tree structure
    ASSERT_EQ(rootA->children.size(), 1);

    const TypeTreeNode* b = rootA->GetChild(200);
    ASSERT_NE(b, nullptr);
    ASSERT_EQ(b->instanceCount, 1);
    ASSERT_EQ(b->children.size(), 1);

    const TypeTreeNode* a2 = b->GetChild(100);
    ASSERT_NE(a2, nullptr);
    ASSERT_EQ(a2->instanceCount, 1);
    ASSERT_EQ(a2->children.size(), 1);

    const TypeTreeNode* c = a2->GetChild(300);
    ASSERT_NE(c, nullptr);
    ASSERT_EQ(c->instanceCount, 1);
    ASSERT_TRUE(c->children.empty());
}

// ============================================================================
// RootInfo Tests
// ============================================================================

TEST(RootInfoTest, Construction)
{
    RootInfo info(0x1000, RootCategory::Stack, 0, 0);
    ASSERT_EQ(info.address, 0x1000);
    ASSERT_EQ(info.category, RootCategory::Stack);
}

// ============================================================================
// TypeReferenceTreeJsonSerializer Tests
// ============================================================================

TEST(TypeReferenceTreeJsonSerializerTest, EmptyTreeReturnsEmptyJson)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    // Should have version and empty roots
    ASSERT_NE(json.find("\"v\":7"), std::string::npos);
    ASSERT_NE(json.find("\"r\":[]"), std::string::npos);
}

TEST(TypeReferenceTreeJsonSerializerTest, NullFrameStoreReturnsEmptyObject)
{
    TypeReferenceTree tree;
    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, nullptr);
    ASSERT_EQ(json, "{}");
}

TEST(TypeReferenceTreeJsonSerializerTest, SingleRootSerializes)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    ClassID typeA = 100;
    frameStore.RegisterType(typeA, "System.String");

    tree.AddRoot(typeA, RootCategory::Stack, 256);

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    // Check version
    ASSERT_NE(json.find("\"v\":7"), std::string::npos);

    // Check type table contains System.String
    ASSERT_NE(json.find("\"System.String\""), std::string::npos);

    // Check root entry exists with category "S" (Stack)
    ASSERT_NE(json.find("\"c\":\"S\""), std::string::npos);

    // Check instance count
    ASSERT_NE(json.find("\"ic\":1"), std::string::npos);

    // Check total size
    ASSERT_NE(json.find("\"ts\":256"), std::string::npos);
}

TEST(TypeReferenceTreeJsonSerializerTest, RootWithChildrenSerializes)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    ClassID typeA = 100;
    ClassID typeB = 200;
    frameStore.RegisterType(typeA, "MyApp.Order");
    frameStore.RegisterType(typeB, "MyApp.Customer");

    // Root: typeA (Order)
    TypeTreeNode* rootNode = tree.AddRoot(typeA, RootCategory::StaticVariable, 128);

    // Add child: Order -> Customer
    TypeTreeNode* childNode = rootNode->GetOrCreateChild(typeB);
    childNode->AddInstance(64);

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    // Check both types are in the type table
    ASSERT_NE(json.find("\"MyApp.Order\""), std::string::npos);
    ASSERT_NE(json.find("\"MyApp.Customer\""), std::string::npos);

    // Check that children array exists
    ASSERT_NE(json.find("\"ch\":["), std::string::npos);

    // Check root category "s" (StaticVariable)
    ASSERT_NE(json.find("\"c\":\"s\""), std::string::npos);
}

TEST(TypeReferenceTreeJsonSerializerTest, MultipleRootsSerialize)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    ClassID typeA = 100;
    ClassID typeB = 200;
    frameStore.RegisterType(typeA, "TypeA");
    frameStore.RegisterType(typeB, "TypeB");

    tree.AddRoot(typeA, RootCategory::Stack, 64);
    tree.AddRoot(typeB, RootCategory::Handle, 128);

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    ASSERT_NE(json.find("\"TypeA\""), std::string::npos);
    ASSERT_NE(json.find("\"TypeB\""), std::string::npos);

    // Count roots in the JSON
    size_t rootCount = 0;
    size_t pos = 0;
    while ((pos = json.find("\"c\":", pos)) != std::string::npos)
    {
        rootCount++;
        pos++;
    }
    ASSERT_GE(rootCount, 2);
}

TEST(TypeReferenceTreeJsonSerializerTest, JsonEscapingWorks)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    ClassID typeA = 100;
    frameStore.RegisterType(typeA, "Namespace.Type<System.String>");

    tree.AddRoot(typeA, RootCategory::Stack, 64);

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    ASSERT_NE(json.find("Namespace.Type<System.String>"), std::string::npos);
}

TEST(TypeReferenceTreeJsonSerializerTest, AllRootCategoriesProduceValidCodes)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    ClassID typeBase = 100;
    RootCategory categories[] = {
        RootCategory::Stack,
        RootCategory::StaticVariable,
        RootCategory::Finalizer,
        RootCategory::Handle,
        RootCategory::Pinning,
        RootCategory::ConditionalWeakTable,
        RootCategory::COM,
        RootCategory::Unknown
    };

    const char* expectedCodes[] = {"S", "s", "F", "H", "P", "W", "R", "?"};

    for (int i = 0; i < 8; i++)
    {
        ClassID typeId = typeBase + i;
        std::string typeName = "Type" + std::to_string(i);
        frameStore.RegisterType(typeId, typeName);
        tree.AddRoot(typeId, categories[i], 64);
    }

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    for (int i = 0; i < 8; i++)
    {
        std::string expectedCode = std::string("\"c\":\"") + expectedCodes[i] + "\"";
        ASSERT_NE(json.find(expectedCode), std::string::npos)
            << "Category code " << expectedCodes[i] << " not found in JSON: " << json;
    }
}

TEST(TypeReferenceTreeJsonSerializerTest, DeepHierarchySerializes)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    ClassID typeRoot = 100;
    ClassID typeL0 = 200;
    ClassID typeL1 = 300;
    ClassID typeL2 = 400;

    frameStore.RegisterType(typeRoot, "Root");
    frameStore.RegisterType(typeL0, "Level0");
    frameStore.RegisterType(typeL1, "Level1");
    frameStore.RegisterType(typeL2, "Level2");

    // Build tree: Root -> Level0 -> Level1 -> Level2
    TypeTreeNode* rootNode = tree.AddRoot(typeRoot, RootCategory::Stack, 64);

    TypeTreeNode* l0 = rootNode->GetOrCreateChild(typeL0);
    l0->AddInstance(48);

    TypeTreeNode* l1 = l0->GetOrCreateChild(typeL1);
    l1->AddInstance(32);

    TypeTreeNode* l2 = l1->GetOrCreateChild(typeL2);
    l2->AddInstance(16);

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    // Verify all types are present
    ASSERT_NE(json.find("\"Root\""), std::string::npos);
    ASSERT_NE(json.find("\"Level0\""), std::string::npos);
    ASSERT_NE(json.find("\"Level1\""), std::string::npos);
    ASSERT_NE(json.find("\"Level2\""), std::string::npos);

    // Verify nested "ch" arrays exist
    size_t chCount = 0;
    size_t pos = 0;
    while ((pos = json.find("\"ch\":[", pos)) != std::string::npos)
    {
        chCount++;
        pos++;
    }
    ASSERT_GE(chCount, 3); // Root->L0, L0->L1, L1->L2
}

TEST(TypeReferenceTreeJsonSerializerTest, ValidJsonStructure)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    ClassID typeA = 100;
    ClassID typeB = 200;
    frameStore.RegisterType(typeA, "TypeA");
    frameStore.RegisterType(typeB, "TypeB");

    TypeTreeNode* rootNode = tree.AddRoot(typeA, RootCategory::Stack, 100);
    TypeTreeNode* childNode = rootNode->GetOrCreateChild(typeB);
    childNode->AddInstance(50);

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    // Verify the JSON starts with { and ends with }
    ASSERT_FALSE(json.empty());
    ASSERT_EQ(json.front(), '{');
    ASSERT_EQ(json.back(), '}');

    // Verify balanced braces and brackets
    int braces = 0;
    int brackets = 0;
    for (char c : json)
    {
        if (c == '{') braces++;
        if (c == '}') braces--;
        if (c == '[') brackets++;
        if (c == ']') brackets--;
        ASSERT_GE(braces, 0) << "Unbalanced braces at: " << json;
        ASSERT_GE(brackets, 0) << "Unbalanced brackets at: " << json;
    }
    ASSERT_EQ(braces, 0) << "Unclosed braces in: " << json;
    ASSERT_EQ(brackets, 0) << "Unclosed brackets in: " << json;
}

// Verify that the tree correctly represents A -> B -> A -> C
// (the key scenario that motivated the tree refactoring)
TEST(TypeReferenceTreeJsonSerializerTest, SameTypeAtDifferentPositions)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    ClassID typeA = 100;
    ClassID typeB = 200;
    ClassID typeC = 300;
    frameStore.RegisterType(typeA, "TypeA");
    frameStore.RegisterType(typeB, "TypeB");
    frameStore.RegisterType(typeC, "TypeC");

    // Build tree: TypeA (root) -> TypeB -> TypeA -> TypeC
    TypeTreeNode* rootA = tree.AddRoot(typeA, RootCategory::Stack, 64);

    TypeTreeNode* childB = rootA->GetOrCreateChild(typeB);
    childB->AddInstance(48);

    TypeTreeNode* childA2 = childB->GetOrCreateChild(typeA);
    childA2->AddInstance(64);

    TypeTreeNode* childC = childA2->GetOrCreateChild(typeC);
    childC->AddInstance(32);

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    // All three types must be present
    ASSERT_NE(json.find("\"TypeA\""), std::string::npos);
    ASSERT_NE(json.find("\"TypeB\""), std::string::npos);
    ASSERT_NE(json.find("\"TypeC\""), std::string::npos);

    // The chain should produce nested "ch" arrays at 3 levels:
    // root A -> ch[B -> ch[A -> ch[C]]]
    size_t chCount = 0;
    size_t pos = 0;
    while ((pos = json.find("\"ch\":[", pos)) != std::string::npos)
    {
        chCount++;
        pos++;
    }
    ASSERT_GE(chCount, 3) << "Expected at least 3 nested ch arrays in: " << json;

    // Verify balanced structure
    int braces = 0, brackets = 0;
    for (char c : json)
    {
        if (c == '{') braces++;
        if (c == '}') braces--;
        if (c == '[') brackets++;
        if (c == ']') brackets--;
    }
    ASSERT_EQ(braces, 0) << "Unclosed braces in: " << json;
    ASSERT_EQ(brackets, 0) << "Unclosed brackets in: " << json;
}

// Verify that type-level cycles in the tree (which shouldn't occur
// since the tree is built with instance-level cycle detection) would
// still be bounded by the tree's finite structure
TEST(TypeReferenceTreeJsonSerializerTest, TreeHasNoInfiniteRecursion)
{
    TypeReferenceTree tree;
    MockFrameStore frameStore;

    ClassID typeA = 100;
    ClassID typeB = 200;
    frameStore.RegisterType(typeA, "SelfRef");
    frameStore.RegisterType(typeB, "TypeB");

    // Build a tree: A (root) -> A -> B (simulates A1 -> A2 -> B,
    // where A2 was stopped by VisitedObjectSet before cycling back)
    TypeTreeNode* rootA = tree.AddRoot(typeA, RootCategory::Handle, 128);
    TypeTreeNode* childA = rootA->GetOrCreateChild(typeA);
    childA->AddInstance(128);
    TypeTreeNode* childB = childA->GetOrCreateChild(typeB);
    childB->AddInstance(64);

    auto json = TypeReferenceTreeJsonSerializer::Serialize(tree, &frameStore);

    ASSERT_FALSE(json.empty());
    ASSERT_NE(json.find("\"SelfRef\""), std::string::npos);
    ASSERT_NE(json.find("\"TypeB\""), std::string::npos);

    int braces = 0, brackets = 0;
    for (char c : json)
    {
        if (c == '{') braces++;
        if (c == '}') braces--;
        if (c == '[') brackets++;
        if (c == ']') brackets--;
    }
    ASSERT_EQ(braces, 0) << "Unclosed braces in: " << json;
    ASSERT_EQ(brackets, 0) << "Unclosed brackets in: " << json;
}
