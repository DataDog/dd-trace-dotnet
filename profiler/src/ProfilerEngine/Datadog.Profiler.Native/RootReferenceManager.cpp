// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <iostream>
#include <string_view>

#include "RootReferenceManager.h"


ObjectNode::ObjectNode(ObjectID objectId)
{
    instance = objectId;
}


RootReferenceManager::RootReferenceManager(ICorProfilerInfo5* pCorProfilerInfo, IFrameStore* frameStore)
    :
    _objectsCount {0},
    _pCorProfilerInfo {pCorProfilerInfo},
    _pFrameStore {frameStore}
{
}


// get the list of roots with their kind/flags
HRESULT RootReferenceManager::OnRootReferences2(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[])
{
    uint16_t stackRoots = 0;
    uint16_t finalizerRoots = 0;
    uint16_t handleRoots = 0;
    uint16_t otherRoots = 0;

    std::lock_guard<std::mutex> lock(_lock);

    for (ULONG current = 0; current < cRootRefs; current++)
    {
        ObjectID objectId = rootRefIds[current];
        if (objectId == 0)
        {
            continue;
        }

        ObjectRoot root;
        root.instance = objectId;
        root.flags = rootFlags[current];
        root.kind = rootKinds[current];
        _roots.push_back(root);


        if (rootKinds[current] == COR_PRF_GC_ROOT_STACK)
        {
            stackRoots++;
        }
        else
        if (rootKinds[current] == COR_PRF_GC_ROOT_FINALIZER)
        {
            finalizerRoots++;
        }
        else
        if (rootKinds[current] == COR_PRF_GC_ROOT_HANDLE)
        {
            handleRoots++;
        }
        else
        {
            otherRoots++;
        }
    }

    std::cout << "OnRootReferences2: " << stackRoots + finalizerRoots + handleRoots + otherRoots << "/" << cRootRefs << " roots." << std::endl;
    std::cout << "            stack:" << stackRoots << std::endl;
    std::cout << "        finalizer:" << finalizerRoots << std::endl;
    std::cout << "           handle:" << handleRoots << std::endl;
    std::cout << "            other:" << otherRoots << std::endl;
    std::cout << "------------------" << std::endl;

    return S_OK;
}

bool Contains(const std::vector<ObjectNode*>& nodes, ObjectID instance)
{
    for (auto& node : nodes)
    {
        if (node->instance == instance)
        {
            return true;
        }
    }

    return false;
}

bool FindRoot(const std::vector<ObjectRoot>& roots, ObjectID instance, COR_PRF_GC_ROOT_KIND& kind, COR_PRF_GC_ROOT_FLAGS& flags)
{
    for (auto& root : roots)
    {
        if (root.instance == instance)
        {
            kind = root.kind;
            flags = root.flags;

            return true;
        }
    }

    return false;
}

HRESULT RootReferenceManager::OnObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs, ObjectID objectRefIds[])
{
    std::lock_guard<std::mutex> lock(_lock);

    _objectsCount++;

    // check if the given object was already in the heap
    std::shared_ptr<ObjectNode> pParentNode = nullptr;
    auto parentSlot = _heap.find(objectId);
    if (parentSlot != _heap.end())
    {
        pParentNode = parentSlot->second;
    }
    else
    {
        pParentNode = std::make_shared<ObjectNode>(objectId);
        _heap.insert_or_assign(objectId, pParentNode);
    }

    // add objectId as parent of all objectRefIds objects
    for (size_t i = 0; i < cObjectRefs; i++)
    {
        std::shared_ptr<ObjectNode> pChildNode = nullptr;
        auto childObjectId = objectRefIds[i];
        auto childSlot = _heap.find(childObjectId);
        if (childSlot != _heap.end())
        {
            pChildNode = childSlot->second;
        }
        else
        {
            pChildNode = std::make_shared<ObjectNode>(childObjectId);
            _heap.insert_or_assign(childObjectId, pChildNode);
        }

        if (!Contains(pChildNode->rootRefs, objectId))
        {
            pChildNode->rootRefs.push_back(pParentNode.get());
        }
    }

    return S_OK;
}

void DumpFlags(COR_PRF_GC_ROOT_FLAGS flags)
{
    if (flags == 0)
    {
        std::cout << "0";
        return;
    }

    if ((flags & COR_PRF_GC_ROOT_PINNING) == COR_PRF_GC_ROOT_PINNING)
    {
        std::cout << "P";
    }
    if ((flags & COR_PRF_GC_ROOT_WEAKREF) == COR_PRF_GC_ROOT_WEAKREF)
    {
        std::cout << "W";
    }
    if ((flags & COR_PRF_GC_ROOT_INTERIOR) == COR_PRF_GC_ROOT_INTERIOR)
    {
        std::cout << "I";
    }
    if ((flags & COR_PRF_GC_ROOT_REFCOUNTED) == COR_PRF_GC_ROOT_REFCOUNTED)
    {
        std::cout << "R";
    }
}

