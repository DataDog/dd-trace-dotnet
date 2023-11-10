// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "CorProfilerCallback.h"

#include <inttypes.h>

#ifdef _WINDOWS
#include <VersionHelpers.h>
#include <windows.h>
#else
#include "cgroup.h"
#include <signal.h>
#include <libunwind.h>
#endif

#include "AllocationsProvider.h"
#include "AppDomainStore.h"
#include "ApplicationStore.h"
#include "ClrEventsParser.h"
#include "ClrLifetime.h"
#include "Configuration.h"
#include "ContentionProvider.h"
#include "CpuTimeProvider.h"
#include "DebugInfoStore.h"
#include "EnabledProfilers.h"
#include "EnvironmentVariables.h"
#include "ExceptionsProvider.h"
#include "FrameStore.h"
#include "GCThreadsCpuProvider.h"
#include "IMetricsSender.h"
#include "IMetricsSenderFactory.h"
#include "ProfileExporter.h"
#include "Log.h"
#include "ManagedThreadList.h"
#include "OpSysTools.h"
#include "OsSpecificApi.h"
#include "ProfilerEngineStatus.h"
#include "RuntimeIdStore.h"
#include "RuntimeInfo.h"
#include "Sample.h"
#include "SampleValueTypeProvider.h"
#include "StackSamplerLoopManager.h"
#include "ThreadsCpuManager.h"
#include "WallTimeProvider.h"
#include "AllocationsRecorder.h"
#include "MetadataProvider.h"
#ifdef LINUX
#include "SystemCallsShield.h"
#endif

#include "shared/src/native-src/environment_variables.h"
#include "shared/src/native-src/pal.h"
#include "shared/src/native-src/string.h"

#include "dd_profiler_version.h"


IClrLifetime* CorProfilerCallback::GetClrLifetime() const
{
    return _pClrLifetime.get();
}

// This can be used to detect profiler version in a memory dump
// in WinDbg:  dt Datadog_Profiler_Native!Profiler_Version
// in gdb: p Profiler_Version
#ifdef _WINDOWS
extern "C" __declspec(dllexport) const char* Profiler_Version = PROFILER_VERSION;
#else
extern "C" __attribute__((visibility("default"))) const char* Profiler_Version = PROFILER_VERSION;
#endif


// Initialization
CorProfilerCallback* CorProfilerCallback::_this = nullptr;

CorProfilerCallback::CorProfilerCallback()
{
    // Keep track of the one and only ICorProfilerCallback implementation.
    // It will be used as root for other services
    _this = this;

    _pClrLifetime = std::make_unique<ClrLifetime>(&_isInitialized);

#ifndef _WINDOWS
    CGroup::Initialize();
#endif
}

// Cleanup
CorProfilerCallback::~CorProfilerCallback()
{
    DisposeInternal();

    _this = nullptr;

#ifndef _WINDOWS
    CGroup::Cleanup();
#endif
}

