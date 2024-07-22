#define _SILENCE_CXX17_CODECVT_HEADER_DEPRECATION_WARNING
#include "clr_helpers.h"

#include <cstring>

#include "dd_profiler_constants.h"
#include "environment_variables.h"
#include "environment_variables_util.h"
#include "logger.h"
#include "macros.h"
#include <set>
#include <stack>

#include "../../../shared/src/native-src/pal.h"

#include <codecvt>

namespace trace
{

RuntimeInformation GetRuntimeInformation(ICorProfilerInfo4* info)
{
    COR_PRF_RUNTIME_TYPE runtime_type;
    USHORT major_version;
    USHORT minor_version;
    USHORT build_version;
    USHORT qfe_version;

    auto hr = info->GetRuntimeInformation(nullptr, &runtime_type, &major_version, &minor_version, &build_version,
                                          &qfe_version, 0, nullptr, nullptr);
    if (FAILED(hr))
    {
        return {};
    }

    return {runtime_type, major_version, minor_version, build_version, qfe_version};
}

AssemblyInfo GetAssemblyInfo(ICorProfilerInfo4* info, const AssemblyID& assembly_id)
{
    WCHAR assembly_name[kNameMaxSize];
    DWORD assembly_name_len = 0;
    AppDomainID app_domain_id;
    ModuleID manifest_module_id;

    auto hr = info->GetAssemblyInfo(assembly_id, kNameMaxSize, &assembly_name_len, assembly_name, &app_domain_id,
                                    &manifest_module_id);

    if (FAILED(hr) || assembly_name_len == 0)
    {
        Logger::Warn("Error loading the assembly info: ", assembly_id, " [", "AssemblyLength=", assembly_name_len,
                     ", HRESULT=0x", std::setfill('0'), std::setw(8), std::hex, hr, "]");
        return {};
    }

    WCHAR app_domain_name[kNameMaxSize];
    DWORD app_domain_name_len = 0;

    hr = info->GetAppDomainInfo(app_domain_id, kNameMaxSize, &app_domain_name_len, app_domain_name, nullptr);

    if (FAILED(hr) || app_domain_name_len == 0)
    {
        Logger::Warn("Error loading the appdomain for assembly: ", assembly_id,
                     " [AssemblyName=", shared::WSTRING(assembly_name), ", AssemblyLength=", assembly_name_len, ", HRESULT=0x",
                     std::setfill('0'), std::setw(8), std::hex, hr, ", AppDomainId=", app_domain_id, "]");
        return {};
    }

    return {assembly_id, shared::WSTRING(assembly_name), manifest_module_id, app_domain_id, shared::WSTRING(app_domain_name)};
}

AssemblyMetadata GetAssemblyImportMetadata(const ComPtr<IMetaDataAssemblyImport>& assembly_import)
{
    mdAssembly current = mdAssemblyNil;
    auto hr = assembly_import->GetAssemblyFromScope(&current);
    if (FAILED(hr))
    {
        return {};
    }
    WCHAR name[kNameMaxSize];
    DWORD name_len = 0;
    ASSEMBLYMETADATA assembly_metadata{};
    DWORD assembly_flags = 0;
    const ModuleID placeholder_module_id = 0;

    hr = assembly_import->GetAssemblyProps(current, nullptr, nullptr, nullptr, name, kNameMaxSize, &name_len,
                                           &assembly_metadata, &assembly_flags);
    if (FAILED(hr) || name_len == 0)
    {
        return {};
    }
    return AssemblyMetadata(placeholder_module_id, name, current, assembly_metadata.usMajorVersion,
                            assembly_metadata.usMinorVersion, assembly_metadata.usBuildNumber,
                            assembly_metadata.usRevisionNumber);
}

AssemblyMetadata GetReferencedAssemblyMetadata(const ComPtr<IMetaDataAssemblyImport>& assembly_import,
                                               const mdAssemblyRef& assembly_ref)
{
    WCHAR name[kNameMaxSize];
    DWORD name_len = 0;
    ASSEMBLYMETADATA assembly_metadata{};
    DWORD assembly_flags = 0;
    const ModuleID module_id_placeholder = 0;
    const auto hr = assembly_import->GetAssemblyRefProps(assembly_ref, nullptr, nullptr, name, kNameMaxSize, &name_len,
                                                         &assembly_metadata, nullptr, nullptr, &assembly_flags);
    if (FAILED(hr) || name_len == 0)
    {
        return {};
    }
    return AssemblyMetadata(module_id_placeholder, name, assembly_ref, assembly_metadata.usMajorVersion,
                            assembly_metadata.usMinorVersion, assembly_metadata.usBuildNumber,
                            assembly_metadata.usRevisionNumber);
}

std::vector<BYTE> GetSignatureByteRepresentation(ULONG signature_length, PCCOR_SIGNATURE raw_signature)
{
    std::vector<BYTE> signature_data(signature_length);
    for (ULONG i = 0; i < signature_length; i++)
    {
        signature_data[i] = raw_signature[i];
    }

    return signature_data;
}

FunctionInfo GetFunctionInfo(const ComPtr<IMetaDataImport2>& metadata_import, const mdToken& token)
{
    mdToken parent_token = mdTokenNil;
    mdToken method_spec_token = mdTokenNil;
    mdToken method_def_token = mdTokenNil;
    WCHAR function_name[kNameMaxSize]{};
    DWORD function_name_len = 0;

    PCCOR_SIGNATURE raw_signature;
    ULONG raw_signature_len;
    BOOL is_generic = false;
    std::vector<BYTE> final_signature_bytes;
    std::vector<BYTE> method_spec_signature;

    HRESULT hr = E_FAIL;
    const auto token_type = TypeFromToken(token);
    switch (token_type)
    {
        case mdtMemberRef:
            hr = metadata_import->GetMemberRefProps(token, &parent_token, function_name, kNameMaxSize,
                                                    &function_name_len, &raw_signature, &raw_signature_len);
            break;
        case mdtMethodDef:
            hr = metadata_import->GetMemberProps(token, &parent_token, function_name, kNameMaxSize, &function_name_len,
                                                 nullptr, &raw_signature, &raw_signature_len, nullptr, nullptr, nullptr,
                                                 nullptr, nullptr);
            break;
        case mdtMethodSpec:
        {
            hr = metadata_import->GetMethodSpecProps(token, &parent_token, &raw_signature, &raw_signature_len);
            is_generic = true;
            if (FAILED(hr))
            {
                return {};
            }
            const auto generic_info = GetFunctionInfo(metadata_import, parent_token);
            final_signature_bytes = generic_info.signature.data;
            method_spec_signature = GetSignatureByteRepresentation(raw_signature_len, raw_signature);
            std::memcpy(function_name, generic_info.name.c_str(), sizeof(WCHAR) * (generic_info.name.length() + 1));
            function_name_len = DWORD(generic_info.name.length() + 1);
            method_spec_token = token;
            method_def_token = generic_info.id;
        }
        break;
        default:
            Logger::Warn("[trace::GetFunctionInfo] unknown token type: {}", token_type);
            return {};
    }
    if (FAILED(hr) || function_name_len == 0)
    {
        return {};
    }

    // parent_token could be: TypeDef, TypeRef, TypeSpec, ModuleRef, MethodDef
    const auto type_info = GetTypeInfo(metadata_import, parent_token);

    if (is_generic)
    {
        // use the generic constructor and feed both method signatures
        return {method_spec_token,
                shared::WSTRING(function_name),
                type_info,
                MethodSignature(final_signature_bytes),
                MethodSignature(method_spec_signature),
                method_def_token,
                FunctionMethodSignature(raw_signature, raw_signature_len)};
    }

    final_signature_bytes = GetSignatureByteRepresentation(raw_signature_len, raw_signature);

    return {token, shared::WSTRING(function_name), type_info, MethodSignature(final_signature_bytes),
            FunctionMethodSignature(raw_signature, raw_signature_len)};
}

ModuleInfo GetModuleInfo(ICorProfilerInfo4* info, const ModuleID& module_id)
{
    const DWORD module_path_size = 260;
    WCHAR module_path[module_path_size]{};
    DWORD module_path_len = 0;
    LPCBYTE base_load_address;
    AssemblyID assembly_id = 0;
    DWORD module_flags = 0;
    const HRESULT hr = info->GetModuleInfo2(module_id, &base_load_address, module_path_size, &module_path_len,
                                            module_path, &assembly_id, &module_flags);
    if (FAILED(hr) || module_path_len == 0)
    {
        return {};
    }
    return {module_id, shared::WSTRING(module_path), GetAssemblyInfo(info, assembly_id), module_flags};
}

TypeInfo GetTypeInfo(const ComPtr<IMetaDataImport2>& metadata_import, const mdToken& token_)
{
    mdToken parent_token = mdTokenNil;
    std::shared_ptr<TypeInfo> parentTypeInfo = nullptr;
    mdToken parent_type_token = mdTokenNil;
    WCHAR type_name[kNameMaxSize]{};
    DWORD type_name_len = 0;
    DWORD type_flags;
    std::shared_ptr<TypeInfo> extendsInfo = nullptr;
    mdToken type_extends = mdTokenNil;
    bool type_valueType = false;
    bool type_isGeneric = false;
    bool type_isAbstract = false;
    bool type_isSealed = false;

    HRESULT hr = E_FAIL;

    auto token = token_;
    auto typeSpec = mdTypeSpecNil;
    std::set<mdToken> processed;

    while (token != mdTokenNil)
    {
        const auto token_type = TypeFromToken(token);
        switch (token_type)
        {
            case mdtTypeDef:
                hr = metadata_import->GetTypeDefProps(token, type_name, kNameMaxSize, &type_name_len, &type_flags,
                                                      &type_extends);

                metadata_import->GetNestedClassProps(token, &parent_type_token);
                if (parent_type_token != mdTokenNil)
                {
                    parentTypeInfo = std::make_shared<TypeInfo>(GetTypeInfo(metadata_import, parent_type_token));
                }

                if (type_extends != mdTokenNil)
                {
                    extendsInfo = std::make_shared<TypeInfo>(GetTypeInfo(metadata_import, type_extends));
                    type_valueType =
                        extendsInfo->name == WStr("System.ValueType") || extendsInfo->name == WStr("System.Enum");
                }

                type_isAbstract = IsTdAbstract(type_flags);
                type_isSealed = IsTdSealed(type_flags);

                break;
            case mdtTypeRef:
                hr = metadata_import->GetTypeRefProps(token, &parent_token, type_name, kNameMaxSize, &type_name_len);
                break;
            case mdtTypeSpec:
            {
                PCCOR_SIGNATURE signature{};
                ULONG signature_length{};

                hr = metadata_import->GetTypeSpecFromToken(token, &signature, &signature_length);

                if (FAILED(hr) || signature_length < 3)
                {
                    return {};
                }

                if (signature[0] & ELEMENT_TYPE_GENERICINST)
                {
                    if (std::find(processed.begin(), processed.end(), token) != processed.end())
                    {
                        return {}; // Break circular reference
                    }
                    processed.insert(token);

                    mdToken type_token;
                    CorSigUncompressToken(&signature[2], &type_token);
                    typeSpec = token;
                    token = type_token;
                    continue;
                }
            }
            break;
            case mdtModuleRef:
                metadata_import->GetModuleRefProps(token, type_name, kNameMaxSize, &type_name_len);
                break;
            case mdtMemberRef:
                return GetFunctionInfo(metadata_import, token).type;
                break;
            case mdtMethodDef:
                return GetFunctionInfo(metadata_import, token).type;
                break;
        }

        if (FAILED(hr) || type_name_len == 0)
        {
            return {};
        }

        const auto type_name_string = shared::WSTRING(type_name);
        const auto generic_token_index = type_name_string.rfind(WStr("`"));
        if (generic_token_index != std::string::npos)
        {
            const auto idxFromRight = type_name_string.length() - generic_token_index - 1;
            type_isGeneric = idxFromRight == 1 || idxFromRight == 2;
        }

        return {token,         type_name_string, typeSpec,       typeSpec != mdTypeSpecNil ? mdtTypeSpec : token_type,
                extendsInfo,   type_valueType,   type_isGeneric, type_isAbstract,
                type_isSealed, parentTypeInfo,   parent_token};
    }
    return {};
}

// Searches for an AssemblyRef whose name and version match exactly.
// The exact version match is critical when two Datadog.Trace.dll assemblies are loaded
// due to a version mismatch. For all of the CallTarget infrastructure, the tracer IL Rewriting
// will emit CallTarget types from the vPROFILER assembly. We must make sure that the integration type
// that is inserted into the CallTarge infrastructure also comes from the same assembly, otherwise
// we may accidentally load the application's version and this would result in no instrumentation
// because there would be a type mismatch between the CallTarget types (specifically CallTargetReturn/CallTargetState)
// from vAPPLICATION and vPROFILER
//
// This was the root cause of the following issue: https://github.com/DataDog/dd-trace-dotnet/pull/2621
mdAssemblyRef FindAssemblyRef(const ComPtr<IMetaDataAssemblyImport>& assembly_import, const shared::WSTRING& assembly_name, const Version& version)
{
    for (mdAssemblyRef assembly_ref : EnumAssemblyRefs(assembly_import))
    {
        auto assemblyMetadata = GetReferencedAssemblyMetadata(assembly_import, assembly_ref);
        if (assemblyMetadata.name == assembly_name && assemblyMetadata.version == version)
        {
            return assembly_ref;
        }
    }
    return mdAssemblyRefNil;
}

HRESULT GetCorLibAssemblyRef(const ComPtr<IMetaDataAssemblyEmit>& assembly_emit, AssemblyProperty& corAssemblyProperty,
                             mdAssemblyRef* corlib_ref)
{
    if (corAssemblyProperty.ppbPublicKey != nullptr)
    {
        // the corlib module is already loaded, use that information to create the assembly ref
        Logger::Debug("Using existing corlib reference: ", corAssemblyProperty.szName);
        return assembly_emit->DefineAssemblyRef(corAssemblyProperty.ppbPublicKey, corAssemblyProperty.pcbPublicKey,
                                                corAssemblyProperty.szName.c_str(), &corAssemblyProperty.pMetaData,
                                                nullptr, 0, corAssemblyProperty.assemblyFlags, corlib_ref);
    }
    else
    {
        // Define an AssemblyRef to mscorlib, needed to create TypeRefs later
        ASSEMBLYMETADATA metadata{};
        metadata.usMajorVersion = 4;
        metadata.usMinorVersion = 0;
        metadata.usBuildNumber = 0;
        metadata.usRevisionNumber = 0;
        BYTE public_key[] = {0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89};
        return assembly_emit->DefineAssemblyRef(public_key, sizeof(public_key), WStr("mscorlib"), &metadata, nullptr, 0,
                                                corAssemblyProperty.assemblyFlags, corlib_ref);
    }
}

// TypeSignature
std::tuple<unsigned, int> TypeSignature::GetElementTypeAndFlags() const
{
    int typeFlags = 0;
    unsigned elementType;

    PCCOR_SIGNATURE pbCur = &pbBase[offset];

    if (*pbCur == ELEMENT_TYPE_PTR)
    {
        pbCur++;
        typeFlags |= TypeFlagByRef;
    }

    if (*pbCur == ELEMENT_TYPE_PINNED)
    {
        pbCur++;
        typeFlags |= TypeFlagPinnedType;
    }

    if (*pbCur == ELEMENT_TYPE_VOID)
    {
        typeFlags |= TypeFlagVoid;
    }

    if (*pbCur == ELEMENT_TYPE_BYREF)
    {
        pbCur++;
        typeFlags |= TypeFlagByRef;
    }

    elementType = *pbCur;

    switch (*pbCur)
    {
        case ELEMENT_TYPE_BOOLEAN:
        case ELEMENT_TYPE_CHAR:
        case ELEMENT_TYPE_I1:
        case ELEMENT_TYPE_U1:
        case ELEMENT_TYPE_U2:
        case ELEMENT_TYPE_I2:
        case ELEMENT_TYPE_I4:
        case ELEMENT_TYPE_U4:
        case ELEMENT_TYPE_I8:
        case ELEMENT_TYPE_U8:
        case ELEMENT_TYPE_R4:
        case ELEMENT_TYPE_R8:
        case ELEMENT_TYPE_I:
        case ELEMENT_TYPE_U:
        case ELEMENT_TYPE_VALUETYPE:
        case ELEMENT_TYPE_MVAR:
        case ELEMENT_TYPE_VAR:
            typeFlags |= TypeFlagBoxedType;
            break;
        case ELEMENT_TYPE_GENERICINST:
            pbCur++;
            if (*pbCur == ELEMENT_TYPE_VALUETYPE)
            {
                typeFlags |= TypeFlagBoxedType;
            }
            break;
        default:
            break;
    }

    return {elementType, typeFlags};
}

mdToken TypeSignature::GetTypeTok(const ComPtr<IMetaDataEmit2>& pEmit, mdAssemblyRef corLibRef) const
{
    mdToken token = mdTokenNil;
    PCCOR_SIGNATURE pbCur = &pbBase[offset];
    const PCCOR_SIGNATURE pStart = pbCur;

    if (*pbCur == ELEMENT_TYPE_PTR || *pbCur == ELEMENT_TYPE_PINNED)
    {
        pbCur++;
    }

    if (*pbCur == ELEMENT_TYPE_BYREF)
    {
        pbCur++;
    }

    switch (*pbCur)
    {
        case ELEMENT_TYPE_BOOLEAN:
            pEmit->DefineTypeRefByName(corLibRef, SystemBoolean, &token);
            break;
        case ELEMENT_TYPE_CHAR:
            pEmit->DefineTypeRefByName(corLibRef, SystemChar, &token);
            break;
        case ELEMENT_TYPE_I1:
            pEmit->DefineTypeRefByName(corLibRef, SystemSByte, &token);
            break;
        case ELEMENT_TYPE_U1:
            pEmit->DefineTypeRefByName(corLibRef, SystemByte, &token);
            break;
        case ELEMENT_TYPE_U2:
            pEmit->DefineTypeRefByName(corLibRef, SystemUInt16, &token);
            break;
        case ELEMENT_TYPE_I2:
            pEmit->DefineTypeRefByName(corLibRef, SystemInt16, &token);
            break;
        case ELEMENT_TYPE_I4:
            pEmit->DefineTypeRefByName(corLibRef, SystemInt32, &token);
            break;
        case ELEMENT_TYPE_U4:
            pEmit->DefineTypeRefByName(corLibRef, SystemUInt32, &token);
            break;
        case ELEMENT_TYPE_I8:
            pEmit->DefineTypeRefByName(corLibRef, SystemInt64, &token);
            break;
        case ELEMENT_TYPE_U8:
            pEmit->DefineTypeRefByName(corLibRef, SystemUInt64, &token);
            break;
        case ELEMENT_TYPE_R4:
            pEmit->DefineTypeRefByName(corLibRef, SystemSingle, &token);
            break;
        case ELEMENT_TYPE_R8:
            pEmit->DefineTypeRefByName(corLibRef, SystemDouble, &token);
            break;
        case ELEMENT_TYPE_I:
            pEmit->DefineTypeRefByName(corLibRef, SystemIntPtr, &token);
            break;
        case ELEMENT_TYPE_U:
            pEmit->DefineTypeRefByName(corLibRef, SystemUIntPtr, &token);
            break;
        case ELEMENT_TYPE_STRING:
            pEmit->DefineTypeRefByName(corLibRef, SystemString, &token);
            break;
        case ELEMENT_TYPE_OBJECT:
            pEmit->DefineTypeRefByName(corLibRef, SystemObject, &token);
            break;
        case ELEMENT_TYPE_CLASS:
            pbCur++;
            token = CorSigUncompressToken(pbCur);
            break;
        case ELEMENT_TYPE_VALUETYPE:
            pbCur++;
            token = CorSigUncompressToken(pbCur);
            break;
        case ELEMENT_TYPE_GENERICINST:
        case ELEMENT_TYPE_SZARRAY:
        case ELEMENT_TYPE_MVAR:
        case ELEMENT_TYPE_VAR:
            pEmit->GetTokenFromTypeSpec(pbCur, length - static_cast<ULONG>(pbCur - pStart), &token);
            break;
        default:
            break;
    }
    return token;
}

shared::WSTRING GetSigTypeTokName(PCCOR_SIGNATURE& pbCur, const ComPtr<IMetaDataImport2>& pImport)
{
    shared::WSTRING tokenName = shared::EmptyWStr;
    bool ref_flag = false;
    if (*pbCur == ELEMENT_TYPE_BYREF)
    {
        pbCur++;
        ref_flag = true;
    }

    bool pointer_flag = false;
    if (*pbCur == ELEMENT_TYPE_PTR)
    {
        pbCur++;
        pointer_flag = true;
    }

    switch (*pbCur)
    {
        case ELEMENT_TYPE_BOOLEAN:
            tokenName = SystemBoolean;
            pbCur++;
            break;
        case ELEMENT_TYPE_CHAR:
            tokenName = SystemChar;
            pbCur++;
            break;
        case ELEMENT_TYPE_I1:
            tokenName = SystemSByte;
            pbCur++;
            break;
        case ELEMENT_TYPE_U1:
            tokenName = SystemByte;
            pbCur++;
            break;
        case ELEMENT_TYPE_U2:
            tokenName = SystemUInt16;
            pbCur++;
            break;
        case ELEMENT_TYPE_I2:
            tokenName = SystemInt16;
            pbCur++;
            break;
        case ELEMENT_TYPE_I4:
            tokenName = SystemInt32;
            pbCur++;
            break;
        case ELEMENT_TYPE_U4:
            tokenName = SystemUInt32;
            pbCur++;
            break;
        case ELEMENT_TYPE_I8:
            tokenName = SystemInt64;
            pbCur++;
            break;
        case ELEMENT_TYPE_U8:
            tokenName = SystemUInt64;
            pbCur++;
            break;
        case ELEMENT_TYPE_R4:
            tokenName = SystemSingle;
            pbCur++;
            break;
        case ELEMENT_TYPE_R8:
            tokenName = SystemDouble;
            pbCur++;
            break;
        case ELEMENT_TYPE_I:
            tokenName = SystemIntPtr;
            pbCur++;
            break;
        case ELEMENT_TYPE_U:
            tokenName = SystemUIntPtr;
            pbCur++;
            break;
        case ELEMENT_TYPE_STRING:
            tokenName = SystemString;
            pbCur++;
            break;
        case ELEMENT_TYPE_OBJECT:
            tokenName = SystemObject;
            pbCur++;
            break;
        case ELEMENT_TYPE_CLASS:
        case ELEMENT_TYPE_VALUETYPE:
        {
            pbCur++;
            mdToken token;
            pbCur += CorSigUncompressToken(pbCur, &token);
            tokenName = GetTypeInfo(pImport, token).name;
            break;
        }
        case ELEMENT_TYPE_SZARRAY:
        {
            pbCur++;
            tokenName = GetSigTypeTokName(pbCur, pImport) + WStr("[]");
            break;
        }
        case ELEMENT_TYPE_GENERICINST:
        {
            pbCur++;
            tokenName = GetSigTypeTokName(pbCur, pImport);
            tokenName += WStr("[");
            ULONG num = 0;
            pbCur += CorSigUncompressData(pbCur, &num);
            for (ULONG i = 0; i < num; i++)
            {
                tokenName += GetSigTypeTokName(pbCur, pImport);
                if (i != num - 1)
                {
                    tokenName += WStr(",");
                }
            }
            tokenName += WStr("]");
            break;
        }
        case ELEMENT_TYPE_MVAR:
        {
            pbCur++;
            ULONG num = 0;
            pbCur += CorSigUncompressData(pbCur, &num);
            tokenName = WStr("!!") + shared::ToWSTRING(std::to_string(num));
            break;
        }
        case ELEMENT_TYPE_VAR:
        {
            pbCur++;
            ULONG num = 0;
            pbCur += CorSigUncompressData(pbCur, &num);
            tokenName = WStr("!") + shared::ToWSTRING(std::to_string(num));
            break;
        }
        default:
            break;
    }

    if (ref_flag)
    {
        tokenName += WStr("&");
    }
    if (pointer_flag)
    {
        tokenName += WStr("*");
    }
    return tokenName;
}

shared::WSTRING TypeSignature::GetTypeTokName(ComPtr<IMetaDataImport2>& pImport) const
{
    PCCOR_SIGNATURE pbCur = &pbBase[offset];
    return GetSigTypeTokName(pbCur, pImport);
}

ULONG TypeSignature::GetSignature(PCCOR_SIGNATURE& data) const
{
    data = &pbBase[offset];
    return length;
}

// FunctionMethodSignature
bool ParseByte(PCCOR_SIGNATURE& pbCur, PCCOR_SIGNATURE pbEnd, unsigned char* pbOut)
{
    if (pbCur < pbEnd)
    {
        *pbOut = *pbCur;
        pbCur++;
        return true;
    }

    return false;
}

bool ParseNumber(PCCOR_SIGNATURE& pbCur, PCCOR_SIGNATURE pbEnd, unsigned* pOut)
{
    // parse the variable length number format (0-4 bytes)

    unsigned char b1 = 0, b2 = 0, b3 = 0, b4 = 0;

    // at least one byte in the encoding, read that

    if (!ParseByte(pbCur, pbEnd, &b1)) return false;

    if (b1 == 0xff)
    {
        // special encoding of 'NULL'
        // not sure what this means as a number, don't expect to see it except for
        // string lengths which we don't encounter anyway so calling it an error
        return false;
    }

    // early out on 1 byte encoding
    if ((b1 & 0x80) == 0)
    {
        *pOut = (int) b1;
        return true;
    }

    // now at least 2 bytes in the encoding, read 2nd byte
    if (!ParseByte(pbCur, pbEnd, &b2)) return false;

    // early out on 2 byte encoding
    if ((b1 & 0x40) == 0)
    {
        *pOut = (((b1 & 0x3f) << 8) | b2);
        return true;
    }

    // must be a 4 byte encoding
    if ((b1 & 0x20) != 0)
    {
        // 4 byte encoding has this bit clear -- error if not
        return false;
    }

    if (!ParseByte(pbCur, pbEnd, &b3)) return false;

    if (!ParseByte(pbCur, pbEnd, &b4)) return false;

    *pOut = ((b1 & 0x1f) << 24) | (b2 << 16) | (b3 << 8) | b4;
    return true;
}

bool ParseTypeDefOrRefEncoded(PCCOR_SIGNATURE& pbCur, PCCOR_SIGNATURE pbEnd, unsigned char* pIndexTypeOut,
                              unsigned* pIndexOut)
{
    // parse an encoded typedef or typeref
    unsigned encoded = 0;

    if (!ParseNumber(pbCur, pbEnd, &encoded)) return false;

    *pIndexTypeOut = (unsigned char) (encoded & 0x3);
    *pIndexOut = (encoded >> 2);
    return true;
}

/*  we don't support
    PTR CustomMod* VOID
    PTR CustomMod* Type
    FNPTR MethodDefSig
    FNPTR MethodRefSig
    ARRAY Type ArrayShape
    SZARRAY CustomMod+ Type (but we do support SZARRAY Type)
 */
bool ParseType(PCCOR_SIGNATURE& pbCur, PCCOR_SIGNATURE pbEnd)
{
    /*
    Type ::= ( BOOLEAN | CHAR | I1 | U1 | U2 | U2 | I4 | U4 | I8 | U8 | R4 | R8 |
    I | U | | VALUETYPE TypeDefOrRefEncoded | CLASS TypeDefOrRefEncoded | STRING
    | OBJECT
    | PTR CustomMod* VOID
    | PTR CustomMod* Type
    | FNPTR MethodDefSig
    | FNPTR MethodRefSig
    | ARRAY Type ArrayShape
    | SZARRAY CustomMod* Type
    | GENERICINST (CLASS | VALUETYPE) TypeDefOrRefEncoded GenArgCount Type *
    | VAR Number
    | MVAR Number
    */

    unsigned char elem_type;
    unsigned index;
    unsigned number;
    unsigned char indexType;

    if (!ParseByte(pbCur, pbEnd, &elem_type)) return false;

    switch (elem_type)
    {
        case ELEMENT_TYPE_BOOLEAN:
        case ELEMENT_TYPE_CHAR:
        case ELEMENT_TYPE_I1:
        case ELEMENT_TYPE_U1:
        case ELEMENT_TYPE_U2:
        case ELEMENT_TYPE_I2:
        case ELEMENT_TYPE_I4:
        case ELEMENT_TYPE_U4:
        case ELEMENT_TYPE_I8:
        case ELEMENT_TYPE_U8:
        case ELEMENT_TYPE_R4:
        case ELEMENT_TYPE_R8:
        case ELEMENT_TYPE_I:
        case ELEMENT_TYPE_U:
        case ELEMENT_TYPE_STRING:
        case ELEMENT_TYPE_OBJECT:
            // simple types
            break;

        case ELEMENT_TYPE_PTR:
            return false;

        case ELEMENT_TYPE_CLASS:
            // CLASS TypeDefOrRefEncoded
            if (!ParseTypeDefOrRefEncoded(pbCur, pbEnd, &indexType, &index)) return false;
            break;

        case ELEMENT_TYPE_VALUETYPE:
            // VALUETYPE TypeDefOrRefEncoded
            if (!ParseTypeDefOrRefEncoded(pbCur, pbEnd, &indexType, &index)) return false;

            break;

        case ELEMENT_TYPE_FNPTR:
            // FNPTR MethodDefSig
            // FNPTR MethodRefSig

            return false;

        case ELEMENT_TYPE_ARRAY:
            // ARRAY Type ArrayShape
            return false;

        case ELEMENT_TYPE_SZARRAY:
            // SZARRAY Type

            if (*pbCur == ELEMENT_TYPE_CMOD_OPT || *pbCur == ELEMENT_TYPE_CMOD_REQD) return false;

            if (!ParseType(pbCur, pbEnd)) return false;

            break;

        case ELEMENT_TYPE_GENERICINST:
            // GENERICINST (CLASS | VALUETYPE) TypeDefOrRefEncoded GenArgCount Type *
            if (!ParseByte(pbCur, pbEnd, &elem_type)) return false;

            if (elem_type != ELEMENT_TYPE_CLASS && elem_type != ELEMENT_TYPE_VALUETYPE) return false;

            if (!ParseTypeDefOrRefEncoded(pbCur, pbEnd, &indexType, &index)) return false;

            if (!ParseNumber(pbCur, pbEnd, &number)) return false;

            for (unsigned i = 0; i < number; i++)
            {
                if (!ParseType(pbCur, pbEnd)) return false;
            }
            break;

        case ELEMENT_TYPE_VAR:
            // VAR Number
            if (!ParseNumber(pbCur, pbEnd, &number)) return false;

            break;

        case ELEMENT_TYPE_MVAR:
            // MVAR Number
            if (!ParseNumber(pbCur, pbEnd, &number)) return false;

            break;
    }

    return true;
}

// Param ::= CustomMod* ( TYPEDBYREF | [BYREF] Type )
// CustomMod* TYPEDBYREF we don't support
bool ParseParamOrLocal(PCCOR_SIGNATURE& pbCur, PCCOR_SIGNATURE pbEnd)
{
    if (*pbCur == ELEMENT_TYPE_CMOD_OPT || *pbCur == ELEMENT_TYPE_CMOD_REQD)
    {
        return false;
    }

    if (pbCur >= pbEnd) return false;

    if (*pbCur == ELEMENT_TYPE_TYPEDBYREF) return false;

    if (*pbCur == ELEMENT_TYPE_BYREF) pbCur++;

    if (*pbCur == ELEMENT_TYPE_PTR) pbCur++;

    return ParseType(pbCur, pbEnd);
}

// RetType ::= CustomMod* ( VOID | TYPEDBYREF | [BYREF] Type )
// CustomMod* TYPEDBYREF we don't support
bool ParseRetType(PCCOR_SIGNATURE& pbCur, PCCOR_SIGNATURE pbEnd)
{

    if (*pbCur == ELEMENT_TYPE_CMOD_OPT || *pbCur == ELEMENT_TYPE_CMOD_REQD) return false;

    if (pbCur >= pbEnd) return false;

    if (*pbCur == ELEMENT_TYPE_TYPEDBYREF) return false;

    if (*pbCur == ELEMENT_TYPE_VOID)
    {
        pbCur++;
        return true;
    }

    if (*pbCur == ELEMENT_TYPE_BYREF) pbCur++;

    return ParseType(pbCur, pbEnd);
}

HRESULT FunctionMethodSignature::TryParse()
{
    PCCOR_SIGNATURE pbCur = pbBase;
    PCCOR_SIGNATURE pbEnd = pbBase + len;
    unsigned char elem_type;

    IfFalseRetFAIL(ParseByte(pbCur, pbEnd, &elem_type));

    if (elem_type & IMAGE_CEE_CS_CALLCONV_GENERIC)
    {
        unsigned gen_param_count;
        IfFalseRetFAIL(ParseNumber(pbCur, pbEnd, &gen_param_count));
        numberOfTypeArguments = gen_param_count;
    }

    unsigned param_count;
    IfFalseRetFAIL(ParseNumber(pbCur, pbEnd, &param_count));
    numberOfArguments = param_count;

    const PCCOR_SIGNATURE pbRet = pbCur;

    IfFalseRetFAIL(ParseRetType(pbCur, pbEnd));
    returnValue.pbBase = pbBase;
    returnValue.length = (ULONG) (pbCur - pbRet);
    returnValue.offset = (ULONG) (pbCur - pbBase - returnValue.length);

    auto fEncounteredSentinal = false;
    for (unsigned i = 0; i < param_count; i++)
    {
        if (pbCur >= pbEnd) return E_FAIL;

        if (*pbCur == ELEMENT_TYPE_SENTINEL)
        {
            if (fEncounteredSentinal) return E_FAIL;

            fEncounteredSentinal = true;
            pbCur++;
        }

        const PCCOR_SIGNATURE pbParam = pbCur;

        IfFalseRetFAIL(ParseParamOrLocal(pbCur, pbEnd));

        TypeSignature argument{};
        argument.pbBase = pbBase;
        argument.length = (ULONG)(pbCur - pbParam);
        argument.offset = (ULONG)(pbCur - pbBase - argument.length);

        params.push_back(argument);
    }

    return S_OK;
}

bool FindTypeDefByName(const shared::WSTRING& instrumentationTargetMethodTypeName, const shared::WSTRING& assemblyName,
                       const ComPtr<IMetaDataImport2>& metadata_import, mdTypeDef& typeDef)
{
    mdTypeDef parentTypeDef = mdTypeDefNil;
    const auto nameParts = shared::Split(instrumentationTargetMethodTypeName, '+');

    for (const auto& namePart : nameParts)
    {
        auto hr = metadata_import->FindTypeDefByName(namePart.c_str(), parentTypeDef, &parentTypeDef);

        if (FAILED(hr))
        {
            // This can happen between .NET framework and .NET core, not all apis are
            // available in both. Eg: WinHttpHandler, CurlHandler, and some methods in
            // System.Data
            Logger::Debug("Can't load the TypeDef for: ", instrumentationTargetMethodTypeName,
                          ", Module: ", assemblyName);
            return false;
        }
    }

    typeDef = parentTypeDef;
    return true;
}

// FunctionLocalSignature
HRESULT FunctionLocalSignature::TryParse(PCCOR_SIGNATURE pbBase, unsigned len, std::vector<TypeSignature>& locals)
{
    PCCOR_SIGNATURE pbCur = pbBase;
    PCCOR_SIGNATURE pbEnd = pbBase + len;

    BYTE temp;
    unsigned get_locals_count;

    const int LocalVarSig = 0x07;

    IfFalseRetFAIL(ParseByte(pbCur, pbEnd, &temp));
    if (temp != LocalVarSig)
    {
        // Not a LocalVarSig
        return E_FAIL;
    }

    // Number of locals - 1 to 0xFFFE (65534)
    IfFalseRetFAIL(ParseNumber(pbCur, pbEnd, &get_locals_count));

    for (unsigned i = 0; i < get_locals_count; i++)
    {
        if (pbCur >= pbEnd) return E_FAIL;

        const PCCOR_SIGNATURE pbLocal = pbCur;

        IfFalseRetFAIL(ParseParamOrLocal(pbCur, pbEnd));

        TypeSignature local{};
        local.pbBase = pbBase;
        local.length = (ULONG) (pbCur - pbLocal);
        local.offset = (ULONG) (pbCur - pbBase - local.length);

        locals.push_back(local);
    }
    return S_OK;
}

shared::WSTRING GetStringValueFromBlob(PCCOR_SIGNATURE& signature)
{
    // If it's null
    if (*signature == UINT8_MAX)
    {
        signature += 1;
        return shared::WSTRING();
    }

    // Read size and advance
    ULONG size{CorSigUncompressData(signature)};
    shared::WSTRING wstr;
    wstr.reserve(size);

    std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
    std::wstring temp =
        converter.from_bytes(reinterpret_cast<const char*>(signature), reinterpret_cast<const char*>(signature) + size);
    wstr.assign(temp.begin(), temp.end());
    signature += size;
    return wstr;
}

HRESULT HasAsyncStateMachineAttribute(const ComPtr<IMetaDataImport2>& metadataImport, const mdMethodDef methodDefToken, bool& hasAsyncAttribute)
{
    const void* ppData = nullptr;
    ULONG pcbData = 0;
    auto hr = metadataImport->GetCustomAttributeByName(
        methodDefToken, WStr("System.Runtime.CompilerServices.AsyncStateMachineAttribute"), &ppData, &pcbData);
    IfFailRet(hr);
    hasAsyncAttribute = pcbData > 0;
    return hr;
}

HRESULT IsByRefLike(const ComPtr<IMetaDataImport2>& metadataImport, const mdTypeDef typeDefToken, bool& hasByRefLikeAttribute)
{
    const void* ppData = nullptr;
    ULONG pcbData = 0;
    auto hr = metadataImport->GetCustomAttributeByName(
        typeDefToken, WStr("System.Runtime.CompilerServices.IsByRefLikeAttribute"), &ppData, &pcbData);

    IfFailRet(hr);
    hasByRefLikeAttribute = pcbData > 0;
    return hr;
}

HRESULT ResolveTypeInternal(ICorProfilerInfo4* info,
                            const std::vector<ModuleID>& loadedModules,
                            const std::vector<WCHAR>& refTypeName,
                            const mdToken parentToken,
                            const shared::WSTRING& resolutionScopeName, mdTypeDef& resolvedTypeDefToken,
                            ComPtr<IMetaDataImport2>& resolvedTypeDefMetadataImport)
{
    mdAssembly candidateAssembly;
    auto foundModule = false;

    if (Logger::IsDebugEnabled())
    {
        Logger::Debug("[ResolveTypeInternal] Trying to resolve type ref to type def for: ", shared::WSTRING(refTypeName.data()));
    }

    // iterate over all the loaded modules and search for the correct one that matches the assemblyRef (resolutionScope)
    for (auto& moduleId : loadedModules)
    {
        if (moduleId == 0) continue;

        ComPtr<IUnknown> metadata_interfaces;
        auto hr = info->GetModuleMetaData(moduleId, ofRead, IID_IMetaDataImport2,
                                                            metadata_interfaces.GetAddressOf());
        if (FAILED(hr))
        {
            Logger::Warn("[ResolveTypeInternal] GetModuleMetaData has failed with: ", shared::WSTRING(refTypeName.data()));
            continue;
        }

        const auto& candidateAssemblyImport =
            metadata_interfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
        const auto& candidateMetadataImport = metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);

        hr = candidateAssemblyImport->GetAssemblyFromScope(&candidateAssembly);
        if (FAILED(hr))
        {
            Logger::Warn("[ResolveTypeInternal] GetAssemblyFromScope has failed with: ", shared::WSTRING(refTypeName.data()));
            continue;
        }

        const auto& assemblyMetadata = GetAssemblyImportMetadata(candidateAssemblyImport);

        if (assemblyMetadata.name == shared::EmptyWStr)
        {
            Logger::Warn("[ResolveTypeInternal] GetAssemblyImportMetadata has failed with: ", shared::WSTRING(refTypeName.data()));
            continue;
        }

        const auto& candidateResolutionScopeName = assemblyMetadata.name;

        if (resolutionScopeName == candidateResolutionScopeName)
        {
            hr = candidateMetadataImport->FindTypeDefByName(refTypeName.data(), parentToken, &resolvedTypeDefToken);
            if (hr == S_OK)
            {
                resolvedTypeDefMetadataImport = candidateMetadataImport;
                foundModule = true;
                break;
            }
            else
            {
                // look for exported type with the same name of the given TypeRef
                mdExportedType exportedType;
                hr = candidateAssemblyImport->FindExportedTypeByName(refTypeName.data(), parentToken, &exportedType);
                if (FAILED(hr))
                {
                    Logger::Warn("[ResolveTypeInternal] FindExportedTypeByName has failed with: ", shared::WSTRING(refTypeName.data()));
                    continue;
                }

                if (hr == S_OK)
                {
                    WCHAR szName[kNameMaxSize];
                    ULONG pchName;
                    mdToken exportedTypeContainerToken; // will hold mdAssemblyRef / mdExportedType  / mdFile
                    mdTypeDef exportedTypeTypeDef;
                    DWORD exportedTypeFlags;

                    hr = candidateAssemblyImport->GetExportedTypeProps(exportedType, szName, kNameMaxSize, &pchName,
                                                                       &exportedTypeContainerToken,
                                                                       &exportedTypeTypeDef, &exportedTypeFlags);
                    if (FAILED(hr))
                    {
                        Logger::Warn("[ResolveTypeInternal] GetExportedTypeProps [1] has failed with: ", shared::WSTRING(refTypeName.data()));
                        continue;
                    }

                    int retryCount = 1000; // To avoid falling into an infinite loop
                    while (retryCount-- > 0 && 
                           exportedTypeContainerToken != mdExportedTypeNil &&
                           TypeFromToken(exportedTypeContainerToken) == mdtExportedType &&
                           IsTdNestedPublic(exportedTypeFlags))
                    {
                        hr = candidateAssemblyImport->GetExportedTypeProps(
                            exportedTypeContainerToken, szName, kNameMaxSize, &pchName, &exportedTypeContainerToken,
                            &exportedTypeTypeDef, &exportedTypeFlags);
                        if (FAILED(hr))
                        {
                            Logger::Warn("[ResolveTypeInternal] GetExportedTypeProps [2] has failed with: ", shared::WSTRING(refTypeName.data()));
                        }
                    }

                    if (retryCount == 0)
                    {
                        Logger::Warn("[ResolveTypeInternal] Reached the maximum amount of tryouts of trying to grab the exported type with: ", shared::WSTRING(refTypeName.data()));
                        return E_FAIL;
                    }

                    if (TypeFromToken(exportedTypeContainerToken) == mdtAssemblyRef)
                    {
                        const auto& assemblyRefMetadata =
                            GetReferencedAssemblyMetadata(candidateAssemblyImport, exportedTypeContainerToken);
                        if (assemblyRefMetadata.name == shared::EmptyWStr)
                        {
                            Logger::Warn(
                                "[ResolveTypeInternal] GetResolutionScopeNameForAssemblyRefTok has failed with: ", shared::WSTRING(refTypeName.data()));
                            continue;
                        }

                        hr = ResolveTypeInternal(info, loadedModules, refTypeName, mdExportedTypeNil, assemblyRefMetadata.name,
                                                resolvedTypeDefToken, resolvedTypeDefMetadataImport);
                        if (FAILED(hr))
                        {
                            Logger::Warn(
                                "[ResolveTypeInternal] Recursive call to ResolveTypeInternal has failed with: ", shared::WSTRING(refTypeName.data()));
                            continue;
                        }

                        return hr;
                    }
                }
            }
        }
    }

