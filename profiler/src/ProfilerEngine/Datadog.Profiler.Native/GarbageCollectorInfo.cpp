// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GarbageCollectorInfo.h"

#include "OsSpecificApi.h"

#include "Log.h"

bool GarbageCollectorInfo::IsGcThread(std::shared_ptr<IThreadInfo> const& thread)
{
    static shared::WSTRING GcServerThread = WStr(".NET Server GC");
    static shared::WSTRING GcBackgroundServerThread = WStr(".NET BGC");

    auto const& name = thread->GetThreadName();
    return name == GcServerThread || name == GcBackgroundServerThread;
}

std::vector<std::shared_ptr<IThreadInfo>> const& GarbageCollectorInfo::GetThreads()
{
    Log::Debug("Get all native thread of the current process");

    // maybe check that the number is k * nb of cores ?
    if (!_gcThreads.empty())
    {
        Log::Debug("GC threads have already been collected. Nb Threads: ", _gcThreads.size());
        return _gcThreads;
    }

    // we may want to allow checking for process time or if managed thread have been created
    if (_number_of_attempts > 2)
    {
        Log::Debug("Failed at retrieving GC threads after ", _number_of_attempts, " of attempts");
        return _gcThreads;
    }

    _number_of_attempts++;

    for (auto const& threadInfo : OsSpecificApi::GetProcessThreads())
    {
        if (IsGcThread(threadInfo))
        {
            Log::Debug("Found GC threads. Name: ", threadInfo->GetThreadName(), ", ID: ", threadInfo->GetOsThreadId());
            _gcThreads.push_back(threadInfo);
        }
        else
        {
            Log::Debug("Found threads. Name: ", threadInfo->GetThreadName(), ", ID: ", threadInfo->GetOsThreadId());
        }
    }

    Log::Debug("Collected ", _gcThreads.size(), " GC threads.");
    return _gcThreads;
}