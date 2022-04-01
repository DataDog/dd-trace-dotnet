// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ApplicationStore.h"

#include "IConfiguration.h"

ApplicationStore::ApplicationStore(IConfiguration* configuration) :
    _pConfiguration{configuration}
{
}

const std::string& ApplicationStore::GetName(std::string_view runtimeId)
{
    return _pConfiguration->GetServiceName();
}
