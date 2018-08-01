#include <string>
#include <fstream>
#include "MetadataBuilder.h"
#include "Macros.h"

HRESULT MetadataBuilder::emit_assembly_ref(const std::wstring& assembly_name,
                                           const ASSEMBLYMETADATA& assembly_metadata,
                                           BYTE public_key_token[],
                                           const ULONG public_key_token_length,
                                           mdAssemblyRef& assembly_ref_out) const
{
    const HRESULT hr = assemblyEmit->DefineAssemblyRef(static_cast<void *>(public_key_token),
                                                       public_key_token_length,
                                                       assembly_name.c_str(),
                                                       &assembly_metadata,
                                                       // hash blob
                                                       nullptr,
                                                       // cb of hash blob
                                                       0,
                                                       // flags
                                                       0,
                                                       &assembly_ref_out);

    LOG_IFFAILEDRET(hr, L"DefineAssemblyRef failed");
    return S_OK;
}

HRESULT MetadataBuilder::find_assembly_ref(const std::wstring& assembly_name,
                                           mdAssemblyRef* assembly_ref_out) const
{
    HCORENUM hEnum = nullptr;
    mdAssemblyRef rgAssemblyRefs[20];
    ULONG cAssemblyRefsReturned;

    do
    {
        const HRESULT hr = assemblyImport->EnumAssemblyRefs(&hEnum,
                                                            rgAssemblyRefs,
                                                            _countof(rgAssemblyRefs),
                                                            &cAssemblyRefsReturned);

        LOG_IFFAILEDRET(hr, L"EnumAssemblyRefs failed, hr = " << HEX(hr));

        if (cAssemblyRefsReturned == 0)
        {
            assemblyImport->CloseEnum(hEnum);
            LOG_APPEND(L"Could not find an AssemblyRef to " << assembly_name);
            return E_FAIL;
        }
    }
    while (find_assembly_ref_iterator(assembly_name,
                                      rgAssemblyRefs,
                                      cAssemblyRefsReturned,
                                      assembly_ref_out) < S_OK);

    assemblyImport->CloseEnum(hEnum);
    return S_OK;
}

HRESULT MetadataBuilder::find_assembly_ref_iterator(const std::wstring& assembly_name,
                                                    mdAssemblyRef assembly_refs[],
                                                    ULONG assembly_ref_count,
                                                    mdAssemblyRef* assembly_ref_out) const
{
    for (ULONG i = 0; i < assembly_ref_count; i++)
    {
        const void* pvPublicKeyOrToken;
        ULONG cbPublicKeyOrToken;
        WCHAR wszName[512];
        ULONG cchNameReturned;
        ASSEMBLYMETADATA asmMetaData{};
        //ZeroMemory(&asmMetaData, sizeof(asmMetaData));
        const void* pbHashValue;
        ULONG cbHashValue;
        DWORD asmRefFlags;

        const HRESULT hr = assemblyImport->GetAssemblyRefProps(assembly_refs[i],
                                                               &pvPublicKeyOrToken,
                                                               &cbPublicKeyOrToken,
                                                               wszName,
                                                               _countof(wszName),
                                                               &cchNameReturned,
                                                               &asmMetaData,
                                                               &pbHashValue,
                                                               &cbHashValue,
                                                               &asmRefFlags);

        LOG_IFFAILEDRET(hr,L"GetAssemblyRefProps failed, hr = " << HEX(hr));

        if (assembly_name == wszName)
        {
            *assembly_ref_out = assembly_refs[i];
            return S_OK;
        }
    }

    return E_FAIL;
}

HRESULT MetadataBuilder::store_wrapper_type_ref(const integration& integration, mdTypeRef& type_ref_out) const
{
    const auto cache_key = integration.get_wrapper_type_key();
    mdTypeRef type_ref = mdTypeRefNil;

    if (metadata.TryGetWrapperParentTypeRef(cache_key, type_ref))
    {
        // this type was already resolved
        type_ref_out = type_ref;
        return S_OK;
    }

    HRESULT hr;
    type_ref = mdTypeRefNil;

    if (metadata.assemblyName == integration.wrapper_assembly_name)
    {
        // type is defined in this assembly
        hr = metadataEmit->DefineTypeRefByName(module, integration.wrapper_type_name.c_str(), &type_ref);
    }
    else
    {
        // type is defined in another assembly,
        // find a reference to the assembly where type lives
        mdAssemblyRef assembly_ref = mdAssemblyRefNil;
        hr = find_assembly_ref(integration.wrapper_assembly_name, &assembly_ref);
        RETURN_IF_FAILED(hr);

        // TODO: emit assembly reference if not found?

        // search for an existing reference to the type
        hr = metadataImport->FindTypeRef(assembly_ref, integration.wrapper_type_name.c_str(), &type_ref);

        if (hr == HRESULT(0x80131130) /* record not found on lookup */)
        {
            // if typeRef not found, create a new one by emiting a metadata token
            hr = metadataEmit->DefineTypeRefByName(assembly_ref, integration.wrapper_type_name.c_str(), &type_ref);
        }
    }

    RETURN_IF_FAILED(hr);

    metadata.SetWrapperParentTypeRef(cache_key, type_ref);
    type_ref_out = type_ref;
    return S_OK;
}

HRESULT MetadataBuilder::store_wrapper_method_ref(const integration& integration, const method_replacement& method) const
{
    const auto cache_key = integration.get_wrapper_method_key(method);
    mdMemberRef member_ref = mdMemberRefNil;

    if (metadata.TryGetWrapperMemberRef(cache_key, member_ref))
    {
        // this member was already resolved
        return S_OK;
    }

    mdTypeRef type_ref = mdTypeRefNil;
    HRESULT hr = store_wrapper_type_ref(integration, type_ref);
    RETURN_IF_FAILED(hr);

    member_ref = mdMemberRefNil;
    hr = metadataImport->FindMemberRef(type_ref,
                                       method.wrapper_method_name.c_str(),
                                       method.wrapper_method_signature.data(),
                                       static_cast<ULONG>(method.wrapper_method_signature.size()),
                                       &member_ref);

    if (hr == HRESULT(0x80131130) /* record not found on lookup */)
    {
        // if memberRef not found, create it by emiting a metadata token
        hr = metadataEmit->DefineMemberRef(type_ref,
                                           method.wrapper_method_name.c_str(),
                                           method.wrapper_method_signature.data(),
                                           static_cast<ULONG>(method.wrapper_method_signature.size()),
                                           &member_ref);
    }

    RETURN_IF_FAILED(hr);

    metadata.SetWrapperMemberRef(cache_key, member_ref);
    return S_OK;
}
