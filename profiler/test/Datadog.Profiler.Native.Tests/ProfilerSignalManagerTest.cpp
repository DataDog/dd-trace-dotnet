// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef LINUX

#include "gtest/gtest.h"

#include "OpSysTools.h"
#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/ProfilerSignalManager.h"

#include <atomic>
#include <csetjmp>
#include <future>
#include <signal.h>

class ProfilerSignalManagerFixture : public ::testing::Test
{
public:
    ProfilerSignalManagerFixture() = default;

    void SetUp() override
    {
        GetSignalManager()->Reset();
    }
    
    static ProfilerSignalManager* GetSignalManager()
    {
        return ProfilerSignalManager::Get(SIGUSR1);
    }
};

bool CustomHandler(int signal, siginfo_t* info, void* context)
{
    return true;
}

bool OtherCustomHandler(int signal, siginfo_t* info, void* context)
{
    return false;
}

TEST_F(ProfilerSignalManagerFixture, CheckSignalIsInstallOnRegistration)
{
    auto* signalManager = GetSignalManager();

    EXPECT_FALSE(signalManager->IsHandlerInPlace());
    EXPECT_TRUE(signalManager->RegisterHandler(CustomHandler));
    EXPECT_TRUE(signalManager->IsHandlerInPlace());
}

TEST_F(ProfilerSignalManagerFixture, CanRegisterOnlyOneCustomHandler)
{
    auto* signalManager = GetSignalManager();

    EXPECT_TRUE(signalManager->RegisterHandler(CustomHandler));
    EXPECT_TRUE(signalManager->IsHandlerInPlace());

#ifdef NDEBUG
    EXPECT_FALSE(signalManager->RegisterHandler(OtherCustomHandler));
#else
    EXPECT_DEATH(signalManager->RegisterHandler(OtherCustomHandler), "");
#endif

    EXPECT_TRUE(signalManager->RegisterHandler(CustomHandler));
}

TEST_F(ProfilerSignalManagerFixture, CheckSignalHandlerIsSetForSignalUSR1)
{
    auto* signalManager = GetSignalManager();
    EXPECT_TRUE(signalManager->RegisterHandler(CustomHandler));

    struct sigaction currentAction;
    EXPECT_EQ(sigaction(SIGUSR1, nullptr, &currentAction), 0) << "Unable to setup Test handler.";
    EXPECT_EQ(currentAction.sa_flags & SA_SIGINFO, SA_SIGINFO) << "sa_flags must contain the SA_SIGINFO mask to use sigaction handler.";
    EXPECT_EQ(currentAction.sa_flags & SA_RESTART, SA_RESTART) << "sa_flags must contain the SA_RESTART mask for threads that were interrupted while waiting on IO.";
    EXPECT_NE(currentAction.sa_sigaction, nullptr) << "sa_sigaction handler must not be nullptr.";
}

bool SigProfHandlerCalled = false;
bool SigProfCustomHandler(int signal, siginfo_t* info, void* context)
{
    SigProfHandlerCalled = true;
    return true;
}

TEST_F(ProfilerSignalManagerFixture, CheckTwoDifferentSignalsInstallation)
{
    auto* sigusr1SignalManager = ProfilerSignalManager::Get(SIGUSR1);
    auto* sigprofSignalManager = ProfilerSignalManager::Get(SIGPROF);

    EXPECT_TRUE(sigusr1SignalManager->RegisterHandler(CustomHandler));
    EXPECT_TRUE(sigprofSignalManager->RegisterHandler(SigProfCustomHandler));

    auto tid = OpSysTools::GetThreadId();
    SigProfHandlerCalled = false;
    sigusr1SignalManager->SendSignal(tid);

    ASSERT_FALSE(SigProfHandlerCalled);

    SigProfHandlerCalled = false;
    sigprofSignalManager->SendSignal(tid);

    ASSERT_TRUE(SigProfHandlerCalled);
}

TEST_F(ProfilerSignalManagerFixture, CheckThrowIfSignalAbove31)
{
    ASSERT_NE(ProfilerSignalManager::Get(SIGUSR1), nullptr);
    ASSERT_NE(ProfilerSignalManager::Get(SIGPROF), nullptr);

    ASSERT_EQ(ProfilerSignalManager::Get(-1), nullptr);
    ASSERT_EQ(ProfilerSignalManager::Get(0), nullptr);
    ASSERT_EQ(ProfilerSignalManager::Get(32), nullptr);
    ASSERT_EQ(ProfilerSignalManager::Get(33), nullptr);
    ASSERT_EQ(ProfilerSignalManager::Get(100), nullptr);
}

TEST_F(ProfilerSignalManagerFixture, CheckSignalDeRegistration)
{
    ProfilerSignalManager* manager = nullptr;
    ASSERT_NE(manager = ProfilerSignalManager::Get(SIGPROF), nullptr);

    ASSERT_TRUE(manager->UnRegisterHandler());

    // Make sure we do not un register more than once
    ASSERT_FALSE(manager->UnRegisterHandler());
}

