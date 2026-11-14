// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef LINUX

#include "gtest/gtest.h"

#include "MemoryFaultGuard.h"
#include "OpSysTools.h"
#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/ProfilerSignalManager.h"

#include <atomic>
#include <chrono>
#include <csetjmp>
#include <cstdio>
#include <future>
#include <pthread.h>
#include <signal.h>
#include <string>
#include <sys/mman.h>
#include <sys/prctl.h>
#include <thread>
#include <unistd.h>
#include <vector>

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

// Where the last call of ClrLikePreviousHandler ran
bool s_previousHandlerWasOnAlternateStack = false;
sigset_t s_previousHandlerMask;

void PreviousHandlerThatDoesNotReturn(int /*signal*/, siginfo_t* /*info*/, void* /*context*/)
{
    s_previousHandlerCalls++;
    siglongjmp(s_fromPreviousHandler, 1);
}

// Ends a loop on the faulting instruction, so that the test fails instead of hanging
void StopFaultLoop(int /*signal*/)
{
    siglongjmp(s_fromPreviousHandler, 2);
}

// A thread that faults in a loop, each fault handled by a CLR-like handler (it leaves with siglongjmp).
// Pause n: s_pauseRequest = n, wait for s_pausedFor == n (no fault in progress), then s_resumed = n.
thread_local sigjmp_buf t_fromFault;
std::atomic<bool> s_stopFaulting{false};
std::atomic<int> s_pauseRequest{0};
std::atomic<int> s_pausedFor{0};
std::atomic<int> s_resumed{0};

void PreviousHandlerForFaultingThread(int /*signal*/, siginfo_t* /*info*/, void* /*context*/)
{
    siglongjmp(t_fromFault, 1);
}

void FaultInALoop()
{
    while (!s_stopFaulting)
    {
        const int pause = s_pauseRequest;
        if (pause > s_resumed)
        {
            s_pausedFor = pause;
            while (s_resumed < pause && !s_stopFaulting)
            {
            }
            continue;
        }

        if (sigsetjmp(t_fromFault, 1) == 0)
        {
            volatile int* volatile address = nullptr;
            *address = 1;
        }
    }
}

bool IsOnAlternateStack()
{
    stack_t stack;
    return sigaltstack(nullptr, &stack) == 0 && (stack.ss_flags & SS_ONSTACK) != 0;
}

sigset_t GetSignalMask()
{
    sigset_t mask;
    pthread_sigmask(SIG_BLOCK, nullptr, &mask);
    return mask;
}

// Like the CLR handler for a hardware exception in managed code: it unblocks the signal, and SIGRTMIN
// (the CLR suspension signal) for SIGSEGV, and does not return.
void ClrLikePreviousHandler(int signal, siginfo_t* /*info*/, void* /*context*/)
{
    s_previousHandlerCalls++;
    s_previousHandlerWasOnAlternateStack = IsOnAlternateStack();
    s_previousHandlerMask = GetSignalMask();

    sigset_t unblocked;
    sigemptyset(&unblocked);
    sigaddset(&unblocked, signal);
    if (signal == SIGSEGV)
    {
        sigaddset(&unblocked, SIGRTMIN);
    }
    pthread_sigmask(SIG_UNBLOCK, &unblocked, nullptr);
    siglongjmp(s_fromPreviousHandler, 1);
}

// The signals blocked in only one of the masks (empty if there are none)
std::string GetMaskDifference(const sigset_t& expected, const sigset_t& actual)
{
    std::string difference;
    for (int signal = 1; signal <= SIGRTMAX; signal++)
    {
        bool isExpected = sigismember(&expected, signal) == 1;
        if (isExpected != (sigismember(&actual, signal) == 1))
        {
            difference += "signal " + std::to_string(signal) + (isExpected ? " is not blocked. " : " is blocked. ");
        }
    }
    return difference;
}

bool CustomHandlerThatChains(int /*signal*/, siginfo_t* /*info*/, void* /*context*/)
{
    s_customHandlerCalls++;
    return false;
}

MemoryFaultGuard::RunResult FaultUnderGuard()
{
    return MemoryFaultGuard::Run([] {
        volatile int* p = nullptr;
        *p = 1;
    });
}

// The CLR gives each of its threads an alternate stack
class AlternateStack
{
public:
    AlternateStack() :
        _memory(64 * 1024)
    {
        stack_t stack = {};
        stack.ss_sp = _memory.data();
        stack.ss_size = _memory.size();
        _isInstalled = sigaltstack(&stack, &_previousStack) == 0;
    }

