// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef LINUX

#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/LibrariesInfoCache.h"
#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/LinuxStackFramesCollector.h"
#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/ProfilerSignalManager.h"

#include "CallstackProvider.h"
#include "ManagedThreadInfo.h"
#include "MemoryResourceManager.h"
#include "OpSysTools.h"
#include "StackSnapshotResultBuffer.h"

#include "ProfilerMockedInterface.h"


#include "gtest/gtest-death-test.h"
#include "gtest/gtest.h"

#include <condition_variable>
#include <future>
#include <mutex>
#include <signal.h>
#include <sys/syscall.h>
#include <thread>
#define _GNU_SOURCE
#include <dlfcn.h>

#include <libunwind.h>

using namespace std::literals;
using ::testing::Return;

// This global variable and function are use defined/declared for the test only
// In production, those symbols will be defined in the Wrapper library
unsigned long long inside_wrapped_functions = 0;
extern "C" unsigned long long dd_inside_wrapped_functions()
{
    return inside_wrapped_functions;
}

extern "C" unsigned long long dd_nb_calls_to_dlopen_dlclose()
{
    // to force the first call to dl_iterate_phdr
    return 1;
}

#define ASSERT_DURATION_LE(secs, stmt)                                            \
    {                                                                             \
        std::promise<bool> completed;                                             \
        auto stmt_future = completed.get_future();                                \
        auto _dd_timeoutThread = std::thread([&](std::promise<bool>& completed) { \
            stmt;                                                                 \
            completed.set_value(true);                                            \
        },                                                                        \
                                             std::ref(completed));                \
                                                                                  \
        if (stmt_future.wait_for(secs) == std::future_status::timeout)            \
        {                                                                         \
            StopTest();                                                           \
            _dd_timeoutThread.join();                                             \
            GTEST_FATAL_FAILURE_("       timed out (> " #secs                     \
                                 " seconds). Check code for infinite loops");     \
        }                                                                         \
        else                                                                      \
        {                                                                         \
            _dd_timeoutThread.join();                                             \
        }                                                                         \
    }

class SignalHandlerForTest
{
public:
    SignalHandlerForTest()
    {
        _forwardCall = false;
        _handlerType = 0;
        EXPECT_EQ(sigaction(SIGUSR1, nullptr, &_oldAction), 0);
    }

    ~SignalHandlerForTest()
    {
        EXPECT_EQ(sigaction(SIGUSR1, &_oldAction, nullptr), 0);
        _forwardCall = false;
        _oldAction = {};
    }

    void InstallSignalHandler(int handlerType, std::function<void()>&& callback, bool forwardCall = false)
    {
        _handlerType = handlerType;
        _forwardCall = forwardCall;
        _callback = std::move(callback);

        struct sigaction action;
        action.sa_flags = _handlerType;
        if (_handlerType == SA_SIGINFO)
        {
            action.sa_sigaction = SignalHandlerForTest::Handler2;
        }
        else
        {
            action.sa_handler = SignalHandlerForTest::Handler;
        }
        EXPECT_EQ(sigaction(SIGUSR1, &action, &_oldAction), 0) << "Unable to setup Test handler.";
    }

    static std::unique_ptr<SignalHandlerForTest> _instance;

private:
    static void Handler(int code)
    {
        EXPECT_NE(_instance.get(), nullptr) << "Static instance of SignalHandlerForTest is not set";
        _instance->HandleSignal(code, nullptr, nullptr);
    }

    static void Handler2(int code, siginfo_t* info, void* context)
    {
        EXPECT_NE(_instance.get(), nullptr) << "Static instance of SignalHandlerForTest is not set";
        _instance->HandleSignal(code, info, context);
    }

    void HandleSignal(int code, siginfo_t* info, void* context)
    {
        _callback();
        if (_forwardCall)
        {
            if ((_oldAction.sa_flags & SA_SIGINFO) == 0)
            {
                _oldAction.sa_handler(code);
            }
            else
            {
                _oldAction.sa_sigaction(code, info, context);
            }
        }
    }

    bool _forwardCall;
    struct sigaction _oldAction;
    int _handlerType;
    std::function<void()> _callback;
};
std::unique_ptr<SignalHandlerForTest> SignalHandlerForTest::_instance = nullptr;

class LinuxStackFramesCollectorFixture : public ::testing::Test
{
public:
    LinuxStackFramesCollectorFixture() = default;

    void SetUp() override
    {
        // save the default action to ensure that each tests
        // starts with it.
        sigaction(SIGUSR1, nullptr, &_oldAction);

        _isStopped = false;

        _stopWorker = false;
        _workerThread = std::make_unique<WorkerThread>(_stopWorker);

        ResetCallbackState();

        _processId = OpSysTools::GetProcId();
        SignalHandlerForTest::_instance = std::make_unique<SignalHandlerForTest>();
        inside_wrapped_functions = 0;
    }

