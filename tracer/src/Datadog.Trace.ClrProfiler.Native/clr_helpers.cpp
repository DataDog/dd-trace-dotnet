#include "clr_helpers.h"

#include <cstring>

#include "dd_profiler_constants.h"
#include "environment_variables.h"
#include "logger.h"
#include "macros.h"
#include <set>
#include <stack>

#include "../../../shared/src/native-src/pal.h"

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

TypeInfo GetTypeInfo(const ComPtr<IMetaDataImport2>& metadata_import, const mdToken& token)
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

    HRESULT hr = E_FAIL;
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
                mdToken type_token;
                CorSigUncompressToken(&signature[2], &type_token);
                const auto baseType = GetTypeInfo(metadata_import, type_token);
                return {baseType.id,        baseType.name,        token,
                        token_type,         baseType.extend_from, baseType.valueType,
                        baseType.isGeneric, baseType.parent_type, baseType.scopeToken};
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

    return {token,       type_name_string, mdTypeSpecNil,  token_type,
            extendsInfo, type_valueType,   type_isGeneric, parentTypeInfo, parent_token};
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
                                                NULL, 0, corAssemblyProperty.assemblyFlags, corlib_ref);
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
        return assembly_emit->DefineAssemblyRef(public_key, sizeof(public_key), WStr("mscorlib"), &metadata, NULL, 0,
                                                corAssemblyProperty.assemblyFlags, corlib_ref);
    }
}

// TypeSignature
std::tuple<unsigned, int> TypeSignature::GetElementTypeAndFlags() const
{
    int typeFlags = 0;
    unsigned elementType;

    PCCOR_SIGNATURE pbCur = &pbBase[offset];

    if (*pbCur == ELEMENT_TYPE_VOID)
    {
        elementType = ELEMENT_TYPE_VOID;
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

mdToken TypeSignature::GetTypeTok(ComPtr<IMetaDataEmit2>& pEmit, mdAssemblyRef corLibRef) const
{
    mdToken token = mdTokenNil;
    PCCOR_SIGNATURE pbCur = &pbBase[offset];
    const PCCOR_SIGNATURE pStart = pbCur;

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

bool FindTypeDefByName(const shared::WSTRING instrumentationTargetMethodTypeName, const shared::WSTRING assemblyName,
                       const ComPtr<IMetaDataImport2>& metadata_import, mdTypeDef& typeDef)
{
    mdTypeDef parentTypeDef = mdTypeDefNil;
    auto nameParts = shared::Split(instrumentationTargetMethodTypeName, '+');
    auto instrumentedMethodTypeName = instrumentationTargetMethodTypeName;

    if (nameParts.size() == 2)
    {
        // We're instrumenting a nested class, find the parent first
        auto hr = metadata_import->FindTypeDefByName(nameParts[0].c_str(), mdTokenNil, &parentTypeDef);

        if (FAILED(hr))
        {
            // This can happen between .NET framework and .NET core, not all apis are
            // available in both. Eg: WinHttpHandler, CurlHandler, and some methods in
            // System.Data
            Logger::Debug("Can't load the parent TypeDef: ", nameParts[0],
                  " for nested class: ", instrumentationTargetMethodTypeName, ", Module: ", assemblyName);
            return false;
        }
        instrumentedMethodTypeName = nameParts[1];
    }
    else if (nameParts.size() > 2)
    {
        Logger::Warn("Invalid TypeDef-only one layer of nested classes are supported: ", instrumentationTargetMethodTypeName,
             ", Module: ", assemblyName);
        return false;
    }

    // Find the type we're instrumenting
    auto hr = metadata_import->FindTypeDefByName(instrumentedMethodTypeName.c_str(), parentTypeDef, &typeDef);
    if (FAILED(hr))
    {
        // This can happen between .NET framework and .NET core, not all apis are
        // available in both. Eg: WinHttpHandler, CurlHandler, and some methods in
        // System.Data
        Logger::Debug("Can't load the TypeDef for: ", instrumentedMethodTypeName, ", Module: ", assemblyName);
        return false;
    }

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

} // namespace trace
