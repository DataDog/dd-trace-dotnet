// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackFrameInfo.h"
#include "ResolvedSymbolsCache.h"
#include "StackFrameCodeKind.h"

#include "shared/src/native-src/string.h"

ManagedTypeInfoMutable::ManagedTypeInfoMutable() :
    _typeName{nullptr},
    _assemblyName{nullptr}
{
}

ManagedTypeInfoMutable::~ManagedTypeInfoMutable()
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_typeName);
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_assemblyName);
}

const ManagedTypeInfo* ManagedTypeInfoMutable::ConvertToNewImmutable(void)
{
    ManagedTypeInfo* pImmutable = new ManagedTypeInfo(_typeName == nullptr ? &ResolvedSymbolsCache::UnknownTypeAndNamespaceWithPrefixMoniker : _typeName,
                                                      _assemblyName == nullptr ? &ResolvedSymbolsCache::UnknownAssemblyNameMoniker : _assemblyName);
    _typeName = nullptr;
    _assemblyName = nullptr;
    return pImmutable;
}

void ManagedTypeInfoMutable::CopyFrom(const ManagedTypeInfo& typeInfoImmutable)
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_typeName);

    const shared::WSTRING* otherTypeName = typeInfoImmutable.GetTypeName();
    this->_typeName = (otherTypeName == nullptr || ResolvedSymbolsCache::IsSharedStaticConstant(otherTypeName))
                          ? otherTypeName
                          : new shared::WSTRING(*otherTypeName);

    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_assemblyName);

    const shared::WSTRING* otherAssemblyName = typeInfoImmutable.GetAssemblyName();
    this->_assemblyName = (otherAssemblyName == nullptr || ResolvedSymbolsCache::IsSharedStaticConstant(otherAssemblyName))
                              ? otherAssemblyName
                              : new shared::WSTRING(*otherAssemblyName);
}

shared::WSTRING* ManagedTypeInfoMutable::GetTypeNameForModifying(void)
{
    if (ResolvedSymbolsCache::IsSharedStaticConstant(_typeName))
    {
        _typeName = new shared::WSTRING(*_typeName);
    }

    return const_cast<shared::WSTRING*>(_typeName);
}

void ManagedTypeInfoMutable::CreateNewTypeName(WCHAR* namespaceNameChars,
                                               std::int32_t namespaceCharCount,
                                               WCHAR* typeNameChars,
                                               std::int32_t typeCharCount)
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_typeName);

    ResolvedSymbolsCache::AdjustCharBuffLenToRemoveTrailingNulls(namespaceNameChars, &namespaceCharCount);
    ResolvedSymbolsCache::AdjustCharBuffLenToRemoveTrailingNulls(typeNameChars, &typeCharCount);

    bool hasNamespace = (namespaceNameChars != nullptr && namespaceCharCount >= 1);
    bool hasType = (typeNameChars != nullptr && typeCharCount >= 1);

    if (hasNamespace || hasType)
    {
        shared::WSTRING* typeName = new shared::WSTRING();

        if (hasNamespace)
        {
            ResolvedSymbolsCache::ReplaceReservedChars(namespaceNameChars, namespaceCharCount);
            typeName->append(ResolvedSymbolsCache::NamespacePrefix);
            typeName->append(namespaceNameChars, namespaceCharCount);
        }
        else
        {
            typeName->append(ResolvedSymbolsCache::EmptyNamespaceWithPrefixMoniker);
        }

        if (hasType)
        {
            ResolvedSymbolsCache::ReplaceReservedChars(typeNameChars, typeCharCount);
            typeName->append(ResolvedSymbolsCache::TypeNamePrefix);
            typeName->append(typeNameChars, typeCharCount);
        }
        else
        {
            typeName->append(ResolvedSymbolsCache::UnknownTypeNameWithPrefixMoniker);
        }

        _typeName = typeName;
    }
    else
    {
        _typeName = &ResolvedSymbolsCache::EmptyNamespaceWithPrefixMoniker;
    }
}

