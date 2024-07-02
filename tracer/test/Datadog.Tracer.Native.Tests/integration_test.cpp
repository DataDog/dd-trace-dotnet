#include "pch.h"

#include "../../src/Datadog.Tracer.Native/integration.h"
#include "../../src/Datadog.Tracer.Native/tracer_integration_definition.h"

using namespace trace;

TEST(IntegrationTest, AssemblyReference) {
  AssemblyReference ref(
      WStr("Some.Assembly, Version=1.2.3.4, Culture=notneutral, PublicKeyToken=0123456789abcdef"));

  EXPECT_EQ(ref.name, WStr("Some.Assembly"));
  EXPECT_EQ(ref.version, Version(1, 2, 3, 4));
  EXPECT_EQ(ref.locale, WStr("notneutral"));
  EXPECT_EQ(ref.public_key,
            PublicKey({0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef}));
}

TEST(IntegrationTest, AssemblyReferenceNameOnly) {
  AssemblyReference ref(WStr("Some.Assembly"));

  EXPECT_EQ(ref.name, WStr("Some.Assembly"));
  EXPECT_EQ(ref.version, Version(0, 0, 0, 0));
  EXPECT_EQ(ref.locale, WStr("neutral"));
  EXPECT_EQ(ref.public_key, PublicKey({0, 0, 0, 0, 0, 0, 0, 0}));
}

TEST(IntegrationTest, AssemblyReferenceInvalidPublicKey) {
  AssemblyReference ref(WStr("Some.Assembly, PublicKeyToken=xyz"));
  EXPECT_EQ(ref.public_key, PublicKey({0, 0, 0, 0, 0, 0, 0, 0}));
}

TEST(IntegrationTest, AssemblyReferenceNullPublicKey) {
  AssemblyReference ref(WStr("Some.Assembly, PublicKeyToken=nulWStr("));
  EXPECT_EQ(ref.public_key, PublicKey({0, 0, 0, 0, 0, 0, 0, 0}));
}

TEST(IntegrationTest, AssemblyReferencePartialVersion) {
  AssemblyReference ref(WStr("Some.Assembly, Version=1.2.3"));
  EXPECT_EQ(ref.version, Version(0, 0, 0, 0));
}

TEST(IntegrationTest, AssemblyReferenceInvalidVersion) {
  AssemblyReference ref(WStr("Some.Assembly, Version=xyz"));
  EXPECT_EQ(ref.version, Version(0, 0, 0, 0));
}

TEST(IntegrationTest, TraceMethodParsingIncludesValidEntries)
{
    TypeReference integration(WStr("TestAssemblyName"), WStr("TestTypeName"), {}, {});

    std::vector<IntegrationDefinition> integrationDefinitions = GetIntegrationsFromTraceMethodsConfiguration(
        integration,
        WStr("Program[Main];Namespace.GenericCollection`1[Get];NonGenericType[GenericMethod]"));
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

    EXPECT_EQ(integrationDefinitions[0].target_method.type.name, WStr("Program"));
    EXPECT_EQ(integrationDefinitions[0].target_method.method_name, WStr("Main"));

    EXPECT_EQ(integrationDefinitions[1].target_method.type.name, WStr("Namespace.GenericCollection`1"));
    EXPECT_EQ(integrationDefinitions[1].target_method.method_name, WStr("Get"));

    EXPECT_EQ(integrationDefinitions[2].target_method.type.name, WStr("NonGenericType"));
    EXPECT_EQ(integrationDefinitions[2].target_method.method_name, WStr("GenericMethod"));
}

TEST(IntegrationTest, TraceMethodParsingExcludesInvalidEntries)
{
    TypeReference integration(WStr("TestAssemblyName"), WStr("TestTypeName"), {}, {});

    std::vector<IntegrationDefinition> integrationDefinitions = GetIntegrationsFromTraceMethodsConfiguration(
        integration,
        WStr("TypeWithNoMethodsSpecified;;TypeWithNoClose[;TypeWithNoOpen];TypeWithoutMethods[]"));
    EXPECT_EQ(integrationDefinitions.size(), 0);
}
