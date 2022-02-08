// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <mutex>

class ProfilerEngineStatus
{
private:
    // The memory for the IsProfilerEngineActive is allocated on the heap and must be properly aligned to ensure fast atomic operations.
    static constexpr int MinimalAlignmentOf_IsProfilerEngineActive = 8;
    static constexpr int ActualAlignmentOf_IsProfilerEngineActive = (MinimalAlignmentOf_IsProfilerEngineActive >= alignof(bool))
                                                                        ? MinimalAlignmentOf_IsProfilerEngineActive
                                                                        : alignof(bool);

    // We will allocate a memory chunk that is at least as big as the alignment, even if we do not need all of that.
    static constexpr int SizeOf_IsProfilerEngineActive = (ActualAlignmentOf_IsProfilerEngineActive >= sizeof(bool))
                                                             ? ActualAlignmentOf_IsProfilerEngineActive
                                                             : sizeof(bool);

public:
    // *(ProfilerEngineStatus::GetReadPtrIsProfilerEngineActive()) tracks ProfilerEngineStatus::_isInitialized.
    // It is used to provide an instance-independent pointer to interested parties (e.g. the managed Trace Context Tracker)
    // that can be dereferenced to see if the Native Profiler Engine is valid and active.
    // We heap-allocate memory for this flag when we first initialize it and we NEVER free it (but we may set the flag to false).
    // This tiny intended leak makes sure that the flag pointer remains valid even if the Native Profiler Engine (i.e. this library)
    // is unloaded from the process. That way a pointer to the flag remains valid for the lifetime of the process.

    // Use GetReadPtrIsProfilerEngineActive() and dereference the obtained pointer to READ the flag value.
    // Use WriteIsProfilerEngineActive(..) to WRITE the flag value.

    static inline const bool* GetReadPtrIsProfilerEngineActive()
    {
        return const_cast<const bool*>(GetPtrIsProfilerEngineActive());
    }

    static bool WriteIsProfilerEngineActive(bool newValue);

private:
    static std::mutex s_updateLock;
    static bool* s_pIsProfilerEngineActive;

private:
    static bool* GetPtrIsProfilerEngineActive();
};
