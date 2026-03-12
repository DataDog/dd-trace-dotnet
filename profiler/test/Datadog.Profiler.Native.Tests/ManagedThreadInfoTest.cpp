#include "gtest/gtest.h"

#include "ManagedThreadInfo.h"
#include "shared/src/native-src/string.h"

TEST(ManagedThreadInfoTest, CheckGetProfileThreadId)
{
    auto threadInfo = ManagedThreadInfo::CreateForTest(1);
    ASSERT_EQ(threadInfo->GetProfileThreadId(), "1");
}

TEST(ManagedThreadInfoTest, CheckGetProfileThreadIdWithSetThreadName)
{
    auto threadInfo = ManagedThreadInfo::CreateForTest(42);
    threadInfo->SetThreadName(WStr("Test Thread"));
    ASSERT_EQ(threadInfo->GetProfileThreadId(), "42");
}

TEST(ManagedThreadInfoTest, CheckGetProfileThreadName)
{
    auto threadInfo = ManagedThreadInfo::CreateForTest(1);
    ASSERT_EQ(threadInfo->GetProfileThreadName(), "");
}

TEST(ManagedThreadInfoTest, CheckGetProfileThreadNameWithSetThreadName)
{
    auto threadInfo = ManagedThreadInfo::CreateForTest(1);
    threadInfo->SetThreadName(WStr("Test Thread"));
    ASSERT_EQ(threadInfo->GetProfileThreadName(), "Test Thread");
}