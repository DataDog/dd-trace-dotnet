// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef LINUX

#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/LinuxStackFramesCollector.h"
#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/ProfilerSignalManager.h"
#include "ManagedThreadInfo.h"
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

        ProfilerSignalManager::Get()->Reset();

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

    void ValidateCallstack(const std::vector<uintptr_t>& ips)
    {
        const auto& expectedCallstack = _workerThread->GetExpectedCallStack();

        const auto expectedNbFrames = expectedCallstack.size();
        const auto collectedNbFrames = ips.size();

        EXPECT_GE(expectedNbFrames, 2);
        EXPECT_GE(collectedNbFrames, 2);

        EXPECT_EQ(ips[collectedNbFrames - 1], (uintptr_t)expectedCallstack[expectedNbFrames - 1]);
        EXPECT_EQ(ips[collectedNbFrames - 2], (uintptr_t)expectedCallstack[expectedNbFrames - 2]);
    }

    static ProfilerSignalManager* GetSignalManager()
    {
        return ProfilerSignalManager::Get();
    }

private:
    class WorkerThread
    {
    public:
        WorkerThread(const std::atomic<bool>& stopWorker) :
            _stopWorker(stopWorker),
            _workerThreadIdPromise(),
            _workerThreadIdFuture{_workerThreadIdPromise.get_future()}

        {
            _worker = std::thread(&WorkerThread::Work, this);
        }

        ~WorkerThread()
        {
            _callstack.clear();
            _worker.join();
        }

        pid_t GetThreadId()
        {
            return _workerThreadIdFuture.get();
        }

        const std::vector<void*>& GetExpectedCallStack() const
        {
            return _callstack;
        }

    private:
        void Work()
        {
            // Get the callstack
            const int32_t nbMaxFrames = 20;
            _callstack.resize(nbMaxFrames);
            auto nb = unw_backtrace(_callstack.data(), nbMaxFrames);
            _callstack.resize(nb);

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
        std::vector<void*> _callstack;
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

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo((DWORD)GetWorkerThreadId(), (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, S_OK);

    std::vector<uintptr_t> ips;
    buffer->CopyInstructionPointers(ips);

    ValidateCallstack(ips);
}

TEST_F(LinuxStackFramesCollectorFixture, CheckSamplingThreadCollectCallStackWithOldWay)
{
    auto* signalManager = GetSignalManager();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(false));

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo((DWORD)GetWorkerThreadId(), (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, S_OK);

    std::vector<uintptr_t> ips;
    buffer->CopyInstructionPointers(ips);

    ValidateCallstack(ips);
}

TEST_F(LinuxStackFramesCollectorFixture, CheckCollectionAbortIfInPthreadCreateCall)
{
    SimulateInsideWrappedFunctions();

    auto* signalManager = ProfilerSignalManager::Get();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo((DWORD)GetWorkerThreadId(), (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, E_FAIL);
    EXPECT_EQ(buffer->GetFramesCount(), 0);
}

TEST_F(LinuxStackFramesCollectorFixture, MustNotCollectIfUnknownThreadId)
{
    auto* signalManager = ProfilerSignalManager::Get();

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, UseBacktrace2()).WillOnce(Return(true));

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo(0, (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;
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

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    // Validate the profiler is working correctly
    auto threadId = (DWORD)GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo(threadId, (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, S_OK);

    std::vector<uintptr_t> ips;
    buffer->CopyInstructionPointers(ips);

    ValidateCallstack(ips);

    // 2nd install test handler
    InstallHandler();

    // The profiler must not work
    ASSERT_DURATION_LE(3s, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, E_FAIL);

    // .. but the other handler yes
    EXPECT_TRUE(WasCallbackCalled());

    // Reset to validate that the profiler will not call the test handler
    ResetCallbackState();
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
    EXPECT_EQ(hr, S_OK);

    buffer->CopyInstructionPointers(ips);

    // Disable this check on Alpine due to flackyness
    // Libunwind randomly fails with unw_backtrace2 (from a signal handler)
    // but unw_backtrace
#ifndef DD_ALPINE
    ValidateCallstack(ips);
#endif

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

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;
    auto threadId = GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo((DWORD)threadId, (HANDLE)0);

    // validate it's working
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));

    EXPECT_EQ(hr, S_OK);

    std::vector<uintptr_t> ips;
    buffer->CopyInstructionPointers(ips);

    ValidateCallstack(ips);

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

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;
    auto threadId = GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo((DWORD)threadId, (HANDLE)0);

    // validate it's working
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));

    EXPECT_EQ(hr, S_OK);

    std::vector<uintptr_t> ips;
    buffer->CopyInstructionPointers(ips);

    ValidateCallstack(ips);

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

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    auto threadId = GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo((DWORD)threadId, (HANDLE)0);

    // validate it's working
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));

    EXPECT_EQ(hr, S_OK);

    std::vector<uintptr_t> ips;
    buffer->CopyInstructionPointers(ips);

    ValidateCallstack(ips);

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

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

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

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    // 3rd now point to the profiler handler
    InstallHandler(SA_SIGINFO, true);

    // Validate the profiler is still working correctly
    auto threadId = (DWORD)GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo(threadId, (HANDLE)0);

    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;
    ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));

    EXPECT_EQ(hr, S_OK);
    std::vector<uintptr_t> ips;
    buffer->CopyInstructionPointers(ips);
    ValidateCallstack(ips);

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

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

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

    auto collector = LinuxStackFramesCollector(signalManager, configuration.get());

    const auto threadId = GetWorkerThreadId();
    auto threadInfo = ManagedThreadInfo((ThreadID)0);
    threadInfo.SetOsInfo((DWORD)threadId, (HANDLE)0);
    std::uint32_t hr;
    StackSnapshotResultBuffer* buffer;

    {
        collector.PrepareForNextCollection();
        // validate it's working
        ASSERT_DURATION_LE(100ms, buffer = collector.CollectStackSample(&threadInfo, &hr));
        EXPECT_EQ(hr, S_OK);

        std::vector<uintptr_t> ips;
        buffer->CopyInstructionPointers(ips);
        ValidateCallstack(ips);
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