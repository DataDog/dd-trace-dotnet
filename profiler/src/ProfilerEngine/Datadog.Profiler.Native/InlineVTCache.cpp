// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "InlineVTCache.h"
#include "Log.h"
#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"

InlineVTCache::InlineVTCache(ICorProfilerInfo12* pCorProfilerInfo, IFrameStore* pFrameStore)
    : _pCorProfilerInfo(pCorProfilerInfo), _pFrameStore(pFrameStore)
{
}

const InlineVTCache::InlineVTInfo* InlineVTCache::GetInlineVTInfo(ClassID classID)
{
    if (classID == 0)
    {
        return nullptr;
    }

    // probably done before calling this helpers but just in case, avoid unneeded cache lookup
    if (!GCDesc::ContainsGCPointers(classID))
    {
        return nullptr;
    }

    auto it = _cache.find(classID);
    if (it != _cache.end())
    {
        return it->second.has_value() ? &it->second.value() : nullptr;
    }

    auto info = BuildInlineVTInfo(classID);
    auto [insertedIt, _] = _cache.emplace(classID, std::move(info));
    return insertedIt->second.has_value() ? &insertedIt->second.value() : nullptr;
}

std::optional<InlineVTCache::InlineVTInfo> InlineVTCache::BuildInlineVTInfo(ClassID classID)
{
    if (!GCDesc::ContainsGCPointers(classID))
    {
        return std::nullopt;
    }

    CorElementType elementType;
    ClassID elementClassID;
    ULONG rank = 0;
    if (_pCorProfilerInfo->IsArrayClass(classID, &elementType, &elementClassID, &rank) == S_OK)
    {
        return std::nullopt;
    }

    ModuleID moduleID;
    mdTypeDef typeDef;
    ClassID parentClassID;
    ULONG32 numTypeArgs = 0;

    HRESULT hr = _pCorProfilerInfo->GetClassIDInfo2(
        classID, &moduleID, &typeDef, &parentClassID, 0, &numTypeArgs, nullptr);

    if (FAILED(hr) || moduleID == 0)
    {
        return std::nullopt;
    }

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

    ULONG fieldCount = 0;
    ULONG classSize = 0;
    hr = _pCorProfilerInfo->GetClassLayout(
        classID, nullptr, 0, &fieldCount, &classSize);

    // Parent fields sit at lower offsets in the object layout, so collect them first.
    std::vector<std::pair<ULONG, ClassID>> inlineVtFields;
    if (parentClassID != 0)
    {
        MergeParentInlineVTs(parentClassID, inlineVtFields);
    }

    if (SUCCEEDED(hr) && fieldCount > 0)
    {
        std::vector<COR_FIELD_OFFSET> fieldOffsets(fieldCount);
        hr = _pCorProfilerInfo->GetClassLayout(
            classID, fieldOffsets.data(), fieldCount, &fieldCount, &classSize);

        if (SUCCEEDED(hr))
        {
            ComPtr<IMetaDataImport> pMetadataImport;
            hr = _pCorProfilerInfo->GetModuleMetaData(
                moduleID, ofRead, IID_IMetaDataImport,
                reinterpret_cast<IUnknown**>(pMetadataImport.GetAddressOf()));

            if (SUCCEEDED(hr) && pMetadataImport.Get() != nullptr)
            {
                for (ULONG i = 0; i < fieldCount; i++)
                {
                    ULONG fieldOffset = fieldOffsets[i].ulOffset;

                    VTFieldInfo fieldInfo;
                    fieldInfo.offset = fieldOffset;
                    fieldInfo.fieldToken = fieldOffsets[i].ridOfField;

                    ResolveValueTypeField(fieldInfo, moduleID, pMetadataImport.Get(), typeArgs);

                    if (fieldInfo.isValueType && fieldInfo.valueTypeClassID != 0)
                    {
                        inlineVtFields.emplace_back(fieldInfo.offset, fieldInfo.valueTypeClassID);
                    }
                }
            }
        }
    }

    if (inlineVtFields.empty())
    {
        return std::nullopt;
    }

    InlineVTInfo info;
    info.fields = std::move(inlineVtFields);
    return info;
}

