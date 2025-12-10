#include <fstream>
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

HRESULT MetadataBuilder::FindIntegrationTypeRef(const IntegrationDefinition& integration_definition, mdTypeRef& type_ref_out) const
{
    const auto& cache_key = integration_definition.integration_type.get_cache_key();
    mdTypeRef type_ref = mdTypeRefNil;

    if (metadata_.TryGetIntegrationTypeRef(cache_key, type_ref))
    {
        // this type was already resolved
        type_ref_out = type_ref;
        return S_OK;
    }

    HRESULT hr;
    type_ref = mdTypeRefNil;
    bool is_same_assembly = false;

    if (metadata_.assemblyName == integration_definition.integration_type.assembly.name)
    {
        // Name matches, now check the version, in case we have multiple versions of the assembly loaded
        // TODO: we should already have this data, so should probably just pass that data through,
        // but it's a bit of a pain, to thread that needle right now
        const auto& current_assembly_metadata = GetAssemblyImportMetadata(assembly_import_);
        if (current_assembly_metadata.version == integration_definition.integration_type.assembly.version)
        {
            // type is defined in this assembly (same name AND version)
            hr = metadata_emit_->DefineTypeRefByName(module_, integration_definition.integration_type.name.c_str(),
                                                 &type_ref);
            is_same_assembly = true;
        }
    }

    if (!is_same_assembly)
    {
        // type is defined in another assembly,
        // find a reference to the assembly where type lives
        const auto assembly_ref =
            FindAssemblyRef(assembly_import_, integration_definition.integration_type.assembly.name, integration_definition.integration_type.assembly.version);
        if (assembly_ref == mdAssemblyRefNil)
        {
            // TODO: emit assembly reference if not found?
            Logger::Warn("Assembly reference for", integration_definition.integration_type.assembly.name, " not found");
            return E_FAIL;
        }

        // search for an existing reference to the type
        hr = metadata_import_->FindTypeRef(assembly_ref, integration_definition.integration_type.name.c_str(),
                                           &type_ref);

        if (hr == HRESULT(0x80131130) /* record not found on lookup */)
        {
            // if typeRef not found, create a new one by emitting a metadata token
            hr = metadata_emit_->DefineTypeRefByName(assembly_ref, integration_definition.integration_type.name.c_str(),
                                                     &type_ref);
        }
    }

    RETURN_IF_FAILED(hr);

    // cache the typeRef in case we need it again
    metadata_.SetIntegrationTypeRef(cache_key, type_ref);
    type_ref_out = type_ref;
    return S_OK;
}

} // namespace trace