    void TearDown() override
    {
        StopTest();
        _workerThread.reset();

        GetSignalManager()->Reset();

        SignalHandlerForTest::_instance.reset();
        sigaction(SIGUSR1, &_oldAction, nullptr);
        inside_wrapped_functions = 0;
    }

    void StopTest()
    {
        if (_isStopped)
            return;

        _isStopped = true;
        _stopWorker = true;
    }

    static void SimulateInsideWrappedFunctions()
    {
        inside_wrapped_functions = 1; // do not profile
    }

    pid_t GetWorkerThreadId()
    {
        return _workerThread->GetThreadId();
    }

    void SendSignal()
    {
        ResetCallbackState();
        EXPECT_EQ(syscall(SYS_tgkill, _processId, _workerThread->GetThreadId(), SIGUSR1), 0) << "Unable to send signal to thread";
    }

    void InstallHandler(int handlerType = 0, bool chain = false)
    {
        SignalHandlerForTest::_instance->InstallSignalHandler(
            handlerType, [this]() { _callbackCalledPromise.set_value(); }, chain);
    }

    bool WasCallbackCalled()
    {
        return _callbackCalledFuture.wait_for(100ms) != std::future_status::timeout;
    }

    void ResetCallbackState()
    {
        std::promise<void>().swap(_callbackCalledPromise);
        _callbackCalledFuture = _callbackCalledPromise.get_future();
    }

    void ValidateCallstack(const Callstack& callstack)
    {
        // Disable this check on Alpine due to flackyness
        // Libunwind randomly fails with unw_backtrace2 (from a signal handler)
        // but unw_backtrace
#ifndef DD_ALPINE
        const auto& expectedCallstack = _workerThread->GetExpectedCallStack();

        const auto expectedNbFrames = expectedCallstack.Size();
        const auto collectedNbFrames = callstack.Size();

        EXPECT_GE(expectedNbFrames, 2);
        EXPECT_GE(collectedNbFrames, 2);

        auto callstackView = callstack.Data();
        auto expectedCallstackView = expectedCallstack.Data();

        EXPECT_EQ(callstackView[collectedNbFrames - 1], expectedCallstackView[expectedNbFrames - 1]);
        EXPECT_EQ(callstackView[collectedNbFrames - 2], expectedCallstackView[expectedNbFrames - 2]);
#endif
    }

    static ProfilerSignalManager* GetSignalManager()
    {
        return ProfilerSignalManager::Get(SIGUSR1);
    }

    static LinuxStackFramesCollector CreateStackFramesCollector(ProfilerSignalManager* signalManager, IConfiguration* configuration, CallstackProvider* p)
    {
        return LinuxStackFramesCollector(signalManager, configuration, p, LibrariesInfoCache::Get());
    }

private:
    class WorkerThread
    {
    public:
        WorkerThread(const std::atomic<bool>& stopWorker) :
            _stopWorker(stopWorker),
            _workerThreadIdPromise(),
            _workerThreadIdFuture{_workerThreadIdPromise.get_future()},
            _callstack{shared::span<std::uintptr_t>(_framesBuffer.data(), _framesBuffer.size())}

        {
            _worker = std::thread(&WorkerThread::Work, this);
        }

        ~WorkerThread()
        {
            _worker.join();
        }

        pid_t GetThreadId()
        {
            return _workerThreadIdFuture.get();
        }

        const Callstack& GetExpectedCallStack() const
        {
            return _callstack;
        }

    private:
        void Work()
        {
            // Get the callstack
            auto buffer = _callstack.Data();
            auto nb = unw_backtrace((void**)buffer.data(), buffer.size());
            _callstack.SetCount(nb);

            _workerThreadIdPromise.set_value(OpSysTools::GetThreadId());
            while (!_stopWorker.load())
            {
                // do nothing
            }
        }

        const std::atomic<bool>& _stopWorker;
        std::promise<pid_t> _workerThreadIdPromise;
        std::shared_future<pid_t> _workerThreadIdFuture;
        std::thread _worker;
        static constexpr std::uint8_t MaxFrames = 20;
        std::array<std::uintptr_t, MaxFrames> _framesBuffer;
        Callstack _callstack;
    };

    bool _isStopped;
    struct sigaction _oldAction;
    pid_t _processId;
    std::atomic<bool> _stopWorker;
    std::promise<void> _callbackCalledPromise;
    std::future<void> _callbackCalledFuture;
    std::unique_ptr<WorkerThread> _workerThread;
};

