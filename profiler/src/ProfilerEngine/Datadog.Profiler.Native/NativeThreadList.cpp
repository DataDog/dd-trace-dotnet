// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NativeThreadList.h"
#include "Log.h"

NativeThreadList::NativeThreadList() :
    ServiceBase()
{
}

const char* NativeThreadList::GetName()
{
    return _serviceName;
}

bool NativeThreadList::StartImpl()
{
    // nothing special to start
    return true;
}

bool NativeThreadList::StopImpl()
{
    // nothing special to stop
    return true;
}

bool NativeThreadList::RegisterThread(uint32_t tid)
{
    std::lock_guard<std::mutex> lock(_mutex);

    auto [iterator, inserted] = _nativeThreadIds.emplace(tid);

    if (inserted)
    {
        Log::Debug("NativeThreadList: thread 0x", std::hex, tid, std::dec, " registered.");
        return true;
    }

    Log::Debug("NativeThreadList: thread 0x", std::hex, tid, std::dec, " was already registered.");
    return false;
}

bool NativeThreadList::Contains(uint32_t tid) const
{
    std::lock_guard<std::mutex> lock(_mutex);

    // NOTE: contains() does not seem to be available when compiled on ARM
    return _nativeThreadIds.find(tid) != _nativeThreadIds.end();
}