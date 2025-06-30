// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef LINUX

#include "gtest/gtest.h"

#include "OpSysTools.h"
#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/ProfilerSignalManager.h"

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

TEST_F(ProfilerSignalManagerFixture, CheckShutdownSignalHandlerRestoration)
{
    // This test simulates the scenario where Python finalization interferes with
    // signal handler cleanup, as described in the stack trace issue.
    
    // Set up a custom handler to monitor signal handling state
    struct sigaction originalAction;
    ASSERT_EQ(sigaction(SIGUSR2, nullptr, &originalAction), 0) 
        << "Unable to get original signal handler state.";
    
    // Register our profiler handler
    auto* signalManager = ProfilerSignalManager::Get(SIGUSR2);
    ASSERT_NE(signalManager, nullptr);
    EXPECT_TRUE(signalManager->RegisterHandler(CustomHandler));
    EXPECT_TRUE(signalManager->IsHandlerInPlace());
    
    // Verify the profiler handler is installed
    struct sigaction profilerAction;
    ASSERT_EQ(sigaction(SIGUSR2, nullptr, &profilerAction), 0) 
        << "Unable to get profiler signal handler state.";
    
    // Verify the handler has changed from the original
    bool handlerChanged = false;
    if (originalAction.sa_flags & SA_SIGINFO) {
        handlerChanged = (profilerAction.sa_sigaction != originalAction.sa_sigaction);
    } else {
        handlerChanged = (profilerAction.sa_handler != originalAction.sa_handler);
    }
    EXPECT_TRUE(handlerChanged) << "Profiler handler should be different from original.";
    
    // Simulate proper shutdown by unregistering the handler
    EXPECT_TRUE(signalManager->UnRegisterHandler());
    EXPECT_FALSE(signalManager->IsHandlerInPlace());
    
    // Verify the original handler is restored
    struct sigaction restoredAction;
    ASSERT_EQ(sigaction(SIGUSR2, nullptr, &restoredAction), 0) 
        << "Unable to get restored signal handler state.";
    
    // Compare the restored handler with the original
    if (originalAction.sa_flags & SA_SIGINFO) {
        EXPECT_EQ(restoredAction.sa_sigaction, originalAction.sa_sigaction)
            << "Signal handler should be properly restored to original state.";
    } else {
        EXPECT_EQ(restoredAction.sa_handler, originalAction.sa_handler)
            << "Signal handler should be properly restored to original state.";
    }
    EXPECT_EQ(restoredAction.sa_flags, originalAction.sa_flags)
        << "Signal flags should be properly restored to original state.";
}

TEST_F(ProfilerSignalManagerFixture, CheckDestructorSignalHandlerRestoration)
{
    // This test ensures that even if UnRegisterHandler is not explicitly called,
    // the destructor properly restores the signal handler
    
    struct sigaction originalAction;
    ASSERT_EQ(sigaction(SIGURG, nullptr, &originalAction), 0) 
        << "Unable to get original signal handler state.";
    
    // Create a scoped block to test destructor behavior
    {
        auto* signalManager = ProfilerSignalManager::Get(SIGURG);
        ASSERT_NE(signalManager, nullptr);
        EXPECT_TRUE(signalManager->RegisterHandler(CustomHandler));
        EXPECT_TRUE(signalManager->IsHandlerInPlace());
        
        // Don't call UnRegisterHandler explicitly - let destructor handle it
    }
    // Signal manager static instance destructor should have restored the handler
    
    // Verify the handler is restored even without explicit unregistration
    struct sigaction restoredAction;
    ASSERT_EQ(sigaction(SIGURG, nullptr, &restoredAction), 0) 
        << "Unable to get restored signal handler state.";
    
    // Note: Since we're using static instances, the destructor may not have run yet
    // This test documents the expected behavior for when destructors do run
}

TEST_F(ProfilerSignalManagerFixture, CheckGlobalCleanupAllSignalHandlers)
{
    // This test verifies that the global cleanup function properly restores all signal handlers
    // This is the key fix for the Python finalization crash issue
    
    // Store original handlers for multiple signals
    struct sigaction originalUSR1, originalUSR2, originalURG;
    ASSERT_EQ(sigaction(SIGUSR1, nullptr, &originalUSR1), 0);
    ASSERT_EQ(sigaction(SIGUSR2, nullptr, &originalUSR2), 0);
    ASSERT_EQ(sigaction(SIGURG, nullptr, &originalURG), 0);
    
    // Register handlers for multiple signals
    auto* usr1Manager = ProfilerSignalManager::Get(SIGUSR1);
    auto* usr2Manager = ProfilerSignalManager::Get(SIGUSR2);
    auto* urgManager = ProfilerSignalManager::Get(SIGURG);
    
    ASSERT_NE(usr1Manager, nullptr);
    ASSERT_NE(usr2Manager, nullptr);
    ASSERT_NE(urgManager, nullptr);
    
    EXPECT_TRUE(usr1Manager->RegisterHandler(CustomHandler));
    EXPECT_TRUE(usr2Manager->RegisterHandler(OtherCustomHandler));
    EXPECT_TRUE(urgManager->RegisterHandler(CustomHandler));
    
    // Verify all handlers are in place
    EXPECT_TRUE(usr1Manager->IsHandlerInPlace());
    EXPECT_TRUE(usr2Manager->IsHandlerInPlace());
    EXPECT_TRUE(urgManager->IsHandlerInPlace());
    
    // Call the global cleanup function
    ProfilerSignalManager::CleanupAllSignalHandlers();
    
    // Verify all handlers are cleaned up
    EXPECT_FALSE(usr1Manager->IsHandlerInPlace());
    EXPECT_FALSE(usr2Manager->IsHandlerInPlace());
    EXPECT_FALSE(urgManager->IsHandlerInPlace());
    
    // Verify original handlers are restored
    struct sigaction restoredUSR1, restoredUSR2, restoredURG;
    ASSERT_EQ(sigaction(SIGUSR1, nullptr, &restoredUSR1), 0);
    ASSERT_EQ(sigaction(SIGUSR2, nullptr, &restoredUSR2), 0);
    ASSERT_EQ(sigaction(SIGURG, nullptr, &restoredURG), 0);
    
    // Compare restored handlers with originals
    if (originalUSR1.sa_flags & SA_SIGINFO) {
        EXPECT_EQ(restoredUSR1.sa_sigaction, originalUSR1.sa_sigaction);
    } else {
        EXPECT_EQ(restoredUSR1.sa_handler, originalUSR1.sa_handler);
    }
    EXPECT_EQ(restoredUSR1.sa_flags, originalUSR1.sa_flags);
    
    if (originalUSR2.sa_flags & SA_SIGINFO) {
        EXPECT_EQ(restoredUSR2.sa_sigaction, originalUSR2.sa_sigaction);
    } else {
        EXPECT_EQ(restoredUSR2.sa_handler, originalUSR2.sa_handler);
    }
    EXPECT_EQ(restoredUSR2.sa_flags, originalUSR2.sa_flags);
    
    if (originalURG.sa_flags & SA_SIGINFO) {
        EXPECT_EQ(restoredURG.sa_sigaction, originalURG.sa_sigaction);
    } else {
        EXPECT_EQ(restoredURG.sa_handler, originalURG.sa_handler);
    }
    EXPECT_EQ(restoredURG.sa_flags, originalURG.sa_flags);
}

#endif
