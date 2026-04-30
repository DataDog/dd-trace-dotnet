// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClassLayoutCache.h"
#include "Log.h"
#include "shared/src/native-src/com_ptr.h"

ClassLayoutCache::ClassLayoutCache(ICorProfilerInfo12* pCorProfilerInfo, IFrameStore* pFrameStore)
    : _pCorProfilerInfo(pCorProfilerInfo), _pFrameStore(pFrameStore)
{
}

const ClassLayoutCache::ClassLayoutData* ClassLayoutCache::GetLayout(ClassID classID)
{
    if (classID == 0)
    {
        return nullptr;
    }

    auto it = _cache.find(classID);
    if (it != _cache.end())
    {
        return &it->second;
    }

    ClassLayoutData layout = BuildLayout(classID);
    if (layout.classSize == 0 && !layout.isArray)
    {
        return nullptr;  // Failed to build layout
    }

    auto [insertedIt, wasInserted] = _cache.emplace(classID, std::move(layout));
    return &insertedIt->second;
}

ClassLayoutCache::ClassLayoutData ClassLayoutCache::BuildLayout(ClassID classID)
{
    ClassLayoutData layout;

    // Check if it's an array
    CorElementType elementType;
    ClassID elementClassID;
    ULONG rank = 0;

    HRESULT hr = _pCorProfilerInfo->IsArrayClass(
        classID, &elementType, &elementClassID, &rank);

    if (hr == S_OK)
    {
        layout.isArray = true;
        layout.arrayElementType = elementType;
        layout.arrayElementClassID = elementClassID;
        layout.arrayRank = rank;
        return layout;
    }

    // Not an array - get class layout
    ModuleID moduleID;
    mdTypeDef typeDef;
    ClassID parentClassID;
    ULONG32 numTypeArgs = 0;

    hr = _pCorProfilerInfo->GetClassIDInfo2(
        classID, &moduleID, &typeDef, &parentClassID, 0, &numTypeArgs, nullptr);

    if (FAILED(hr))
    {
        return layout;
    }

    // For generic types, retrieve the concrete type arguments so we can resolve
    // ELEMENT_TYPE_VAR fields (e.g., TValue in Dictionary<int, Order>.Entry).
    std::vector<ClassID> typeArgs;
    if (numTypeArgs > 0)
    {
        typeArgs.resize(numTypeArgs);
        hr = _pCorProfilerInfo->GetClassIDInfo2(
            classID, nullptr, nullptr, nullptr, numTypeArgs, &numTypeArgs, typeArgs.data());
        if (FAILED(hr))
        {
            typeArgs.clear();
        }
    }

    // Get class size and field count
    ULONG fieldCount = 0;
    hr = _pCorProfilerInfo->GetClassLayout(
        classID, nullptr, 0, &fieldCount, &layout.classSize);

    if (FAILED(hr) || fieldCount == 0)
    {
        if (parentClassID != 0)
        {
            MergeParentLayout(parentClassID, layout.refFieldOffsets, layout.inlineVtFields);
        }
        layout.elementHasReferenceFields = !layout.refFieldOffsets.empty();
        return layout;
    }

    // Get field offsets
    std::vector<COR_FIELD_OFFSET> fieldOffsets(fieldCount);
    hr = _pCorProfilerInfo->GetClassLayout(
        classID, fieldOffsets.data(), fieldCount, &fieldCount, &layout.classSize);

    if (FAILED(hr))
    {
        return layout;
    }

    // Get metadata import to inspect field types
    ComPtr<IMetaDataImport> pMetadataImport;
    hr = _pCorProfilerInfo->GetModuleMetaData(
        moduleID, ofRead, IID_IMetaDataImport,
        reinterpret_cast<IUnknown**>(pMetadataImport.GetAddressOf()));

    if (FAILED(hr) || pMetadataImport.Get() == nullptr)
    {
        return layout;
    }

    // Process each field using local FieldInfo (not stored in the cache).
    // Classify into compact ref-offset and inline-VT lists.
    for (ULONG i = 0; i < fieldCount; i++)
    {
        FieldInfo fieldInfo;
        fieldInfo.offset = fieldOffsets[i].ulOffset;
        fieldInfo.fieldToken = fieldOffsets[i].ridOfField;

        fieldInfo.isReferenceType = IsFieldReferenceType(
            fieldInfo.fieldToken, moduleID, pMetadataImport.Get(), typeArgs);

        if (fieldInfo.isReferenceType)
        {
            layout.refFieldOffsets.push_back(fieldInfo.offset);
        }
        else
        {
            ResolveValueTypeField(fieldInfo, moduleID, pMetadataImport.Get(), typeArgs);
            if (fieldInfo.isValueType && fieldInfo.valueTypeClassID != 0)
            {
                layout.inlineVtFields.emplace_back(fieldInfo.offset, fieldInfo.valueTypeClassID);
            }
        }
    }

    // Merge parent class's compact lists (parent fields at lower offsets).
    if (parentClassID != 0)
    {
        MergeParentLayout(parentClassID, layout.refFieldOffsets, layout.inlineVtFields);
    }

    layout.elementHasReferenceFields = !layout.refFieldOffsets.empty();
    return layout;
}