    if (foundModule)
    {
        return S_OK;
    }
    
    resolvedTypeDefToken = mdTokenNil;
    Logger::Warn("[ResolveTypeInternal] ResolveTypeInternal has failed. Reason: Module not found in the loaded modules for type name: ", shared::WSTRING(refTypeName.data()));
    return E_NOTIMPL;
}

/**
 Resolves TypeRef to TypeDef.
 Note: If the module where the TypeRef is defined is not loaded, E_FAIL will return.
 */
HRESULT ResolveType(ICorProfilerInfo4* info,
                    const ComPtr<IMetaDataImport2>& metadata_import,
                    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
                    const mdTypeRef typeRefToken,
                    mdTypeDef& resolvedTypeDefToken,
                    ComPtr<IMetaDataImport2>& resolvedMetadataImport)
{
    mdToken resolutionScope = mdTokenNil; // will hold either AssemblyRef or ModuleRef token
    mdToken enclosingType = mdTokenNil;
    ULONG nameSize = 0;
    std::vector<WCHAR> refTypeName(kNameMaxSize);

    // get the type name & resolution scope
    auto hr = metadata_import->GetTypeRefProps(typeRefToken, &resolutionScope, refTypeName.data(), kNameMaxSize, &nameSize);
    if (FAILED(hr) || resolutionScope == mdTokenNil)
    {
        Logger::Info("[ResolveType] GetTypeRefProps [1] has failed. typeRefToken: ", typeRefToken);
        return E_FAIL;
    }

    mdToken tempToken = mdTokenNil;
    // To avoid ending up in an infinite loop, I'm limiting the execution to 1000 (arbitrary large number that will be
    // well beyond enough)
    int retryCount = 1000;
    while (retryCount-- > 0 && 
        TypeFromToken(resolutionScope) != mdtAssemblyRef && 
        TypeFromToken(resolutionScope) != mdtModuleRef &&
        resolutionScope != mdTokenNil)
    {
        tempToken = resolutionScope;
        if (enclosingType == mdTokenNil)
        {
            enclosingType = tempToken;
        }
        hr = metadata_import->GetTypeRefProps(tempToken, &resolutionScope, nullptr, 0, &nameSize);
        if (FAILED(hr))
        {
            Logger::Warn("[ResolveType] GetTypeRefProps [2] has failed. typeRefToken: ", typeRefToken);
        }
        IfFailRet(hr);
    }

    if (retryCount == 0)
    {
        Logger::Warn(
            "[ResolveType] Reached the maximum amount of tryouts of resolving the resolution scope. typeRefToken: ",
            typeRefToken);
        return E_FAIL;
    }

    if (resolutionScope == mdTokenNil)
    {
        Logger::Warn("[ResolveType] resolutionScope is nil. typeRefToken: ", typeRefToken);
        return E_FAIL;
    }

    const auto& assemblyMetadata = GetReferencedAssemblyMetadata(assembly_import, resolutionScope);
    if (assemblyMetadata.name == shared::EmptyWStr)
    {
        Logger::Warn("[ResolveType] GetReferencedAssemblyMetadata has failed. typeRefToken: ", typeRefToken);
        return E_FAIL;
    }

    ICorProfilerModuleEnum* moduleEnum;
    hr = info->EnumModules(&moduleEnum);
    if (FAILED(hr))
    {
        Logger::Warn("[ResolveType] EnumModules has failed. typeRefToken: ", typeRefToken);
    }
    IfFailRet(hr);

    std::vector<ModuleID> loadedModules;
    size_t resultIndex = 0;

    retryCount = 1000;
    // iterate over the loaded modules enumeration and collect the module ids into loadedModules
    while (retryCount-- > 0)
    {
        const ULONG valueToRetrieve = 20;
        ULONG valueRetrieved = 0;

        std::vector<ModuleID> tempValues(valueToRetrieve, 0);
        hr = moduleEnum->Next(valueToRetrieve, tempValues.data(), &valueRetrieved);
        if (FAILED(hr))
        {
            Logger::Warn("[ResolveType] EnumModules.Next has failed. typeRefToken: ", typeRefToken);
        }
        IfFailRet(hr);

        if (valueRetrieved == 0)
        {
            break;
        }

        loadedModules.resize(loadedModules.size() + valueToRetrieve);
        for (size_t k = 0; k < valueRetrieved; ++k)
        {
            loadedModules[resultIndex] = tempValues[k];
            ++resultIndex;
        }
    }

    if (retryCount == 0)
    {
        Logger::Warn(
            "[ResolveType] Reached the maximum amount of tryouts of enumerating the loaded modules. typeRefToken: ",
            typeRefToken);
        return E_FAIL;
    }

    const auto& resolutionScopeName = assemblyMetadata.name;

    resolvedTypeDefToken = mdTokenNil;
    if (enclosingType != mdTokenNil)
    {
        Logger::Debug("ResolveType: Found enclosing type, try to get parent token");
        std::vector<WCHAR> enclosingRefTypeName(kNameMaxSize);
        hr = metadata_import->GetTypeRefProps(enclosingType, &resolutionScope, enclosingRefTypeName.data(),
                                              kNameMaxSize, &nameSize);
        if (FAILED(hr))
        {
            Logger::Warn("[ResolveType] GetTypeRefProps [3] has failed. typeRefToken: ", typeRefToken);
        }
        IfFailRet(hr);

        hr = ResolveTypeInternal(info, loadedModules, enclosingRefTypeName, mdTokenNil, resolutionScopeName,
                                 resolvedTypeDefToken, resolvedMetadataImport);
        IfFailRet(hr);
    }

    return ResolveTypeInternal(info, loadedModules, refTypeName, resolvedTypeDefToken, resolutionScopeName,
                             resolvedTypeDefToken, resolvedMetadataImport);
}