bool CorProfilerCallback::InitializeServices()
{
    _metricsSender = IMetricsSenderFactory::Create();

    _pAppDomainStore = std::make_unique<AppDomainStore>(_pCorProfilerInfo);

    _pDebugInfoStore = std::make_unique<DebugInfoStore>(_pCorProfilerInfo, _pConfiguration.get());

#ifdef LINUX
    if (_pConfiguration->IsSystemCallsShieldEnabled())
    {
        // This service must be started before StackSamplerLoop-based profilers to help with non-restartable system calls (ex: socket operations)
        _systemCallsShield = RegisterService<SystemCallsShield>(_pConfiguration.get());
    }
#endif

    _pFrameStore = std::make_unique<FrameStore>(_pCorProfilerInfo, _pConfiguration.get(), _pDebugInfoStore.get());

    // Create service instances
    _pThreadsCpuManager = RegisterService<ThreadsCpuManager>();

    _pManagedThreadList = RegisterService<ManagedThreadList>(_pCorProfilerInfo);
    _managedThreadsMetric = _metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_managed_threads", [this]() {
        return _pManagedThreadList->Count();
    });

    _pCodeHotspotsThreadList = RegisterService<ManagedThreadList>(_pCorProfilerInfo);
    _managedThreadsWithContextMetric = _metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_managed_threads_with_context", [this]() {
        return _pCodeHotspotsThreadList->Count();
    });

    auto* pRuntimeIdStore = RegisterService<RuntimeIdStore>();

    auto valueTypeProvider = SampleValueTypeProvider();

    if (_pConfiguration->IsThreadLifetimeEnabled())
    {
        _pThreadLifetimeProvider = RegisterService<ThreadLifetimeProvider>(
            valueTypeProvider,
            _pFrameStore.get(),
            _pThreadsCpuManager,
            _pAppDomainStore.get(),
            pRuntimeIdStore,
            _pConfiguration.get());
    }

    if (_pConfiguration->IsWallTimeProfilingEnabled())
    {
        _pWallTimeProvider = RegisterService<WallTimeProvider>(valueTypeProvider, _pThreadsCpuManager, _pFrameStore.get(), _pAppDomainStore.get(), pRuntimeIdStore, _pConfiguration.get());
    }

    if (_pConfiguration->IsCpuProfilingEnabled())
    {
        _pCpuTimeProvider = RegisterService<CpuTimeProvider>(valueTypeProvider, _pThreadsCpuManager, _pFrameStore.get(), _pAppDomainStore.get(), pRuntimeIdStore, _pConfiguration.get());
    }

    if (_pConfiguration->IsExceptionProfilingEnabled())
    {
        _pExceptionsProvider = RegisterService<ExceptionsProvider>(
            valueTypeProvider,
            _pCorProfilerInfo,
            _pManagedThreadList,
            _pFrameStore.get(),
            _pConfiguration.get(),
            _pThreadsCpuManager,
            _pAppDomainStore.get(),
            pRuntimeIdStore,
            _metricsRegistry);
    }

    // _pCorProfilerInfoEvents must have been set for any CLR events-based profiler to work
    if (_pCorProfilerInfoEvents != nullptr)
    {
        // live objects profiling requires allocations profiling
        if (_pConfiguration->IsHeapProfilingEnabled())
        {
            if (_pCorProfilerInfoLiveHeap != nullptr)
            {
                _pLiveObjectsProvider = RegisterService<LiveObjectsProvider>(
                    valueTypeProvider,
                    _pCorProfilerInfoLiveHeap,
                    _pManagedThreadList,
                    _pFrameStore.get(),
                    _pThreadsCpuManager,
                    _pAppDomainStore.get(),
                    pRuntimeIdStore,
                    _pConfiguration.get(),
                    _metricsRegistry);

                _pAllocationsProvider = RegisterService<AllocationsProvider>(
                    valueTypeProvider,
                    _pCorProfilerInfo,
                    _pManagedThreadList,
                    _pFrameStore.get(),
                    _pThreadsCpuManager,
                    _pAppDomainStore.get(),
                    pRuntimeIdStore,
                    _pConfiguration.get(),
                    _pLiveObjectsProvider,
                    _metricsRegistry
                    );

                if (!_pConfiguration->IsAllocationProfilingEnabled())
                {
                    Log::Warn("Allocations profiling is enabled due to activated live objects profiling.");
                }
            }
            else
            {
                Log::Warn("Live Heap profiling is disabled: .NET 7+ is required.");
            }
        }

        // check for allocations profiling only (without heap profiling)
        if (_pConfiguration->IsAllocationProfilingEnabled() && (_pAllocationsProvider == nullptr))
        {
            _pAllocationsProvider = RegisterService<AllocationsProvider>(
                valueTypeProvider,
                _pCorProfilerInfo,
                _pManagedThreadList,
                _pFrameStore.get(),
                _pThreadsCpuManager,
                _pAppDomainStore.get(),
                pRuntimeIdStore,
                _pConfiguration.get(),
                nullptr, // no listener
                _metricsRegistry
                );
        }

        if (_pConfiguration->IsContentionProfilingEnabled())
        {
            _pContentionProvider = RegisterService<ContentionProvider>(
                valueTypeProvider,
                _pCorProfilerInfo,
                _pManagedThreadList,
                _pFrameStore.get(),
                _pThreadsCpuManager,
                _pAppDomainStore.get(),
                pRuntimeIdStore,
                _pConfiguration.get(),
                _metricsRegistry
                );
        }

        if (_pConfiguration->IsGarbageCollectionProfilingEnabled())
        {
            _pStopTheWorldProvider = RegisterService<StopTheWorldGCProvider>(
                valueTypeProvider,
                _pFrameStore.get(),
                _pThreadsCpuManager,
                _pAppDomainStore.get(),
                pRuntimeIdStore,
                _pConfiguration.get()
                );
            _pGarbageCollectionProvider = RegisterService<GarbageCollectionProvider>(
                valueTypeProvider,
                _pFrameStore.get(),
                _pThreadsCpuManager,
                _pAppDomainStore.get(),
                pRuntimeIdStore,
                _pConfiguration.get(),
                _metricsRegistry
                );
        }
        else
        {
            _pStopTheWorldProvider = nullptr;
            _pGarbageCollectionProvider = nullptr;
        }

        // TODO: add new CLR events-based providers to the event parser
        _pClrEventsParser = std::make_unique<ClrEventsParser>(
            _pCorProfilerInfoEvents,
            _pAllocationsProvider,
            _pContentionProvider,
            _pStopTheWorldProvider
            );

        if (_pGarbageCollectionProvider != nullptr)
        {
            _pClrEventsParser->Register(_pGarbageCollectionProvider);
        }
        if (_pLiveObjectsProvider != nullptr)
        {
            _pClrEventsParser->Register(_pLiveObjectsProvider);
        }
        // TODO: register any provider that needs to get notified when GCs start and end
    }

    if (_pConfiguration->IsAllocationRecorderEnabled() && !_pConfiguration->GetProfilesOutputDirectory().empty())
    {
        _pAllocationsRecorder = std::make_unique<AllocationsRecorder>(_pCorProfilerInfo, _pFrameStore.get());
    }

    // Avoid iterating twice on all providers in order to inject this value in each constructor
    // and store it in CollectorBase so it can be used in TransformRawSample (where the sample is created)
    auto const& sampleTypeDefinitions = valueTypeProvider.GetValueTypes();
    Sample::ValuesCount = sampleTypeDefinitions.size();

    // compute enabled profilers based on configuration and receivable CLR events
    _pEnabledProfilers = std::make_unique<EnabledProfilers>(_pConfiguration.get(), _pCorProfilerInfoEvents != nullptr, _pLiveObjectsProvider != nullptr);

    _pStackSamplerLoopManager = RegisterService<StackSamplerLoopManager>(
        _pCorProfilerInfo,
        _pConfiguration.get(),
        _metricsSender,
        _pClrLifetime.get(),
        _pThreadsCpuManager,
        _pManagedThreadList,
        _pCodeHotspotsThreadList,
        _pWallTimeProvider,
        _pCpuTimeProvider,
        _metricsRegistry);

    _pApplicationStore = RegisterService<ApplicationStore>(_pConfiguration.get());

    // The different elements of the libddprof pipeline are created and linked together
    // i.e. the exporter is passed to the aggregator and each provider is added to the aggregator.
    _pExporter = std::make_unique<ProfileExporter>(
        sampleTypeDefinitions,
        _pConfiguration.get(),
        _pApplicationStore,
        _pRuntimeInfo.get(),
        _pEnabledProfilers.get(),
        _metricsRegistry,
        _pMetadataProvider.get(),
        _pAllocationsRecorder.get()
        );

    if (_pConfiguration->IsGcThreadsCpuTimeEnabled() &&
        _pCpuTimeProvider != nullptr &&
        _pRuntimeInfo->GetDotnetMajorVersion() >= 5)
    {
        _gcThreadsCpuProvider = std::make_unique<GCThreadsCpuProvider>(_pCpuTimeProvider, _metricsRegistry);

        _pExporter->RegisterProcessSamplesProvider(_gcThreadsCpuProvider.get());
    }

    if (_pContentionProvider != nullptr)
    {
        _pExporter->RegisterUpscaleProvider(_pContentionProvider);
    }
    if (_pExceptionsProvider != nullptr)
    {
        _pExporter->RegisterUpscaleProvider(_pExceptionsProvider);
    }

    _pSamplesCollector = RegisterService<SamplesCollector>(
        _pConfiguration.get(),
        _pThreadsCpuManager,
        _pExporter.get(),
        _metricsSender.get());

    if (_pConfiguration->IsThreadLifetimeEnabled())
    {
        _pSamplesCollector->Register(_pThreadLifetimeProvider);
    }

    if (_pConfiguration->IsWallTimeProfilingEnabled())
    {
        _pSamplesCollector->Register(_pWallTimeProvider);
    }

    if (_pConfiguration->IsCpuProfilingEnabled())
    {
        _pSamplesCollector->Register(_pCpuTimeProvider);
    }

    if (_pConfiguration->IsExceptionProfilingEnabled())
    {
        _pSamplesCollector->Register(_pExceptionsProvider);
    }

    // CLR events-based providers require ICorProfilerInfo12 (stored in _pCorProfilerInfoEvents).
    // If not set, no need to even check the configuration
    if (_pCorProfilerInfoEvents != nullptr)
    {
        if (_pConfiguration->IsAllocationProfilingEnabled())
        {
            _pSamplesCollector->Register(_pAllocationsProvider);
        }

        // Live heap profiling required .NET 7+ and ICorProfilerInfo13 in _pCorProfilerInfoLiveHeap
        // --> _pLiveObjectsProvider is null otherwise
        if (_pConfiguration->IsHeapProfilingEnabled() && (_pLiveObjectsProvider != nullptr))
        {
            _pSamplesCollector->RegisterBatchedProvider(_pLiveObjectsProvider);
        }

        if (_pConfiguration->IsContentionProfilingEnabled())
        {
            _pSamplesCollector->Register(_pContentionProvider);
        }

        if (_pConfiguration->IsGarbageCollectionProfilingEnabled())
        {
            _pSamplesCollector->Register(_pStopTheWorldProvider);
            _pSamplesCollector->Register(_pGarbageCollectionProvider);
        }
    }

    auto started = StartServices();
    if (!started)
    {
        Log::Error("One or multiple services failed to start. Stopping all services.");
        StopServices();
    }

    return started;
}

