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
#endif

#include "AppDomainStore.h"
#include "ApplicationStore.h"
#include "ClrLifetime.h"
#include "Configuration.h"
#include "CpuTimeProvider.h"
#include "EnvironmentVariables.h"
#include "FrameStore.h"
#include "IMetricsSender.h"
#include "IMetricsSenderFactory.h"
#include "LibddprofExporter.h"
#include "Log.h"
#include "ManagedThreadList.h"
#include "OpSysTools.h"
#include "OsSpecificApi.h"
#include "ProfilerEngineStatus.h"
#include "RuntimeIdStore.h"
#include "SamplesAggregator.h"
#include "StackSamplerLoopManager.h"
#include "ThreadsCpuManager.h"
#include "WallTimeProvider.h"
#include "ExceptionsProvider.h"

#include "shared/src/native-src/environment_variables.h"
#include "shared/src/native-src/pal.h"
#include "shared/src/native-src/string.h"

// The following macros are used to construct the profiler file:
#ifdef _WINDOWS
#define LIBRARY_FILE_EXTENSION ".dll"
#elif LINUX
#define LIBRARY_FILE_EXTENSION ".so"
#elif MACOS
#define LIBRARY_FILE_EXTENSION ".dylib"
#else
Error("unknown platform");
#endif

#ifdef BIT64
#define PROFILER_LIBRARY_BINARY_FILE_NAME WStr("Datadog.AutoInstrumentation.Profiler.Native.x64" LIBRARY_FILE_EXTENSION)
#else
#define PROFILER_LIBRARY_BINARY_FILE_NAME WStr("Datadog.AutoInstrumentation.Profiler.Native.x86" LIBRARY_FILE_EXTENSION)
#endif

// Static helpers
IClrLifetime* CorProfilerCallback::GetClrLifetime()
{
    return _this->_pClrLifetime.get();
}

// Initialization

CorProfilerCallback* CorProfilerCallback::_this = nullptr;

CorProfilerCallback::CorProfilerCallback()
{
    // Keep track of the one and only ICorProfilerCallback implementation.
    // It will be used as root for other services
    _this = this;

    _pClrLifetime = std::make_unique<ClrLifetime>(&_isInitialized);
}

// Cleanup
CorProfilerCallback::~CorProfilerCallback()
{
    DisposeInternal();

    _this = nullptr;
}