// GenericTypeProps
HRESULT GenericTypeProps::TryParse()
{
    // Note: Not all signatures are supported. Internal Jira ticket: DEBUG-1243.
    // The following two signatures are supported for now:
    // VAR | MVAR Number
    // or GENERICINST (CLASS | VALUETYPE) TypeDefOrRefEncoded GenArgCount Type Type*

    PCCOR_SIGNATURE pbCur = pbBase;
    const PCCOR_SIGNATURE pbEnd = pbBase + len;
    unsigned char specElementType;

    unsigned offset = 0;
    mdToken openGenericToken = mdTokenNil;

    IfFalseRetFAIL(ParseByte(pbCur, pbEnd, &specElementType));

    while (specElementType == ELEMENT_TYPE_BYREF)
    {
        IfFalseRetFAIL(ParseByte(pbCur, pbEnd, &specElementType));
        offset++;
    }

    unsigned genericParamPosition;
    if (specElementType == ELEMENT_TYPE_VAR || specElementType == ELEMENT_TYPE_MVAR)
    {
        IfFalseRetFAIL(ParseNumber(pbCur, pbEnd, &genericParamPosition));

        std::vector<std::tuple<PCCOR_SIGNATURE, ULONG, unsigned char>> signatures;
        SpecElementType = specElementType;
        OpenTypeToken = openGenericToken;
        GenericElementType = 0;
        ParamsCount = 0;
        IndexOfFirstParam = offset;
        ParamPosition = genericParamPosition;
        ParamsSignature = std::move(signatures);

        return S_OK;
    }

    if (specElementType != ELEMENT_TYPE_GENERICINST) return E_FAIL;

    offset++;
    unsigned char elementType;
    IfFalseRetFAIL(ParseByte(pbCur, pbEnd, &elementType));

    // Skip ELEMENT_TYPE_BYREF. Add more elements that could be here, if any...
    while (elementType == ELEMENT_TYPE_BYREF)
    {
        IfFalseRetFAIL(ParseByte(pbCur, pbEnd, &elementType));
        offset++;
    }

    if (elementType != ELEMENT_TYPE_CLASS && elementType != ELEMENT_TYPE_VALUETYPE)
        return E_FAIL;

    offset++;
    const auto tokenSigLength = CorSigUncompressToken(pbCur, &openGenericToken);
    if (tokenSigLength == static_cast<ULONG>(-1))
        return E_FAIL;
    offset += tokenSigLength;

    pbCur += tokenSigLength;
    unsigned paramCount;
    IfFalseRetFAIL(ParseNumber(pbCur, pbEnd, &paramCount));

    offset++;
    std::vector<std::tuple<PCCOR_SIGNATURE, ULONG, unsigned char>> signatures;
    for (unsigned i = 0; i < paramCount; i++)
    {
        PCCOR_SIGNATURE copySig = pbCur;
        IfFalseRetFAIL(ParseType(pbCur, pbEnd));
        
        PCCOR_SIGNATURE elementTypeSig = copySig;

        while (*elementTypeSig == ELEMENT_TYPE_BYREF)
        {
            elementTypeSig++;
        }

        unsigned char paramElementType = *elementTypeSig;
        signatures.emplace_back(std::make_tuple(copySig, static_cast<ULONG>(pbCur - copySig), paramElementType));
    }

    SpecElementType = specElementType;
    OpenTypeToken = openGenericToken;
    GenericElementType = elementType;
    ParamsCount = paramCount;
    IndexOfFirstParam = offset;
    ParamPosition = 0;
    ParamsSignature = std::move(signatures);

    return S_OK;
}

