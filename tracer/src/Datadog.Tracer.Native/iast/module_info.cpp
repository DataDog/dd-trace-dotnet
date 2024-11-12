#include "module_info.h"
#include "../cor_profiler.h"
#include "aspect.h"
#include "dataflow.h"
#include "iast_util.h"
#include "../dd_profiler_constants.h"
#include "dataflow_il_analysis.h"
#include "dataflow_il_rewriter.h"
#include "method_info.h"
#include "signature_info.h"

namespace iast
{
ModuleInfo::ModuleInfo(Dataflow* pDataflow, AppDomainInfo* pAppDomain, ModuleID moduleId, const WSTRING& path,
                       AssemblyID assemblyId, const WSTRING& name, bool isGeneric)
{
    this->_dataflow = pDataflow;
    this->_appDomain = *pAppDomain;
    this->_id = moduleId;
    this->_path = path;
    this->_assemblyId = assemblyId;
    this->_name = name;

    HRESULT hr = _dataflow->GetModuleInterfaces(_id, &_metadataImport, &_metadataEmit, &_assemblyImport, &_assemblyEmit);

    if (_appDomain.IsExcluded)
    {
        this->_isExcluded = true;
    }
    else
    {
        MatchResult includedMatch, excludedMatch;
        this->_isExcluded = _dataflow->IsAssemblyExcluded(name, &includedMatch, &excludedMatch);
    }
}
ModuleInfo::~ModuleInfo()
{
    DEL_MAP_VALUES(_types);
    DEL_MAP_VALUES(_members);
    DEL_MAP_VALUES(_specs);
    DEL_MAP_VALUES(_methods);
    DEL_MAP_VALUES(_fields);
    DEL_MAP_VALUES(_properties);
    DEL_MAP_VALUES(_signatures);
}

Dataflow* ModuleInfo::GetDataflow()
{
    return _dataflow;
}
IMetaDataImport2* ModuleInfo::GetMetaDataImport()
{
    return _metadataImport;
}

bool ModuleInfo::IsValid()
{
    return _appDomain.IsValid() && _id != 0 && _metadataImport != nullptr;
}

bool ModuleInfo::IsExcluded()
{
    return _isExcluded;
}

bool ModuleInfo::IsCoreLib()
{
    return false;
}

WSTRING ModuleInfo::GetModuleFullName()
{
    std::stringstream res;
    res << "(" << Hex((ULONG)_id) << ") " << shared::ToString(_name) << " [" << shared::ToString(_appDomain.Name) << "]  on "
        << shared::ToString(_path);
    return ToWSTRING(res.str());
}

bool ModuleInfo::IsInlineEnabled()
{
    return true;
}

bool ModuleInfo::IsNestedType(DWORD typeDefFlags)
{
    return typeDefFlags & tdNestedPublic || typeDefFlags & tdNestedAssembly || typeDefFlags & tdNestedFamANDAssem ||
           typeDefFlags & tdNestedFamORAssem || typeDefFlags & tdNestedFamily || typeDefFlags & tdNestedPrivate;
}

std::vector<mdMethodDef> ModuleInfo::GetTypeMethodDefs(mdTypeDef typeDef)
{
    HCORENUM hCorEnum = nullptr;
    std::vector<mdMethodDef> methods;
    mdMethodDef enumeratedMethods[64];
    ULONG enumCount;

    while (this->_metadataImport->EnumMethods(&hCorEnum, typeDef, enumeratedMethods,
                                             sizeof(enumeratedMethods) / sizeof(enumeratedMethods[0]),
                                             &enumCount) == S_OK)
    {
        methods.insert(methods.end(), std::begin(enumeratedMethods), std::begin(enumeratedMethods) + enumCount);
    }

    this->_metadataImport->CloseEnum(hCorEnum);
    return methods;
}
HRESULT ModuleInfo::GetTypeDef(const WSTRING& typeName, mdTypeDef* pTypeDef)
{
    HRESULT hr = S_OK;
    auto parts = Split(typeName, WStr("+"));
    if (parts.size() > 1)
    {
        mdTypeDef parentTypeDef;
        mdTypeDef typeDef = 0;
        for (int x = 0; x < parts.size(); x++)
        {
            hr = _metadataImport->FindTypeDefByName(parts[x].c_str(), typeDef, &parentTypeDef);
            if (SUCCEEDED(hr))
            {
                typeDef = parentTypeDef;
            }
        }
        *pTypeDef = typeDef;
    }
    else
    {
        hr = _metadataImport->FindTypeDefByName(typeName.c_str(), 0, pTypeDef);
    }
    return hr;
}
HRESULT ModuleInfo::GetMethodDef(mdTypeDef typeDef, const WSTRING& methodName, PCCOR_SIGNATURE pSignature,
                                 ULONG nSignature, mdMethodDef* pMethodDef)
{
    auto methodInfo = GetMethod(typeDef, methodName, pSignature, nSignature);
    if (methodInfo != nullptr)
    {
        *pMethodDef = methodInfo->_id;
    }
    return methodInfo != nullptr ? S_OK : E_INVALIDARG;
}

TypeInfo* ModuleInfo::GetTypeInfo(mdTypeDef typeDef)
{
    CSGUARD(_cs);
    return Get<mdTypeDef, TypeInfo>(_types, typeDef, [this, typeDef]() { return new iast::TypeInfo(this, typeDef); });
}

MemberRefInfo* ModuleInfo::GetMemberRefInfo(mdMemberRef token)
{
    auto typeFromToken = TypeFromToken(token);
    if (typeFromToken == mdtMemberRef)
    {
        CSGUARD(_cs);
        return Get<mdMemberRef, MemberRefInfo>(_members, token, [this, token]() { return new iast::MemberRefInfo(this, token); });
    }
    else if (typeFromToken == mdtMethodDef)
    {
        return GetMethodInfo(token);
    }
    else if (typeFromToken == mdtFieldDef)
    {
        return GetFieldInfo(token);
    }
    else if (typeFromToken == mdtProperty)
    {
        return GetPropertyInfo(token);
    }
    else if (typeFromToken == mdtMethodSpec)
    {
        return GetMethodSpec(token);
    }
    return nullptr;
}

MethodInfo* ModuleInfo::GetMethodInfo(mdMethodDef methodDef)
{
    if (methodDef == 0x06000000)
    {
        return nullptr; // Wrong methodDef
    }
    CSGUARD(_cs);
    return Get<mdMethodDef, MethodInfo>(_methods, methodDef, [this, methodDef]() { return new iast::MethodInfo(this, methodDef); });
}

FieldInfo* ModuleInfo::GetFieldInfo(mdFieldDef fieldDef)
{
    CSGUARD(_cs);
    return Get<mdFieldDef, FieldInfo>(_fields, fieldDef, [this, fieldDef]() { return new iast::FieldInfo(this, fieldDef); });
}

PropertyInfo* ModuleInfo::GetPropertyInfo(mdProperty propId)
{
    CSGUARD(_cs);
    return Get<mdProperty, PropertyInfo>(_properties, propId, [this, propId]() { return new iast::PropertyInfo(this, propId); });
}

MethodSpec* ModuleInfo::GetMethodSpec(mdMethodSpec methodSpec)
{
    CSGUARD(_cs);
    return Get<mdMethodSpec, MethodSpec>(_specs, methodSpec, [this, methodSpec]() { return new iast::MethodSpec(this, methodSpec); });
}
SignatureInfo* ModuleInfo::GetSignature(mdSignature signatureToken)
{
    CSGUARD(_cs);
    auto it = _signatures.find(signatureToken);
    if (it != _signatures.end())
    {
        return it->second;
    }
    PCCOR_SIGNATURE pSig;
    ULONG nSig;
    if (SUCCEEDED(this->_metadataImport->GetSigFromToken(signatureToken, &pSig, &nSig)))
    {
        SignatureInfo* res = new SignatureInfo(this, pSig, nSig);
        _signatures[signatureToken] = res;
        return res;
    }
    return nullptr;
}

bool ModuleInfo::AreSameTypes(mdTypeRef typeRef1, mdTypeRef typeRef2)
{
    if (typeRef1 == typeRef2)
    {
        return true;
    }
    HRESULT hr = S_OK;
    WCHAR typeName1[1024];
    WCHAR typeName2[1024];
    hr = _metadataImport->GetTypeRefProps(typeRef1, nullptr, typeName1, 1024, nullptr);
    if (SUCCEEDED(hr))
    {
        hr = _metadataImport->GetTypeRefProps(typeRef2, nullptr, typeName2, 1024, nullptr);
    }
    if (SUCCEEDED(hr))
    {
        WSTRING t1(typeName1);
        WSTRING t2(typeName2);
        return t1 == t2;
    }
    return false;
}

HRESULT ModuleInfo::GetAssemblyTypeRef(const WSTRING& assemblyName, const WSTRING& typeName, mdTypeRef* typeRef)
{
    HRESULT hr = S_OK;

    mdAssemblyRef assemblyRef;
    IfFailRet(GetAssemblyRef(assemblyName, &assemblyRef));
    IfFailRet(GetTypeRef(assemblyRef, typeName, typeRef));
    return hr;
}

std::vector<MethodInfo*> ModuleInfo::GetMethods(mdTypeDef typeDef)
{
    HCORENUM hCorEnum = nullptr;
    std::vector<MethodInfo*> methods;
    mdMethodDef enumeratedMethods[128];
    ULONG enumCount;

    HRESULT hr = this->_metadataImport->EnumMethods(
        &hCorEnum, typeDef, enumeratedMethods, sizeof(enumeratedMethods) / sizeof(enumeratedMethods[0]), &enumCount);
    if (SUCCEEDED(hr) && enumCount > 0)
    {
        methods.reserve(enumCount);
        for (ULONG i = 0; i < enumCount; i++)
        {
            methods.push_back(GetMethodInfo(enumeratedMethods[i]));
        }
    }

    this->_metadataImport->CloseEnum(hCorEnum);
    return methods;
}

std::vector<PropertyInfo*> ModuleInfo::GetProperties(mdTypeDef typeDef)
{
    HCORENUM hCorEnum = nullptr;
    std::vector<PropertyInfo*> res;
    mdProperty elements[64];
    ULONG elementCount;

    HRESULT hr = this->_metadataImport->EnumProperties(&hCorEnum, typeDef, elements,
                                                            sizeof(elements) / sizeof(elements[0]),
                                                            &elementCount);
    if (SUCCEEDED(hr) && elementCount > 0)
    {
        res.reserve(elementCount);
        for (ULONG i = 0; i < elementCount; i++)
        {
            auto element = GetPropertyInfo(elements[i]);
            if (element)
            {
                res.push_back(element);
            }
        }
    }

    this->_metadataImport->CloseEnum(hCorEnum);
    return res;
}

std::vector<MethodInfo*> ModuleInfo::GetMethods(mdTypeDef typeDef, const WSTRING& name)
{
    HCORENUM hCorEnum = nullptr;
    std::vector<MethodInfo*> methods;
    mdMethodDef enumeratedMethods[64];
    ULONG enumCount;

    HRESULT hr =
        this->_metadataImport->EnumMethodsWithName(&hCorEnum, typeDef, name.c_str(), enumeratedMethods,
                                                  sizeof(enumeratedMethods) / sizeof(enumeratedMethods[0]), &enumCount);
    if (SUCCEEDED(hr) && enumCount > 0)
    {
        methods.reserve(enumCount);
        for (ULONG i = 0; i < enumCount; i++)
        {
            auto methodInfo = GetMethodInfo(enumeratedMethods[i]);
            if (methodInfo)
            {
                methods.push_back(methodInfo);
            }
        }
    }

    this->_metadataImport->CloseEnum(hCorEnum);
    return methods;
}
std::vector<MethodInfo*> ModuleInfo::GetMethods(mdTypeDef typeDef, const WSTRING& methodName, ULONG paramCount)
{
    std::vector<MethodInfo*> methods = this->GetMethods(typeDef, methodName);
    std::vector<MethodInfo*> res;
    std::copy_if(methods.begin(), methods.end(), std::back_inserter(res),
                 [=](MethodInfo* mi) { return mi->GetParameterCount() == paramCount; });
    return res;
}
std::vector<MethodInfo*> ModuleInfo::GetMethods(const WSTRING& typeName, const WSTRING& methodName)
{
    mdTypeDef typeDef;
    if (SUCCEEDED(GetTypeDef(typeName, &typeDef)))
    {
        return GetMethods(typeDef, methodName);
    }
    return std::vector<MethodInfo*>();
}

MethodInfo* ModuleInfo::GetMethod(const WSTRING& typeName, const WSTRING& methodName, PCCOR_SIGNATURE pSignature,
                                  ULONG nSignature)
{
    HRESULT hr = S_OK;
    mdTypeDef typeDef;
    if (FAILED(GetTypeDef(typeName, &typeDef)))
    {
        return nullptr;
    }
    return GetMethod(typeDef, methodName, pSignature, nSignature);
}
MethodInfo* ModuleInfo::GetMethod(mdTypeDef typeDef, const WSTRING& methodName, PCCOR_SIGNATURE pSignature,
                                  ULONG nSignature)
{
    HRESULT hr = S_OK;
    std::vector<MethodInfo*> methods = this->GetMethods(typeDef, methodName);
    std::vector<MethodInfo*>::const_iterator captureMethod =
        std::find_if(methods.begin(), methods.end(), [=](const MethodInfo* mi) {
            return memcmp(mi->_pSig, pSignature, MIN(mi->_nSig, nSignature)) == 0;
        });

    if (captureMethod == methods.end())
    {
        return nullptr;
    }
    return *captureMethod;
}
MethodInfo* ModuleInfo::GetMethod(const WSTRING& typeName, const WSTRING& methodName, int paramCount)
{
    mdTypeDef typeDef;
    if (FAILED(GetTypeDef(typeName, &typeDef)))
    {
        return nullptr;
    }
    return GetMethod(typeDef, methodName, paramCount);
}
MethodInfo* ModuleInfo::GetMethod(mdTypeDef typeDef, const WSTRING& methodName, int paramCount)
{
    std::vector<MethodInfo*> methods = this->GetMethods(typeDef, methodName, paramCount);
    if (methods.size() == 0)
    {
        return nullptr;
    }
    return methods[0];
}

MethodInfo* ModuleInfo::GetMethod(const WSTRING& typeName, const WSTRING& methodName, const WSTRING& methodParams)
{
    mdTypeDef typeDef;
    if (FAILED(GetTypeDef(typeName, &typeDef)))
    {
        return nullptr;
    }
    return GetMethod(typeDef, methodName, methodParams);
}
MethodInfo* ModuleInfo::GetMethod(mdTypeDef typeDef, const WSTRING& methodName, const WSTRING& methodParams)
{
    auto methods = GetMethods(typeDef, methodName);
    if (methods.size() == 0)
    {
        return nullptr;
    }
    if (methods.size() == 1)
    {
        return methods[0];
    }
    for (auto method : methods)
    {
        if (auto sig = method->GetSignature())
        {
            auto sigRepresentation = sig->GetParamsRepresentation();
            if (sigRepresentation == methodParams)
            {
                return method;
            }
        }
    }
    return nullptr;
}

WSTRING ModuleInfo::GetTypeName(mdTypeDef typeDef)
{
    HRESULT hr = S_OK;
    WSTRING res = EmptyWStr;
    WCHAR typeName[1024];
    DWORD typeDefFlags = 0;

    auto typeFromToken = TypeFromToken(typeDef);
    if (typeFromToken == mdtTypeDef)
    {
        _metadataImport->GetTypeDefProps(typeDef, typeName, 1024, nullptr, &typeDefFlags, nullptr);
        res = typeName;
    }
    else if (typeFromToken == mdtTypeRef)
    {
        mdToken typeScope;
        _metadataImport->GetTypeRefProps(typeDef, &typeScope, typeName, 1024, nullptr);
        res = typeName;
    }
    else if (typeFromToken == mdtTypeSpec)
    {
        PCCOR_SIGNATURE pSig;
        ULONG nSig;
        if (SUCCEEDED(_metadataImport->GetTypeSpecFromToken(typeDef, &pSig, &nSig)))
        {
            res = WStr("TypeSpec");
        }
    }

    while (IsNestedType(typeDefFlags))
    {
        mdTypeDef enclosingClassTypeDef;
        hr = _metadataImport->GetNestedClassProps(typeDef, &enclosingClassTypeDef);
        if (SUCCEEDED(hr))
        {
            hr = _metadataImport->GetTypeDefProps(enclosingClassTypeDef, typeName, 1024, nullptr, &typeDefFlags, nullptr);
        }
        if (FAILED(hr))
        {
            break;
        }
        std::stringstream tmp;
        tmp << shared::ToString(typeName) << "." << shared::ToString(res);
        res = ToWSTRING(tmp.str());
        typeDef = enclosingClassTypeDef;
    }
    return res;
}

WSTRING ModuleInfo::GetAssemblyName()
{
    WSTRING AssemblyName;
    // if (AssemblyName.empty())
    {
        HRESULT hr = E_FAIL;
        WCHAR assemblyNameBuffer[255];
        ULONG numChars = 0;
        DWORD attrFlags = 0;
        char* publicKeyToken = nullptr;
        char* hashVal = nullptr;
        ULONG pktLen = 0;
        ULONG hashLen = 0;
        DWORD flags = 0;
        ASSEMBLYMETADATA amd{0};
        mdAssembly mdAsemProp = mdAssemblyNil;
        _assemblyImport->GetAssemblyFromScope(&mdAsemProp);
        _assemblyImport->GetAssemblyProps(mdAsemProp, (const void**) &publicKeyToken, &pktLen, &hashLen,
                                         assemblyNameBuffer, 255, &numChars, &amd, &flags);
        AssemblyName.assign(assemblyNameBuffer);
    }

    return AssemblyName;
}

HRESULT ModuleInfo::GetAssemblyRef(const WSTRING& assemblyName, mdAssemblyRef* assemblyRef, bool create)
{
    if (assemblyName == _name)
    {
        *assemblyRef = -1; // Self assembly
        return S_FALSE;
    }
    return GetAssemblyRef(assemblyName, nullptr, nullptr, 0, assemblyRef, create);
}
HRESULT ModuleInfo::GetAssemblyRef(const WSTRING& assemblyName, const ASSEMBLYMETADATA* assemblyMetadata,
                                   const void* pPublicKeyToken, UINT nPublicKeyToken, mdAssemblyRef* assemblyRef,
                                   bool create)
{
    HRESULT hr = S_OK;
    HCORENUM hEnum = nullptr;
    mdAssemblyRef pAssemblyRefs[100];
    ULONG nAssemblyRefs;
    IfFailRet(_assemblyImport->EnumAssemblyRefs(&hEnum, pAssemblyRefs, 100, &nAssemblyRefs));
    _assemblyImport->CloseEnum(hEnum);

    for (ULONG i = 0; i < nAssemblyRefs; i++)
    {
        const void* pvPublicKeyOrToken;
        ULONG cbPublicKeyOrToken;
        WCHAR wszName[512];
        ULONG cchNameReturned;
        ASSEMBLYMETADATA asmMetaData;
        ZeroMemory(&asmMetaData, sizeof(asmMetaData));
        const void* pbHashValue;
        ULONG cbHashValue;
        DWORD asmRefFlags;

        IfFailRet(_assemblyImport->GetAssemblyRefProps(pAssemblyRefs[i], &pvPublicKeyOrToken, &cbPublicKeyOrToken,
                                                      wszName, 512, &cchNameReturned, &asmMetaData,
                                                      &pbHashValue, &cbHashValue, &asmRefFlags));
        if (EndsWith(wszName, assemblyName))
        {
            *assemblyRef = pAssemblyRefs[i];
            return S_OK;
        }
    }

    if (create)
    {
        // If not found, we define the reference
        hr = _assemblyEmit->DefineAssemblyRef(pPublicKeyToken, nPublicKeyToken, assemblyName.c_str(), assemblyMetadata,
                                             nullptr, 0, 0, assemblyRef);
    }
    return hr;
}
HRESULT ModuleInfo::GetSystemCoreAssemblyRef(mdAssemblyRef* assemblyRef)
{
    HRESULT hr = S_OK;
    ASSEMBLYMETADATA systemCoreMetadata;
    ZeroMemory(&systemCoreMetadata, sizeof(systemCoreMetadata));

    systemCoreMetadata.usMajorVersion = 4;
    systemCoreMetadata.usMinorVersion = 0;
    return GetAssemblyRef(Constants::SystemCore, &systemCoreMetadata, Constants::MscorlibAssemblyPublicKeyToken,
                          sizeof(Constants::MscorlibAssemblyPublicKeyToken), assemblyRef);
}
HRESULT ModuleInfo::GetMscorlibAssemblyRef(mdAssemblyRef* assemblyRef)
{
    return GetAssemblyRef(Constants::mscorlib, assemblyRef);
}

HRESULT ModuleInfo::GetTypeRef(mdToken tkResolutionScope, const WSTRING& name, mdTypeRef* typeRef, bool create)
{
    HRESULT hr = S_OK;
    if (tkResolutionScope == -1) // Self assembly
    {
        return GetTypeDef(name, typeRef);
    }
    hr = _metadataImport->FindTypeRef(tkResolutionScope, name.c_str(), typeRef);
    if (FAILED(hr) && create)
    {
        hr = _metadataEmit->DefineTypeRefByName(tkResolutionScope, name.c_str(), typeRef);
    }
    return hr;
}
HRESULT ModuleInfo::GetMemberRefInfo(mdTypeRef typeRef, const WSTRING& memberName, PCCOR_SIGNATURE pSignature,
                                 ULONG nSignature, mdMemberRef* memberRef, bool create)
{
    HRESULT hr = S_OK;
    if (TypeFromToken(typeRef) == mdtTypeDef)
    {
        return GetMethodDef(typeRef, memberName, pSignature, nSignature, memberRef);
    }
    hr = _metadataImport->FindMemberRef(typeRef, memberName.c_str(), pSignature, nSignature, memberRef);
    if (FAILED(hr) && create)
    {
        hr = _metadataEmit->DefineMemberRef(typeRef, memberName.c_str(), pSignature, nSignature, memberRef);
    }
    return hr;
}

HRESULT ModuleInfo::FindTypeRefByName(const WSTRING& name, mdTypeRef* typeRef, mdToken* tkResolutionScope)
{
    HRESULT hr = E_INVALIDARG;
    HCORENUM hCorEnum = nullptr;
    mdTypeRef typeRefs[256];
    ULONG enumCount;
    *typeRef = 0;
    if (tkResolutionScope)
    {
        *tkResolutionScope = 0;
    }

    while (this->_metadataImport->EnumTypeRefs(&hCorEnum, typeRefs, 256, &enumCount) == S_OK)
    {
        for (ULONG i = 0; i < enumCount; i++)
        {
            WCHAR typeName[1024];
            ULONG typeNameLength = 1024;
            mdToken typeScope;

            if (this->_metadataImport->GetTypeRefProps(typeRefs[i], &typeScope, typeName, typeNameLength,
                                                      &typeNameLength) == S_OK)
            {
                WSTRING t1(typeName);
                if (name == t1)
                {
                    *typeRef = typeRefs[i];
                    if (tkResolutionScope)
                    {
                        *tkResolutionScope = typeScope;
                    }
                    hr = S_OK;
                    break;
                }
            }
            else
            {
                trace::Logger::Error("ERROR: Failed to parse name from typeRef ", typeRefs[i]);
            }
        }
        if (hr == S_OK)
        {
            break;
        }
    }
    this->_metadataImport->CloseEnum(hCorEnum);
    return hr;
}
HRESULT ModuleInfo::FindMemberRefsByName(mdTypeRef typeRef, const WSTRING& memberName,
                                         std::vector<mdMemberRef>& memberRefs)
{
    HRESULT hr = E_INVALIDARG;
    HCORENUM hCorEnum = nullptr;
    mdTypeRef refs[256];
    ULONG enumCount;
    memberRefs.clear();
    while (this->_metadataImport->EnumMemberRefs(&hCorEnum, typeRef, refs, 256 / sizeof(refs[0]),
                                                &enumCount) == S_OK)
    {
        for (ULONG i = 0; i < enumCount; i++)
        {
            WCHAR name[1024];
            ULONG nameLength = 1024;
            mdToken typeScope;

            if (this->_metadataImport->GetMemberRefProps(refs[i], &typeScope, name, nameLength, &nameLength, nullptr,
                                                         nullptr) == S_OK)
            {
                WSTRING n(name);
                if (n == memberName)
                {
                    memberRefs.push_back(refs[i]);
                    hr = S_OK;
                }
            }
            else
            {
                trace::Logger::Error("ERROR: Failed to parse name from typeRef ", memberRefs[i]);
            }
        }
    }
    this->_metadataImport->CloseEnum(hCorEnum);
    return hr;
}

mdString ModuleInfo::DefineUserString(const WSTRING& string)
{
    mdString res = 0;
    _metadataEmit->DefineUserString(string.c_str(), (ULONG)string.length(), &res);
    return res;
}
WSTRING ModuleInfo::GetUserString(mdString token)
{
    if (TypeFromToken(token) != mdtString)
    {
        return EmptyWStr;
    }
    WSTRING res = EmptyWStr;
    ULONG txtLength = 0;
    if (SUCCEEDED(_metadataImport->GetUserString(token, nullptr, 0, &txtLength)) && txtLength > 0)
    {
        WCHAR* txt = new WCHAR[txtLength + 1];
        if (SUCCEEDED(_metadataImport->GetUserString(token, txt, txtLength, &txtLength)))
        {
            txt[txtLength] = 0;
            res = txt;
        }
        DEL_ARR(txt);
    }
    return res;
}

HRESULT ModuleInfo::GetILRewriter(MethodInfo* methodInfo, ILRewriter** rewriter)
{
    return methodInfo->GetILRewriter(rewriter);
}
HRESULT ModuleInfo::GetILRewriter(const WSTRING& typeName, const WSTRING& methodName, int requiredParamCount,
                                  ILRewriter** rewriter)
{
    HRESULT hr = S_OK;
    mdTypeDef typeDef;
    hr = GetTypeDef(typeName, &typeDef);
    if (FAILED(hr))
    {
        trace::Logger::Error("ModuleInfo::GetILRewriter -> Type ", typeName, " not found");
        return hr;
    }

    std::vector<MethodInfo*> methods = this->GetMethods(typeDef, methodName, requiredParamCount);
    if (methods.size() == 0)
    {
        trace::Logger::Info("ModuleInfo::GetILRewriter -> Method ", methodName, " not found on Type ", typeName);
        (*rewriter) = nullptr;
        return E_INVALIDARG;
    }
    auto methodInfo = *methods.begin();
    return GetILRewriter(methodInfo, rewriter);
}
HRESULT ModuleInfo::GetILRewriter(const WSTRING& typeName, const WSTRING& methodName, PCCOR_SIGNATURE pSignature,
                                  ULONG nSignature, ILRewriter** rewriter, MethodInfo** pMethodInfo)
{
    HRESULT hr = S_OK;
    mdTypeDef typeDef;
    IfFailRet(GetTypeDef(typeName, &typeDef));

    MethodInfo* methodInfo = GetMethod(typeDef, methodName, pSignature, nSignature);
    if (pMethodInfo)
    {
        *pMethodInfo = methodInfo;
    }
    if (methodInfo == nullptr)
    {
        *rewriter = nullptr;
        return E_INVALIDARG;
    }
    return GetILRewriter(methodInfo, rewriter);
}
HRESULT ModuleInfo::CommitILRewriter(ILRewriter** rewriter)
{
    HRESULT hr = S_FALSE;
    if (*rewriter != nullptr)
    {
        auto method = (*rewriter)->GetMethodInfo();
        if (method != nullptr)
        {
            hr = method->CommitILRewriter();
        }
    }
    *rewriter = nullptr;
    return hr;
}


ModuleInfo* ModuleInfo::GetModuleInfoByName(WSTRING moduleName)
{
    auto res = _dataflow->GetModuleInfo(moduleName, _appDomain.Id);
    if (res)
    {
        return res;
    }
    if (moduleName == managed_profiler_name)
    {
        // In .NetFramework, "Datadog.trace" might be in the shared assembly repository
        res = _dataflow->GetModuleInfo(moduleName, _appDomain.Id, true);

        if (res) 
        {
            return res;
        }            
    }
    
    trace::Logger::Info("Module ", moduleName, " NOT FOUND for AppDomain ", _appDomain.Name, " using fallback...");
    return res;
}

mdToken ModuleInfo::DefineMemberRef(const WSTRING& moduleName, const WSTRING& typeName, const WSTRING& methodName, const WSTRING& methodParams)
{
    HRESULT hr = E_INVALIDARG;
    mdMemberRef methodRef = 0;
    AssemblyImportInfo assemblyImport{0};
    ModuleInfo* moduleInfo = nullptr;
    std::stringstream memberKeyBuilder;
    memberKeyBuilder << shared::ToString(moduleName) << "::" << shared::ToString(typeName) << "."
                     << shared::ToString(methodName) << shared::ToString(methodParams);
    WSTRING memberKey = ToWSTRING(memberKeyBuilder.str());

    auto memberValue = _mMemberImports.find(memberKey);
    if (memberValue != _mMemberImports.end())
    {
        return memberValue->second;
    }

    auto value = _mAssemblyImports.find(moduleName);
    if (value != _mAssemblyImports.end())
    {
        assemblyImport = value->second;
        moduleInfo = _dataflow->GetModuleInfo(assemblyImport.moduleId);
        if (!moduleInfo)
        {
            trace::Logger::Info("DefineMemberRef : FAILED GetModuleInfo ", Hex((ULONG)assemblyImport.moduleId));
        }
    }
    if (FAILED(hr))
    {
        moduleInfo = GetModuleInfoByName(moduleName);
        if (!moduleInfo)
        {
            trace::Logger::Info("Module ", moduleName, " NOT FOUND for ", GetModuleFullName());
            return 0;
        }
        mdAssemblyRef assemblyRef;
        hr = _assemblyEmit->DefineAssemblyRef((void*) Constants::DDAssemblyPublicKeyToken,
                                             sizeof(Constants::DDAssemblyPublicKeyToken), moduleName.c_str(),
                                             GetDatadogAssemblyMetadata(),
                                             nullptr, // hash blob
                                             0,    // cb of hash blob
                                             0,    // flags
                                             &assemblyRef);
        if (FAILED(hr) || !moduleInfo)
        {
            trace::Logger::Info("DefineMemberRef : DefineAssemblyRef FAILED ", Hex(hr));
        }

        assemblyImport.moduleId = moduleInfo->_id;
        assemblyImport.assemblyRef = assemblyRef;
        _mAssemblyImports[moduleName] = assemblyImport;
    }

    auto methodInfo = moduleInfo->GetMethod(typeName, methodName, methodParams);
    if (methodInfo == nullptr)
    {
        trace::Logger::Debug("DefineMemberRef : Could not find Method ", shared::ToString(typeName), ".", shared::ToString(methodName), shared::ToString(methodParams));
        return 0;
    }

    mdTypeRef typeRef;
    if (SUCCEEDED(hr))
    {
        hr = _metadataEmit->DefineTypeRefByName(assemblyImport.assemblyRef, typeName.c_str(), &typeRef);
    }
    if (SUCCEEDED(hr))
    {
        hr = _metadataEmit->DefineImportMember(moduleInfo->_assemblyImport, nullptr, 0, moduleInfo->_metadataImport,
                                               methodInfo->_id, _assemblyEmit, typeRef, &methodRef);
    }

    if (SUCCEEDED(hr))
    {
        _mMemberImports[memberKey] = methodRef;
        auto memberRefInfo = GetMemberRefInfo(methodRef);
        trace::Logger::Debug("DefineMemberRef : ", Hex(methodRef), " for ", memberKey,
                             " MethodName: ", memberRefInfo->GetFullName(), " Module: ", GetModuleFullName());
        return methodRef;
    }
    else
    {
        trace::Logger::Warn("DefineImportMember failed with code ", hr , " typeName:" , typeName ,
                            " methodName: " , methodName);
        return 0;
    }
}

mdMethodSpec ModuleInfo::DefineMethodSpec(mdMemberRef targetMethod, SignatureInfo* sig)
{
    mdMethodSpec methodSpec = 0; 
    HRESULT hr = _metadataEmit->DefineMethodSpec(targetMethod, sig->_pSig, sig->_nSig, &methodSpec);
    if (FAILED(hr))
    {
        trace::Logger::Warn("DefineMethodSpec failed with code ", hr);
        methodSpec = 0;
    }

    return methodSpec;
}

std::vector<WSTRING> ModuleInfo::GetCustomAttributes(mdToken token)
{
    std::vector<WSTRING> res;

    HCORENUM hCorEnum = nullptr;
    mdCustomAttribute enumeratedElements[64];
    ULONG enumCount;

    while (_metadataImport->EnumCustomAttributes(&hCorEnum, token, mdTokenNil, enumeratedElements,
                                                sizeof(enumeratedElements) / sizeof(enumeratedElements[0]),
                                                &enumCount) == S_OK)
    {
        mdToken attributeParentToken = mdTokenNil;
        mdToken attributeCtorToken = mdTokenNil;
        const void* attribute_data = nullptr; // Pointer to receive attribute data, which is not needed for our purposes
        DWORD data_size = 0;

        for (ULONG i = 0; i < enumCount; i++)
        {
            HRESULT hr = _metadataImport->GetCustomAttributeProps(enumeratedElements[i], &attributeParentToken,
                                                                 &attributeCtorToken, &attribute_data, &data_size);
            if (SUCCEEDED(hr))
            {
                auto attrCtor = GetMemberRefInfo(attributeCtorToken);
                res.push_back(attrCtor->GetTypeName());
            }
        }
    }

    _metadataImport->CloseEnum(hCorEnum);
    return res;
}


} // namespace iast