bool CorProfilerCallback::InitializeServices()
{
    _metricsSender = IMetricsSenderFactory::Create();

    _pConfiguration = std::make_unique<Configuration>();

    _pAppDomainStore = std::make_unique<AppDomainStore>(_pCorProfilerInfo);

    _pFrameStore = std::make_unique<FrameStore>(_pCorProfilerInfo, _pConfiguration.get());

    // Create service instances
    _pThreadsCpuManager = RegisterService<ThreadsCpuManager>();

    _pManagedThreadList = RegisterService<ManagedThreadList>(_pCorProfilerInfo);

    auto* pRuntimeIdStore = RegisterService<RuntimeIdStore>();

    if (_pConfiguration->IsWallTimeProfilingEnabled())
    {
        _pWallTimeProvider = RegisterService<WallTimeProvider>(_pThreadsCpuManager, _pFrameStore.get(), _pAppDomainStore.get(), pRuntimeIdStore);
    }

    if (_pConfiguration->IsCpuProfilingEnabled())
    {
        _pCpuTimeProvider = RegisterService<CpuTimeProvider>(_pThreadsCpuManager, _pFrameStore.get(), _pAppDomainStore.get(), pRuntimeIdStore);
    }

    if (_pConfiguration->IsExceptionProfilingEnabled())
    {
        _pExceptionsProvider = RegisterService<ExceptionsProvider>(
            _pCorProfilerInfo,
            _pManagedThreadList,
            _pFrameStore.get(),
            _pConfiguration.get(),
            _pThreadsCpuManager,
            _pAppDomainStore.get(),
            pRuntimeIdStore
            );
    }

    _pStackSamplerLoopManager = RegisterService<StackSamplerLoopManager>(
        _pCorProfilerInfo,
        _pConfiguration.get(),
        _metricsSender,
        _pClrLifetime.get(),
        _pThreadsCpuManager,
        _pManagedThreadList,
        _pWallTimeProvider,
        _pCpuTimeProvider);

    _pApplicationStore = RegisterService<ApplicationStore>(_pConfiguration.get());

    // The different elements of the libddprof pipeline are created and linked together
    // i.e. the exporter is passed to the aggregator and each provider is added to the aggregator.
    _pExporter = std::make_unique<LibddprofExporter>(_pConfiguration.get(), _pApplicationStore);
    _pSamplesAggregator = RegisterService<SamplesAggregator>(_pConfiguration.get(), _pThreadsCpuManager, _pExporter.get(), _metricsSender.get());

    if (_pConfiguration->IsWallTimeProfilingEnabled())
    {
        _pSamplesAggregator->Register(_pWallTimeProvider);
    }

    if (_pConfiguration->IsCpuProfilingEnabled())
    {
        _pSamplesAggregator->Register(_pCpuTimeProvider);
    }

    if (_pConfiguration->IsExceptionProfilingEnabled())
    {
        _pSamplesAggregator->Register(_pExceptionsProvider);
    }

    auto started = StartServices();
    if (!started)
    {
        Log::Warn("One or multiple services failed to start. Stopping all services.");
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

void CorProfilerCallback::DisposeInternal(void)
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

        ICorProfilerInfo4* pCorProfilerInfo = _pCorProfilerInfo;
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

ULONG STDMETHODCALLTYPE CorProfilerCallback::AddRef(void)
{
    ULONG refCount = _refCount.fetch_add(1) + 1;
    return refCount;
}

ULONG STDMETHODCALLTYPE CorProfilerCallback::Release(void)
{
    ULONG refCount = _refCount.fetch_sub(1) - 1;

    if (refCount == 0)
    {
        delete this;
    }

    return refCount;
}

ULONG STDMETHODCALLTYPE CorProfilerCallback::GetRefCount(void) const
{
    ULONG refCount = _refCount.load();
    return refCount;
}

void CorProfilerCallback::InspectRuntimeCompatibility(IUnknown* corProfilerInfoUnk)
{
    IUnknown* tstVerProfilerInfo;
    if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo11), (void**)&tstVerProfilerInfo))
    {
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo11 available. Profiling API compatibility: .NET Core 5.0 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo10), (void**)&tstVerProfilerInfo))
    {
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo10 available. Profiling API compatibility: .NET Core 3.0 or later.");
        tstVerProfilerInfo->Release();
    }
    else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo9), (void**)&tstVerProfilerInfo))
    {
        _isNet46OrGreater = true;
        Log::Info("ICorProfilerInfo9 available. Profiling API compatibility: .NET Core 2.2 or later.");
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
        Log::Info("No ICorProfilerInfoXxx available.");
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

void CorProfilerCallback::InspectProcessorInfo(void)
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

void CorProfilerCallback::InspectRuntimeVersion(ICorProfilerInfo4* pCorProfilerInfo)
{
    USHORT clrInstanceId;
    COR_PRF_RUNTIME_TYPE runtimeType;
    USHORT majorVersion;
    USHORT minorVersion;
    USHORT buildNumber;
    USHORT qfeVersion;

    HRESULT hr = pCorProfilerInfo->GetRuntimeInformation(
        &clrInstanceId,
        &runtimeType,
        &majorVersion,
        &minorVersion,
        &buildNumber,
        &qfeVersion,
        0,
        nullptr,
        nullptr);

    if (FAILED(hr))
    {
        Log::Info("Initializing the Profiler: Exact runtime version could not be obtained (0x", std::hex, hr, std::dec, ")");
    }
    else
    {
        Log::Info("Initializing the Profiler: Reported runtime version : { clrInstanceId: ", clrInstanceId,
                  ", runtimeType:",
                  ((runtimeType == COR_PRF_DESKTOP_CLR) ? "DESKTOP_CLR"
                   : (runtimeType == COR_PRF_CORE_CLR)
                       ? "CORE_CLR"
                       : (std::string("unknown(") + std::to_string(runtimeType) + std::string(")"))),
                  ", majorVersion: ", majorVersion,
                  ", minorVersion: ", minorVersion,
                  ", buildNumber: ", buildNumber,
                  ", qfeVersion: ", qfeVersion,
                  " }.");
    }
}

void CorProfilerCallback::ConfigureDebugLog(void)
{
    // For now we want debug log to be ON by default. In future releases, this may require explicit opt-in.
    // For that, change 'IsLogDebugEnabledDefault' to be initialized to 'false' by default (@ToDo).

    constexpr const bool IsLogDebugEnabledDefault = false;
    bool isLogDebugEnabled;

    shared::WSTRING isLogDebugEnabledStr = shared::GetEnvironmentValue(EnvironmentVariables::DebugLogEnabled);

    // no environment variable set
    if (isLogDebugEnabledStr.empty())
    {
        Log::Info("No \"", EnvironmentVariables::DebugLogEnabled, "\" environment variable has been found.",
                  " Enable debug log = ", IsLogDebugEnabledDefault, " (default).");

        isLogDebugEnabled = IsLogDebugEnabledDefault;
    }
    else
    {
        if (!shared::TryParseBooleanEnvironmentValue(isLogDebugEnabledStr, isLogDebugEnabled))
        {
            // invalid value for environment variable
            Log::Info("Non boolean value \"", isLogDebugEnabledStr, "\" for \"",
                      EnvironmentVariables::DebugLogEnabled, "\" environment variable.",
                      " Enable debug log = ", IsLogDebugEnabledDefault, " (default).");

            isLogDebugEnabled = IsLogDebugEnabledDefault;
        }
        else
        {
            // take environment variable into account
            Log::Info("Enable debug log = ", isLogDebugEnabled, " from (", EnvironmentVariables::DebugLogEnabled, " environment variable)");
        }
    }

    if (isLogDebugEnabled)
    {
        Log::EnableDebug();
    }
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::Initialize(IUnknown* corProfilerInfoUnk)
{
    Log::Info("CorProfilerCallback is initializing.");

    ConfigureDebugLog();

    // Log some important environment info:
    CorProfilerCallback::InspectProcessorInfo();
    CorProfilerCallback::InspectRuntimeCompatibility(corProfilerInfoUnk);

    // Initialize _pCorProfilerInfo:
    if (corProfilerInfoUnk == nullptr)
    {
        Log::Info("No IUnknown is passed to CorProfilerCallback::Initialize(). The profiler will not run.");
        return E_FAIL;
    }

    HRESULT hr = corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo4), (void**)&_pCorProfilerInfo);
    if (hr == E_NOINTERFACE)
    {
        Log::Error("This runtime does not support any ICorProfilerInfo4+ interface. .NET Framework 4.5 or later is required.");
        return E_FAIL;
    }
    else if (FAILED(hr))
    {
        Log::Error("An error occurred while obtaining the ICorProfilerInfo interface from the CLR: 0x", std::hex, hr, std::dec, ".");
        return E_FAIL;
    }

    // Log some more important environment info:
    CorProfilerCallback::InspectRuntimeVersion(_pCorProfilerInfo);

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

    hr = _pCorProfilerInfo->SetEventMask(eventMask);
    if (FAILED(hr))
    {
        Log::Error("SetEventMask(0x", std::hex, eventMask, ") returned an unexpected result: 0x", std::hex, hr, std::dec, ".");
        return E_FAIL;
    }

    // Initialization complete:
    _isInitialized.store(true);
    ProfilerEngineStatus::WriteIsProfilerEngineActive(true);

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::Shutdown(void)
{
    Log::Info("CorProfilerCallback::Shutdown()");

    // A final .pprof should be generated before exiting
    // All providers should be stopped and flushed before the aggregator
    // sends the last samples to the exporter
    _pStackSamplerLoopManager->Stop();

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

    // It is now time to aggregate the remaining samples and export the last .pprof
    _pSamplesAggregator->Stop();


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

    _pManagedThreadList->GetOrCreateThread(threadId);
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

    ManagedThreadInfo* pThreadInfo;
    if (_pManagedThreadList->UnregisterThread(threadId, &pThreadInfo))
    {
        // The docs require that we do not allow to destroy a thread while it is being stack-walked.
        // TO ensure this, SetThreadDestroyed(..) acquires the StackWalkLock associated with this ThreadInfo.
        pThreadInfo->SetThreadDestroyed();
        pThreadInfo->Release();
    }

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

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingClientInvocationStarted(void)
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

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingClientInvocationFinished(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingServerInvocationStarted(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RemotingServerInvocationReturned(void)
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

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeSuspendFinished(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeSuspendAborted(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeResumeStarted(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::RuntimeResumeFinished(void)
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

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionSearchFunctionLeave(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionSearchFilterEnter(FunctionID functionId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionSearchFilterLeave(void)
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

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionUnwindFunctionLeave(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionUnwindFinallyEnter(FunctionID functionId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionUnwindFinallyLeave(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionCatcherLeave(void)
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

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionCLRCatcherFound(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ExceptionCLRCatcherExecute(void)
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

HRESULT STDMETHODCALLTYPE CorProfilerCallback::GarbageCollectionFinished(void)
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

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ProfilerAttachComplete(void)
{
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::ProfilerDetachSucceeded(void)
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
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallback::EventPipeProviderCreated(EVENTPIPE_PROVIDER provider)
{
    return S_OK;
}
