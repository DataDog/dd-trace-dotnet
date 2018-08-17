#include "pch.h"

#include <sstream>

#include "../../src/Datadog.Trace.ClrProfiler.Native/IntegrationLoader.h"

TEST(IntegrationLoaderTest, HandlesMissingFile) {
    auto integrations = IntegrationLoader::load_integrations_from_file(L"missing-file");
    EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesInvalidIntegration) {
    std::stringstream str("[{}]");
    auto integrations = IntegrationLoader::load_integrations_from_stream(str);
    // 0 because name is required
    EXPECT_EQ(0, integrations.size());
}

TEST(IntegrationLoaderTest, HandlesSingleIntegrationWithNoMethods) {
    std::stringstream str(R"TEXT(
        [{ "name": "test-integration" }]
    )TEXT");
    auto integrations = IntegrationLoader::load_integrations_from_stream(str);
    EXPECT_EQ(1, integrations.size());
    EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());
    EXPECT_EQ(0, integrations[0].method_replacements.size());
}

TEST(IntegrationLoaderTest, HandlesSingleIntegrationWithInvalidMethodReplacementType) {
    std::stringstream str(R"TEXT(
        [{ "name": "test-integration", "method_replacements": 1234 }]
    )TEXT");
    auto integrations = IntegrationLoader::load_integrations_from_stream(str);
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
                "wrapper": { "assembly": "Assembly.Two", "type": "Type.Two", "method": "Method.Two" }
            }] 
        }]
    )TEXT");
    auto integrations = IntegrationLoader::load_integrations_from_stream(str);
    EXPECT_EQ(1, integrations.size());
    EXPECT_STREQ(L"test-integration", integrations[0].integration_name.c_str());

    EXPECT_EQ(1, integrations[0].method_replacements.size());    
    auto mr = integrations[0].method_replacements[0];
    EXPECT_STREQ(L"", mr.caller_method.assembly_name.c_str());
    EXPECT_STREQ(L"", mr.caller_method.type_name.c_str());
    EXPECT_STREQ(L"", mr.caller_method.method_name.c_str());
    EXPECT_STREQ(L"Assembly.One", mr.target_method.assembly_name.c_str());
    EXPECT_STREQ(L"Type.One", mr.target_method.type_name.c_str());
    EXPECT_STREQ(L"Method.One", mr.target_method.method_name.c_str());
    EXPECT_STREQ(L"Assembly.Two", mr.wrapper_method.assembly_name.c_str());
    EXPECT_STREQ(L"Type.Two", mr.wrapper_method.type_name.c_str());
    EXPECT_STREQ(L"Method.Two", mr.wrapper_method.method_name.c_str());

}