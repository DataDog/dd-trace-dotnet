// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef LINUX

#include "gtest/gtest.h"

#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/ProfilerSignalManager.h"

#include <future>
#include <signal.h>

class ProfilerSignalManagerFixture : public ::testing::Test
{
public:
    ProfilerSignalManagerFixture() = default;

    void SetUp() override
    {
        ProfilerSignalManager::Get()->Reset();
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
    auto* signalManager = ProfilerSignalManager::Get();

    EXPECT_FALSE(signalManager->IsHandlerInPlace());
    EXPECT_TRUE(signalManager->RegisterHandler(CustomHandler));
    EXPECT_TRUE(signalManager->IsHandlerInPlace());
}

TEST_F(ProfilerSignalManagerFixture, CanRegisterOnlyOneCustomHandler)
{
    auto* signalManager = ProfilerSignalManager::Get();

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
    auto* signalManager = ProfilerSignalManager::Get();
    EXPECT_TRUE(signalManager->RegisterHandler(CustomHandler));

    struct sigaction currentAction;
    EXPECT_EQ(sigaction(SIGUSR1, nullptr, &currentAction), 0) << "Unable to setup Test handler.";
    EXPECT_EQ(currentAction.sa_flags & SA_SIGINFO, SA_SIGINFO) << "sa_flags must contain the SA_SIGINFO mask to use sigaction handler.";
    EXPECT_EQ(currentAction.sa_flags & SA_RESTART, SA_RESTART) << "sa_flags must contain the SA_RESTART mask for threads that were interrupted while waiting on IO.";
    EXPECT_NE(currentAction.sa_sigaction, nullptr) << "sa_sigaction handler must not be nullptr.";
}

#endif