void InlineVTCache::ResolveValueTypeField(
    VTFieldInfo& fieldInfo,
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
    idx++; // Skip IMAGE_CEE_CS_CALLCONV_FIELD

    if (idx >= signatureSize)
    {
        return;
    }

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
            if (IsClassIDValueType(argClassID))
            {
                valueTypeClassID = argClassID;
            }
        }
    }
    else if (elementType == ELEMENT_TYPE_VALUETYPE)
    {
        idx++;
        if (idx >= signatureSize)
        {
            return;
        }
        mdToken vtToken;
        idx += CorSigUncompressToken(&pSignature[idx], &vtToken);

        hr = _pCorProfilerInfo->GetClassFromTokenAndTypeArgs(
            moduleID, vtToken, 0, nullptr, &valueTypeClassID);
        if (FAILED(hr))
        {
            valueTypeClassID = 0;
        }
    }
    else if (elementType == ELEMENT_TYPE_GENERICINST)
    {
        idx++;
        if (idx >= signatureSize)
        {
            return;
        }
        CorElementType genericBase = static_cast<CorElementType>(pSignature[idx]);
        if (genericBase != ELEMENT_TYPE_VALUETYPE)
        {
            return;
        }

        idx++;
        if (idx >= signatureSize)
        {
            return;
        }
        mdToken vtToken;
        idx += CorSigUncompressToken(&pSignature[idx], &vtToken);

        if (idx >= signatureSize)
        {
            return;
        }
        ULONG genericArgCount;
        idx += CorSigUncompressData(&pSignature[idx], &genericArgCount);

        std::vector<ClassID> genericArgs;
        genericArgs.reserve(genericArgCount);
        for (ULONG argIndex = 0; argIndex < genericArgCount && idx < signatureSize; argIndex++)
        {
            ClassID argClassID = ResolveGenericArgClassID(
                pSignature, signatureSize, idx, moduleID, pMetadataImport, typeArgs);
            if (argClassID == 0)
            {
                return;
            }
            genericArgs.push_back(argClassID);
        }

        if (genericArgs.size() != genericArgCount)
        {
            return;
        }

        hr = _pCorProfilerInfo->GetClassFromTokenAndTypeArgs(
            moduleID, vtToken, genericArgCount, genericArgs.data(), &valueTypeClassID);
        if (FAILED(hr))
        {
            valueTypeClassID = 0;
        }
    }

    if (valueTypeClassID == 0)
    {
        return;
    }

    if (!GCDesc::ContainsGCPointers(valueTypeClassID))
    {
        return;
    }

    fieldInfo.isValueType = true;
    fieldInfo.valueTypeClassID = valueTypeClassID;
}

