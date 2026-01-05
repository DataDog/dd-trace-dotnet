// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include "DlPhdrInfoWrapper.h"

#include "AutoResetEvent.h"
#include "MemoryResourceManager.h"
#include "ServiceBase.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <atomic>
#include <libunwind.h>
#include <link.h>
#include <memory>
#include <shared_mutex>
#include <thread>
#include <vector>

class LibrariesInfoCache : public ServiceBase
{
public:
    LibrariesInfoCache(shared::pmr::memory_resource* resource);
    ~LibrariesInfoCache();

    LibrariesInfoCache(LibrariesInfoCache const&) = delete;
    LibrariesInfoCache& operator=(LibrariesInfoCache const&) = delete;

    LibrariesInfoCache(LibrariesInfoCache&&) = delete;
    LibrariesInfoCache& operator=(LibrariesInfoCache&&) = delete;

    const char* GetName() final override;

protected:
    bool StartImpl() final override;
    bool StopImpl() final override;

private:
    static int DlIteratePhdr(unw_iterate_phdr_callback_t callback, void* data);
    static void NotifyCacheUpdate();

    void UpdateCache();
    int DlIteratePhdrImpl(unw_iterate_phdr_callback_t callback, void* data);
#ifdef DD_TEST
public:
#endif
    void NotifyCacheUpdateImpl();
#ifdef DD_TEST
private:
#endif
    void Work(std::shared_ptr<AutoResetEvent> startEvent);

    static std::atomic<LibrariesInfoCache*> s_instance;

    std::shared_mutex _cacheLock;
    std::vector<DlPhdrInfoWrapper> _librariesInfo;

    std::thread _worker;
    std::atomic<bool> _stopRequested;
    AutoResetEvent _event;
    shared::pmr::memory_resource* _wrappersAllocator;
};
