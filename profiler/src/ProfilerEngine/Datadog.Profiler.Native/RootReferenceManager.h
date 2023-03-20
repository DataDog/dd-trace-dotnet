// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <unordered_map>

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "FrameStore.h"
#include "IRootReferenceManager.h"


// each ObjectNode represents an object in the heap
struct ObjectNode
{
public:
    ObjectNode(ObjectID objectId);

public:
    ObjectID instance;
    std::vector<ObjectNode*> rootRefs;

    // not sure it is needed to keep track of the object type
    // --> too expensive compared to call ICorProfilerInfo::GetClassID
    // ClassID type;
};

// need to keep track of roots and wait for the end of the GC
// to get their type (especially arrays)
struct ObjectRoot
{
public:
    ObjectID instance;
    COR_PRF_GC_ROOT_KIND kind;
    COR_PRF_GC_ROOT_FLAGS flags;
};

class RootReferenceManager : public IRootReferenceManager
{
public:
    RootReferenceManager(ICorProfilerInfo5* pCorProfilerInfo, IFrameStore* frameStore);

    // Inherited via IRootReferenceManager
    virtual HRESULT OnRootReferences2(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]) override;
    virtual HRESULT OnObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs, ObjectID objectRefIds[]) override;
    virtual void OnGarbageCollectionFinished() override;
    virtual void DumpReferences(ObjectID objectId) override;

private:
    void DumpRoot(ObjectID objectId, COR_PRF_GC_ROOT_KIND kind, COR_PRF_GC_ROOT_FLAGS flag);
    bool DumpNode(ObjectNode* node, std::vector<ObjectID>& referenceStack);

private:
    ICorProfilerInfo5* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;
    std::unordered_map<ObjectID, std::shared_ptr<ObjectNode>> _heap;
    std::vector<ObjectRoot> _roots;
    uint64_t _objectsCount;
    std::mutex _lock;
};
