#include "GcDumpProvider.h"

GcDumpProvider::GcDumpProvider()
{
}

bool GcDumpProvider::Get(IGcDumpProvider::gcdump_t& gcDump)
{
    static int count = 0;

    // trigger the GC and get the dump
    GcDump gcd(::GetCurrentProcessId());
    gcd.TriggerDump();

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

        //auto tuple = std::make_tuple<std::string, uint64_t, uint64_t>(std::move(typeInfo._name), std::move(instancesCount), std::move(instancesSize));
        //auto tuple = std::tuple<std::string, uint64_t, uint64_t>(std::move(typeInfo._name), instancesCount, instancesSize);
        gcDump.push_back({typeInfo._name, instancesCount, instancesSize});
    }

    count++;

    return true;
}