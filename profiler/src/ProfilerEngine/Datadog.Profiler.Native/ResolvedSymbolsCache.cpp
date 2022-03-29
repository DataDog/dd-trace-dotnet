// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <string>

#include "ResolvedSymbolsCache.h"
#include "StackFrameCodeKind.h"

#include "shared/src/native-src/string.h"

const std::hash<mdTypeDef> ResolvedSymbolsCache::TypeDefTokenAndModuleId::Hasher::TokenHasher = std::hash<mdTypeDef>();
const std::hash<ModuleID> ResolvedSymbolsCache::TypeDefTokenAndModuleId::Hasher::ModuleIdHasher = std::hash<ModuleID>();

const shared::WSTRING ResolvedSymbolsCache::UnknownThreadName = WStr("");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_PrivateScope = WStr("access:none");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_Private = WStr("private");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_FamANDAssem = WStr("private protected");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_Assem = WStr("internal");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_Family = WStr("protected");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_FamORAssem = WStr("protected internal");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_Public = WStr("public");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_UnknownAccess = WStr("access:unknown");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_Static = WStr("static");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_Final = WStr("final");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_Virtual = WStr("virtual");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_Abstract = WStr("abstract");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_SpecialName = WStr("SpecialName");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_PinvokeImpl = WStr("PInvoke");
const shared::WSTRING ResolvedSymbolsCache::MethodFlagMoniker_UnmanagedExport = WStr("ManagedExportedToUnmanaged");
const shared::WSTRING ResolvedSymbolsCache::NamespacePrefix = WStr(" |ns:"); // WStr("｛｝"); // ⦃⦄ ❴❵ ﹛﹜ ｛｝  {}  🗍
const shared::WSTRING ResolvedSymbolsCache::TypeNamePrefix = WStr(" |ct:");  // WStr(" 🗋 ⚙ ∋ | :: ");
const shared::WSTRING ResolvedSymbolsCache::NestedTypeSeparator = WStr(".");
const shared::WSTRING ResolvedSymbolsCache::EmptyNamespaceMoniker = WStr("<Empty-Namespace>");
const shared::WSTRING ResolvedSymbolsCache::UnknownFunctionNameMoniker = WStr("Unknown-Function-or-Method");
const shared::WSTRING ResolvedSymbolsCache::UnknownTypeNameMoniker = WStr("Unknown-Type");
const shared::WSTRING ResolvedSymbolsCache::UnknownAssemblyNameMoniker = WStr("Unknown-Assembly-or-Library");
const shared::WSTRING ResolvedSymbolsCache::EmptyNamespaceWithPrefixMoniker = NamespacePrefix + EmptyNamespaceMoniker;
const shared::WSTRING ResolvedSymbolsCache::UnknownTypeNameWithPrefixMoniker = TypeNamePrefix + UnknownTypeNameMoniker;
const shared::WSTRING ResolvedSymbolsCache::SpecialTypeMoniker_Array = WStr("Array-Type");
const shared::WSTRING ResolvedSymbolsCache::SpecialTypeMoniker_NonArrayComposite = WStr("Non-Array-Composite-Type");
const shared::WSTRING ResolvedSymbolsCache::SpecialTypeMoniker_SystemDotCanon = NamespacePrefix + WStr("System") + TypeNamePrefix + WStr("__Canon"); // WStr("System.__Canon");
const shared::WSTRING ResolvedSymbolsCache::UnknownTypeAndNamespaceWithPrefixMoniker = EmptyNamespaceWithPrefixMoniker + UnknownTypeNameWithPrefixMoniker;

const WCHAR* ResolvedSymbolsCache::GenericParamList_Open = WStr("{");
const WCHAR* ResolvedSymbolsCache::GenericParamList_Close = WStr("}");
const WCHAR* ResolvedSymbolsCache::GenericParamList_Separator = WStr(", ");
const WCHAR* ResolvedSymbolsCache::GenericParamList_MultipleUnknownItemsPlaceholder = WStr("...");
const WCHAR* ResolvedSymbolsCache::GenericParamList_SingleUnknownItemPlaceholderBase = WStr("T");
const WCHAR* ResolvedSymbolsCache::ReservedApiNameChars = WStr("|:{}\0");
const WCHAR ResolvedSymbolsCache::ReservedCharReplacement = WStr('_');

