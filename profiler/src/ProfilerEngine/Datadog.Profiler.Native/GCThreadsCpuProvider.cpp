// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GCThreadsCpuProvider.h"

#include "CpuTimeProvider.h"
#include "Log.h"
#include "OsSpecificApi.h"
#include "RawCpuSample.h"

GCThreadsCpuProvider::GCThreadsCpuProvider(CpuTimeProvider* cpuTimeProvider, MetricsRegistry& metricsRegistry) :
    NativeThreadsCpuProviderBase(cpuTimeProvider)
{
    _cpuDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_gc_cpu_duration");
}

const char* GCThreadsCpuProvider::GetName()
{
    return "Garbage Collector Threads CPU provider";
}

bool GCThreadsCpuProvider::IsGcThread(std::shared_ptr<IThreadInfo> const& thread)
{
    static shared::WSTRING GcServerThread = WStr(".NET Server GC");
    static shared::WSTRING GcBackgroundServerThread = WStr(".NET BGC");

    auto const& name = thread->GetThreadName();
    return name == GcServerThread || name == GcBackgroundServerThread;
}

std::vector<std::shared_ptr<IThreadInfo>> const& GCThreadsCpuProvider::GetThreads()
{
    Log::Debug("Get all native threads of the current process");

    if (!_gcThreads.empty())
    {
        return _gcThreads;
    }

    // we may want to allow checking for process time or if managed thread have been created
    if (_number_of_attempts > 2)
    {
        static bool alreadyLogged = false;
        if (!alreadyLogged)
        {
            alreadyLogged = true;
            Log::Debug("Failed at retrieving GC threads after ", _number_of_attempts, " of attempts");
        }
        return _gcThreads;
    }

    _number_of_attempts++;

    for (auto const& threadInfo : OsSpecificApi::GetProcessThreads())
    {
        if (IsGcThread(threadInfo))
        {
            Log::Debug("Found GC threads. Name: ", threadInfo->GetThreadName(), ", ID: ", threadInfo->GetOsThreadId());
            _gcThreads.push_back(threadInfo);
        }
    }

    Log::Debug("Collected ", _gcThreads.size(), " GC threads.");
    return _gcThreads;
}

std::vector<FrameInfoView> GCThreadsCpuProvider::GetFrames()
{
    return
    {
        {"", "|lm:[native] GC |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:", "", 0},
        {"", "|lm:[native] CLR |ns: |ct: |cg: |fn:.NET |fg: |sg:", "", 0}
    };
}

void GCThreadsCpuProvider::OnCpuDuration(std::uint64_t cpuTime)
{
    _cpuDurationMetric->Add(static_cast<double>(cpuTime));
}