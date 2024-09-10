// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include "DlPhdrInfoWrapper.h"

#include "ServiceBase.h"

#include <libunwind.h>
#include <link.h>
#include <shared_mutex>
#include <thread>
#include <vector>

class LibrariesInfoCache : public ServiceBase
{
public:
    LibrariesInfoCache();
    ~LibrariesInfoCache();

    LibrariesInfoCache(LibrariesInfoCache const &) = delete;
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
    void NotifyCacheUpdateImpl();
    void Work();

    std::shared_mutex _cacheLock;
    std::vector<DlPhdrInfoWrapper> LibrariesInfo;

    static LibrariesInfoCache* s_instance;
    std::thread _worker;
    bool _stopRequested;
    std::atomic_flag _shouldReload;
};
