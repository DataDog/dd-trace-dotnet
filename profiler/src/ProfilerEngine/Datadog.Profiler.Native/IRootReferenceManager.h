// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

class IRootReferenceManager
{
public:
    virtual HRESULT OnRootReferences2(
        ULONG cRootRefs,
        ObjectID rootRefIds[],
        COR_PRF_GC_ROOT_KIND rootKinds[],
        COR_PRF_GC_ROOT_FLAGS rootFlags[],
        UINT_PTR rootIds[]) = 0;

    virtual HRESULT OnObjectReferences(
        ObjectID objectId,
        ClassID classId,
        ULONG cObjectRefs,
        ObjectID objectRefIds[]) = 0;

    virtual void OnGarbageCollectionFinished() = 0;

public:
    virtual void DumpReferences(ObjectID objectId) = 0;

public:
    virtual ~IRootReferenceManager() = default;
};