TEST_F(LinuxStackFramesCollectorFixture, CheckSamplingThreadCollectCallStack)
{
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo((DWORD)GetWorkerThreadId(), (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, S_OK);

    auto callstack = buffer->GetCallstack();

    ValidateCallstack(callstack);
}

TEST_F(LinuxStackFramesCollectorFixture, CheckSamplingThreadCollectCallStackWithOldWay)
{
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(false));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo((DWORD)GetWorkerThreadId(), (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, S_OK);

    auto callstack = buffer->GetCallstack();

    ValidateCallstack(callstack);
}

TEST_F(LinuxStackFramesCollectorFixture, CheckCollectionAbortIfInPthreadCreateCall)
{
    SimulateInsideWrappedFunctions();

    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo((DWORD)GetWorkerThreadId(), (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, E_FAIL);
    EXPECT_EQ(buffer->GetFramesCount(), 0);
}

TEST_F(LinuxStackFramesCollectorFixture, MustNotCollectIfUnknownThreadId)
{
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo(0, (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;
    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));

    EXPECT_EQ(hr, E_FAIL);
    EXPECT_EQ(buffer->GetFramesCount(), 0);
}

TEST_F(LinuxStackFramesCollectorFixture, CheckProfilerSignalHandlerIsRestoredIfAnotherHandlerReplacedIt)
{
    // 1st setup the signal handler
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    // Validate the profiler is working correctly
    auto threadId = (DWORD)GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo(threadId, (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    collector.PrepareForNextCollection();

    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, S_OK);

    auto callstack = buffer->GetCallstack();

    ValidateCallstack(callstack);

    // 2nd install test handler
    InstallHandler();

    // The profiler must not work
    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(3s, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, E_FAIL);

    // .. but the other handler yes
    EXPECT_TRUE(WasCallbackCalled());

    // Reset to validate that the profiler will not call the test handler
    ResetCallbackState();
    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, S_OK);

    callstack = buffer->GetCallstack();

    ValidateCallstack(callstack);

    // now the custmer handler must not work
    EXPECT_FALSE(WasCallbackCalled());

    SendSignal();
    EXPECT_TRUE(WasCallbackCalled());
}

TEST_F(LinuxStackFramesCollectorFixture, CheckProfilerHandlerIsInstalledCorrectlyIfSignalWasAlreadyInstalled)
{
    // 1st installed the test handler
    InstallHandler(SA_SIGINFO);

    // validate it's working
    SendSignal();
    EXPECT_TRUE(WasCallbackCalled()) << "Test handler was not called.";

    // Reset to validate that the profiler will not call the test handler
    ResetCallbackState();

    // 2nd install profiler handler
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;
    auto threadId = GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo((DWORD)threadId, (HANDLE)0);

    // validate it's working
    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));

    EXPECT_EQ(hr, S_OK);

    auto callstack = buffer->GetCallstack();

    ValidateCallstack(callstack);

    EXPECT_FALSE(WasCallbackCalled()) << "Test handler was called.";

    // 3rd check Test handler still working
    SendSignal();
    EXPECT_TRUE(WasCallbackCalled()) << "Test handler was not called.";
}

TEST_F(LinuxStackFramesCollectorFixture, CheckProfilerHandlerIsInstalledCorrectlyIfSignalWasAlreadyInstalled2)
{
    // 1st installed the test handler
    InstallHandler();

    // validate it's working
    SendSignal();
    EXPECT_TRUE(WasCallbackCalled()) << "Test handler was not called.";

    // Reset to validate that the profiler will not call the test handler
    ResetCallbackState();

    // 2nd install profiler handler
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;
    auto threadId = GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo((DWORD)threadId, (HANDLE)0);

    // validate it's working
    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));

    EXPECT_EQ(hr, S_OK);

    auto callstack = buffer->GetCallstack();

    ValidateCallstack(callstack);

    EXPECT_FALSE(WasCallbackCalled()) << "Test handler was called.";

    // 3rd check Test handler still working
    SendSignal();
    EXPECT_TRUE(WasCallbackCalled()) << "Test handler was not called.";
}

TEST_F(LinuxStackFramesCollectorFixture, CheckProfilerHandlerIsInstalledCorrectlyIfSignalWasAlreadyInstalledOnDifferentHandler)
{
    // 1st installed Test handler
    InstallHandler(SA_SIGINFO);

    // validate it's
    SendSignal();
    EXPECT_TRUE(WasCallbackCalled()) << "Test handler was not called.";

    // Reset to validate that the profiler will not call the Test handler
    ResetCallbackState();

    // 2nd install profiler handler
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    auto threadId = GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo((DWORD)threadId, (HANDLE)0);

    // validate it's working
    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));

    EXPECT_EQ(hr, S_OK);

    auto callstack = buffer->GetCallstack();

    ValidateCallstack(callstack);

    EXPECT_FALSE(WasCallbackCalled());

    // 3rd check Test handler still working
    SendSignal();
    EXPECT_TRUE(WasCallbackCalled()) << "Test handler was not called.";
}