bool ClassLayoutCache::IsFieldReferenceType(
    mdFieldDef fieldToken,
    ModuleID moduleID,
    IMetaDataImport* pMetadataImport,
    const std::vector<ClassID>& typeArgs)
{
    if (pMetadataImport == nullptr || fieldToken == 0)
    {
        return false;
    }

    // Get field signature from GetFieldProps
    PCCOR_SIGNATURE pSignature = nullptr;
    ULONG signatureSize = 0;

    HRESULT hr = pMetadataImport->GetFieldProps(
        fieldToken, nullptr, nullptr, 0, nullptr,
        nullptr, &pSignature, &signatureSize,
        nullptr, nullptr, nullptr);

    if (FAILED(hr) || pSignature == nullptr || signatureSize < 2)
    {
        return false;
    }

    // Parse the field signature
    // Format: FIELD (0x06) [custom_modifiers...] type_encoding
    ULONG idx = 0;

    // Skip calling convention byte (IMAGE_CEE_CS_CALLCONV_FIELD = 0x06)
    idx++;

    if (idx >= signatureSize)
    {
        return false;
    }

    // Skip optional custom modifiers (CMOD_OPT, CMOD_REQD) and PINNED/BYREF prefixes.
    while (idx < signatureSize)
    {
        CorElementType prefix = static_cast<CorElementType>(pSignature[idx]);
        if (prefix == ELEMENT_TYPE_CMOD_OPT || prefix == ELEMENT_TYPE_CMOD_REQD)
        {
            idx++;
            mdToken token;
            idx += CorSigUncompressToken(&pSignature[idx], &token);
            continue;
        }
        if (prefix == ELEMENT_TYPE_PINNED || prefix == ELEMENT_TYPE_BYREF)
        {
            idx++;
            continue;
        }
        break;
    }

    if (idx >= signatureSize)
    {
        return false;
    }

    CorElementType elementType = static_cast<CorElementType>(pSignature[idx]);

    if (elementType == ELEMENT_TYPE_CLASS ||
        elementType == ELEMENT_TYPE_STRING ||
        elementType == ELEMENT_TYPE_OBJECT ||
        elementType == ELEMENT_TYPE_SZARRAY ||
        elementType == ELEMENT_TYPE_ARRAY)
    {
        return true;
    }

    // Generic instantiation: GENERICINST (CLASS | VALUETYPE) token arg_count args...
    if (elementType == ELEMENT_TYPE_GENERICINST)
    {
        idx++;
        if (idx >= signatureSize)
        {
            return false;
        }
        CorElementType genericBase = static_cast<CorElementType>(pSignature[idx]);
        return (genericBase == ELEMENT_TYPE_CLASS);
    }

    // Generic type parameter: resolve against the concrete type arguments.
    if (elementType == ELEMENT_TYPE_VAR)
    {
        idx++;
        if (idx >= signatureSize || typeArgs.empty())
        {
            return false;
        }
        ULONG varIndex;
        CorSigUncompressData(&pSignature[idx], &varIndex);

        if (varIndex < static_cast<ULONG>(typeArgs.size()))
        {
            return IsClassIDReferenceType(typeArgs[varIndex]);
        }
        return false;
    }

    return false;
}