void LogManagedProfilerAssemblyDetails()
{
    if (!IsDebugEnabled())
    {
        return;
    }

    Logger::Debug("pcbPublicKey: ", managed_profiler_assembly_property.pcbPublicKey);
    Logger::Debug("ppbPublicKey: ", shared::HexStr(managed_profiler_assembly_property.ppbPublicKey,
                                                  managed_profiler_assembly_property.pcbPublicKey));
    Logger::Debug("szName: ", managed_profiler_assembly_property.szName);

    Logger::Debug("Metadata.cbLocale: ", managed_profiler_assembly_property.pMetaData.cbLocale);
    Logger::Debug("Metadata.szLocale: ", managed_profiler_assembly_property.pMetaData.szLocale);

    if (managed_profiler_assembly_property.pMetaData.rOS != nullptr)
    {
        Logger::Debug("Metadata.rOS.dwOSMajorVersion: ",
                     managed_profiler_assembly_property.pMetaData.rOS->dwOSMajorVersion);
        Logger::Debug("Metadata.rOS.dwOSMinorVersion: ",
                     managed_profiler_assembly_property.pMetaData.rOS->dwOSMinorVersion);
        Logger::Debug("Metadata.rOS.dwOSPlatformId: ",
                     managed_profiler_assembly_property.pMetaData.rOS->dwOSPlatformId);
    }

    Logger::Debug("Metadata.usBuildNumber: ", managed_profiler_assembly_property.pMetaData.usBuildNumber);
    Logger::Debug("Metadata.usMajorVersion: ", managed_profiler_assembly_property.pMetaData.usMajorVersion);
    Logger::Debug("Metadata.usMinorVersion: ", managed_profiler_assembly_property.pMetaData.usMinorVersion);
    Logger::Debug("Metadata.usRevisionNumber: ", managed_profiler_assembly_property.pMetaData.usRevisionNumber);

    Logger::Debug("pulHashAlgId: ", managed_profiler_assembly_property.pulHashAlgId);
    Logger::Debug("sizeof(pulHashAlgId): ", sizeof(managed_profiler_assembly_property.pulHashAlgId));
    Logger::Debug("assemblyFlags: ", managed_profiler_assembly_property.assemblyFlags);
}