    ~AlternateStack()
    {
        if (_isInstalled)
        {
            sigaltstack(&_previousStack, nullptr);
        }
    }

    bool IsInstalled() const
    {
        return _isInstalled;
    }

private:
    std::vector<char> _memory;
    stack_t _previousStack = {};
    bool _isInstalled = false;
};
} // namespace

class ProfilerSignalManagerFaultSignalsFixture : public ::testing::Test
{
public:
    void SetUp() override
    {
        // Writing to a mapped page beyond the end of the (empty) file raises SIGBUS
        FILE* file = tmpfile();
        ASSERT_NE(file, nullptr);
        void* page = mmap(nullptr, getpagesize(), PROT_READ | PROT_WRITE, MAP_SHARED, fileno(file), 0);
        fclose(file);
        ASSERT_NE(page, MAP_FAILED);
        _pageBeyondEndOfFile = static_cast<char*>(page);
    }

    void TearDown() override
    {
        RestoreSavedState();
    }

    ~ProfilerSignalManagerFaultSignalsFixture() override
    {
        if (_pageBeyondEndOfFile != nullptr)
        {
            munmap(_pageBeyondEndOfFile, getpagesize());
        }
    }

protected:
    // Reset() clears the callback and restores only the chained action. Put the
    // callback back too, or MemoryFaultGuard stays "installed" with no handler and
    // the next intentional fault loops.
    void RestoreSavedState()
    {
        if (!_hasSavedState)
        {
            return;
        }

        ProfilerSignalManager::Get(_signal)->RestoreState(_saved);
        _hasSavedState = false;
    }

    void Init(int signal)
    {
        RestoreSavedState();
        _signal = signal;
        s_previousHandlerCalls = 0;
        s_customHandlerCalls = 0;
        _saved = ProfilerSignalManager::Get(signal)->SaveState();
        _hasSavedState = true;
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

    // The CLR registers its SIGSEGV handler on the alternate stack with SIGRTMIN blocked, and its SIGBUS
    // handler with neither (handle_signal in pal/src/exception/signal.cpp).
    void InstallClrLikePreviousAction()
    {
        struct sigaction action = {};
        action.sa_flags = SA_RESTART | SA_SIGINFO;
        action.sa_sigaction = ClrLikePreviousHandler;
        sigemptyset(&action.sa_mask);
        if (_signal == SIGSEGV)
        {
            action.sa_flags |= SA_ONSTACK;
            sigaddset(&action.sa_mask, SIGRTMIN);
        }
        ASSERT_EQ(sigaction(_signal, &action, nullptr), 0);
    }

    // SIG_DFL or SIG_IGN
    void InstallPreviousDisposition(void (*disposition)(int))
    {
        struct sigaction action = {};
        action.sa_handler = disposition;
        sigemptyset(&action.sa_mask);
        ASSERT_EQ(sigaction(_signal, &action, nullptr), 0);
    }

    // A real fault: unlike raise(), the faulting instruction is restarted if the handlers return.
    void Fault()
    {
        if (_signal == SIGSEGV)
        {
            volatile int* volatile address = nullptr;
            *address = 1;
        }
        else
        {
            *static_cast<volatile char*>(_pageBeyondEndOfFile) = 1;
        }
    }

    // savemask = 0: the CLR handler does not return, so the kernel does not restore the signal mask.
    // Returns false if the fault is still not handled after 5 seconds: the thread loops on the faulting
    // instruction (https://github.com/DataDog/dd-trace-dotnet/issues/9563).
    bool DeliverFault()
    {
        const sigset_t mask = GetSignalMask();
        struct sigaction timeoutAction = {};
        timeoutAction.sa_handler = StopFaultLoop;
        sigemptyset(&timeoutAction.sa_mask);
        struct sigaction previousTimeoutAction;
        sigaction(SIGALRM, &timeoutAction, &previousTimeoutAction);

        const int jump = sigsetjmp(s_fromPreviousHandler, 0);
        if (jump == 0)
        {
            alarm(5);
            Fault();
        }
        alarm(0);
        sigaction(SIGALRM, &previousTimeoutAction, nullptr);
        if (jump == 2)
        {
            pthread_sigmask(SIG_SETMASK, &mask, nullptr);
        }
        return jump != 2;
    }

    // The profiler handler calls the previous one: it must run on the same stack and with the same signals
    // blocked as when the kernel calls it. As it does not return, what it does not unblock stays blocked.
    void CheckPreviousHandlerRunsAsIfCalledByTheKernel(int signal)
    {
        AlternateStack alternateStack;
        ASSERT_TRUE(alternateStack.IsInstalled());
        Init(signal);
        ASSERT_NO_FATAL_FAILURE(InstallClrLikePreviousAction());
        const sigset_t maskBeforeFault = GetSignalMask();

        ASSERT_TRUE(DeliverFault());
        ASSERT_EQ(s_previousHandlerCalls.load(), 1);
        const bool wasOnAlternateStack = s_previousHandlerWasOnAlternateStack;
        const sigset_t expectedMask = s_previousHandlerMask;
        EXPECT_EQ(wasOnAlternateStack, signal == SIGSEGV);
        EXPECT_EQ(GetMaskDifference(maskBeforeFault, GetSignalMask()), "");
        pthread_sigmask(SIG_SETMASK, &maskBeforeFault, nullptr);

        ASSERT_TRUE(ProfilerSignalManager::Get(signal)->RegisterHandler(CustomHandlerThatChains));
        ASSERT_TRUE(DeliverFault()) << "The thread loops on the faulting instruction";
        const sigset_t maskAfterFault = GetSignalMask();
        pthread_sigmask(SIG_SETMASK, &maskBeforeFault, nullptr);

        ASSERT_EQ(s_previousHandlerCalls.load(), 2);
        EXPECT_EQ(s_previousHandlerWasOnAlternateStack, wasOnAlternateStack) << "Alternate stack";
        EXPECT_EQ(GetMaskDifference(expectedMask, s_previousHandlerMask), "") << "Mask in the previous handler";
        EXPECT_EQ(GetMaskDifference(maskBeforeFault, maskAfterFault), "") << "Mask after the previous handler";
    }

    // With nothing to chain to, the profiler handler must not return: the faulting instruction would be
    // restarted forever (https://github.com/DataDog/dd-trace-dotnet/issues/9563). The fault must end the
    // process, as without the profiler.
    void CheckFaultEndsTheProcess()
    {
        testing::FLAGS_gtest_death_test_style = "threadsafe";
        EXPECT_EXIT(
            {
                alarm(5);                  // instead of hanging forever
                prctl(PR_SET_DUMPABLE, 0); // no core dump
                Fault();
            },
            testing::KilledBySignal(_signal), "");
    }

    int _signal = 0;
    bool _hasSavedState = false;
    ProfilerSignalManager::SavedState _saved{};
    char* _pageBeyondEndOfFile = nullptr;
};

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckPreviousSigsegvHandlerIsCalledOnEachSignal)
{
    CheckPreviousHandlerIsCalledOnEachSignal(SIGSEGV);
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckPreviousSigbusHandlerIsCalledOnEachSignal)
{
    CheckPreviousHandlerIsCalledOnEachSignal(SIGBUS);
}