void DumpKind(COR_PRF_GC_ROOT_KIND kind)
{
    if (kind == COR_PRF_GC_ROOT_STACK)
    {
        std::cout << "S";
    }
    else if (kind == COR_PRF_GC_ROOT_FINALIZER)
    {
        std::cout << "F";
    }
    else if (kind == COR_PRF_GC_ROOT_HANDLE)
    {
        std::cout << "H";
    }
    else
    {
        std::cout << kind;
    }
}

void RootReferenceManager::DumpRoot(ObjectID objectId, COR_PRF_GC_ROOT_KIND kind, COR_PRF_GC_ROOT_FLAGS flags)
{
    std::cout << "   ";
    DumpKind(kind);
    std::cout << "-";
    DumpFlags(flags);

    ClassID classId;
    if (FAILED(_pCorProfilerInfo->GetClassFromObject(objectId, &classId)))
    {
        std::cout << " " << std::hex << objectId << std::dec << std::endl;
        return;
    }

    std::string_view name;
    if (_pFrameStore->GetTypeName(classId, name))
    {
        std::cout << " " << name << std::endl;
    }
    else
    {
        std::cout << " No type name " << std::hex << objectId << std::dec << std::endl;
    }
}

void DumpObjectType(ObjectID objectId, ICorProfilerInfo5* pCorProfilerInfo, IFrameStore* pFrameStore)
{
    ClassID classId;
    if (FAILED(pCorProfilerInfo->GetClassFromObject(objectId, &classId)))
    {
        std::cout << "??" << std::dec;
        return;
    }

    std::string_view name;
    if (pFrameStore->GetTypeName(classId, name))
    {
        std::cout << name;
    }
    else
    {
        std::cout << "???" << std::dec;
    }
}

void RootReferenceManager::OnGarbageCollectionFinished()
{
    std::lock_guard<std::mutex> lock(_lock);

    std::cout << "OnGarbageCollectionFinished: " << _objectsCount << " objects in the heap." << std::endl
              << std::endl;

    //for (auto& root: _roots)
    //{
    //    DumpRoot(root.instance, root.kind, root.flags);
    //}

    _objectsCount = 0;
    _heap.clear();
    _roots.clear();
}

void RootReferenceManager::DumpReferences(ObjectID objectId)
{
    std::lock_guard<std::mutex> lock(_lock);

    auto slot = _heap.find(objectId);
    if (slot == _heap.end())
    {
        std::cout << std::hex << objectId << std::dec;
        DumpObjectType(objectId, _pCorProfilerInfo, _pFrameStore);
        std::cout << std::endl;
        return;
    }

    auto node = slot->second.get();

    std::vector<ObjectID> referenceStack;
    referenceStack.reserve(64);
    DumpNode(node, referenceStack);
    std::cout << "=====================================" << std::endl << std::endl;
}

void ShiftCout(uint16_t depth)
{
    for (size_t i = 0; i < depth; i++)
    {
        std::cout << "   ";
    }
}

bool Find(std::vector<ObjectID>& referenceStack, ObjectID reference)
{
    for (auto objectId : referenceStack)
    {
        if (objectId == reference)
        {
            return true;
        }
    }

    return false;
}

bool RootReferenceManager::DumpNode(ObjectNode* node, std::vector<ObjectID>& referenceStack)
{
    // end of recursion: the node is a root
    if (node->rootRefs.size() == 0)
    {
        //  dump the root
        std::cout << std::endl;
        std::cout << std::hex << node->instance << std::dec;
        COR_PRF_GC_ROOT_KIND kind;
        COR_PRF_GC_ROOT_FLAGS flags;
        if (FindRoot(_roots, node->instance, kind, flags))
        {
            std::cout << " | ";
            DumpKind(kind);
            std::cout << " - ";
            DumpFlags(flags);
        }
        else
        {
            std::cout << " | ?";
        }
        std::cout << " = ";

        DumpObjectType(node->instance, _pCorProfilerInfo, _pFrameStore);
        std::cout << std::endl;

        // dump the references from the root
        for (int16_t i = referenceStack.size()-1; i >= 0; i--)
        {
            ObjectID reference = referenceStack[i];
            std::cout << " --> ";
            std::cout << std::hex << reference << std::dec;
            std::cout << " = ";
            DumpObjectType(reference, _pCorProfilerInfo, _pFrameStore);
            std::cout << std::endl;
        }

        return true;
    }

    // detect cycles
    if (Find(referenceStack, node->instance))
    {
        return false;
    }

    // go up into the reference chain
    referenceStack.push_back(node->instance);
    for (auto& parentNode : node->rootRefs)
    {
        if (DumpNode(parentNode, referenceStack))
        {
            return true;
        }
    }
    referenceStack.pop_back();

    return false;
}
