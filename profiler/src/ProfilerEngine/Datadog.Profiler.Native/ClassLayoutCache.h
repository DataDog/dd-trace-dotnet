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
struct FieldInfo
{
    ULONG offset;           // Field offset in object
    ClassID fieldTypeID;    // Type of the field (0 if not a reference type)
    bool isReferenceType;   // True if field contains a reference
    mdFieldDef fieldToken;  // Metadata token for the field

    // For inline value type fields that contain reference sub-fields (e.g., the state machine
    // struct inside AsyncStateMachineBox<TStateMachine>). When true, use valueTypeClassID
    // with ClassLayoutCache::GetLayout() to enumerate the value type's sub-fields.
    bool isValueType;
    ClassID valueTypeClassID;

    FieldInfo() : offset(0), fieldTypeID(0), isReferenceType(false), fieldToken(0),
                  isValueType(false), valueTypeClassID(0)
    {
    }
};

// Cached class layout information
class ClassLayoutCache
{
public:
    struct ClassLayoutData
    {
        ULONG classSize;
        std::vector<FieldInfo> fields;
        bool isArray;
        CorElementType arrayElementType;
        ClassID arrayElementClassID;
        ULONG arrayRank;

        ClassLayoutData() : classSize(0), isArray(false), arrayElementType(ELEMENT_TYPE_END),
                           arrayElementClassID(0), arrayRank(0)
        {
        }
    };

    ClassLayoutCache(ICorProfilerInfo12* pCorProfilerInfo, IFrameStore* pFrameStore);

    const ClassLayoutData* GetLayout(ClassID classID);

    void Clear()
    {
        _cache.clear();
        _referenceTypeCache.clear();
    }

    size_t GetMemorySize() const;
    size_t GetEntryCount() const { return _cache.size(); }

private:
    ClassLayoutData BuildLayout(ClassID classID);
    bool IsReferenceType(mdTypeDef typeDef, IMetaDataImport* pMetadataImport);
    bool IsClassIDReferenceType(ClassID classID);
    void GetParentClassFields(ClassID parentClassID, std::vector<FieldInfo>& fields);
    bool IsFieldReferenceType(
        mdFieldDef fieldToken,
        ModuleID moduleID,
        IMetaDataImport* pMetadataImport,
        const std::vector<ClassID>& typeArgs);

    // For non-reference fields, try to detect inline value types whose layout contains
    // reference sub-fields.  Currently resolves ELEMENT_TYPE_VAR (generic type parameter)
    // fields — this covers async state machine structs inside AsyncStateMachineBox<T>.
    void ResolveValueTypeField(
        FieldInfo& fieldInfo,
        ModuleID moduleID,
        IMetaDataImport* pMetadataImport,
        const std::vector<ClassID>& typeArgs);

    std::string GetClassName(ClassID classID) const;

    ICorProfilerInfo12* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;
    std::unordered_map<ClassID, ClassLayoutData> _cache;
    std::unordered_map<ClassID, bool> _referenceTypeCache;
};