bool ClassLayoutCache::IsClassIDReferenceType(ClassID classID)
{
    if (classID == 0)
    {
        return false;
    }

    auto cacheIt = _referenceTypeCache.find(classID);
    if (cacheIt != _referenceTypeCache.end())
    {
        return cacheIt->second;
    }

    // Arrays are always reference types
    CorElementType et;
    ClassID elemID;
    ULONG rank;
    if (_pCorProfilerInfo->IsArrayClass(classID, &et, &elemID, &rank) == S_OK)
    {
        _referenceTypeCache[classID] = true;
        return true;
    }

    ModuleID moduleID;
    mdTypeDef typeDef;
    HRESULT hr = _pCorProfilerInfo->GetClassIDInfo2(
        classID, &moduleID, &typeDef, nullptr, 0, nullptr, nullptr);
    if (FAILED(hr))
    {
        _referenceTypeCache[classID] = true;
        return true;
    }

    ComPtr<IMetaDataImport> pMetadataImport;
    hr = _pCorProfilerInfo->GetModuleMetaData(
        moduleID, ofRead, IID_IMetaDataImport,
        reinterpret_cast<IUnknown**>(pMetadataImport.GetAddressOf()));

    if (FAILED(hr) || pMetadataImport.Get() == nullptr)
    {
        _referenceTypeCache[classID] = false;
        return false;
    }

    bool result = IsReferenceType(typeDef, pMetadataImport.Get());
    _referenceTypeCache[classID] = result;
    return result;
}

void ClassLayoutCache::MergeParentLayout(
    ClassID parentClassID,
    std::vector<ULONG>& refFieldOffsets,
    std::vector<std::pair<ULONG, ClassID>>& inlineVtFields)
{
    const ClassLayoutData* parentLayout = GetLayout(parentClassID);
    if (parentLayout == nullptr)
    {
        return;
    }

    refFieldOffsets.insert(refFieldOffsets.end(),
                           parentLayout->refFieldOffsets.begin(),
                           parentLayout->refFieldOffsets.end());
    inlineVtFields.insert(inlineVtFields.end(),
                          parentLayout->inlineVtFields.begin(),
                          parentLayout->inlineVtFields.end());
}

std::string ClassLayoutCache::GetClassName(ClassID classID) const
{
    std::string name;
    if (_pFrameStore != nullptr && _pFrameStore->GetTypeName(classID, name))
    {
        return name;
    }
    return "<classID=" + std::to_string(classID) + ">";
}

bool ClassLayoutCache::IsReferenceType(mdTypeDef typeDef, IMetaDataImport* pMetadataImport)
{
    if (pMetadataImport == nullptr)
    {
        return false;
    }

    // Get type properties
    DWORD flags = 0;
    mdToken extendsToken = mdTokenNil;
    ULONG nameLen = 0;

    HRESULT hr = pMetadataImport->GetTypeDefProps(
        typeDef, nullptr, 0, &nameLen, &flags, &extendsToken);

    if (FAILED(hr))
    {
        return false;
    }

    // Check if it's a value type
    // Value types extend System.ValueType or System.Enum
    if (TypeFromToken(extendsToken) == mdtTypeRef ||
        TypeFromToken(extendsToken) == mdtTypeDef)
    {
        // Get the base type name
        WCHAR baseTypeName[256];
        ULONG baseTypeNameLen = 0;

        if (TypeFromToken(extendsToken) == mdtTypeRef)
        {
            hr = pMetadataImport->GetTypeRefProps(
                extendsToken, nullptr, baseTypeName, 256, &baseTypeNameLen);
        }
        else
        {
            hr = pMetadataImport->GetTypeDefProps(
                extendsToken, baseTypeName, 256, &baseTypeNameLen, nullptr, nullptr);
        }

        if (SUCCEEDED(hr))
        {
            // Check if it's System.ValueType or System.Enum
            if (wcscmp(baseTypeName, L"System.ValueType") == 0 ||
                wcscmp(baseTypeName, L"System.Enum") == 0)
            {
                return false;  // It's a value type
            }
        }
    }

    // Check type flags
    if (IsTdInterface(flags))
    {
        return true;  // Interfaces are reference types
    }

    // Default: assume reference type if not explicitly a value type
    return true;
}

