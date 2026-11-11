#include "gtest/gtest.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/runtimeid_store.h"

// we _MUST_ define/declare those tests in the same namespace as the runtimeid_store class
// to ensure the friend trick works.
namespace datadog::shared::nativeloader
{
TEST(runtimeid_store, EnsureRuntimeIdIsNotEmptyAtFirstCall)
{
    AppDomainID id = 42;
    RuntimeIdStore store;
    auto const& rid = store.Get(id);

    ASSERT_FALSE(rid.empty());
}

TEST(runtimeid_store, EnsureRuntimeIdIsTheSameAfterManyCallsToGetForTheSameAppDomain)
{
    AppDomainID id = 42;
    RuntimeIdStore store;
    auto const& rid = store.Get(id);

    auto const& rid2 = store.Get(id);

    ASSERT_FALSE(rid.empty());
    ASSERT_FALSE(rid2.empty());
    ASSERT_EQ(rid, rid2);

    auto const& rid3 = store.Get(id);

    ASSERT_EQ(rid, rid3);
}

TEST(runtimeid_store, EnsureRuntimeIsDifferentFor2DifferentAppDomains)
{
    AppDomainID appId1 = 42;
    AppDomainID appId2 = 21;

    RuntimeIdStore store(true);
    auto const& rid = store.Get(appId1);

    auto const& rid2 = store.Get(appId2);

    ASSERT_FALSE(rid.empty());
    ASSERT_FALSE(rid2.empty());
    ASSERT_NE(rid, rid2);
}

TEST(runtimeid_store, EnsureNewAppDomainAtReusedAddressGetsNewRuntimeId)
{
    AppDomainID reusedId = 42;
    AppDomainID otherId = 21;

    RuntimeIdStore store(true);
    const char* oldRidPtr = store.Get(reusedId).c_str();
    const std::string oldRid = oldRidPtr;
    const std::string otherRid = store.Get(otherId);

    store.OnAppDomainCreationStarted(reusedId);
    auto const& newRid = store.Get(reusedId);

    ASSERT_FALSE(newRid.empty());
    ASSERT_NE(oldRid, newRid);
    ASSERT_EQ(newRid, store.Get(reusedId));
    ASSERT_EQ(oldRid, oldRidPtr);
    ASSERT_EQ(otherRid, store.Get(otherId));
}

TEST(runtimeid_store, EnsureNewAppDomainAtUnseenAddressGetsRuntimeId)
{
    AppDomainID newId = 42;
    AppDomainID otherId = 21;

    RuntimeIdStore store(true);
    const std::string otherRid = store.Get(otherId);

    store.OnAppDomainCreationStarted(newId);
    auto const& newRid = store.Get(newId);

    ASSERT_FALSE(newRid.empty());
    ASSERT_NE(otherRid, newRid);
    ASSERT_EQ(newRid, store.Get(newId));
    ASSERT_EQ(otherRid, store.Get(otherId));
}

TEST(runtimeid_store, EnsureRuntimeIdIsPerProcessOutsideIisWhenAppDomainIsCreated)
{
    AppDomainID id = 42;
    RuntimeIdStore store;
    const std::string rid = store.Get(id);

    store.OnAppDomainCreationStarted(id);

    ASSERT_EQ(rid, store.Get(id));
}
} // namespace datadog::shared::nativeloader