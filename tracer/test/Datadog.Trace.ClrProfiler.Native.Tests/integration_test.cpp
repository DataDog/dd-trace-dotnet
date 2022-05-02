#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/integration.h"

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

TEST(IntegrationTest, TraceMethodParsingIncludesValidEntries)
{
    TypeReference integration(L"TestAssemblyName", L"TestTypeName", {}, {});

    std::vector<IntegrationDefinition> integrationDefinitions = GetIntegrationsFromTraceMethodsConfiguration(
        integration,
        L"Program[Main];Namespace.GenericCollection`1[Get];NonGenericType[GenericMethod]");
    EXPECT_EQ(integrationDefinitions.size(), 3);

    for (auto integrationDefinition : integrationDefinitions)
    {
        EXPECT_EQ(integrationDefinition.integration_type.assembly.name, integration.assembly.name);
        EXPECT_EQ(integrationDefinition.integration_type.name, integration.name);

        EXPECT_EQ(integrationDefinition.target_method.type.assembly.name, tracemethodintegration_assemblyname);
        EXPECT_EQ(integrationDefinition.target_method.signature_types.size(), 0);
        EXPECT_EQ(integrationDefinition.is_exact_signature_match, false);
        EXPECT_EQ(integrationDefinition.is_derived, false);
    }

    EXPECT_EQ(integrationDefinitions[0].target_method.type.name, L"Program");
    EXPECT_EQ(integrationDefinitions[0].target_method.method_name, L"Main");

    EXPECT_EQ(integrationDefinitions[1].target_method.type.name, L"Namespace.GenericCollection`1");
    EXPECT_EQ(integrationDefinitions[1].target_method.method_name, L"Get");

    EXPECT_EQ(integrationDefinitions[2].target_method.type.name, L"NonGenericType");
    EXPECT_EQ(integrationDefinitions[2].target_method.method_name, L"GenericMethod");
}

TEST(IntegrationTest, TraceMethodParsingExcludesInvalidEntries)
{
    TypeReference integration(L"TestAssemblyName", L"TestTypeName", {}, {});

    std::vector<IntegrationDefinition> integrationDefinitions = GetIntegrationsFromTraceMethodsConfiguration(
        integration,
        L"TypeWithNoMethodsSpecified;;TypeWithNoClose[;TypeWithNoOpen];TypeWithoutMethods[]");
    EXPECT_EQ(integrationDefinitions.size(), 0);
}