bool CorProfilerCallback::StartServices()
{
    bool result = true;
    bool success = true;

    for (auto const& service : _services)
    {
        auto name = service->GetName();
        success = service->Start();
        if (success)
        {
            Log::Info(name, " started successfully.");
        }
        else
        {
            Log::Error(name, " failed to start.");
        }
        result &= success;
    }

    return result;
}

bool CorProfilerCallback::DisposeServices()
{
    bool result = StopServices();

    // Delete all services instances in reverse order of their creation
    // to avoid using a deleted service...

    _services.clear();

    _pThreadsCpuManager = nullptr;
    _pStackSamplerLoopManager = nullptr;
    _pManagedThreadList = nullptr;
    _pCodeHotspotsThreadList = nullptr;
    _pApplicationStore = nullptr;

    return result;
}

bool CorProfilerCallback::StopServices()
{
    // We need to destroy items here in the reverse order to what they have
    // been initialized in the Initialize() method.
    bool result = true;
    bool success = true;

    // stop all services
    for (size_t i = _services.size(); i > 0; i--)
    {
        const auto& service = _services[i - 1];
        const auto* name = service->GetName();
        success = service->Stop();
        if (success)
        {
            Log::Info(name, " stopped successfully.");
        }
        else
        {
            Log::Error(name, " failed to stop.");
        }
        result &= success;
    }

    return result;
}

void CorProfilerCallback::DisposeInternal()
{
    // This method is called from the destructor as well as from the Shutdown callback.
    // Most operations here are idempotent - calling them multiple time is benign.
    // However, we defensively use an additional flag to make this entire method idempotent as a whole.

    // Note the race here. _isInitialized protects DisposeInternal() from being called multiple times sequentially.
    // It does NOT protect against concurrent invocations!

    bool isInitialized = _isInitialized.load();

    Log::Info("CorProfilerCallback::DisposeInternal() invoked. _isInitialized = ", isInitialized);

    if (isInitialized)
    {
        ProfilerEngineStatus::WriteIsProfilerEngineActive(false);
        _isInitialized.store(false);

        // From that time, we need to ensure that ALL native threads are stop and don't call back to managed world
        // So, don't sleep before stopping the threads

        DisposeServices();

        // Don't forget to stop the CLR events session if any
        auto* pInfo = _pCorProfilerInfoEvents;
        if (pInfo != nullptr)
        {
            if (_session != 0)
            {
                pInfo->EventPipeStopSession(_session);
                _session = 0;
            }

            pInfo->Release();
            _pCorProfilerInfoEvents = nullptr;
        }

        ICorProfilerInfo5* pCorProfilerInfo = _pCorProfilerInfo;
        if (pCorProfilerInfo != nullptr)
        {
            pCorProfilerInfo->Release();
            _pCorProfilerInfo = nullptr;
        }

        // So we are about to turn off the Native Profiler Engine.
        // We signaled that to the anyone who is interested (e.g. the TraceContextTracking library) using ProfilerEngineStatus::WriteIsProfilerEngineActive(..),
        // which included flushing thread buffers. However, it is possible that a reader of ProfilerEngineStatus::GetReadPtrIsProfilerEngineActive()
        // (e.g. the TraceContextTracking library) just started doing something that requires the engine to be present.
        // Engine unloads are extremely rare, and so components are not expected synchronize with a potential unload process.
        // So we will now pause the unload process and allow other threads to run for a long time (hundreds of milliseconds).
        // This will allow any users of the Engine who missed the above WriteIsProfilerEngineActive(..) notification to finsh doing whatecer they are doing.
        // This is not a guarantee, but it makes problems extremely unlikely.
        constexpr std::chrono::microseconds EngineShutdownSafetyPeriodMS = std::chrono::microseconds(500);

        Log::Debug("CorProfilerCallback::DisposeInternal(): Starting a pause of ",
                   EngineShutdownSafetyPeriodMS.count(), " millisecs to allow ongoing Profiler Engine usage operations to complete.");

        std::this_thread::sleep_for(EngineShutdownSafetyPeriodMS);

        Log::Debug("CorProfilerCallback::DisposeInternal():  Pause completed.");
    }
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::QueryInterface(REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr)
    {
        return E_POINTER;
    }

    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICorProfilerCallback) // 176FBED1-A55C-4796-98CA-A9DA0EF883E7
        || riid == __uuidof(ICorProfilerCallback2)                           // 8A8CC829-CCF2-49fe-BBAE-0F022228071A
        || riid == __uuidof(ICorProfilerCallback3)                           // 4FD2ED52-7731-4b8d-9469-03D2CC3086C5
        || riid == __uuidof(ICorProfilerCallback4)                           // 7B63B2E3-107D-4d48-B2F6-F61E229470D2
        || riid == __uuidof(ICorProfilerCallback5)                           // 8DFBA405-8C9F-45F8-BFFA-83B14CEF78B5
        || riid == __uuidof(ICorProfilerCallback6)                           // FC13DF4B-4448-4F4F-950C-BA8D19D00C36
        || riid == __uuidof(ICorProfilerCallback7)                           // F76A2DBA-1D52-4539-866C-2AA518F9EFC3
        || riid == __uuidof(ICorProfilerCallback8)                           // 5BED9B15-C079-4D47-BFE2-215A140C07E0
        || riid == __uuidof(ICorProfilerCallback9)                           // 27583EC3-C8F5-482F-8052-194B8CE4705A
        || riid == __uuidof(ICorProfilerCallback10))                         // CEC5B60E-C69C-495F-87F6-84D28EE16FFB
    {
        *ppvObject = this;
        this->AddRef();

        return S_OK;
    }

    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE CorProfilerCallback::AddRef()
{
    ULONG refCount = _refCount.fetch_add(1) + 1;
    return refCount;
}

