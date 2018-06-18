#pragma once

#include <corhlpr.h>
#include "MetadataReferenceLookups.h"
#include "IntegrationBase.h"

struct ModuleInfo
{
    // TODO: use std::string
    WCHAR m_wszModulePath[512] = L"";
    IMetaDataImport* m_pImport = nullptr;

    TypeRefLookup m_TypeRefLookup = {};
    MemberRefLookup m_MemberRefLookup = {};

    std::vector<const IntegrationBase*> m_Integrations = {};
};