void ClassLayoutCache::ResolveValueTypeField(
    FieldInfo& fieldInfo,
    ModuleID moduleID,
    IMetaDataImport* pMetadataImport,
    const std::vector<ClassID>& typeArgs)
{
    if (pMetadataImport == nullptr || fieldInfo.fieldToken == 0)
    {
        return;
    }

    PCCOR_SIGNATURE pSignature = nullptr;
    ULONG signatureSize = 0;

    HRESULT hr = pMetadataImport->GetFieldProps(
        fieldInfo.fieldToken, nullptr, nullptr, 0, nullptr,
        nullptr, &pSignature, &signatureSize,
        nullptr, nullptr, nullptr);

    if (FAILED(hr) || pSignature == nullptr || signatureSize < 2)
    {
        return;
    }

    ULONG idx = 0;
    idx++; // Skip calling convention byte (IMAGE_CEE_CS_CALLCONV_FIELD)

    if (idx >= signatureSize)
    {
        return;
    }

    // Skip optional custom modifiers and prefixes (same as IsFieldReferenceType)
    while (idx < signatureSize)
    {
        CorElementType prefix = static_cast<CorElementType>(pSignature[idx]);
        if (prefix == ELEMENT_TYPE_CMOD_OPT || prefix == ELEMENT_TYPE_CMOD_REQD)
        {
            idx++;
            mdToken token;
            idx += CorSigUncompressToken(&pSignature[idx], &token);
            continue;
        }
        if (prefix == ELEMENT_TYPE_PINNED || prefix == ELEMENT_TYPE_BYREF)
        {
            idx++;
            continue;
        }
        break;
    }

    if (idx >= signatureSize)
    {
        return;
    }

    CorElementType elementType = static_cast<CorElementType>(pSignature[idx]);

    ClassID valueTypeClassID = 0;

    // Generic type parameter: resolve against the concrete type arguments.
    // This covers the common case of AsyncStateMachineBox<TStateMachine> where the
    // StateMachine field is of type TStateMachine (ELEMENT_TYPE_VAR, index 0).
    if (elementType == ELEMENT_TYPE_VAR)
    {
        idx++;
        if (idx >= signatureSize || typeArgs.empty())
        {
            return;
        }
        ULONG varIndex;
        CorSigUncompressData(&pSignature[idx], &varIndex);

        if (varIndex < static_cast<ULONG>(typeArgs.size()))
        {
            ClassID argClassID = typeArgs[varIndex];
            if (!IsClassIDReferenceType(argClassID))
            {
                valueTypeClassID = argClassID;
            }
        }
    }

    if (valueTypeClassID == 0)
    {
        return;
    }

    // Check if the value type's layout contains any reference fields (direct or nested).
    // The cached layout's compact lists already encode this: non-empty refFieldOffsets
    // means direct reference sub-fields; non-empty inlineVtFields means nested VTs
    // that themselves carry references.
    const ClassLayoutData* vtLayout = GetLayout(valueTypeClassID);
    if (vtLayout == nullptr)
    {
        return;
    }

    if (!vtLayout->refFieldOffsets.empty() || !vtLayout->inlineVtFields.empty())
    {
        fieldInfo.isValueType = true;
        fieldInfo.valueTypeClassID = valueTypeClassID;
    }
}

size_t ClassLayoutCache::GetMemorySize() const
{
    size_t total = sizeof(ClassLayoutCache);

    total += _cache.bucket_count() * sizeof(void*);
    for (const auto& [id, layout] : _cache)
    {
        total += sizeof(ClassID) + sizeof(ClassLayoutData);
        total += layout.refFieldOffsets.capacity() * sizeof(ULONG);
        total += layout.inlineVtFields.capacity() * sizeof(std::pair<ULONG, ClassID>);
    }

    total += _referenceTypeCache.bucket_count() * sizeof(void*);
    total += _referenceTypeCache.size() * (sizeof(ClassID) + sizeof(bool) + sizeof(void*));

    return total;
}