ULONG STDMETHODCALLTYPE CorProfilerCallback::Release()
{
    ULONG refCount = _refCount.fetch_sub(1) - 1;

    if (refCount == 0)
    {
        delete this;
    }

    return refCount;
}

ULONG STDMETHODCALLTYPE CorProfilerCallback::GetRefCount() const
{
    ULONG refCount = _refCount.load();
    return refCount;
}

void CorProfilerCallback::InspectRuntimeCompatibility(IUnknown* corProfilerInfoUnk, uint16_t& runtimeMajor, uint16_t& runtimeMinor)
{
    runtimeMajor = 0;
    runtimeMinor = 0;
    IUnknown* tstVerProfilerInfo;
    if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo13), (void**)&tstVerProfilerInfo))
    {
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo13 available. Profiling API compatibility: .NET Core 7.0 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo12), (void**)&tstVerProfilerInfo))
    {
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo12 available. Profiling API compatibility: .NET Core 5.0 or later."); // could be 6 too
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo11), (void**)&tstVerProfilerInfo))
    {
        runtimeMajor = 3;
        runtimeMinor = 1;
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo11 available. Profiling API compatibility: .NET Core 3.1 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo10), (void**)&tstVerProfilerInfo))
    {
        runtimeMajor = 3;
        runtimeMinor = 0;
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo10 available. Profiling API compatibility: .NET Core 3.0 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo9), (void**)&tstVerProfilerInfo))
    {
        runtimeMajor = 2;
        runtimeMinor = 1; // could also be 2.2
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo9 available. Profiling API compatibility: .NET Core 2.1 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo8), (void**)&tstVerProfilerInfo))
    {
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo8 available. Profiling API compatibility: .NET Fx 4.7.2 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo7), (void**)&tstVerProfilerInfo))
    {
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo7 available. Profiling API compatibility: .NET Fx 4.6.1 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo6), (void**)&tstVerProfilerInfo))
    {
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo6 available. Profiling API compatibility: .NET Fx 4.6 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo5), (void**)&tstVerProfilerInfo))
    {
        Log::Info("ICorProfilerInfo5 available. Profiling API compatibility: .NET Fx 4.5.2 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo4), (void**)&tstVerProfilerInfo))
    {
        Log::Info("ICorProfilerInfo4 available. Profiling API compatibility: .NET Fx 4.5 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo3), (void**)&tstVerProfilerInfo))
    {
        Log::Info("ICorProfilerInfo3 available. Profiling API compatibility: .NET Fx 4.0 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo2), (void**)&tstVerProfilerInfo))
    {
        Log::Info("ICorProfilerInfo2 available. Profiling API compatibility: .NET Fx 2.0 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo), (void**)&tstVerProfilerInfo))
    {
        Log::Info("ICorProfilerInfo available. Profiling API compatibility: .NET Fx 2 or later.");
        tstVerProfilerInfo->Release();
    }
    else
    {
        Log::Error("No ICorProfilerInfoXxx available.");
    }
}

const char* CorProfilerCallback::SysInfoProcessorArchitectureToStr(WORD wProcArch)
{
    switch (wProcArch)
    {
        case PROCESSOR_ARCHITECTURE_AMD64:
            return "x64 AMD or Intel";
        case PROCESSOR_ARCHITECTURE_ARM:
            return "ARM";
        case PROCESSOR_ARCHITECTURE_ARM64:
            return "ARM64";
        case PROCESSOR_ARCHITECTURE_IA64:
            return "Intel Itanium-based";
        case PROCESSOR_ARCHITECTURE_INTEL:
            return "x86";
        default:
            return "Unknown architecture";
    }
}

void CorProfilerCallback::InspectProcessorInfo()
{
#ifdef _WINDOWS
    BOOL isWow64Process;
    if (IsWow64Process(GetCurrentProcess(), &isWow64Process))
    {
        Log::Info("IsWow64Process : ", (isWow64Process ? "True" : "False"));
    }

    Log::Info("IsWindowsServer: ", (IsWindowsServer() ? "True" : "False"), ".");

    SYSTEM_INFO systemInfo;
    GetNativeSystemInfo(&systemInfo);
    Log::Info("GetNativeSystemInfo results:"
              " wProcessorArchitecture=\"",
              SysInfoProcessorArchitectureToStr(systemInfo.wProcessorArchitecture), "\"",
              "(=", systemInfo.wProcessorArchitecture, ")",
              ", dwPageSize=", systemInfo.dwPageSize,
              ", lpMinimumApplicationAddress=0x", std::setw(16), std::setfill('0'), std::hex, systemInfo.lpMinimumApplicationAddress,
              ", lpMaximumApplicationAddress=0x", std::setw(16), std::setfill('0'), std::hex, systemInfo.lpMaximumApplicationAddress,
              ", dwActiveProcessorMask=0x", std::hex, systemInfo.dwActiveProcessorMask, std::dec,
              ", dwNumberOfProcessors=", systemInfo.dwNumberOfProcessors,
              ", dwProcessorType=", std::dec, systemInfo.dwProcessorType,
              ", dwAllocationGranularity=", systemInfo.dwAllocationGranularity,
              ", wProcessorLevel=", systemInfo.wProcessorLevel,
              ", wProcessorRevision=", std::dec, systemInfo.wProcessorRevision);

#else
    // Running under non-Windows OS. Inspect Processor Info is currently not supported
#endif
}

void CorProfilerCallback::InspectRuntimeVersion(ICorProfilerInfo5* pCorProfilerInfo, USHORT& major, USHORT& minor, COR_PRF_RUNTIME_TYPE& runtimeType)
{
    USHORT clrInstanceId;
    USHORT buildNumber;
    USHORT qfeVersion;

    HRESULT hr = pCorProfilerInfo->GetRuntimeInformation(
        &clrInstanceId,
        &runtimeType,
        &major,
        &minor,
        &buildNumber,
        &qfeVersion,
        0,
        nullptr,
        nullptr);

    if (FAILED(hr))
    {
        Log::Info("Initializing the Profiler: Exact runtime version could not be obtained (0x", std::hex, hr, std::dec, ")");
        CorProfilerCallback::_runtimeDescription = "Unknown version of the .NET runtime";
    }
    else
    {
        std::stringstream buffer;
        buffer << "{ "
               << "clrInstanceId:" << clrInstanceId
               << ", runtimeType:" <<
                    ((runtimeType == COR_PRF_DESKTOP_CLR) ? "DESKTOP_CLR" :
                    (runtimeType == COR_PRF_CORE_CLR) ? "CORE_CLR" :
                    (std::string("unknown(") + std::to_string(runtimeType) + std::string(")")))
                << ", majorVersion: " << major
                << ", minorVersion: " << minor
                << ", buildNumber: " << buildNumber
                << ", qfeVersion: " << qfeVersion
                << " }";

        CorProfilerCallback::_runtimeDescription = buffer.str();
        Log::Info("Initializing the Profiler: Reported runtime version :", CorProfilerCallback::_runtimeDescription);
    }
}

void CorProfilerCallback::ConfigureDebugLog()
{
    shared::WSTRING isLogDebugEnabledStr = shared::GetEnvironmentValue(EnvironmentVariables::DebugLogEnabled);
    bool enabled = false;

    // no environment variable set
    if (isLogDebugEnabledStr.empty())
    {
        Log::Info("No \"", EnvironmentVariables::DebugLogEnabled, "\" environment variable has been found.",
                  " Enable debug log = ", enabled, " (default).");
    }
    else
    {
        if (!shared::TryParseBooleanEnvironmentValue(isLogDebugEnabledStr, enabled))
        {
            // invalid value for environment variable
            Log::Info("Non boolean value \"", isLogDebugEnabledStr, "\" for \"",
                      EnvironmentVariables::DebugLogEnabled, "\" environment variable.",
                      " Enable debug log = ", enabled, " (default).");
        }
        else
        {
            // take environment variable into account
            Log::Info("Enable debug log = ", enabled, " from (\"", EnvironmentVariables::DebugLogEnabled, "\" environment variable)");
        }
    }

    if (enabled)
    {
        Log::EnableDebug();
    }
}

#define PRINT_ENV_VAR_IF_SET(name)                            \
    {                                                         \
        auto envVarValue = shared::GetEnvironmentValue(name); \
        if (!envVarValue.empty())                             \
        {                                                     \
            Log::Info("  ", name, ": ", envVarValue);         \
        }                                                     \
    }

void CorProfilerCallback::PrintEnvironmentVariables()
{
    // TODO: add more env vars values
    // --> should we dump the important ones to ensure that we get them during support investigations?

    Log::Info("Environment variables:");
    PRINT_ENV_VAR_IF_SET(EnvironmentVariables::UseBacktrace2);
}

// CLR event verbosity definition
const uint32_t InformationalVerbosity = 4;
const uint32_t VerboseVerbosity = 5;

HRESULT STDMETHODCALLTYPE CorProfilerCallback::Initialize(IUnknown* corProfilerInfoUnk)
{
    Log::Info("CorProfilerCallback is initializing.");

    ConfigureDebugLog();

    _pConfiguration = std::make_unique<Configuration>();
    _pMetadataProvider = std::make_unique<MetadataProvider>();
    _pMetadataProvider->Initialize();
    PrintEnvironmentVariables();

    double coresThreshold = _pConfiguration->MinimumCores();
    double cpuLimit = 0;
    if (!OpSysTools::IsSafeToStartProfiler(coresThreshold, cpuLimit))
    {
        Log::Warn("It is not safe to start the profiler. See previous log messages for more info.");
        return E_FAIL;
    }
    _pMetadataProvider->Add(MetadataProvider::SectionRuntimeSettings, MetadataProvider::CpuLimit, std::to_string(cpuLimit));
    _pMetadataProvider->Add(MetadataProvider::SectionRuntimeSettings, MetadataProvider::NbCores, std::to_string(OsSpecificApi::GetProcessorCount()));

    // Log some important environment info:
    CorProfilerCallback::InspectProcessorInfo();
    uint16_t runtimeMajor;
    uint16_t runtimeMinor;
    CorProfilerCallback::InspectRuntimeCompatibility(corProfilerInfoUnk, runtimeMajor, runtimeMinor);

    // Initialize _pCorProfilerInfo:
    if (corProfilerInfoUnk == nullptr)
    {
        Log::Error("No IUnknown is passed to CorProfilerCallback::Initialize(). The profiler will not run.");
        return E_FAIL;
    }

    HRESULT hr = corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo5), (void**)&_pCorProfilerInfo);
    if (hr == E_NOINTERFACE)
    {
        Log::Error("This runtime does not support any ICorProfilerInfo5+ interface. .NET Framework 4.5 or later is required.");
        return E_FAIL;
    }
    else if (FAILED(hr))
    {
        Log::Error("An error occurred while obtaining the ICorProfilerInfo interface from the CLR: 0x", std::hex, hr, std::dec, ".");
        return E_FAIL;
    }

    // Log some more important environment info:
    USHORT major = 0;
    USHORT minor = 0;
    COR_PRF_RUNTIME_TYPE runtimeType;
    CorProfilerCallback::InspectRuntimeVersion(_pCorProfilerInfo, major, minor, runtimeType);

    // for .NET Core 2.1, 3.0 and 3.1, from https://github.com/dotnet/runtime/issues/11555#issuecomment-727037353,
    // it is needed to check ICorProfilerInfo11 for 3.1, 10 for 3.0 and 9 for 2.1 since major and minor will be 4.0
    // from GetRuntimeInformation
    if ((runtimeType == COR_PRF_CORE_CLR) && (major == 4))
    {
        major = runtimeMajor;
        minor = runtimeMinor;
    }

    _pRuntimeInfo = std::make_unique<RuntimeInfo>(major, minor, (runtimeType == COR_PRF_DESKTOP_CLR));
    _pMetadataProvider->Add(MetadataProvider::SectionRuntimeSettings, MetadataProvider::ClrVersion, _pRuntimeInfo->GetClrString());

    // CLR events-based profilers need ICorProfilerInfo12 (i.e. .NET 5+) to setup the communication.
    // If no such provider is enabled, no need to trigger it.
    // TODO: update the test when a new CLR events-based profiler is added (contention, GC, ...)
    bool AreEventBasedProfilersEnabled =
        _pConfiguration->IsHeapProfilingEnabled() ||
        _pConfiguration->IsAllocationProfilingEnabled() ||
        _pConfiguration->IsContentionProfilingEnabled() ||
        _pConfiguration->IsGarbageCollectionProfilingEnabled()
        ;
    if ((major >= 5) && AreEventBasedProfilersEnabled)
    {
        // Live heap profiling requires .NET 7+ and ICorProfilerInfo13
        if (major >= 7)
        {
            HRESULT hr = corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo13), (void**)&_pCorProfilerInfoLiveHeap);
            if (FAILED(hr))
            {
                Log::Error("Failed to get ICorProfilerInfo13: 0x", std::hex, hr, std::dec, ".");
                _pCorProfilerInfoLiveHeap = nullptr;

                // we continue: the live heap profiler will be disabled...
            }
        }

        HRESULT hr = corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo12), (void**)&_pCorProfilerInfoEvents);
        if (FAILED(hr))
        {
            Log::Error("Failed to get ICorProfilerInfo12: 0x", std::hex, hr, std::dec, ".");
            _pCorProfilerInfoEvents = nullptr;

            // we continue: the CLR events won't be received so no contention/memory profilers...
        }
    }
    else
    {
        // TODO: Need to listen to ETW for .NET Framework and EventPipe for .NET Core 3.0/3.1.
        //       This would be possible for contention but not for AllocationTick_V4 because
        //       the address received in the payload might not point to a real object. Because
        //       the events are received asynchronously, a GC might have happened and moved the
        //       object somewhere else (i.e. the address will point to unknown content in memory;
        //       even unmapped memory if the segment has been reclaimed).
        if (major < 5)
        {
            if (AreEventBasedProfilersEnabled)
            {
                Log::Warn("Event-based profilers (Allocation, LockContention) are not supported for .NET", major, ".", minor, " (.NET 5+ is required)");
            }
        }
    }

    // Init global state:
    OpSysTools::InitHighPrecisionTimer();

    // Init global services:
    if (!InitializeServices())
    {
        Log::Error("Failed to initialize all services (at least one failed). Stopping the profiler initialization.");
        return E_FAIL;
    }

    // Configure which profiler callbacks we want to receive by setting the event mask:
    DWORD eventMask = COR_PRF_MONITOR_THREADS | COR_PRF_ENABLE_STACK_SNAPSHOT;

    if (_pConfiguration->IsExceptionProfilingEnabled())
    {
        eventMask |= COR_PRF_MONITOR_EXCEPTIONS | COR_PRF_MONITOR_MODULE_LOADS;
    }

    if (_pConfiguration->IsAllocationRecorderEnabled() && !_pConfiguration->GetProfilesOutputDirectory().empty())
    {
        //              for GC                              for JIT
        eventMask |= COR_PRF_MONITOR_OBJECT_ALLOCATED | COR_PRF_ENABLE_OBJECT_ALLOCATED;
    }

    if (_pCorProfilerInfoEvents != nullptr)
    {
        // listen to CLR events via ICorProfilerCallback
        DWORD highMask = COR_PRF_HIGH_MONITOR_EVENT_PIPE;
        hr = _pCorProfilerInfo->SetEventMask2(eventMask, highMask);
        if (FAILED(hr))
        {
            Log::Error("SetEventMask2(0x", std::hex, eventMask, ", ", std::hex, highMask, ") returned an unexpected result: 0x", std::hex, hr, std::dec, ".");
            return E_FAIL;
        }

        // Microsoft-Windows-DotNETRuntime is the provider for the runtime events.
        // Listen to interesting events:
        //  - AllocationTick_V4
        //  - ContentionStop_V1
        //  - GC related events

        UINT64 activatedKeywords = 0;
        uint32_t verbosity = InformationalVerbosity;

        if (
            _pConfiguration->IsAllocationProfilingEnabled() ||
            _pConfiguration->IsHeapProfilingEnabled()
           )
        {
            activatedKeywords |= ClrEventsParser::KEYWORD_GC;

            // the documentation states that AllocationTick is Informational but... need Verbose  :^(
            verbosity = VerboseVerbosity;
        }
        if (_pConfiguration->IsGarbageCollectionProfilingEnabled())
        {
            activatedKeywords |= ClrEventsParser::KEYWORD_GC;
        }
        if (_pConfiguration->IsContentionProfilingEnabled())
        {
            activatedKeywords |= ClrEventsParser::KEYWORD_CONTENTION;
        }

        COR_PRF_EVENTPIPE_PROVIDER_CONFIG providers[] =
            {
                {
                    WStr("Microsoft-Windows-DotNETRuntime"),
                    activatedKeywords,
                    verbosity,
                    nullptr
                }
            };

        hr = _pCorProfilerInfoEvents->EventPipeStartSession(
            sizeof(providers) / sizeof(providers[0]),
            providers,
            false,
            &_session);

        if (FAILED(hr))
        {
            _session = 0;
            printf("Failed to start event pipe session with hr=0x%x\n", hr);
            return hr;
        }
    }
    else
    {
        hr = _pCorProfilerInfo->SetEventMask(eventMask);
        if (FAILED(hr))
        {
            Log::Error("SetEventMask(0x", std::hex, eventMask, ") returned an unexpected result: 0x", std::hex, hr, std::dec, ".");
            return E_FAIL;
        }
    }

    // Initialization complete:
    _isInitialized.store(true);
    ProfilerEngineStatus::WriteIsProfilerEngineActive(true);

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::Shutdown()
{
    Log::Info("CorProfilerCallback::Shutdown()");

    // A final .pprof should be generated before exiting
    // The aggregator must be stopped before the provider, since it will call them to get the last samples
    _pStackSamplerLoopManager->Stop();

    _pSamplesCollector->Stop();

    // Calling Stop on providers transforms the last raw samples
    if (_pWallTimeProvider != nullptr)
    {
        _pWallTimeProvider->Stop();
    }
    if (_pCpuTimeProvider != nullptr)
    {
        _pCpuTimeProvider->Stop();
    }
    if (_pExceptionsProvider != nullptr)
    {
        _pExceptionsProvider->Stop();
    }
    if (_pAllocationsProvider != nullptr)
    {
        _pAllocationsProvider->Stop();
    }

    if (_pContentionProvider != nullptr)
    {
        _pContentionProvider->Stop();
    }

    if (_pStopTheWorldProvider != nullptr)
    {
        _pStopTheWorldProvider->Stop();
    }

    if (_pGarbageCollectionProvider != nullptr)
    {
        _pGarbageCollectionProvider->Stop();
    }

    if (_pLiveObjectsProvider != nullptr)
    {
        _pLiveObjectsProvider->Stop();
    }

    if (_pThreadLifetimeProvider != nullptr)
    {
        _pThreadLifetimeProvider->Stop();
    }

    // dump all threads time
    _pThreadsCpuManager->LogCpuTimes();

    // DisposeInternal() already respects the _isInitialized flag.
    // If any code is added directly here, remember to respect _isInitialized as required.
    DisposeInternal();

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::AppDomainCreationStarted(AppDomainID appDomainId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::AppDomainCreationFinished(AppDomainID appDomainId, HRESULT hrStatus)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::AppDomainShutdownStarted(AppDomainID appDomainId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::AssemblyLoadStarted(AssemblyID assemblyId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::AssemblyUnloadStarted(AssemblyID assemblyId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::AssemblyUnloadFinished(AssemblyID assemblyId, HRESULT hrStatus)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ModuleLoadStarted(ModuleID moduleId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus)
{
    if (false == _isInitialized.load())
    {
        // If this CorProfilerCallback has not yet initialized, or if it has already shut down, then this callback is a No-Op.
        return S_OK;
    }

    if (_pConfiguration->IsExceptionProfilingEnabled())
    {
        _pExceptionsProvider->OnModuleLoaded(moduleId);
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ModuleUnloadStarted(ModuleID moduleId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID AssemblyId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ClassLoadStarted(ClassID classId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ClassLoadFinished(ClassID classId, HRESULT hrStatus)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ClassUnloadStarted(ClassID classId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ClassUnloadFinished(ClassID classId, HRESULT hrStatus)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::FunctionUnloadStarted(FunctionID functionId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::JITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::JITCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::JITCachedFunctionSearchFinished(FunctionID functionId, COR_PRF_JIT_CACHE result)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::JITFunctionPitched(FunctionID functionId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ThreadCreated(ThreadID threadId)
{
    Log::Debug("Callback invoked: ThreadCreated(threadId=0x", std::hex, threadId, std::dec, ")");

    if (false == _isInitialized.load())
    {
        // If this CorProfilerCallback has not yet initialized, or if it has already shut down, then this callback is a No-Op.
        return S_OK;
    }

    if (_pThreadLifetimeProvider != nullptr)
    {
        std::shared_ptr<ManagedThreadInfo> pThreadInfo = _pManagedThreadList->GetOrCreate(threadId);
        _pThreadLifetimeProvider->OnThreadStart(pThreadInfo);
    }
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ThreadDestroyed(ThreadID threadId)
{
    Log::Debug("Callback invoked: ThreadDestroyed(threadId=0x", std::hex, threadId, std::dec, ")");

    if (false == _isInitialized.load())
    {
        // If this CorProfilerCallback has not yet initialized, or if it has already shut down, then this callback is a No-Op.
        return S_OK;
    }

    std::shared_ptr<ManagedThreadInfo> pThreadInfo;
    Log::Debug("Removing thread ", std::hex, threadId, " from the trace context threads list.");
    if (_pCodeHotspotsThreadList->UnregisterThread(threadId, pThreadInfo))
    {
        // The docs require that we do not allow to destroy a thread while it is being stack-walked.
        // TO ensure this, SetThreadDestroyed(..) acquires the StackWalkLock associated with this ThreadInfo.
        pThreadInfo->SetThreadDestroyed();
        pThreadInfo.reset();
    }

    Log::Debug("Removing thread ", std::hex, threadId, " from the main managed thread list.");
    if (_pManagedThreadList->UnregisterThread(threadId, pThreadInfo))
    {
        // The docs require that we do not allow to destroy a thread while it is being stack-walked.
        // TO ensure this, SetThreadDestroyed(..) acquires the StackWalkLock associated with this ThreadInfo.
        pThreadInfo->SetThreadDestroyed();

        if (_pThreadLifetimeProvider != nullptr)
        {
            _pThreadLifetimeProvider->OnThreadStop(pThreadInfo);
        }
    }
#ifdef LINUX
    if (_systemCallsShield != nullptr)
    {
        _systemCallsShield->Unregister();
    }
#endif

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ThreadAssignedToOSThread(ThreadID managedThreadId, DWORD osThreadId)
{
    Log::Debug("Callback invoked: ThreadAssignedToOSThread(managedThreadId=0x", std::hex, managedThreadId, ", osThreadId=", std::dec, osThreadId, ")");

    if (false == _isInitialized.load())
    {
        // If this CorProfilerCallback has not yet initialized, or if it has already shut down, then this callback is a No-Op.
        return S_OK;
    }

    HANDLE origOsThreadHandle;
    HRESULT hr = _pCorProfilerInfo->GetHandleFromThread(managedThreadId, &origOsThreadHandle);
    if (hr != S_OK)
    {
        Log::Debug("GetHandleFromThread() failed.");
        return hr;
    }

    HANDLE dupOsThreadHandle;

#ifdef _WINDOWS
    HANDLE hProcess = OpSysTools::GetCurrentProcess();
    auto success = ::DuplicateHandle(hProcess, origOsThreadHandle, hProcess, &dupOsThreadHandle, THREAD_ALL_ACCESS, false, 0);
    if (!success)
    {
        Log::Debug("DuplicateHandle() failed.");
        return E_FAIL;
    }
#else
    dupOsThreadHandle = origOsThreadHandle;
#endif

#ifdef LINUX
    if (_systemCallsShield != nullptr)
    {
        // Register/Unregister rely on the following assumption:
        // The native thread calling ThreadAssignedToOSThread/ThreadDestroyed is the same native thread assigned to the managed thread.
        // This assumption has been tested and verified experimentally but there the documentation does not say that.
        // If at some point, it's not true, we can remove Register/Unregister on the SystemCallsShield class.
        // Then initiliaze the TLS managedThreadInfo (by calling TryGetCurrentThreadInfo) the first time a call is made in SystemCallsShield
        // SystemCallsShield::SetSharedMemory callback.
        auto threadInfo = _pManagedThreadList->GetOrCreate(managedThreadId);
        _systemCallsShield->Register(threadInfo);
    }

    // TL;DR prevent the profiler from deadlocking application thread on malloc
    // When calling uwn_backtraceXX, libunwind will initialize data structures for the current
    // thread using TLS (Thread Local Storage).
    // Initialization of TLS object does call malloc. Unfortunately, if those calls to malloc
    // occurs in our profiler signal handler, we end up deadlocking the application.
    // To prevent that, we call unw_backtrace here for the current thread, to force libunwind
    // initializing the TLS'd data structures for the current thread.
    uintptr_t tab[1];
    unw_backtrace((void**)tab, 1);

    // check if SIGUSR1 signal is blocked for current thread
    sigset_t currentMask;
    pthread_sigmask(SIG_SETMASK, nullptr, &currentMask);
    if (sigismember(&currentMask, SIGUSR1) == 1)
    {
        Log::Debug("The current thread won't be added to the managed threads list because SIGUSR1 is blocked for that thread (managedThreadId=0x", std::hex, managedThreadId, ", osThreadId=", std::dec, osThreadId, ")");
        return S_OK;
    }
#endif
    _pManagedThreadList->SetThreadOsInfo(managedThreadId, osThreadId, dupOsThreadHandle);

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ThreadNameChanged(ThreadID threadId, ULONG cchName, WCHAR name[])
{
    if (false == _isInitialized.load())
    {
        // If this CorProfilerCallback has not yet initialized, or if it has already shut down, then this callback is a No-Op.
        return S_OK;
    }

    auto threadName = (cchName == 0)
                          ? shared::WSTRING()
                          : shared::WSTRING(name, cchName);

    Log::Debug("CorProfilerCallback::ThreadNameChanged(threadId=0x", std::hex, threadId, std::dec, ", name=\"", threadName, "\")");

    DWORD osThreadId = 0;
    _pCorProfilerInfo->GetThreadInfo(threadId, &osThreadId);
    _pThreadsCpuManager->Map(osThreadId, threadName.c_str());
    _pManagedThreadList->SetThreadName(threadId, std::move(threadName));

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingClientInvocationStarted()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingClientInvocationFinished()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingServerInvocationStarted()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingServerInvocationReturned()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::UnmanagedToManagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ManagedToUnmanagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeSuspendFinished()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeSuspendAborted()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeResumeStarted()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeResumeFinished()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeThreadSuspended(ThreadID threadId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeThreadResumed(ThreadID threadId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[])
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ObjectAllocated(ObjectID objectId, ClassID classId)
{
    if (_pAllocationsRecorder != nullptr)
    {
        _pAllocationsRecorder->OnObjectAllocated(objectId, classId);
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ObjectsAllocatedByClass(ULONG cClassCount, ClassID classIds[], ULONG cObjects[])
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs, ObjectID objectRefIds[])
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RootReferences(ULONG cRootRefs, ObjectID rootRefIds[])
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionThrown(ObjectID thrownObjectId)
{
    if (false == _isInitialized.load())
    {
        // If this CorProfilerCallback has not yet initialized, or if it has already shut down, then this callback is a No-Op.
        return S_OK;
    }

    if (_pConfiguration->IsExceptionProfilingEnabled())
    {
        _pExceptionsProvider->OnExceptionThrown(thrownObjectId);
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionSearchFunctionEnter(FunctionID functionId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionSearchFunctionLeave()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionSearchFilterEnter(FunctionID functionId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionSearchFilterLeave()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionSearchCatcherFound(FunctionID functionId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionOSHandlerEnter(UINT_PTR __unused)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionOSHandlerLeave(UINT_PTR __unused)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionUnwindFunctionEnter(FunctionID functionId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionUnwindFunctionLeave()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionUnwindFinallyEnter(FunctionID functionId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionUnwindFinallyLeave()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionCatcherLeave()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::COMClassicVTableCreated(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable, ULONG cSlots)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::COMClassicVTableDestroyed(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionCLRCatcherFound()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionCLRCatcherExecute()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::GarbageCollectionStarted(int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::SurvivingReferences(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], ULONG cObjectIDRangeLength[])
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::GarbageCollectionFinished()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[])
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::HandleCreated(GCHandleID handleId, ObjectID initialObjectId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::HandleDestroyed(GCHandleID handleId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData, UINT cbClientData)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ProfilerAttachComplete()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ProfilerDetachSucceeded()
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId, BOOL fIsSafeToBlock)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::GetReJITParameters(ModuleID moduleId, mdMethodDef methodId, ICorProfilerFunctionControl* pFunctionControl)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId, HRESULT hrStatus, BOOL fIsSafeToBlock)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId, HRESULT hrStatus)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::MovedReferences2(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], SIZE_T cObjectIDRangeLength[])
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::SurvivingReferences2(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], SIZE_T cObjectIDRangeLength[])
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ConditionalWeakTableElementReferences(ULONG cRootRefs, ObjectID keyRefIds[], ObjectID valueRefIds[], GCHandleID rootIds[])
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::GetAssemblyReferences(const WCHAR* wszAssemblyPath, ICorProfilerAssemblyReferenceProvider* pAsmRefProvider)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ModuleInMemorySymbolsUpdated(ModuleID moduleId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::DynamicMethodJITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock, LPCBYTE pILHeader, ULONG cbILHeader)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::DynamicMethodJITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::DynamicMethodUnloaded(FunctionID functionId)
{
    return S_OK;
}
HRESULT STDMETHODCALLTYPE CorProfilerCallback::EventPipeEventDelivered(EVENTPIPE_PROVIDER provider,
                                                                       DWORD eventId,
                                                                       DWORD eventVersion,
                                                                       ULONG cbMetadataBlob,
                                                                       LPCBYTE metadataBlob,
                                                                       ULONG cbEventData,
                                                                       LPCBYTE eventData,
                                                                       LPCGUID pActivityId,
                                                                       LPCGUID pRelatedActivityId,
                                                                       ThreadID eventThread,
                                                                       ULONG numStackFrames,
                                                                       UINT_PTR stackFrames[])
{
    if (_pClrEventsParser != nullptr)
    {
        _pClrEventsParser->ParseEvent(
            provider,
            eventId,
            eventVersion,
            cbMetadataBlob,
            metadataBlob,
            cbEventData,
            eventData,
            pActivityId,
            pRelatedActivityId,
            eventThread,
            numStackFrames,
            stackFrames);
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::EventPipeProviderCreated(EVENTPIPE_PROVIDER provider)
{
    return S_OK;
}
