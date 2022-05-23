// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RuntimeIdStoreHelper.h"

std::string RuntimeIdStoreHelper::_guid = "00000000-1111-2222-3333-456789ABCDEF";

RuntimeIdStoreHelper::RuntimeIdStoreHelper()
{
}

const char* RuntimeIdStoreHelper::GetId(AppDomainID appDomainId)
{
    return _guid.c_str();
}
