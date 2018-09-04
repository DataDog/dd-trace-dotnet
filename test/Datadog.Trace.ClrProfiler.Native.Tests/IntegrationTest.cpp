#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/Integration.h"

using namespace trace;

TEST(IntegrationTest, AssemblyReference) {
  AssemblyReference ref(
      L"Some.Assembly, Version=1.2.3.4, Culture=notneutral, "
      L"PublicKeyToken=0123456789abcdef");

  EXPECT_EQ(ref.name, L"Some.Assembly");
  EXPECT_EQ(ref.version, Version(1, 2, 3, 4));
  EXPECT_EQ(ref.locale, L"notneutral");
  EXPECT_EQ(ref.public_key,
            PublicKey({0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef}));
}

TEST(IntegrationTest, AssemblyReferenceNameOnly) {
  AssemblyReference ref(L"Some.Assembly");

  EXPECT_EQ(ref.name, L"Some.Assembly");
  EXPECT_EQ(ref.version, Version(0, 0, 0, 0));
  EXPECT_EQ(ref.locale, L"neutral");
  EXPECT_EQ(ref.public_key, PublicKey({0, 0, 0, 0, 0, 0, 0, 0}));
}

TEST(IntegrationTest, AssemblyReferenceInvalidPublicKey) {
  AssemblyReference ref(L"Some.Assembly, PublicKeyToken=xyz");
  EXPECT_EQ(ref.public_key, PublicKey({0, 0, 0, 0, 0, 0, 0, 0}));
}

TEST(IntegrationTest, AssemblyReferenceNullPublicKey) {
  AssemblyReference ref(L"Some.Assembly, PublicKeyToken=null");
  EXPECT_EQ(ref.public_key, PublicKey({0, 0, 0, 0, 0, 0, 0, 0}));
}

TEST(IntegrationTest, AssemblyReferencePartialVersion) {
  AssemblyReference ref(L"Some.Assembly, Version=1.2.3");
  EXPECT_EQ(ref.version, Version(0, 0, 0, 0));
}

TEST(IntegrationTest, AssemblyReferenceInvalidVersion) {
  AssemblyReference ref(L"Some.Assembly, Version=xyz");
  EXPECT_EQ(ref.version, Version(0, 0, 0, 0));
}
