#include "pch.h"

#include "../../src/Datadog.Tracer.Native/rejit_handler.h"

class RejitHandlerTests : public ::testing::Test
{
};

TEST(RejitHandlerTests, HasBeenRejittedUsesSet)
{
    trace::RejitHandler handler(static_cast<ICorProfilerInfo7*>(nullptr), nullptr);
    handler.SetRejitTracking(true);

    const trace::MethodKey key{static_cast<ModuleID>(1), static_cast<mdMethodDef>(2)};
    handler.AddRejitHistoryEntryForTest(key.module_id, key.method_def);

    EXPECT_TRUE(handler.HasBeenRejitted(key.module_id, key.method_def));
    EXPECT_FALSE(handler.HasBeenRejitted(key.module_id, static_cast<mdMethodDef>(3)));
}
