#include "GcDumpProvider.h"

GcDumpProvider::GcDumpProvider()
    :
    _session(::GetCurrentProcessId())
{
}

void GcDumpProvider::Trigger()
{
    _session.TriggerDump();
}

IGcDumpProvider::gcdump_t GcDumpProvider::Get()
{
    static int count = 0;

    IGcDumpProvider::gcdump_t gcdump;
    //gcdump.push_back(std::make_tuple<std::string, uint64_t, uint64_t>("System.String", 12 + count * 2, 12345));
    //gcdump.push_back(std::make_tuple<std::string, uint64_t, uint64_t>("Datadog.Trace", 42, 678));
    //gcdump.push_back(std::make_tuple<std::string, uint64_t, uint64_t>("System.Diagnostics.GCDump", 4, 9876 + count * 10));

    // trigger the GC and get the dump
    _session.TriggerDump();
    auto dump = _session.GetGcDumpState();
    auto types = dump->_types;
    for (auto& type : types)
    {
        auto typeInfo = type.second;
        auto name = typeInfo._name;

        uint64_t instancesCount = typeInfo._instances.size();
        uint64_t instancesSize = 0;
        for (size_t i = 0; i < instancesCount; i++)
        {
            instancesSize += typeInfo._instances[i]._size;
        }

        auto tuple = std::make_tuple<std::string, uint64_t, uint64_t>(name, instancesCount, instancesSize);
        gcdump.push_back(tuple);
    }

    count++;

    return gcdump;
}