bool ResolvedSymbolsCache::IsSharedStaticConstant(const shared::WSTRING* str)
{
    return (
        str == &ResolvedSymbolsCache::UnknownThreadName ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_PrivateScope ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_Private ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_FamANDAssem ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_Assem ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_Family ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_FamORAssem ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_Public ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_UnknownAccess ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_Static ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_Final ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_Virtual ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_Abstract ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_SpecialName ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_PinvokeImpl ||
        str == &ResolvedSymbolsCache::MethodFlagMoniker_UnmanagedExport ||
        str == &ResolvedSymbolsCache::NamespacePrefix ||
        str == &ResolvedSymbolsCache::TypeNamePrefix ||
        str == &ResolvedSymbolsCache::NestedTypeSeparator ||
        str == &ResolvedSymbolsCache::EmptyNamespaceMoniker ||
        str == &ResolvedSymbolsCache::UnknownFunctionNameMoniker ||
        str == &ResolvedSymbolsCache::UnknownTypeNameMoniker ||
        str == &ResolvedSymbolsCache::UnknownAssemblyNameMoniker ||
        str == &ResolvedSymbolsCache::EmptyNamespaceWithPrefixMoniker ||
        str == &ResolvedSymbolsCache::UnknownTypeNameWithPrefixMoniker ||
        str == &ResolvedSymbolsCache::SpecialTypeMoniker_Array ||
        str == &ResolvedSymbolsCache::SpecialTypeMoniker_NonArrayComposite ||
        str == &ResolvedSymbolsCache::SpecialTypeMoniker_SystemDotCanon ||
        str == &ResolvedSymbolsCache::UnknownTypeAndNamespaceWithPrefixMoniker
        );
}

void ResolvedSymbolsCache::ReleaseWStringIfNotShared(const shared::WSTRING** ppWString)
{
    if (ppWString != nullptr && *ppWString != nullptr)
    {
        if (!ResolvedSymbolsCache::IsSharedStaticConstant(*ppWString))
        {
            delete *ppWString;
        }

        *ppWString = nullptr;
    }
}

void ResolvedSymbolsCache::ReplaceReservedChars(WCHAR* wcharBuff, std::int32_t wcharCount)
{
    if (wcharBuff != nullptr)
    {
        std::int32_t offs = 0;
        WCHAR c = *wcharBuff;
        while (offs < wcharCount && c != WStr('\0'))
        {
            const WCHAR* reservedChar = ResolvedSymbolsCache::ReservedApiNameChars;
            while (*reservedChar != WStr('\0'))
            {
                if (c == *reservedChar)
                {
                    *(wcharBuff + offs) = ResolvedSymbolsCache::ReservedCharReplacement;
                    break;
                }

                reservedChar++;
            }

            offs++;
            c = *(wcharBuff + offs);
        }
    }
}

ResolvedSymbolsCache::ResolvedSymbolsCache()
{
    _pTypeInfosByClassId = new std::unordered_map<ClassID, const ManagedTypeInfo*>();
    _pTypeInfosByTypeDefAndModuleId = new std::unordered_map<TypeDefTokenAndModuleId, const ManagedTypeInfo*, TypeDefTokenAndModuleId::Hasher>();
    _pStackFrameInfosByFunctionId = new std::unordered_map<FunctionID, const StackFrameInfo*>();
    _pNoInfoStackFrameInfosByCodeKind = new std::unordered_map<StackFrameCodeKind, const StackFrameInfo*>();
}

ResolvedSymbolsCache::~ResolvedSymbolsCache()
{
    {
        const std::lock_guard<std::mutex> lock(_lockTypeInfosByClassId);
        DeleteTrackedMap<
            std::unordered_map<ClassID, const ManagedTypeInfo*>,
            ManagedTypeInfo*>(&_pTypeInfosByClassId);
    }

    {
        const std::lock_guard<std::mutex> lock(_lockTypeInfosByTypeDefAndModuleId);
        DeleteTrackedMap<
            std::unordered_map<TypeDefTokenAndModuleId, const ManagedTypeInfo*, TypeDefTokenAndModuleId::Hasher>,
            ManagedTypeInfo*>(&_pTypeInfosByTypeDefAndModuleId);
    }

    {
        const std::lock_guard<std::mutex> lock(_lockStackFrameInfosByFunctionId);
        DeleteTrackedMap<
            std::unordered_map<FunctionID, const StackFrameInfo*>,
            StackFrameInfo*>(&_pStackFrameInfosByFunctionId);
    }

    {
        const std::lock_guard<std::mutex> lock(_lockNoInfoStackFrameInfosByCodeKind);
        DeleteTrackedMap<
            std::unordered_map<StackFrameCodeKind, const StackFrameInfo*>,
            StackFrameInfo*>(&_pNoInfoStackFrameInfosByCodeKind);
    }
}

bool ResolvedSymbolsCache::TryGetFrameInfoForFunctionId(FunctionID clrFunctionId, const StackFrameInfo** ppFrameInfo)
{
    const std::lock_guard<std::mutex> lock(_lockStackFrameInfosByFunctionId);

    if (TryGetFromMap(_pStackFrameInfosByFunctionId, clrFunctionId, ppFrameInfo))
    {
        const_cast<StackFrameInfo*>(*ppFrameInfo)->AddRef();
        return true;
    }

    return false;
}

