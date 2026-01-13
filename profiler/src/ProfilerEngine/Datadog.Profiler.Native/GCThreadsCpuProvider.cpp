// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GCThreadsCpuProvider.h"

#include "Log.h"
#include "OsSpecificApi.h"
#include "RawSampleTransformer.h"

#include "shared/src/native-src/string.h"
#include "shared/src/native-src/util.h"

GCThreadsCpuProvider::GCThreadsCpuProvider(SampleValueTypeProvider& valueTypeProvider, RawSampleTransformer* cpuSampleTransformer, MetricsRegistry& metricsRegistry) :
    NativeThreadsCpuProviderBase(valueTypeProvider, cpuSampleTransformer)
{
    _cpuDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_gc_cpu_duration");
}

const char* GCThreadsCpuProvider::GetName()
{
    return "Garbage Collector Threads CPU provider";
}

GCMode GCThreadsCpuProvider::GetMode()
{
    // Based on https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector#workstation-vs-server
    // Note:
    //  - DOTNET_gcServer overrides COMPlus_gcServer
    //  - env var override config files
    auto gcServerEnvValue = shared::Trim(shared::GetEnvironmentValue(WStr("DOTNET_gcServer")));
    if (!gcServerEnvValue.empty())
    {
        if ((gcServerEnvValue == WStr("1")) || (gcServerEnvValue == WStr("0x1")))
        {
            return GCMode::Server;
        }
        else // all other values are considered as 0
        {
            return GCMode::Workstation;
        }
    }

    gcServerEnvValue = shared::Trim(shared::GetEnvironmentValue(WStr("COMPlus_gcServer")));
    if (!gcServerEnvValue.empty())
    {
        if ((gcServerEnvValue == WStr("1")) || (gcServerEnvValue == WStr("0x1")))
        {
            return GCMode::Server;
        }
        else // all other values are considered as 0
        {
            return GCMode::Workstation;
        }
    }

    // TODO: check if we can directy call the exported function by the CLR
    //   [DllImport("QCall", EntryPoint = "GCInterface_EnumerateConfigurationValues", ExactSpelling = true)]
    //   [LibraryImport("QCall", EntryPoint = "GCInterface_EnumerateConfigurationValues")]
    //   internal unsafe static extern void _EnumerateConfigurationValues(void* configurationDictionary, delegate* unmanaged<void*, void*, void*, GCConfigurationType, long, void> callback);
    //
    // Note: server mode can be enable via config files so we need to check the GC threads
    // ensure that we have attempted to get the GC threads at least once
    if (_number_of_attempts == 0)
    {
        GetThreads();
    }

    return _gcThreads.empty() ? GCMode::Workstation : GCMode::Server;
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
        LogOnce(Debug, "Failed at retrieving GC threads after ", _number_of_attempts, " of attempts");
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

Labels GCThreadsCpuProvider::GetLabels()
{
    return Labels{StringLabel{"gc_cpu_sample", "true"}};
}

std::vector<FrameInfoView> GCThreadsCpuProvider::GetFrames()
{
    return
    {
        {"", "|lm:[native] GC |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:", "", 0},
        {"", "|lm:[native] CLR |ns: |ct: |cg: |fn:.NET |fg: |sg:", "", 0}
    };
}

void GCThreadsCpuProvider::OnCpuDuration(std::chrono::milliseconds cpuTime)
{
    _cpuDurationMetric->Add(static_cast<double>(cpuTime.count()));
}