// The .NET 6 CLR SIGSEGV handler crashes if it is called from the thread stack, and the CLR suspension
// signal must not interrupt it on the alternate stack. The CLR SIGBUS handler runs on the thread stack.
TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckPreviousSigsegvHandlerRunsAsIfCalledByTheKernel)
{
    CheckPreviousHandlerRunsAsIfCalledByTheKernel(SIGSEGV);
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckPreviousSigbusHandlerRunsAsIfCalledByTheKernel)
{
    CheckPreviousHandlerRunsAsIfCalledByTheKernel(SIGBUS);
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckRegisteringAgainKeepsThePreviousHandler)
{
    Init(SIGSEGV);
    auto* signalManager = ProfilerSignalManager::Get(SIGSEGV);
    ASSERT_NO_FATAL_FAILURE(InstallPreviousAction(PreviousHandlerThatDoesNotReturn));
    ASSERT_TRUE(signalManager->RegisterHandler(CustomHandlerThatChains));

    // Registered again while the profiler handler is installed: it must not become its own previous handler
    struct sigaction profilerAction;
    ASSERT_EQ(sigaction(SIGSEGV, nullptr, &profilerAction), 0);
    signalManager->Reset();
    ASSERT_EQ(sigaction(SIGSEGV, &profilerAction, nullptr), 0);
    ASSERT_TRUE(signalManager->RegisterHandler(CustomHandlerThatChains));

    Deliver();
    EXPECT_EQ(s_customHandlerCalls.load(), 1);
    EXPECT_EQ(s_previousHandlerCalls.load(), 1);
}