bool ResolvedSymbolsCache::AddFrameInfoForFunctionId(FunctionID clrFunctionId, const StackFrameInfo* pFrameInfo)
{
    const std::lock_guard<std::mutex> lock(_lockStackFrameInfosByFunctionId);

    if (TryInsertIntoMap(_pStackFrameInfosByFunctionId, clrFunctionId, pFrameInfo))
    {
        const_cast<StackFrameInfo*>(pFrameInfo)->AddRef();
        return true;
    }
    else
    {
        return false;
    }
}

bool ResolvedSymbolsCache::TryGetTypeInfoForClassId(ClassID clrClassId, const ManagedTypeInfo** ppTypeInfo)
{
    const std::lock_guard<std::mutex> lock(_lockTypeInfosByClassId);

    if (TryGetFromMap(_pTypeInfosByClassId, clrClassId, ppTypeInfo))
    {

        const_cast<ManagedTypeInfo*>(*ppTypeInfo)->AddRef();
        return true;
    }

    return false;
}

bool ResolvedSymbolsCache::AddTypeInfoForClassId(ClassID clrClassId, const ManagedTypeInfo* pTypeInfo)
{
    const std::lock_guard<std::mutex> lock(_lockTypeInfosByClassId);

    if (TryInsertIntoMap(_pTypeInfosByClassId, clrClassId, pTypeInfo))
    {
        const_cast<ManagedTypeInfo*>(pTypeInfo)->AddRef();
        return true;
    }
    else
    {
        return false;
    }
}

bool ResolvedSymbolsCache::TryGetTypeInfoForTypeDefToken(mdTypeDef mdTypeDefToken, ModuleID clrModuleId, const ManagedTypeInfo** ppTypeInfo)
{
    const std::lock_guard<std::mutex> lock(_lockTypeInfosByTypeDefAndModuleId);

    TypeDefTokenAndModuleId key(mdTypeDefToken, clrModuleId);
    if (TryGetFromMap(_pTypeInfosByTypeDefAndModuleId, key, ppTypeInfo))
    {

        const_cast<ManagedTypeInfo*>(*ppTypeInfo)->AddRef();
        return true;
    }

    return false;
}

bool ResolvedSymbolsCache::AddTypeInfoForForTypeDefToken(mdTypeDef mdTypeDefToken, ModuleID clrModuleId, const ManagedTypeInfo* pTypeInfo)
{
    const std::lock_guard<std::mutex> lock(_lockTypeInfosByTypeDefAndModuleId);

    TypeDefTokenAndModuleId key(mdTypeDefToken, clrModuleId);
    if (TryInsertIntoMap(_pTypeInfosByTypeDefAndModuleId, key, pTypeInfo))
    {
        const_cast<ManagedTypeInfo*>(pTypeInfo)->AddRef();
        return true;
    }
    else
    {
        return false;
    }
}

const StackFrameInfo* ResolvedSymbolsCache::GetNoInfoFrameForCodeKind(StackFrameCodeKind codeKind)
{
    const std::lock_guard<std::mutex> lock(_lockNoInfoStackFrameInfosByCodeKind);

    const StackFrameInfo* pExistingStackFrameInfo;

    if (TryGetFromMap(_pNoInfoStackFrameInfosByCodeKind, codeKind, &pExistingStackFrameInfo))
    {
        // // The caller must call Release() it when done (COM style).
        return const_cast<StackFrameInfo*>(pExistingStackFrameInfo)->AddRef<const StackFrameInfo>();
    }

    const StackFrameInfo* pNewStackFrameInfo = new StackFrameInfo(codeKind);
    if (TryInsertIntoMap(_pNoInfoStackFrameInfosByCodeKind,
                         codeKind,
                         pNewStackFrameInfo,
                         &pExistingStackFrameInfo))
    {
        // TryInsertIntoMap() returned true, so pNewStackFrameInfo and pExistingStackFrameInfo point to the same
        // object and that's the object we just inserted. This AddRef is for storing the item in the cache:
        const_cast<StackFrameInfo*>(pNewStackFrameInfo)->AddRef();
    }

    // If TryInsertIntoMap() returned false, we lost the race and someone else inserted first.
    // This should actually never happen since we do this under lock.
    // Either way, we do not need an AddRef for inserting into the cache.

    // Regardless of what TryInsertIntoMap() returned, we need an AddRef is for the "retrieved" instance.
    // The caller must release it when done (COM style).
    return const_cast<StackFrameInfo*>(pExistingStackFrameInfo)->AddRef<const StackFrameInfo>();
}
