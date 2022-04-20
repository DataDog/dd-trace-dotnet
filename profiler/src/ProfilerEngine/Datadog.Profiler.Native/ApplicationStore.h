// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IApplicationStore.h"

// forward declarations
class IConfiguration;


/// <summary>
/// This class is not complete yet. Today it's just a proxy that return the service name from the configuration
/// Later, this class will be fed with information from the Tracer
/// </summary>
class ApplicationStore : public IApplicationStore
{
public:
    ApplicationStore(IConfiguration* configuration);

    const std::string& GetName(std::string_view runtimeId) override;

private:
    IConfiguration* const _pConfiguration;
};