// SIGSEGV and SIGBUS: the previous handler (the CLR one) may never return.
// It is replaced here by a handler that leaves with siglongjmp, and counts its calls.
namespace
{
sigjmp_buf s_fromPreviousHandler;
std::atomic<int> s_previousHandlerCalls{0};
std::atomic<int> s_customHandlerCalls{0};

void PreviousHandlerThatDoesNotReturn(int /*signal*/, siginfo_t* /*info*/, void* /*context*/)
{
    s_previousHandlerCalls++;
    siglongjmp(s_fromPreviousHandler, 1);
}

bool CustomHandlerThatChains(int /*signal*/, siginfo_t* /*info*/, void* /*context*/)
{
    s_customHandlerCalls++;
    return false;
}
} // namespace

class ProfilerSignalManagerFaultSignalsFixture : public ::testing::Test
{
public:
    void TearDown() override
    {
        // Restores the previous action saved by the manager, then the one that was set before the test
        if (_signal != 0)
        {
            ProfilerSignalManager::Get(_signal)->Reset();
            sigaction(_signal, &_originalAction, nullptr);
        }
    }

protected:
    void Init(int signal)
    {
        _signal = signal;
        s_previousHandlerCalls = 0;
        s_customHandlerCalls = 0;
        ASSERT_EQ(sigaction(signal, nullptr, &_originalAction), 0);
        ProfilerSignalManager::Get(signal)->Reset();
    }

    void InstallPreviousAction(void (*handler)(int, siginfo_t*, void*))
    {
        struct sigaction action = {};
        action.sa_flags = SA_SIGINFO;
        action.sa_sigaction = handler;
        sigemptyset(&action.sa_mask);
        ASSERT_EQ(sigaction(_signal, &action, nullptr), 0);
    }

    // Delivers the signal synchronously to the current thread.
    // If the previous handler is called, it leaves with siglongjmp, like the CLR one does.
    void Deliver()
    {
        if (sigsetjmp(s_fromPreviousHandler, 1) == 0)
        {
            raise(_signal);
        }
    }

    void CheckPreviousHandlerIsCalledOnEachSignal(int signal)
    {
        Init(signal);
        ASSERT_NO_FATAL_FAILURE(InstallPreviousAction(PreviousHandlerThatDoesNotReturn));
        ASSERT_TRUE(ProfilerSignalManager::Get(signal)->RegisterHandler(CustomHandlerThatChains));

        for (int i = 1; i <= 3; i++)
        {
            Deliver();
            EXPECT_EQ(s_customHandlerCalls.load(), i);
            EXPECT_EQ(s_previousHandlerCalls.load(), i) << "The previous handler was not called for signal #" << i;
        }
    }

    int _signal = 0;
    struct sigaction _originalAction = {};
};

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckPreviousSigsegvHandlerIsCalledOnEachSignal)
{
    CheckPreviousHandlerIsCalledOnEachSignal(SIGSEGV);
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckPreviousSigbusHandlerIsCalledOnEachSignal)
{
    CheckPreviousHandlerIsCalledOnEachSignal(SIGBUS);
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckFaultSignalHandlersRunOnTheAlternateStack)
{
    // The .NET 6 CLR handler crashes if the handlers that chain to it do not run on the alternate stack
    for (int signal : {SIGSEGV, SIGBUS})
    {
        Init(signal);
        ASSERT_TRUE(ProfilerSignalManager::Get(signal)->RegisterHandler(CustomHandlerThatChains));

        struct sigaction action;
        ASSERT_EQ(sigaction(signal, nullptr, &action), 0);
        EXPECT_NE(action.sa_flags & SA_ONSTACK, 0) << "Signal " << signal;

        TearDown();
    }
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckProfilerHandlerIsNotChainedToItself)
{
    Init(SIGSEGV);
    auto* signalManager = ProfilerSignalManager::Get(SIGSEGV);

    // Get the address of ProfilerSignalManager::SignalHandler (private)
    ASSERT_TRUE(signalManager->RegisterHandler(CustomHandlerThatChains));
    struct sigaction profilerAction;
    ASSERT_EQ(sigaction(SIGSEGV, nullptr, &profilerAction), 0);
    signalManager->Reset();

    // The previous action is now the profiler handler itself
    ASSERT_EQ(sigaction(SIGSEGV, &profilerAction, nullptr), 0);
    ASSERT_TRUE(signalManager->RegisterHandler(CustomHandlerThatChains));
    s_customHandlerCalls = 0;

    raise(SIGSEGV);
    EXPECT_EQ(s_customHandlerCalls.load(), 1);

    raise(SIGSEGV);
    EXPECT_EQ(s_customHandlerCalls.load(), 2);
}

#endif
