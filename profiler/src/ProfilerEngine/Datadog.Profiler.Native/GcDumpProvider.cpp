#include "GcDumpProvider.h"

GcDumpProvider::GcDumpProvider()
{
}

IGcDumpProvider::gcdump_t GcDumpProvider::Get()
{
    static int count = 0;

    IGcDumpProvider::gcdump_t gcdump;

    // TODO: trigger the GC and wait for the end event

    gcdump.push_back(std::make_tuple<std::string, uint64_t, uint64_t>("System.String", 12 + count * 2, 12345));
    gcdump.push_back(std::make_tuple<std::string, uint64_t, uint64_t>("Datadog.Trace", 42, 678));
    gcdump.push_back(std::make_tuple<std::string, uint64_t, uint64_t>("System.Diagnostics.GCDump", 4, 9876 + count * 10));
    count++;

    return gcdump;
}