// Another thread can fault as soon as the profiler handler is installed: the handler must already know the
// CLR handler. Otherwise it finds nothing to chain to, and the fault ends the process.
TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckFaultDuringRegistrationIsChained)
{
    Init(SIGSEGV);
    testing::FLAGS_gtest_death_test_style = "threadsafe";
    EXPECT_EXIT(
        {
            alarm(10);                 // instead of hanging forever
            prctl(PR_SET_DUMPABLE, 0); // no core dump

            // The state of a manager that has never registered, with the CLR handler installed
            ProfilerSignalManager::SavedState firstRegistration = {};
            firstRegistration.installedAction.sa_flags = SA_SIGINFO;
            firstRegistration.installedAction.sa_sigaction = PreviousHandlerForFaultingThread;
            sigemptyset(&firstRegistration.installedAction.sa_mask);
            firstRegistration.canReplaceSignalHandler = true;
            auto* signalManager = ProfilerSignalManager::Get(SIGSEGV);

            s_pauseRequest = 1;
            std::thread faultingThread(FaultInALoop);
            // The window is a few instructions long: it is not hit on every run, but often enough to notice.
            // Bounded in time too, for the sanitizer builds.
            const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(2);
            for (int i = 1; i <= 300000 && std::chrono::steady_clock::now() < deadline; i++)
            {
                s_pauseRequest = i;
                while (s_pausedFor != i)
                {
                }
                signalManager->RestoreState(firstRegistration);
                s_resumed = i;
                signalManager->RegisterHandler(CustomHandlerThatChains);
            }
            s_stopFaulting = true;
            faultingThread.join();
            _exit(0);
        },
        testing::ExitedWithCode(0), "");
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckSigsegvEndsTheProcessWithoutPreviousHandler)
{
    Init(SIGSEGV);
    ASSERT_NO_FATAL_FAILURE(InstallPreviousDisposition(SIG_DFL));
    ASSERT_TRUE(ProfilerSignalManager::Get(SIGSEGV)->RegisterHandler(CustomHandlerThatChains));
    CheckFaultEndsTheProcess();
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckSigbusEndsTheProcessWithoutPreviousHandler)
{
    Init(SIGBUS);
    ASSERT_NO_FATAL_FAILURE(InstallPreviousDisposition(SIG_DFL));
    ASSERT_TRUE(ProfilerSignalManager::Get(SIGBUS)->RegisterHandler(CustomHandlerThatChains));
    CheckFaultEndsTheProcess();
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckSigsegvEndsTheProcessWhenThePreviousActionIgnoresIt)
{
    // Without the profiler, the kernel ends the process: a fault cannot be ignored
    Init(SIGSEGV);
    ASSERT_NO_FATAL_FAILURE(InstallPreviousDisposition(SIG_IGN));
    ASSERT_TRUE(ProfilerSignalManager::Get(SIGSEGV)->RegisterHandler(CustomHandlerThatChains));
    CheckFaultEndsTheProcess();
}

TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckMemoryFaultGuardStillRecoversAfterTearDown)
{
    ASSERT_EQ(FaultUnderGuard(), MemoryFaultGuard::RunResult::Faulted);

    for (int signal : {SIGSEGV, SIGBUS})
    {
        Init(signal);
        ASSERT_NO_FATAL_FAILURE(InstallPreviousAction(PreviousHandlerThatDoesNotReturn));
        ASSERT_TRUE(ProfilerSignalManager::Get(signal)->RegisterHandler(CustomHandlerThatChains));
        Deliver();
        TearDown();
    }

    ASSERT_EQ(FaultUnderGuard(), MemoryFaultGuard::RunResult::Faulted);
}

// The profiler SIGSEGV handler blocks what the CLR one blocks (SIGRTMIN). MemoryFaultGuard leaves it with
// siglongjmp, so the kernel does not restore the signal mask: the guard must.
TEST_F(ProfilerSignalManagerFaultSignalsFixture, CheckMemoryFaultGuardRestoresTheSignalMask)
{
    // Installs the guard first, so that its handler is part of the saved state
    ASSERT_EQ(FaultUnderGuard(), MemoryFaultGuard::RunResult::Faulted);

    AlternateStack alternateStack;
    ASSERT_TRUE(alternateStack.IsInstalled());
    Init(SIGSEGV);
    ASSERT_NO_FATAL_FAILURE(InstallClrLikePreviousAction());
    ASSERT_TRUE(ProfilerSignalManager::Get(SIGSEGV)->RegisterHandler(&MemoryFaultGuard::details::FaultHandler));
    const sigset_t maskBeforeFault = GetSignalMask();

    for (int i = 1; i <= 2; i++)
    {
        ASSERT_EQ(FaultUnderGuard(), MemoryFaultGuard::RunResult::Faulted);
        EXPECT_EQ(GetMaskDifference(maskBeforeFault, GetSignalMask()), "") << "Fault #" << i;
    }
    EXPECT_EQ(s_previousHandlerCalls.load(), 0);
}

#endif
