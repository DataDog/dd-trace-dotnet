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

#endif