ClassID InlineVTCache::ResolveGenericArgClassID(
    PCCOR_SIGNATURE pSignature, ULONG signatureSize, ULONG& idx,
    ModuleID moduleID, IMetaDataImport* pMetadataImport,
    const std::vector<ClassID>& typeArgs)
{
    if (idx >= signatureSize)
    {
        return 0;
    }

    CorElementType argType = static_cast<CorElementType>(pSignature[idx]);

    if (argType == ELEMENT_TYPE_VAR)
    {
        idx++;
        ULONG varIndex = 0;
        idx += CorSigUncompressData(&pSignature[idx], &varIndex);
        if (varIndex >= static_cast<ULONG>(typeArgs.size()) || typeArgs[varIndex] == 0)
        {
            return 0;
        }
        return typeArgs[varIndex];
    }

    if (argType == ELEMENT_TYPE_VALUETYPE || argType == ELEMENT_TYPE_CLASS)
    {
        idx++;
        mdToken argToken = mdTokenNil;
        idx += CorSigUncompressToken(&pSignature[idx], &argToken);

        ClassID argClassID = 0;
        HRESULT hr = _pCorProfilerInfo->GetClassFromTokenAndTypeArgs(
            moduleID, argToken, 0, nullptr, &argClassID);
        return SUCCEEDED(hr) ? argClassID : 0;
    }

    if (argType == ELEMENT_TYPE_GENERICINST)
    {
        idx++;
        if (idx >= signatureSize)
        {
            return 0;
        }
        CorElementType base = static_cast<CorElementType>(pSignature[idx]);
        if (base != ELEMENT_TYPE_VALUETYPE && base != ELEMENT_TYPE_CLASS)
        {
            return 0;
        }

        idx++;
        if (idx >= signatureSize)
        {
            return 0;
        }
        mdToken token = mdTokenNil;
        idx += CorSigUncompressToken(&pSignature[idx], &token);

        if (idx >= signatureSize)
        {
            return 0;
        }
        ULONG nestedArgCount = 0;
        idx += CorSigUncompressData(&pSignature[idx], &nestedArgCount);

        std::vector<ClassID> nestedArgs;
        nestedArgs.reserve(nestedArgCount);
        for (ULONG i = 0; i < nestedArgCount && idx < signatureSize; i++)
        {
            ClassID nested = ResolveGenericArgClassID(
                pSignature, signatureSize, idx, moduleID, pMetadataImport, typeArgs);
            if (nested == 0)
            {
                return 0;
            }
            nestedArgs.push_back(nested);
        }

        if (nestedArgs.size() != nestedArgCount)
        {
            return 0;
        }

        ClassID result = 0;
        HRESULT hr = _pCorProfilerInfo->GetClassFromTokenAndTypeArgs(
            moduleID, token, nestedArgCount, nestedArgs.data(), &result);
        return SUCCEEDED(hr) ? result : 0;
    }

    // Primitive / well-known types (single-byte element types with no trailing token).
    const WCHAR* name = GetPrimitiveTypeName(argType);
    if (name != nullptr)
    {
        idx++;
        return ResolvePrimitiveClassID(argType, moduleID, pMetadataImport);
    }

    return 0;
}

const WCHAR* InlineVTCache::GetPrimitiveTypeName(CorElementType elementType)
{
    switch (elementType)
    {
        case ELEMENT_TYPE_BOOLEAN: return WStr("System.Boolean");
        case ELEMENT_TYPE_CHAR:    return WStr("System.Char");
        case ELEMENT_TYPE_I1:      return WStr("System.SByte");
        case ELEMENT_TYPE_U1:      return WStr("System.Byte");
        case ELEMENT_TYPE_I2:      return WStr("System.Int16");
        case ELEMENT_TYPE_U2:      return WStr("System.UInt16");
        case ELEMENT_TYPE_I4:      return WStr("System.Int32");
        case ELEMENT_TYPE_U4:      return WStr("System.UInt32");
        case ELEMENT_TYPE_I8:      return WStr("System.Int64");
        case ELEMENT_TYPE_U8:      return WStr("System.UInt64");
        case ELEMENT_TYPE_R4:      return WStr("System.Single");
        case ELEMENT_TYPE_R8:      return WStr("System.Double");
        case ELEMENT_TYPE_I:       return WStr("System.IntPtr");
        case ELEMENT_TYPE_U:       return WStr("System.UIntPtr");
        case ELEMENT_TYPE_STRING:  return WStr("System.String");
        case ELEMENT_TYPE_OBJECT:  return WStr("System.Object");
        default:                   return nullptr;
    }
}

