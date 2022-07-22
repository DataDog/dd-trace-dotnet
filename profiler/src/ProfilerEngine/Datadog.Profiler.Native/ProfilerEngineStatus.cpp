// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProfilerEngineStatus.h"
#include "OpSysTools.h"

std::mutex ProfilerEngineStatus::s_updateLock;
bool* ProfilerEngineStatus::s_pIsProfilerEngineActive = nullptr;

void ProfilerEngineStatus::WriteIsProfilerEngineActive(bool newValue)
{
    bool* pIsProfilerEngineActive = GetPtrIsProfilerEngineActive();

    {
        std::lock_guard<std::mutex> lock(s_updateLock);

        *pIsProfilerEngineActive = newValue;

        OpSysTools::MemoryBarrierProcessWide();
    }
}

bool* ProfilerEngineStatus::GetPtrIsProfilerEngineActive()
{
    bool* pIsProfilerEngineActive = s_pIsProfilerEngineActive;
    if (nullptr == pIsProfilerEngineActive)
    {
        std::lock_guard<std::mutex> lock(s_updateLock);

        pIsProfilerEngineActive = s_pIsProfilerEngineActive;
        if (nullptr == pIsProfilerEngineActive)
        {
            void* newMemRegion = OpSysTools::AlignedMAlloc(ActualAlignmentOf_IsProfilerEngineActive, SizeOf_IsProfilerEngineActive);
            s_pIsProfilerEngineActive = pIsProfilerEngineActive = static_cast<bool*>(newMemRegion);
            *s_pIsProfilerEngineActive = false;
        }
    }

    return pIsProfilerEngineActive;
}