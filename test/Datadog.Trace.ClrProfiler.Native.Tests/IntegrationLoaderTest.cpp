#include "pch.h"

#include <codecvt>
#include <filesystem>
#include <fstream>
#include <locale>
#include <sstream>
#include <string>

#include "../../src/Datadog.Trace.ClrProfiler.Native/IntegrationLoader.h"

using namespace trace;

TEST(IntegrationLoaderTest, HandlesMissingFile) {
  auto integrations = LoadIntegrationsFromFile(L"missing-file");
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesInvalidIntegrationNoName) {
  std::stringstream str("[{}]");
  auto integrations = LoadIntegrationsFromStream(str);
  // 0 because name is required
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesInvalidIntegrationBadJson) {
  std::stringstream str("[");
  auto integrations = LoadIntegrationsFromStream(str);
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesInvalidIntegrationNotAnObject) {
  std::stringstream str("[1,2,3]");
  auto integrations = LoadIntegrationsFromStream(str);
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesInvalidIntegrationNotAnArray) {
  std::stringstream str(R"TEXT(
        {"name": "test-integration"}
    )TEXT");
  auto integrations = LoadIntegrationsFromStream(str);
  EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesSingleIntegrationWithNoMethods) {
  std::stringstream str(R"TEXT(
        [{ "name": "test-integration" }]
    )TEXT");

  auto integrations = LoadIntegrationsFromStream(str);
  EXPECT_EQ(1, integrations.size());
  EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());
  EXPECT_EQ(0, integrations[0].method_replacements.size());
}

TEST(IntegrationLoaderTest,
     HandlesSingleIntegrationWithInvalidMethodReplacementType) {
  std::stringstream str(R"TEXT(
        [{ "name": "test-integration", "method_replacements": 1234 }]
    )TEXT");

  auto integrations = LoadIntegrationsFromStream(str);
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

  auto integrations = LoadIntegrationsFromStream(str);
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

  auto integrations = LoadIntegrationsFromStream(str);
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

  auto integrations = LoadIntegrationsFromStream(str);
  EXPECT_EQ(1, integrations.size());
  EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());

  EXPECT_EQ(1, integrations[0].method_replacements.size());
  auto mr = integrations[0].method_replacements[0];
  EXPECT_STREQ(L"", mr.target_method.assembly.name.c_str());
  EXPECT_STREQ(L"", mr.target_method.type_name.c_str());
  EXPECT_STREQ(L"", mr.target_method.method_name.c_str());
}

TEST(IntegrationLoaderTest, LoadsFromEnvironment) {
  auto tmpname1 = std::filesystem::temp_directory_path() / "test-1.json";
  auto tmpname2 = std::filesystem::temp_directory_path() / "test-2.json";
  std::ofstream f;
  f.open(tmpname1);
  f << R"TEXT(
        [{ "name": "test-integration-1" }]
    )TEXT";
  f.close();
  f.open(tmpname2);
  f << R"TEXT(
        [{ "name": "test-integration-2" }]
    )TEXT";
  f.close();

  auto name = tmpname1.wstring() + L";" + tmpname2.wstring();

  SetEnvironmentVariableW(kIntegrationsEnvironmentName.data(), name.data());

  std::vector<std::wstring> expected_names = {L"test-integration-1",
                                              L"test-integration-2"};
  std::vector<std::wstring> actual_names;
  for (auto& integration : LoadIntegrationsFromEnvironment()) {
    actual_names.push_back(integration.integration_name);
  }
  EXPECT_EQ(expected_names, actual_names);

  std::filesystem::remove(tmpname1);
  std::filesystem::remove(tmpname2);
}