ClassID InlineVTCache::ResolvePrimitiveClassID(
    CorElementType elementType,
    ModuleID moduleID,
    IMetaDataImport* pMetadataImport)
{
    auto cached = _primitiveClassIDs.find(elementType);
    if (cached != _primitiveClassIDs.end())
    {
        return cached->second;
    }

    const WCHAR* typeName = GetPrimitiveTypeName(elementType);
    if (typeName == nullptr)
    {
        return 0;
    }

    mdToken token = mdTokenNil;

    // Try TypeDef first (works when the module IS the core library).
    HRESULT hr = pMetadataImport->FindTypeDefByName(typeName, mdTokenNil, &token);
    if (FAILED(hr))
    {
        // Enumerate assembly refs and try FindTypeRef for each.
        ComPtr<IMetaDataAssemblyImport> pAsmImport;
        hr = pMetadataImport->QueryInterface(
            IID_IMetaDataAssemblyImport,
            reinterpret_cast<void**>(pAsmImport.GetAddressOf()));
        if (FAILED(hr) || pAsmImport.Get() == nullptr)
        {
            return 0;
        }

        HCORENUM hEnum = nullptr;
        mdAssemblyRef asmRefs[32];
        ULONG count = 0;
        while (SUCCEEDED(pAsmImport->EnumAssemblyRefs(&hEnum, asmRefs, 32, &count)) && count > 0)
        {
            for (ULONG i = 0; i < count; i++)
            {
                hr = pMetadataImport->FindTypeRef(asmRefs[i], typeName, &token);
                if (SUCCEEDED(hr))
                {
                    pAsmImport->CloseEnum(hEnum);
                    goto resolved;
                }
            }
        }
        pAsmImport->CloseEnum(hEnum);
        return 0;
    }

resolved:
    ClassID classID = 0;
    hr = _pCorProfilerInfo->GetClassFromTokenAndTypeArgs(moduleID, token, 0, nullptr, &classID);
    if (SUCCEEDED(hr) && classID != 0)
    {
        _primitiveClassIDs[elementType] = classID;
    }
    return classID;
}

void InlineVTCache::MergeParentInlineVTs(
    ClassID parentClassID,
    std::vector<std::pair<ULONG, ClassID>>& fields)
{
    const InlineVTInfo* parentInfo = GetInlineVTInfo(parentClassID);
    if (parentInfo == nullptr)
    {
        return;
    }

    fields.insert(fields.end(),
                  parentInfo->fields.begin(),
                  parentInfo->fields.end());
}

bool InlineVTCache::IsClassIDValueType(ClassID classID)
{
    if (classID == 0)
    {
        return false;
    }

    CorElementType et;
    ClassID elemID;
    ULONG rank;
    if (_pCorProfilerInfo->IsArrayClass(classID, &et, &elemID, &rank) == S_OK)
    {
        return false;
    }

    ModuleID moduleID;
    mdTypeDef typeDef;
    HRESULT hr = _pCorProfilerInfo->GetClassIDInfo2(
        classID, &moduleID, &typeDef, nullptr, 0, nullptr, nullptr);
    if (FAILED(hr))
    {
        return false;
    }

    ComPtr<IMetaDataImport> pMetadataImport;
    hr = _pCorProfilerInfo->GetModuleMetaData(
        moduleID, ofRead, IID_IMetaDataImport,
        reinterpret_cast<IUnknown**>(pMetadataImport.GetAddressOf()));

    if (FAILED(hr) || pMetadataImport.Get() == nullptr)
    {
        return false;
    }

    DWORD flags = 0;
    mdToken extendsToken = mdTokenNil;
    hr = pMetadataImport->GetTypeDefProps(
        typeDef, nullptr, 0, nullptr, &flags, &extendsToken);

    if (FAILED(hr))
    {
        return false;
    }

    if (TypeFromToken(extendsToken) == mdtTypeRef ||
        TypeFromToken(extendsToken) == mdtTypeDef)
    {
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
            if (WStrCmp(baseTypeName, WStr("System.ValueType")) == 0 ||
                WStrCmp(baseTypeName, WStr("System.Enum")) == 0)
            {
                return true;
            }
        }
    }

    return false;
}

size_t InlineVTCache::GetMemorySize() const
{
    size_t total = sizeof(InlineVTCache);

    total += _cache.bucket_count() * sizeof(void*);
    for (const auto& [id, opt] : _cache)
    {
        total += sizeof(ClassID) + sizeof(std::optional<InlineVTInfo>);
        if (opt.has_value())
        {
            total += opt->fields.capacity() * sizeof(std::pair<ULONG, ClassID>);
        }
    }

    total += _primitiveClassIDs.bucket_count() * sizeof(void*);
    total += _primitiveClassIDs.size() * (sizeof(CorElementType) + sizeof(ClassID));

    return total;
}

size_t InlineVTCache::GetEntryCount() const
{
    size_t count = 0;
    for (const auto& [_, opt] : _cache)
    {
        if (opt.has_value())
        {
            count++;
        }
    }
    return count;
}
