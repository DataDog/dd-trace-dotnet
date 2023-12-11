// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <iomanip>
#include <iostream>

#include <windows.h>

#include "..\..\ProfilerEngine\Datadog.Profiler.Native\GcDump.h"
#include "..\..\ProfilerEngine\Datadog.Profiler.Native\IGcDumpProvider.h"


// -pid : pid
void ParseCommandLine(int argc, wchar_t* argv[], DWORD& pid)
{
    pid = -1;

    for (int i = 0; i < argc; i++)
    {
        if (lstrcmp(argv[i], L"-pid") == 0)
        {
            if (i + 1 == argc)
                return;
            i++;

            pid = wcstol(argv[i], nullptr, 10);
        }
    }
}

bool GetGcDump(int pid, IGcDumpProvider::gcdump_t& gcDump)
{
    GcDump gcd(pid);
    if (!gcd.TriggerDump())
    {
        return false;
    }

    auto const& dump = gcd.GetGcDumpState();
    auto& types = dump._types;
    for (auto& type : types)
    {
        auto& typeInfo = type.second;

        uint64_t instancesCount = typeInfo._instances.size();
        uint64_t instancesSize = 0;
        for (size_t i = 0; i < instancesCount; i++)
        {
            instancesSize += typeInfo._instances[i]._size;
        }

        gcDump.push_back({typeInfo._name, instancesCount, instancesSize});
    }

    return true;
}


int wmain(int argc, wchar_t* argv[])
{
    DWORD pid = -1;
    ParseCommandLine(argc, argv, pid);
    if (pid == -1)
    {
        std::cout << "Missing -pid <pid>...\n";
        return -1;
    }


    std::cout << "Press x to stop and anything else to trigger a dump...\n\n";
    IGcDumpProvider::gcdump_t gcDump;
    while (true)
    {
        int input = std::getchar();
        if (input == 'x')
        {
            return 0;
        }

        gcDump.clear();
        if (!GetGcDump(pid, gcDump))
        {
            std::cout << "Failed to trigger GC dump...\n";
            return -1;
        }
    }

    for (auto& ti : gcDump)
    {
        std::cout << std::setfill(' ') << std::setw(9) << std::get<1>(ti) << std::setw(12) << std::get<2>(ti) << "  " << std::get<0>(ti) << std::endl;
    }

    std::cout << "Exit application\n\n";
    std::cout << "\n";
}

