// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "IFrameStore.h"
#include <unordered_map>
#include <vector>
#include <memory>

// Forward declarations
struct COR_FIELD_OFFSET;

// Field information
struct FieldInfo {
    ULONG offset;           // Field offset in object
    ClassID fieldTypeID;    // Type of the field (0 if not a reference type)
    bool isReferenceType;   // True if field contains a reference
    mdFieldDef fieldToken;  // Metadata token for the field

    FieldInfo() : offset(0), fieldTypeID(0), isReferenceType(false), fieldToken(0) {}
};

// Cached class layout information
class ClassLayoutCache {
public:
    struct ClassLayoutData {
        ULONG classSize;
        std::vector<FieldInfo> fields;
        bool isArray;
        CorElementType arrayElementType;
        ClassID arrayElementClassID;
        ULONG arrayRank;

        ClassLayoutData() : classSize(0), isArray(false), arrayElementType(ELEMENT_TYPE_END),
                           arrayElementClassID(0), arrayRank(0) {}
    };

    ClassLayoutCache(ICorProfilerInfo12* pCorProfilerInfo, IFrameStore* pFrameStore);

    const ClassLayoutData* GetLayout(ClassID classID);

    void Clear() { _cache.clear(); }

private:
    ClassLayoutData BuildLayout(ClassID classID);
    bool IsReferenceType(ClassID classID, mdTypeDef typeDef, ModuleID moduleID);
    void GetParentClassFields(ClassID parentClassID, std::vector<FieldInfo>& fields);
    bool IsFieldReferenceType(mdFieldDef fieldToken, ModuleID moduleID, IMetaDataImport* pMetadataImport);

    ICorProfilerInfo12* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;
    std::unordered_map<ClassID, ClassLayoutData> _cache;
};
