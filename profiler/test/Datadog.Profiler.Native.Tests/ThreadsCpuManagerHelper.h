// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IThreadsCpuManager.h"

class ThreadsCpuManagerHelper : public IThreadsCpuManager
{
    // Inherited via IThreadsCpuManager
    void Map(DWORD threadOSId, const WCHAR* name) override;
    void LogCpuTimes() override;
};
