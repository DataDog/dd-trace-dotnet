#include "gtest/gtest.h"

#include "environment_variable_wrapper.h"

#include "../../src/native-src/pal.h"
#include "../../src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"

using namespace shared;

static const WSTRING IsRunningOnAAS = WStr("DD_AZURE_APP_SERVICES");

TEST(pal, EnvironmentVariables)
{
#ifdef WINDOWS
    EXPECT_EQ(WStr("Windows_NT"), GetEnvironmentValue(WStr("OS")));
#endif
    EXPECT_TRUE(GetEnvironmentValues(WStr("PATH")).size() > 0);

    bool isSet = SetEnvironmentValue(WStr("CUSTOM_ENV_VAR_KEY"), WStr("CUSTOM_ENV_VAR_VALUE"));
    EXPECT_TRUE(isSet);
    EXPECT_TRUE(GetEnvironmentValue(WStr("CUSTOM_ENV_VAR_KEY")) == WStr("CUSTOM_ENV_VAR_VALUE"));

    isSet = SetEnvironmentValue(WStr("CUSTOM_ENV_VAR_KEY"),
                                WStr("CUSTOM_ENV_VAR_VALUE1;CUSTOM_ENV_VAR_VALUE2;CUSTOM_ENV_VAR_VALUE3"));
    EXPECT_TRUE(isSet);
    std::vector<WSTRING> values = GetEnvironmentValues(WStr("CUSTOM_ENV_VAR_KEY"), ';');
    EXPECT_EQ(3, values.size());
    EXPECT_TRUE(WStr("CUSTOM_ENV_VAR_VALUE1") == values[0]);
    EXPECT_TRUE(WStr("CUSTOM_ENV_VAR_VALUE2") == values[1]);
    EXPECT_TRUE(WStr("CUSTOM_ENV_VAR_VALUE3") == values[2]);
}

struct Dummy
{
#ifdef _WIN32
    inline static const shared::WSTRING folder_path = WStr(R"(Datadog .NET Tracer\logs)");
#endif
};

TEST(pal, DefaultLogFolderOnAAS)
{
    EnvironmentVariable ev(IsRunningOnAAS, WStr("1"));

    auto defaultPath = GetDefaultLogDir<Dummy>();
    auto expectedPath =
#ifdef _WINDOWS
        fs::path(R"(C:\home\LogFiles\datadog\)");
#else
        fs::path("/home/LogFiles/datadog/");
#endif

    ASSERT_EQ(expectedPath, defaultPath);
}

TEST(pal, DefaultLogFolderOnNonAAS)
{
    UnsetEnvironmentValue(IsRunningOnAAS); // to make sure this env. var. is not set (other test)

    auto defaultPath = GetDefaultLogDir<Dummy>();
    auto expectedPath =
#ifdef _WINDOWS
        //                         <-- comes from Dummy -->
        fs::path(R"(C:\ProgramData\Datadog .NET Tracer\logs)");
#else
        fs::path("/var/log/datadog/dotnet/");
#endif

    ASSERT_EQ(expectedPath, defaultPath);
}