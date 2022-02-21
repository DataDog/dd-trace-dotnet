#include "gtest/gtest.h"

#include "../../src/Datadog.AutoInstrumentation.NativeLoader/runtimeid_store.h"

using namespace datadog::shared::nativeloader;

TEST(runtimeid_store, EnsureWeHandleTheCaseWhereTheClrReuseTheSameAppDomainID)
{
    AppDomainID id = 42;
    RuntimeIdStore store;
    store.Generate(id);

    auto rid = store.Get(id);

    store.Generate(id);
    auto anotherRid = store.Get(id);

    ASSERT_NE(rid, anotherRid);
}

TEST(runtimeid_store, DoNotThrowIfGetIdForUnknownAppDomain)
{
    AppDomainID id = 42;
    RuntimeIdStore store;

    std::string rid;
    EXPECT_NO_THROW(rid = store.Get(id));
    ASSERT_EQ("", rid);
}

TEST(runtimeid_store, MakeSureRuntimeIdIsNotEmptyAfterCallToGenerate)
{
    AppDomainID id = 42;
    RuntimeIdStore store;

    store.Get(id); // associate emtpy string to id.

    store.Generate(id);

    ASSERT_NE("", store.Get(id));
}