TEST_F(LinuxStackFramesCollectorFixture, CheckNoCrashIfPreviousHandlerWasMarkedAsIgnored)
{
    testing::FLAGS_gtest_death_test_style = "threadsafe";

    struct sigaction currentAction;
    EXPECT_EQ(sigaction(SIGUSR1, nullptr, &currentAction), 0) << "Unable to get current action.";
    currentAction.sa_handler = SIG_IGN;
    EXPECT_EQ(sigaction(SIGUSR1, &currentAction, nullptr), 0) << "Unable to update the current action with SIG_IGN handler";

    // create collector to setup profiler signal handler
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    EXPECT_EQ(sigaction(SIGUSR1, nullptr, &currentAction), 0) << "Unable to get current action.";
    EXPECT_NE(currentAction.sa_handler, SIG_DFL);
    EXPECT_NE(currentAction.sa_handler, SIG_IGN);

    SendSignal();
    // safe to release the pointer on the configuration
    // This prevents the GTest leak detector to fail the test
    EXPECT_EXIT(configuration.reset(); exit(WasCallbackCalled() ? 1 : 0), testing::ExitedWithCode(0), "");
}

TEST_F(LinuxStackFramesCollectorFixture, CheckThatProfilerHandlerAndOtherHandlerStopCallingEachOther)
{
    // 1st setup the other handler so the profiler get a pointer to it
    InstallHandler(SA_SIGINFO);

    // 2nd setup the signal handler (which will points to the custom handler)
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    // 3rd now point to the profiler handler
    InstallHandler(SA_SIGINFO, true);

    // Validate the profiler is still working correctly
    auto threadId = (DWORD)GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo(threadId, (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;
    collector.PrepareForNextCollection();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));

    EXPECT_EQ(hr, S_OK);
    auto callstack = buffer->GetCallstack();
    ValidateCallstack(callstack);

    SendSignal();
    // here I do not know if the stack collection was done
    EXPECT_TRUE(WasCallbackCalled()) << "Test handler was not called.";
}

TEST_F(LinuxStackFramesCollectorFixture, CheckNoCrashIfNoPreviousHandlerInstalled)
{
    testing::FLAGS_gtest_death_test_style = "threadsafe";

    struct sigaction currentAction;
    EXPECT_EQ(sigaction(SIGUSR1, nullptr, &currentAction), 0) << "Unable to get current action.";
    EXPECT_EQ(currentAction.sa_handler, SIG_DFL) << "Current handler is not the default one";

    // create collector to setup profiler signal handler
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    EXPECT_EQ(sigaction(SIGUSR1, nullptr, &currentAction), 0) << "Unable to get current action.";
    EXPECT_NE(currentAction.sa_handler, SIG_DFL);
    EXPECT_NE(currentAction.sa_handler, SIG_IGN);

    SendSignal();
    // safe to release the pointer on the configuration
    // This prevents the GTest leak detector to fail the test
    EXPECT_EXIT(configuration.reset(); exit(WasCallbackCalled() ? 1 : 0), testing::ExitedWithCode(0), "");
}

TEST_F(LinuxStackFramesCollectorFixture, CheckTheProfilerStopWorkingIfSignalHandlerKeepsChanging)
{
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    CallstackProvider p(MemoryResourceManager::GetDefault());
    auto collector = CreateStackFramesCollector(signalManager, configuration.get(), &p);

    const auto threadId = GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0, nullptr);
    threadInfo.SetOsInfo((DWORD)threadId, (HANDLE)0);
    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    {
        collector.PrepareForNextCollection();
        // validate it's working
        ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
        EXPECT_EQ(hr, S_OK);

        auto callstack = buffer->GetCallstack();
        ValidateCallstack(callstack);
    }

    InstallHandler(SA_SIGINFO);

    {
        // profiler handler was replaced, so the signal will be lost and we will return after 2s
        collector.PrepareForNextCollection();
        ASSERT_DURATION_LE(3s, buffer = collector.CollectStackSample(&threadInfo, &hr));
        EXPECT_EQ(hr, E_FAIL);

        // At this point, the profiler restored its handler, ensure it's working as expected
        collector.PrepareForNextCollection();
        ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
        EXPECT_EQ(hr, S_OK);
    }

    // replace the handler again
    ResetCallbackState();
    InstallHandler(SA_SIGINFO);

    {
        // profiler handler was replaced, so the signal will be lost and we will return after 2s
        collector.PrepareForNextCollection();
        ASSERT_DURATION_LE(3s, buffer = collector.CollectStackSample(&threadInfo, &hr));
        EXPECT_EQ(hr, E_FAIL);

        ResetCallbackState();

        // At this point, we stop restoring the profiler signal handler and stop profiling
        collector.PrepareForNextCollection();
        ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
        EXPECT_EQ(hr, E_FAIL);
    }
}

#endif
