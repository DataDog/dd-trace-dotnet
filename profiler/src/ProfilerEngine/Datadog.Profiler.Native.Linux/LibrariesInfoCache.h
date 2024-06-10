// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "DlPhdrInfoWrapper.h"

#include <libunwind.h>
#include <link.h>
#include <vector>

struct LibrariesInfoCache
{
public:
    LibrariesInfoCache();
    ~LibrariesInfoCache();

    void UpdateCache();

private:
    static int CustomDlIteratePhdr(unw_iterate_phdr_callback_t callback, void* data);

    std::vector<DlPhdrInfoWrapper> LibrariesInfo;

    static LibrariesInfoCache* s_instance;
    unsigned int NbCallsToDlopenDlclose;
};
