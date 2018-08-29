#include "pch.h"

#include <sstream>

#include "../../src/Datadog.Trace.ClrProfiler.Native/integration_loader.h"

using namespace trace;

TEST(IntegrationLoaderTest, HandlesMissingFile) {
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromFile(L"missing-file");
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesInvalidIntegrationNoName) {
  std::stringstream str("[{}]");
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromStream(str);
  // 0 because name is required
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesInvalidIntegrationBadJson) {
  std::stringstream str("[");
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromStream(str);
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesInvalidIntegrationNotAnObject) {
  std::stringstream str("[1,2,3]");
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromStream(str);
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesInvalidIntegrationNotAnArray) {
  std::stringstream str(R"TEXT(
        {"name": "test-integration"}
    )TEXT");
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromStream(str);
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesSingleIntegrationWithNoMethods) {
  std::stringstream str(R"TEXT(
        [{ "name": "test-integration" }]
    )TEXT");
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromStream(str);
  EXPECT_EQ(1, integrations.size());
  EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());
  EXPECT_EQ(0, integrations[0].method_replacements.size());
}

TEST(IntegrationLoaderTest,
     HandlesSingleIntegrationWithInvalidMethodReplacementType) {
  std::stringstream str(R"TEXT(
        [{ "name": "test-integration", "method_replacements": 1234 }]
    )TEXT");
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromStream(str);
  EXPECT_EQ(1, integrations.size());
  EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());
  EXPECT_EQ(0, integrations[0].method_replacements.size());
}

TEST(IntegrationLoaderTest, HandlesSingleIntegrationWithMethodReplacements) {
  std::stringstream str(R"TEXT(
        [{ 
            "name": "test-integration", 
            "method_replacements": [{
                "caller": { },
                "target": { "assembly": "Assembly.One", "type": "Type.One", "method": "Method.One" },
                "wrapper": { "assembly": "Assembly.Two", "type": "Type.Two", "method": "Method.Two", "signature": [0, 1, 1, 28] }
            }] 
        }]
    )TEXT");
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromStream(str);
  EXPECT_EQ(1, integrations.size());
  EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());

  EXPECT_EQ(1, integrations[0].method_replacements.size());
  auto mr = integrations[0].method_replacements[0];
  EXPECT_STREQ(L"", mr.caller_method.assembly.name.c_str());
  EXPECT_STREQ(L"", mr.caller_method.type_name.c_str());
  EXPECT_STREQ(L"", mr.caller_method.method_name.c_str());
  EXPECT_STREQ(L"Assembly.One", mr.target_method.assembly.name.c_str());
  EXPECT_STREQ(L"Type.One", mr.target_method.type_name.c_str());
  EXPECT_STREQ(L"Method.One", mr.target_method.method_name.c_str());
  EXPECT_STREQ(L"Assembly.Two", mr.wrapper_method.assembly.name.c_str());
  EXPECT_STREQ(L"Type.Two", mr.wrapper_method.type_name.c_str());
  EXPECT_STREQ(L"Method.Two", mr.wrapper_method.method_name.c_str());
  EXPECT_EQ(std::vector<uint8_t>({0, 1, 1, 28}),
            mr.wrapper_method.method_signature.data);
}

TEST(IntegrationLoaderTest, HandlesSingleIntegrationWithMissingCaller) {
  std::stringstream str(R"TEXT(
        [{ 
            "name": "test-integration", 
            "method_replacements": [{
                "target": { "assembly": "Assembly.One", "type": "Type.One", "method": "Method.One" },
                "wrapper": { "assembly": "Assembly.Two", "type": "Type.Two", "method": "Method.Two", "signature": [0, 1, 1, 28] }
            }] 
        }]
    )TEXT");
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromStream(str);
  EXPECT_EQ(1, integrations.size());
  EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());

  EXPECT_EQ(1, integrations[0].method_replacements.size());
  auto mr = integrations[0].method_replacements[0];
  EXPECT_STREQ(L"", mr.caller_method.assembly.name.c_str());
  EXPECT_STREQ(L"", mr.caller_method.type_name.c_str());
  EXPECT_STREQ(L"", mr.caller_method.method_name.c_str());
  EXPECT_STREQ(L"Assembly.One", mr.target_method.assembly.name.c_str());
  EXPECT_STREQ(L"Type.One", mr.target_method.type_name.c_str());
  EXPECT_STREQ(L"Method.One", mr.target_method.method_name.c_str());
  EXPECT_STREQ(L"Assembly.Two", mr.wrapper_method.assembly.name.c_str());
  EXPECT_STREQ(L"Type.Two", mr.wrapper_method.type_name.c_str());
  EXPECT_STREQ(L"Method.Two", mr.wrapper_method.method_name.c_str());
  EXPECT_EQ(std::vector<uint8_t>({0, 1, 1, 28}),
            mr.wrapper_method.method_signature.data);
}

TEST(IntegrationLoaderTest, HandlesSingleIntegrationWithInvalidTarget) {
  std::stringstream str(R"TEXT(
        [{ 
            "name": "test-integration", 
            "method_replacements": [{
                "target": 1234,
                "wrapper": { "assembly": "Assembly.Two", "type": "Type.Two", "method": "Method.Two" }
            }] 
        }]
    )TEXT");
  IntegrationLoader loader;
  auto integrations = loader.LoadIntegrationsFromStream(str);
  EXPECT_EQ(1, integrations.size());
  EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());

  EXPECT_EQ(1, integrations[0].method_replacements.size());
  auto mr = integrations[0].method_replacements[0];
  EXPECT_STREQ(L"", mr.target_method.assembly.name.c_str());
  EXPECT_STREQ(L"", mr.target_method.type_name.c_str());
  EXPECT_STREQ(L"", mr.target_method.method_name.c_str());
}