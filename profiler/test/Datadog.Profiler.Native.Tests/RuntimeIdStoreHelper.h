// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IRuntimeIdStore.h"

class RuntimeIdStoreHelper : public IRuntimeIdStore
{
public:
    RuntimeIdStoreHelper();  // always returns the same guid

public:
// Inherited via IRuntimeIdStore
    const char* GetId(AppDomainID appDomainId) override;

private:
    static std::string _guid;
};