HRESULT IsTypeByRefLike(ICorProfilerInfo4* corProfilerInfo4, const ModuleMetadataBase& module_metadata, const TypeSignature& typeSig,
                                        const mdAssemblyRef& corLibAssemblyRef, bool& isTypeIsByRefLike) {
    auto metaDataImportOfTypeDef = module_metadata.metadata_import;
    auto metaDataEmitOfTypeDef = module_metadata.metadata_emit;
    auto typeDefOrRefOrSpecToken = typeSig.GetTypeTok(metaDataEmitOfTypeDef, corLibAssemblyRef);

    // Get open type from type spec
    if (TypeFromToken(typeDefOrRefOrSpecToken) == mdtTypeSpec)
    {
        PCCOR_SIGNATURE sig;
        const ULONG sigLength = typeSig.GetSignature(sig);
        GenericTypeProps genericProps {sig, sigLength};
        const auto hr = genericProps.TryParse();

        if (hr == S_OK && genericProps.OpenTypeToken != mdTokenNil)
        {
            typeDefOrRefOrSpecToken = genericProps.OpenTypeToken;
        }
        else if (genericProps.SpecElementType == ELEMENT_TYPE_VAR || genericProps.SpecElementType == ELEMENT_TYPE_MVAR)
        {
            isTypeIsByRefLike = false;
            return S_OK;
        }
        else
        {
            Logger::Warn("[IsTypeByRefLike] Failed to get open type token for generic type. Assuming no byref-like.");
            isTypeIsByRefLike = false;
            return S_OK;
        }
    }

    return IsTypeTokenByRefLike(corProfilerInfo4, module_metadata, typeDefOrRefOrSpecToken, isTypeIsByRefLike);
}

