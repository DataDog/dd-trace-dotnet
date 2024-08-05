// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include "DlPhdrInfoWrapper.h"

#include <libunwind.h>
#include <link.h>
#include <shared_mutex>
#include <vector>

class LibrariesInfoCache
{
public:
    static LibrariesInfoCache* Get();
    ~LibrariesInfoCache();

    LibrariesInfoCache(LibrariesInfoCache const &) = delete;
    LibrariesInfoCache& operator=(LibrariesInfoCache const&) = delete;

    LibrariesInfoCache(LibrariesInfoCache&&) = delete;
    LibrariesInfoCache& operator=(LibrariesInfoCache&&) = delete;

    void UpdateCache();

private:
    LibrariesInfoCache();

    static int DlIteratePhdr(unw_iterate_phdr_callback_t callback, void* data);
    int DlIteratePhdrImpl(unw_iterate_phdr_callback_t callback, void* data);

    std::shared_mutex _cacheLock;
    std::vector<DlPhdrInfoWrapper> LibrariesInfo;

    static LibrariesInfoCache* s_instance;
    unsigned long long NbCallsToDlopenDlclose;
};