void ManagedTypeInfoMutable::AttachEnclosedTypeName(WCHAR* typeNameChars, std::int32_t typeCharCount)
{
    if (typeNameChars == nullptr || typeCharCount < 1)
    {
        return;
    }

    ResolvedSymbolsCache::AdjustCharBuffLenToRemoveTrailingNulls(typeNameChars, &typeCharCount);
    ResolvedSymbolsCache::ReplaceReservedChars(typeNameChars, typeCharCount);

    shared::WSTRING* typeName = GetTypeNameForModifying();
    typeName->append(ResolvedSymbolsCache::NestedTypeSeparator);
    typeName->append(typeNameChars, typeCharCount);
}

void ManagedTypeInfoMutable::UseTypeName(const shared::WSTRING* name)
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_typeName);
    _typeName = (name == nullptr) ? &ResolvedSymbolsCache::UnknownTypeAndNamespaceWithPrefixMoniker : name;
}

void ManagedTypeInfoMutable::CreateNewAssemblyName(WCHAR* nameChars, std::int32_t nameCharCount)
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_assemblyName);

    if (nameChars == nullptr || nameCharCount < 1)
    {
        _assemblyName = new shared::WSTRING(ResolvedSymbolsCache::UnknownAssemblyNameMoniker);
    }
    else
    {
        ResolvedSymbolsCache::AdjustCharBuffLenToRemoveTrailingNulls(nameChars, &nameCharCount);
        ResolvedSymbolsCache::ReplaceReservedChars(nameChars, nameCharCount);
        _assemblyName = new shared::WSTRING(nameChars, nameCharCount);
    }
}

void ManagedTypeInfoMutable::UseAssemblyName(const shared::WSTRING* name)
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_assemblyName);
    _assemblyName = (name == nullptr) ? &ResolvedSymbolsCache::UnknownAssemblyNameMoniker : name;
}

/* ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- */

ManagedTypeInfo::~ManagedTypeInfo()
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_typeName);
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_assemblyName);
}

/* ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- */

StackFrameInfo::StackFrameInfo(const StackFrameCodeKind codeKind,
                               const shared::WSTRING* pFunctionName,
                               const ManagedTypeInfo* pContainingTypeInfo,
                               const shared::WSTRING** ppManagedMethodFlags) :
    _codeKind(codeKind),
    _pFunctionName(pFunctionName == nullptr ? &ResolvedSymbolsCache::UnknownFunctionNameMoniker : pFunctionName),
    _pContainingTypeInfo(pContainingTypeInfo),
    _ppManagedMethodFlags(ppManagedMethodFlags)
{
    if (ppManagedMethodFlags == nullptr)
    {
        _ppManagedMethodFlags = new const shared::WSTRING*[1];
        _ppManagedMethodFlags[0] = nullptr;
    }
}

StackFrameInfo::~StackFrameInfo()
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_pFunctionName);

    const ManagedTypeInfo* pContainingTypeInfo = _pContainingTypeInfo;
    if (pContainingTypeInfo != nullptr)
    {
        _pContainingTypeInfo = nullptr;
        const_cast<ManagedTypeInfo*>(pContainingTypeInfo)->Release();
    }

    const shared::WSTRING** ppManagedMethodFlags = _ppManagedMethodFlags;
    if (ppManagedMethodFlags != nullptr)
    {
        _ppManagedMethodFlags = nullptr;
        delete[] * ppManagedMethodFlags;
    }
}

const shared::WSTRING* StackFrameInfo::GetContainingTypeName(void) const
{
    return (_pContainingTypeInfo == nullptr)
               ? &ResolvedSymbolsCache::UnknownTypeAndNamespaceWithPrefixMoniker
               : _pContainingTypeInfo->GetTypeName();
}

const shared::WSTRING* StackFrameInfo::GetContainingAssemblyName(void) const
{
    return (_pContainingTypeInfo == nullptr)
               ? &ResolvedSymbolsCache::UnknownAssemblyNameMoniker
               : _pContainingTypeInfo->GetAssemblyName();
}

shared::WSTRING* StackFrameInfo::GetFunctionNameForModifying(void)
{
    if (ResolvedSymbolsCache::IsSharedStaticConstant(_pFunctionName))
    {
        _pFunctionName = new shared::WSTRING(*_pFunctionName);
    }

    return const_cast<shared::WSTRING*>(_pFunctionName);
}