HRESULT IsTypeTokenByRefLike(ICorProfilerInfo4* corProfilerInfo4, const ModuleMetadataBase& module_metadata, mdToken typeDefOrRefOrSpecToken,
                                             bool& isTypeIsByRefLike) {
    auto metaDataImportOfTypeDef = module_metadata.metadata_import;

    // Get open type from type spec
    if (TypeFromToken(typeDefOrRefOrSpecToken) == mdtTypeSpec)
    {
        Logger::Warn("IsTypeTokenByRefLike is not resolving type specs. Use IsTypeByRefLike instead.");
        isTypeIsByRefLike = false;
        return S_OK;
    }

    if (TypeFromToken(typeDefOrRefOrSpecToken) == mdtTypeRef)
    {
        const auto& metadata_import = module_metadata.metadata_import;
        const auto& assembly_import = module_metadata.assembly_import;

        auto hr = ResolveType(corProfilerInfo4, metadata_import, assembly_import, typeDefOrRefOrSpecToken,
                              typeDefOrRefOrSpecToken, metaDataImportOfTypeDef);

        if (FAILED(hr))
        {
            // For now we ignore issues with resolving types.
            isTypeIsByRefLike = false;
            return S_OK;
        }
    }

    return IsByRefLike(metaDataImportOfTypeDef, typeDefOrRefOrSpecToken, isTypeIsByRefLike);
}

} // namespace trace
