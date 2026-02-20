// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClassLayoutCache.h"
#include "Log.h"
#include "shared/src/native-src/com_ptr.h"

#include <algorithm>

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

    _cache[classID] = layout;
    return &_cache[classID];
}

ClassLayoutCache::ClassLayoutData ClassLayoutCache::BuildLayout(ClassID classID)
{
    ClassLayoutData layout;
    layout.isArray = false;

    // Check if it's an array
    CorElementType elementType;
    ClassID elementClassID;
    ULONG rank = 0;

    HRESULT hr = _pCorProfilerInfo->IsArrayClass(
        classID, &elementType, &elementClassID, &rank);

    if (hr == S_OK)
    {
        // It's an array
        layout.isArray = true;
        layout.arrayElementType = elementType;
        layout.arrayElementClassID = elementClassID;
        layout.arrayRank = rank;

        // Arrays have a special layout - elements start after the array header
        // For reference type arrays, each element is a pointer
        if (elementType == ELEMENT_TYPE_CLASS ||
            elementType == ELEMENT_TYPE_STRING ||
            elementType == ELEMENT_TYPE_OBJECT ||
            elementType == ELEMENT_TYPE_SZARRAY ||
            elementType == ELEMENT_TYPE_ARRAY)
        {
            // Array of reference types - we'll handle this specially
            FieldInfo fieldInfo;
            fieldInfo.isReferenceType = true;
            fieldInfo.fieldTypeID = elementClassID;
            layout.fields.push_back(fieldInfo);
        }

        return layout;
    }

    // Not an array - get class layout
    ModuleID moduleID;
    mdTypeDef typeDef;
    ClassID parentClassID;

    hr = _pCorProfilerInfo->GetClassIDInfo2(
        classID, &moduleID, &typeDef, &parentClassID, 0, nullptr, nullptr);

    if (FAILED(hr))
    {
        return layout;
    }

    // Get class size and field count
    ULONG fieldCount = 0;
    hr = _pCorProfilerInfo->GetClassLayout(
        classID, nullptr, 0, &fieldCount, &layout.classSize);

    if (FAILED(hr) || fieldCount == 0)
    {
        // Get parent class fields if any
        if (parentClassID != 0)
        {
            GetParentClassFields(parentClassID, layout.fields);
        }
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

    // Process each field
    for (ULONG i = 0; i < fieldCount; i++)
    {
        FieldInfo fieldInfo;
        fieldInfo.offset = fieldOffsets[i].ulOffset;
        fieldInfo.fieldToken = fieldOffsets[i].ridOfField;

        // Check if field is a reference type
        fieldInfo.isReferenceType = IsFieldReferenceType(
            fieldInfo.fieldToken, moduleID, pMetadataImport.Get());

        // If it's a reference type, try to get the field type ClassID
        // (This would require parsing the signature, which is complex)
        // For now, we'll determine the type during traversal by reading the actual field value

        layout.fields.push_back(fieldInfo);
    }

    // Get parent class fields
    if (parentClassID != 0)
    {
        GetParentClassFields(parentClassID, layout.fields);
    }

    return layout;
}

bool ClassLayoutCache::IsFieldReferenceType(mdFieldDef fieldToken, ModuleID moduleID, IMetaDataImport* pMetadataImport)
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
    // These can appear before the actual element type in the signature.
    while (idx < signatureSize)
    {
        CorElementType prefix = static_cast<CorElementType>(pSignature[idx]);
        if (prefix == ELEMENT_TYPE_CMOD_OPT || prefix == ELEMENT_TYPE_CMOD_REQD)
        {
            idx++; // skip the CMOD byte
            // skip the compressed token that follows the CMOD
            mdToken token;
            idx += CorSigUncompressToken(&pSignature[idx], &token);
            continue;
        }
        if (prefix == ELEMENT_TYPE_PINNED || prefix == ELEMENT_TYPE_BYREF)
        {
            idx++; // skip the prefix byte
            continue;
        }
        break; // actual element type reached
    }

    if (idx >= signatureSize)
    {
        return false;
    }

    // Read the actual element type
    CorElementType elementType = static_cast<CorElementType>(pSignature[idx]);

    // Direct reference types
    if (elementType == ELEMENT_TYPE_CLASS ||
        elementType == ELEMENT_TYPE_STRING ||
        elementType == ELEMENT_TYPE_OBJECT ||
        elementType == ELEMENT_TYPE_SZARRAY ||
        elementType == ELEMENT_TYPE_ARRAY)
    {
        return true;
    }

    // Generic instantiation: GENERICINST (CLASS | VALUETYPE) token arg_count args...
    // If the generic is instantiated over CLASS, it's a reference type (e.g., List<string>).
    // If over VALUETYPE, it's a value type (e.g., Nullable<int>).
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

    // ELEMENT_TYPE_VAR / ELEMENT_TYPE_MVAR are generic type/method parameters.
    // At the type level we can't tell if T is a reference or value type without
    // resolving the concrete instantiation. Skip for safety (conservative: treat as non-reference).

    // Value types (ELEMENT_TYPE_VALUETYPE, primitives, etc.) are not references.
    return false;
}

void ClassLayoutCache::GetParentClassFields(ClassID parentClassID, std::vector<FieldInfo>& fields)
{
    // Recursively get parent class fields
    const ClassLayoutData* parentLayout = GetLayout(parentClassID);
    if (parentLayout != nullptr)
    {
        // Append parent fields (they come before child fields in memory)
        fields.insert(fields.begin(), parentLayout->fields.begin(), parentLayout->fields.end());
    }
}

bool ClassLayoutCache::IsReferenceType(ClassID classID, mdTypeDef typeDef, ModuleID moduleID)
{
    // Get metadata import for the module
    ComPtr<IMetaDataImport> pMetadataImport;
    HRESULT hr = _pCorProfilerInfo->GetModuleMetaData(
        moduleID, ofRead, IID_IMetaDataImport,
        reinterpret_cast<IUnknown**>(pMetadataImport.GetAddressOf()));

    if (FAILED(hr) || pMetadataImport.Get() == nullptr)
    {
        return false;
    }

    // Get type properties
    DWORD flags = 0;
    mdToken extendsToken = mdTokenNil;
    ULONG nameLen = 0;

    hr = pMetadataImport->GetTypeDefProps(
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
