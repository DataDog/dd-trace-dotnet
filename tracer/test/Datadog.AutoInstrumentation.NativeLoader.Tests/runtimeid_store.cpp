#include "gtest/gtest.h"

#include "../../src/Datadog.AutoInstrumentation.NativeLoader/runtimeid_store.h"

using namespace datadog::shared::nativeloader;

TEST(runtimeid_store, SameIdIfAddMoreThanOnce)
{
    AppDomainID id = 42;
    RuntimeIdStore store;
    store.Generate(id);

    auto rid = store.Get(id);

    store.Generate(id);
    auto anotherRid = store.Get(id);

    ASSERT_EQ(rid, anotherRid);
}

TEST(runtimeid_store, DoNotThrowIfGetIdForUnknownAppDomain)
{
    AppDomainID id = 42;
    RuntimeIdStore store;

    EXPECT_NO_THROW(store.Get(id));
}