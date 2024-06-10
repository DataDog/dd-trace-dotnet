// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibrariesInfoCache.h"

LibrariesInfoCache* LibrariesInfoCache::s_instance = nullptr;

// This function is exposed by the ld_preload wrapper library.
// This allows us to know if we have to reload the cache or not
extern "C" unsigned long long dd_nb_calls_to_dlopen_dlclose() __attribute__((weak));

LibrariesInfoCache::LibrariesInfoCache()
{
    LibrariesInfo.reserve(100);
    NbCallsToDlopenDlclose = 0;
    s_instance = this;
    unw_set_iterate_phdr_function(unw_local_addr_space, LibrariesInfoCache::CustomDlIteratePhdr);
}

LibrariesInfoCache::~LibrariesInfoCache()
{
    unw_set_iterate_phdr_function(unw_local_addr_space, dl_iterate_phdr);
    s_instance = nullptr;
}

void LibrariesInfoCache::UpdateCache()
{
    auto nbCallsToDlopenDlclose = dd_nb_calls_to_dlopen_dlclose != nullptr ? dd_nb_calls_to_dlopen_dlclose() : NbCallsToDlopenDlclose;
    if (nbCallsToDlopenDlclose != NbCallsToDlopenDlclose)
    {
        NbCallsToDlopenDlclose = nbCallsToDlopenDlclose;
        LibrariesInfo.clear();
        dl_iterate_phdr([](struct dl_phdr_info* info, std::size_t size, void* data) {
            auto* instance = static_cast<LibrariesInfoCache*>(data);
            instance->LibrariesInfo.push_back(DlPhdrInfoWrapper(info, size));
            return 0;
        },
                        this);
    }
}

int LibrariesInfoCache::CustomDlIteratePhdr(unw_iterate_phdr_callback_t callback, void* data)
{
    int rc = 0;
    auto* instance = s_instance;
    if (instance == nullptr)
    {
        return rc;
    }

    for (auto& wrappedInfo : instance->LibrariesInfo)
    {
        auto [info, size] = wrappedInfo.Get();
        rc = callback(info, size, data);
        if (rc != 0)
        {
            break;
        }
    }
    return rc;
}