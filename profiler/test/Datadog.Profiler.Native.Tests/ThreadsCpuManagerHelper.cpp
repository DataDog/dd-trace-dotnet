// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ThreadsCpuManagerHelper.h"

const char* ThreadsCpuManagerHelper::GetName()
{
    return "ThreadsCpuManagerHelper";
}

bool ThreadsCpuManagerHelper::Start()
{
    return true;
}

bool ThreadsCpuManagerHelper::Stop()
{
    return true;
}

void ThreadsCpuManagerHelper::Map(DWORD threadOSId, const WCHAR* name)
{
}

void ThreadsCpuManagerHelper::LogCpuTimes()
{
}
