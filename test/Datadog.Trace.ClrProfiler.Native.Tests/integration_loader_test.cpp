#include "pch.h"

#include <codecvt>
#include <filesystem>
#include <fstream>
#include <locale>
#include <sstream>
#include <string>

#include "../../src/Datadog.Trace.ClrProfiler.Native/integration_loader.h"
#include "../../src/Datadog.Trace.ClrProfiler.Native/environment_variables.h"

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
                "target": { "assembly": "Assembly.One", "type": "Type.One", "method": "Method.One", "minimum_major": 0, "minimum_minor": 1, "maximum_major": 10, "maximum_minor": 0 },
                "wrapper": { "assembly": "Assembly.Two", "type": "Type.Two", "method": "Method.Two", "signature": [0, 1, 1, 28] }
            }]
        }]
    )TEXT");

  auto integrations = LoadIntegrationsFromStream(str);
  EXPECT_EQ(1, integrations.size());
  EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());
}

TEST(IntegrationLoaderTest, DoesNotCrashWithOutOfRangeVersion) {
  std::stringstream str(R"TEXT(
        [{
            "name": "test-integration",
            "method_replacements": [{
                "caller": { },
                "target": { "assembly": "Assembly.One", "type": "Type.One", "method": "Method.One", "minimum_major": 0, "minimum_minor": 1, "maximum_major": 75555, "maximum_minor": 0 },
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
                "target": { "assembly": "Assembly.One", "type": "Type.One", "method": "Method.One", "minimum_major": 1, "minimum_minor": 2, "maximum_major": 10, "maximum_minor": 99 },
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
  EXPECT_STREQ(L"Method.Two", mr.wrapper_method.method_name.c_str());
  EXPECT_EQ(1, mr.target_method.min_version.major);
  EXPECT_EQ(2, mr.target_method.min_version.minor);
  EXPECT_EQ(0, mr.target_method.min_version.build);
  EXPECT_EQ(10, mr.target_method.max_version.major);
  EXPECT_EQ(99, mr.target_method.max_version.minor);
  EXPECT_EQ(USHRT_MAX, mr.target_method.max_version.build);
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

TEST(IntegrationLoaderTest, LoadsFromEnvironmentWithOnlyIntegrationsEnv) {
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
  std::vector<std::wstring> expected_names = {L"test-integration-1",
                                              L"test-integration-2"};
  std::vector<std::wstring> actual_names;

  SetEnvironmentVariableW(trace::environment::integrations_path.data(),
                          name.data());
  for (auto& integration : LoadIntegrationsFromEnvironment()) {
    actual_names.push_back(integration.integration_name);
  }

  SetEnvironmentVariableW(trace::environment::integrations_path.data(),
                          NULL);
  std::filesystem::remove(tmpname1);
  std::filesystem::remove(tmpname2);

  EXPECT_EQ(expected_names, actual_names);
}

TEST(IntegrationLoaderTest, LoadsFromEnvironmentWithOnlyTracerHomeEnv) {
  auto tracerHomeDir = std::filesystem::temp_directory_path() / "TracerHome1" / "";
  auto tmpIntegrations = tracerHomeDir / "integrations.json";
  std::filesystem::create_directory(tracerHomeDir);
  std::ofstream f;
  f.open(tmpIntegrations);
  f << R"TEXT(
        [{ "name": "test-integration-1" }]
    )TEXT";
  f.close();

  auto tracerHome = tracerHomeDir.wstring();
  std::vector<std::wstring> expected_names = {L"test-integration-1"};
  std::vector<std::wstring> actual_names;

  SetEnvironmentVariableW(trace::environment::profiler_home_path.data(),
                          tracerHome.data());
  for (auto& integration : LoadIntegrationsFromEnvironment()) {
    actual_names.push_back(integration.integration_name);
  }
  SetEnvironmentVariableW(trace::environment::profiler_home_path.data(),
                          NULL);
  std::filesystem::remove(tmpIntegrations);
  std::filesystem::remove(tracerHomeDir);

  EXPECT_EQ(expected_names, actual_names);
}

TEST(IntegrationLoaderTest, LoadsFromEnvironmentWithOnlyTracerHomeEnvNoTrailingSlash) {
  auto tracerHomeDir = std::filesystem::temp_directory_path() / "TracerHome2";
  auto tmpIntegrations = tracerHomeDir / "integrations.json";
  std::filesystem::create_directory(tracerHomeDir);
  std::ofstream f;
  f.open(tmpIntegrations);
  f << R"TEXT(
        [{ "name": "test-integration-1" }]
    )TEXT";
  f.close();

  auto tracerHome = tracerHomeDir.wstring();
  std::vector<std::wstring> expected_names = {L"test-integration-1"};
  std::vector<std::wstring> actual_names;

  SetEnvironmentVariableW(trace::environment::profiler_home_path.data(),
                          tracerHome.data());
  for (auto& integration : LoadIntegrationsFromEnvironment()) {
    actual_names.push_back(integration.integration_name);
  }
  SetEnvironmentVariableW(trace::environment::profiler_home_path.data(), NULL);
  std::filesystem::remove(tmpIntegrations);
  std::filesystem::remove(tracerHomeDir);

  EXPECT_EQ(expected_names, actual_names);
}

TEST(IntegrationLoaderTest, LoadsFromEnvironmentPrefersIntegrationsEnv) {
  auto targetIntegrations = std::filesystem::temp_directory_path() / "target.json";
  auto tracerHomeDir = std::filesystem::temp_directory_path() / "TracerHome3" / "";
  auto fallbackIntegrations = tracerHomeDir / "integrations.json";
  std::filesystem::create_directory(tracerHomeDir);
  std::ofstream f;
  f.open(targetIntegrations);
  f << R"TEXT(
        [{ "name": "test-integration-target" }]
    )TEXT";
  f.close();
  f.open(fallbackIntegrations);
  f << R"TEXT(
        [{ "name": "test-integration-fallback" }]
    )TEXT";
  f.close();

  auto integrationsPath = targetIntegrations.wstring();
  auto tracerHome = tracerHomeDir.wstring();
  std::vector<std::wstring> expected_names = {L"test-integration-target"};
  std::vector<std::wstring> actual_names;

  SetEnvironmentVariableW(trace::environment::integrations_path.data(),
                          integrationsPath.data());
  SetEnvironmentVariableW(trace::environment::profiler_home_path.data(),
                          tracerHome.data());
  for (auto& integration : LoadIntegrationsFromEnvironment()) {
    actual_names.push_back(integration.integration_name);
  }
  SetEnvironmentVariableW(trace::environment::integrations_path.data(),
                          NULL);
  SetEnvironmentVariableW(trace::environment::profiler_home_path.data(),
                          NULL);
  std::filesystem::remove(targetIntegrations);
  std::filesystem::remove(fallbackIntegrations);
  std::filesystem::remove(tracerHomeDir);

  EXPECT_EQ(expected_names, actual_names);
}

TEST(IntegrationLoaderTest, DeserializesSignatureTypeArray) {
  std::stringstream str(R"TEXT(
        [{
            "name": "test-integration",
            "method_replacements": [{
                "caller": { },
                "target": { "assembly": "Assembly.One", "type": "Type.One", "method": "Method.One", "signature_types": ["System.Void", "_", "FakeClient.Pipeline'1<T>"] },
                "wrapper": { "assembly": "Assembly.Two", "type": "Type.Two", "method": "Method.One", "signature": [0, 1, 1, 28] }
            }]
        }]
    )TEXT");

  auto integrations = LoadIntegrationsFromStream(str);
  const auto target = integrations[0].method_replacements[0].target_method;
  EXPECT_STREQ(L"System.Void", target.signature_types[0].c_str());
  EXPECT_STREQ(L"_", target.signature_types[1].c_str());
  EXPECT_STREQ(L"FakeClient.Pipeline'1<T>", target.signature_types[2].c_str());
}
