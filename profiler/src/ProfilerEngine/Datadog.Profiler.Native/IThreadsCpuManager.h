// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IService.h"


class IThreadsCpuManager : public IService
{
public:
    virtual void Map(DWORD threadOSId, const WCHAR* name) = 0;
    virtual void LogCpuTimes() = 0;
};
