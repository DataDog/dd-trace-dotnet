// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "Exporter.h"
#include "ExporterBuilder.h"
#include "Profile.h"
#include "ProfilerMockedInterface.h"
#include "Tags.h"

#include "shared/src/native-src/dd_filesystem.hpp"

namespace libdatadog {

Profile CreateEmptyProfile(std::unique_ptr<IConfiguration> const& configuration)
{
    return Profile(configuration.get(), {{"cpu", "nanosecond"}}, "RealTime", "Nanoseconds", "my app");
}

TEST(ExporterTest, EnsureCrashOnDebug)
{
    auto builder = ExporterBuilder();
    std::unique_ptr<Exporter> exporter;
    EXPECT_DEBUG_DEATH({ exporter = builder.Build(); }, "") << "Asserts must fail in debug";
#ifdef NDEBUG // RELEASE
    ASSERT_NE(exporter, nullptr);
#else
    // in Debug, one assert will crash the call to Build. So exporter will be equal to nullptr
    ASSERT_EQ(exporter, nullptr);
#endif
}

TEST(ExporterTest, EnsureWithAgentDoesNotCrash)
{
    std::unique_ptr<Exporter> exporter;
    ASSERT_NO_FATAL_FAILURE(exporter = ExporterBuilder()
                                           .SetLanguageFamily("familly")
                                           .SetLibraryName("dotnet")
                                           .SetLibraryVersion("42")
                                           .WithAgent("http://host:port")
                                           .Build();)
        << "Failed to create exporter";

    ASSERT_NE(exporter, nullptr);
}

TEST(ExporterTest, EnsureWithoutAgentDoesNotCrash)
{
    std::unique_ptr<Exporter> exporter;
    ASSERT_NO_FATAL_FAILURE(
        exporter = ExporterBuilder()
                       .SetLanguageFamily("familly")
                       .SetLibraryName("dotnet")
                       .SetLibraryVersion("42")
                       .WithoutAgent("site", "apiKey")
                       .Build();)
        << "Failed to create exporter";

    ASSERT_NE(exporter, nullptr);
}

TEST(ExporterTest, EnsureAddingTagsDoesNotCrash)
{
    Tags tags = {{"tag1", "value1"},
                 {"tag2", "value2"}};

    std::unique_ptr<Exporter> exporter;
    ASSERT_NO_FATAL_FAILURE(
        exporter = ExporterBuilder()
                       .SetLanguageFamily("familly")
                       .SetLibraryName("dotnet")
                       .SetLibraryVersion("42")
                       .WithoutAgent("site", "apiKey")
                       .SetTags(std::move(tags))
                       .Build();)
        << "Failed to create exporter";

    ASSERT_NE(exporter, nullptr);
}

TEST(ExporterTest, CheckFileCreatedWithFileExporter)
{
    auto* testInfo = ::testing::UnitTest::GetInstance()->current_test_info();
    auto outputFolder = fs::temp_directory_path() / testInfo->test_suite_name() / testInfo->name();
    if (fs::exists(outputFolder))
    {
        fs::remove_all(outputFolder);
    }
    fs::create_directories(outputFolder);

    auto exporter = ExporterBuilder()
                        .SetLanguageFamily("family")
                        .SetLibraryName("dotnet")
                        .SetLibraryVersion("42")
                        .WithoutAgent("site", "apiKey")
                        .SetOutputDirectory(outputFolder)
                        .Build();

    ASSERT_NE(exporter, nullptr);

    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto profile = CreateEmptyProfile(configuration);

    auto tags = Tags();
    ASSERT_NO_FATAL_FAILURE(exporter->Send(&profile, std::move(tags), {}, std::string(), std::string())) << "sending the profile crashed";

    ASSERT_FALSE(fs::is_empty(outputFolder));
}
} // namespace libdatadog