void StackFrameInfo::CreateNewFunctionName(WCHAR* nameChars, std::int32_t nameCharCount)
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_pFunctionName);

    if (nameChars == nullptr || nameCharCount < 1)
    {
        _pFunctionName = new shared::WSTRING(ResolvedSymbolsCache::UnknownFunctionNameMoniker);
    }
    else
    {
        ResolvedSymbolsCache::AdjustCharBuffLenToRemoveTrailingNulls(nameChars, &nameCharCount);
        ResolvedSymbolsCache::ReplaceReservedChars(nameChars, nameCharCount);
        _pFunctionName = new shared::WSTRING(nameChars, nameCharCount);
    }
}

void StackFrameInfo::UseFunctionName(const shared::WSTRING* name)
{
    ResolvedSymbolsCache::ReleaseWStringIfNotShared(&_pFunctionName);
    _pFunctionName = (name == nullptr) ? &ResolvedSymbolsCache::UnknownFunctionNameMoniker : name;
}

void StackFrameInfo::SetContainingTypeInfo(const ManagedTypeInfo* pTypeInfo)
{
    const ManagedTypeInfo* pContainingTypeInfo = _pContainingTypeInfo;
    if (pContainingTypeInfo != nullptr)
    {
        _pContainingTypeInfo = nullptr;
        const_cast<ManagedTypeInfo*>(pContainingTypeInfo)->Release();
    }

    if (pTypeInfo != nullptr)
    {
        const_cast<ManagedTypeInfo*>(pTypeInfo)->AddRef();
        _pContainingTypeInfo = pTypeInfo;
    }
}

void StackFrameInfo::SetManagedMethodFlags(const std::vector<const shared::WSTRING*>& methodFlagMoikers)
{

    //StackFrameInfo::ReleaseInternalWStringArrayIfRequired(&_ppManagedMethodFlags);

    _ppManagedMethodFlags = new const shared::WSTRING*[methodFlagMoikers.size() + 1];
    for (std::vector<const shared::WSTRING*>::size_type i = 0; i < methodFlagMoikers.size(); i++)
    {
        _ppManagedMethodFlags[i] = methodFlagMoikers.at(i);
    }

    _ppManagedMethodFlags[methodFlagMoikers.size()] = nullptr;
}

void StackFrameInfo::ToDisplayString(shared::WSTRING* output) const
{
    if (output == nullptr)
    {
        return;
    }

    switch (_codeKind)
    {
        case StackFrameCodeKind::NotDetermined:
            output->append(WStr("Undetermined frame:  "));
            break;
        case StackFrameCodeKind::ClrManaged:
            output->append(WStr("ClrManaged frame:    "));
            break;
        case StackFrameCodeKind::ClrNative:
            output->append(WStr("ClrNative frame:     "));
            break;
        case StackFrameCodeKind::UserNative:
            output->append(WStr("UserNative frame:    "));
            break;
        case StackFrameCodeKind::UnknownNative:
            output->append(WStr("UnknownNative frame: "));
            break;
        case StackFrameCodeKind::Kernel:
            output->append(WStr("Kernel frame:        "));
            break;
        case StackFrameCodeKind::MultipleMixed:
            output->append(WStr("Multiple frames:     "));
            break;
        case StackFrameCodeKind::Dummy:
            output->append(WStr("Dummy frame:         "));
            break;
        default:
            output->append(WStr("Unknown kind frame:  "));
            break;
    }

    if (_codeKind == StackFrameCodeKind::MultipleMixed)
    {
        output->append(WStr("One or more additional frames are present; no detailed information is available."));
        return;
    }

    size_t lenBeforeFlags = output->length();
    output->append(WStr("["));

    const shared::WSTRING** flag = _ppManagedMethodFlags;
    while (*flag != nullptr)
    {
        if (flag != _ppManagedMethodFlags)
        {
            output->append(WStr(", "));
        }

        output->append(**flag);
        flag++;
    }

    output->append(WStr("] "));
    size_t lenAfterFlags = output->length();
    size_t flagsLen = lenAfterFlags - lenBeforeFlags;
    static const size_t FlagsColumnMinWidth = 27;
    while (flagsLen < FlagsColumnMinWidth)
    {
        output->append(WStr(" "));
        flagsLen++;
    }

    output->append(*GetContainingAssemblyName());
    output->append(WStr("::"));
    output->append(*GetContainingTypeName());
    output->append(WStr("::"));
    output->append(*GetFunctionName());
}
