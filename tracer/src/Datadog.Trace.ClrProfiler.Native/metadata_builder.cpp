﻿#include <fstream>
#include <string>

#include "clr_helpers.h"
#include "logger.h"
#include "macros.h"
#include "metadata_builder.h"

namespace trace
{

HRESULT MetadataBuilder::EmitAssemblyRef(const trace::AssemblyReference& assembly_ref) const
{
    ASSEMBLYMETADATA assembly_metadata{};
    assembly_metadata.usMajorVersion = assembly_ref.version.major;
    assembly_metadata.usMinorVersion = assembly_ref.version.minor;
    assembly_metadata.usBuildNumber = assembly_ref.version.build;
    assembly_metadata.usRevisionNumber = assembly_ref.version.revision;
    if (assembly_ref.locale == WStr("neutral"))
    {
        assembly_metadata.szLocale = nullptr;
        assembly_metadata.cbLocale = 0;
    }
    else
    {
        assembly_metadata.szLocale = const_cast<WCHAR*>(assembly_ref.locale.c_str());
        assembly_metadata.cbLocale = (DWORD)(assembly_ref.locale.size());
    }

    DWORD public_key_size = 8;
    if (assembly_ref.public_key == trace::PublicKey())
    {
        public_key_size = 0;
    }

    mdAssemblyRef assembly_ref_out;
    const HRESULT hr = assembly_emit_->DefineAssemblyRef(&assembly_ref.public_key.data[0], public_key_size,
                                                         assembly_ref.name.c_str(), &assembly_metadata,
                                                         // hash blob
                                                         nullptr,
                                                         // cb of hash blob
                                                         0,
                                                         // flags
                                                         0, &assembly_ref_out);

    if (FAILED(hr))
    {
        Logger::Warn("DefineAssemblyRef failed");
    }
    return S_OK;
}

HRESULT MetadataBuilder::FindWrapperTypeRef(const MethodReplacement& method_replacement, mdTypeRef& type_ref_out) const
{
    const auto& cache_key = method_replacement.wrapper_method.get_type_cache_key();
    mdTypeRef type_ref = mdTypeRefNil;

    if (metadata_.TryGetWrapperParentTypeRef(cache_key, type_ref))
    {
        // this type was already resolved
        type_ref_out = type_ref;
        return S_OK;
    }

    HRESULT hr;
    type_ref = mdTypeRefNil;

    if (metadata_.assemblyName == method_replacement.wrapper_method.assembly.name)
    {
        // type is defined in this assembly
        hr = metadata_emit_->DefineTypeRefByName(module_, method_replacement.wrapper_method.type_name.c_str(),
                                                 &type_ref);
    }
    else
    {
        // type is defined in another assembly,
        // find a reference to the assembly where type lives
        const auto assembly_ref = FindAssemblyRef(assembly_import_, method_replacement.wrapper_method.assembly.name);
        if (assembly_ref == mdAssemblyRefNil)
        {
            // TODO: emit assembly reference if not found?
            Logger::Warn("Assembly reference for", method_replacement.wrapper_method.assembly.name, " not found");
            return E_FAIL;
        }

        // search for an existing reference to the type
        hr =
            metadata_import_->FindTypeRef(assembly_ref, method_replacement.wrapper_method.type_name.c_str(), &type_ref);

        if (hr == HRESULT(0x80131130) /* record not found on lookup */)
        {
            // if typeRef not found, create a new one by emitting a metadata token
            hr = metadata_emit_->DefineTypeRefByName(assembly_ref, method_replacement.wrapper_method.type_name.c_str(),
                                                     &type_ref);
        }
    }

    RETURN_IF_FAILED(hr);

    // cache the typeRef in case we need it again
    metadata_.SetWrapperParentTypeRef(cache_key, type_ref);
    type_ref_out = type_ref;
    return S_OK;
}

HRESULT MetadataBuilder::StoreWrapperMethodRef(const MethodReplacement& method_replacement) const
{
    const auto& cache_key = method_replacement.wrapper_method.get_method_cache_key();
    mdMemberRef member_ref = mdMemberRefNil;

    if (metadata_.TryGetWrapperMemberRef(cache_key, member_ref))
    {
        // this member was already resolved
        return S_OK;
    }

    mdTypeRef type_ref = mdTypeRefNil;
    HRESULT hr = FindWrapperTypeRef(method_replacement, type_ref);
    if (FAILED(hr))
    {
        // Record that this cache_key failed
        metadata_.SetFailedWrapperMemberKey(cache_key);
        return hr;
    }

    member_ref = mdMemberRefNil;

    auto signature_data = method_replacement.wrapper_method.method_signature.data;

    // If the signature data size is greater than zero means we need to load the methodRef
    // for CallSite instrumentation.
    // In case of the signature data size is zero we asume we are in a calltarget scenario
    // where we use the TypeRef but not a MemberRef.

    if (signature_data.size() > 0)
    {
        // callsite integrations do this path.
        hr = metadata_import_->FindMemberRef(type_ref, method_replacement.wrapper_method.method_name.c_str(),
                                             signature_data.data(), (DWORD)(signature_data.size()), &member_ref);

        if (hr == HRESULT(0x80131130) /* record not found on lookup */)
        {
            // if memberRef not found, create it by emitting a metadata token
            hr = metadata_emit_->DefineMemberRef(type_ref, method_replacement.wrapper_method.method_name.c_str(),
                                                 signature_data.data(), (DWORD)(signature_data.size()), &member_ref);
        }

        if (FAILED(hr))
        {
            // Record that this cache_key failed
            metadata_.SetFailedWrapperMemberKey(cache_key);
            return hr;
        }
    }

    metadata_.SetWrapperMemberRef(cache_key, member_ref);
    return S_OK;
}

} // namespace trace
